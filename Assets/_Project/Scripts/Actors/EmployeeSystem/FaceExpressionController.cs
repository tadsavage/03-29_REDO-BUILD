using UnityEngine;

/// <summary>
/// The mood a face is currently showing. Add a value here, then a pose in <see cref="FaceExpressionController"/>'s table; nothing else changes.
/// </summary>
public enum FaceMood { Neutral, Surprise, Fear, Anger, Confusion, Tired, Sleeping, Sad }

/// <summary>
/// Skyrim-style facial expression driver for the Avatar 2.0 rig (brows, eyelids, eye gaze, optional jaw) with a random blink.
/// Added by EmployeeSpawner to every modular avatar (Neutral by default; Fear for a gagged head).
///
/// How it works
///  - Each mood is a POSE: a short list of numbers per side (brow inner/mid/outer lift, inner pull toward the nose, upper/lower lid
///    close in degrees, gaze yaw/pitch) plus a jaw angle. Poses live in the <see cref="Poses"/> table.
///  - The controller eases the current pose toward the target pose every frame (exponential smoothing) so moods blend instead of snapping.
///  - On top of the pose sit: a BLINK (random every ~6-14 s, mood-dependent speed/rate), small random gaze saccades, a tremble in fear, and
///    a periodic STARTLE (a brief surprise spike) for moods that ask for it.
///  - Everything is written in LateUpdate, after the Animator, from the bones' captured REST values, so the clip can never fight it.
///
/// Axis independence: nothing assumes bone-local axes. Brow moves are WORLD-space offsets (up / toward the nose, with "toward the nose"
/// worked out from the centre brow bone's position). Lids and eyes rotate about WORLD axes (avatar right / up), and the rotation direction
/// for "closing" is derived from geometry (a lid closes when its front point moves toward the opposite lid), so it is correct whatever the
/// FBX import did to the bone axes.
///
/// Missing bones are skipped, so this is safe on avatars/FBXs that predate the eyelid bones (lids simply won't move until the new head is
/// exported). Runs on scaled game time: freezes when the game is paused.
///
/// Rig notes (Avatar 2.0): lid bones are EyelidUpper_L/R and EyelidLower_L/R; the baked idle shape has the upper lid edge ~17 deg above the
/// eye centre and the lower ~26 deg below, so fully closed = ClosedUpperDeg / ClosedLowerDeg of rotation from that rest shape.
/// </summary>
[DisallowMultipleComponent]
public class FaceExpressionController : MonoBehaviour
{
    // ---------------------------------------------------------------- tuning
    private const float ClosedUpperDeg = 28f;     // upper lid rotation that seals the eye from the baked idle shape (23 left white specks at the top)
    private const float ClosedLowerDeg = 28f;
    private const float PoseSmoothSeconds = 0.14f;

    private const float BlinkGapMin = 2.5f, BlinkGapMax = 5f;     // people blink every ~3-4 s
    private const float LookGapMin = 2.5f, LookGapMax = 6f;        // glance somewhere every ~3-6 s
    private const float LookHoldMin = 1f, LookHoldMax = 2.5f, LookDegrees = 32f;
    private const float SmileGapMin = 9f, SmileGapMax = 22f;       // a smile every ~10-20 s (not while gagged / non-neutral)
    private const float SmileHoldMin = 1.6f, SmileHoldMax = 3.6f;
    private const float SmileCornerUp = 0.0315f, SmileCornerOut = 0.0255f, SmileCheekUp = 0.0225f, SmileSquintDeg = 6.75f;
    private float _smile, _smileTarget, _smileVel, _nextSmile = 6f, _smileHoldLeft;
    private const float HeadFollow = 0.9f;                         // head turns this fraction of the eye yaw (eyes lead)
    private const float LookDownChance = 0.18f, LookUpChance = 0.15f;   // glance at the shoes / up at the sky
    private const float HeadDownDegrees = 22f, HeadUpDegrees = 24f;
    private const float BlinkCloseSeconds = 0.07f, BlinkHoldSeconds = 0.04f, BlinkOpenSeconds = 0.11f;
    private const float DoubleBlinkChance = 0.15f;

    private const float SaccadeGapMin = 1.2f, SaccadeGapMax = 3.5f;   // tiny gaze flicks so the eyes look alive
    private const float SaccadeDegrees = 2.2f;

    // ---------------------------------------------------------------- pose layout
    // Per side (L = 0, R = 1), 8 floats each; one shared jaw float at the end.
    private const int InnerUp = 0, InnerIn = 1, MidUp = 2, OuterUp = 3, LidUpper = 4, LidLower = 5, GazeYaw = 6, GazePitch = 7;
    private const int PerSide = 8, JawIndex = PerSide * 2, PoseLen = PerSide * 2 + 1;

    private struct Mood
    {
        public float[] pose;
        public float blinkRate;          // multiplies blink frequency (>1 = more often)
        public float blinkSlow;          // multiplies blink duration (>1 = slower, heavier)
        public bool noBlink;
        public float startleGapMin, startleGapMax;   // 0 = no startle spikes
        public float tremble;            // meters of brow jitter
    }

    private static float[] Sym(float innerUp, float innerIn, float midUp, float outerUp, float lidUpper, float lidLower,
                               float yaw = 0f, float pitch = 0f, float jaw = 0f)
    {
        var p = new float[PoseLen];
        for (int s = 0; s < 2; s++)
        {
            int b = s * PerSide;
            p[b + InnerUp] = innerUp; p[b + InnerIn] = innerIn; p[b + MidUp] = midUp; p[b + OuterUp] = outerUp;
            p[b + LidUpper] = lidUpper; p[b + LidLower] = lidLower; p[b + GazeYaw] = yaw; p[b + GazePitch] = pitch;
        }
        p[JawIndex] = jaw;
        return p;
    }

    // Brow numbers are meters (world-space), lids are degrees of CLOSING from the baked idle shape (negative = wider than idle),
    // gaze/jaw are degrees. Tune freely; this table is the whole "expression library".
    private static readonly Mood[] Poses = BuildPoses();
    private static Mood[] BuildPoses()
    {
        var m = new Mood[System.Enum.GetValues(typeof(FaceMood)).Length];
        m[(int)FaceMood.Neutral]  = new Mood { pose = Sym(0, 0, 0, 0, 0, 0), blinkRate = 1f, blinkSlow = 1f };
        m[(int)FaceMood.Surprise] = new Mood { pose = Sym(0.0150f, 0.0010f, 0.0150f, 0.0120f, -7f, -1f, jaw: 6f), blinkRate = 0.35f, blinkSlow = 1f };
        m[(int)FaceMood.Fear]     = new Mood { pose = Sym(0.0180f, 0.0120f, 0.0090f, -0.0055f, -7f, -1f, 0f, 0f, 3f), blinkRate = 0.6f, blinkSlow = 0.8f,
                                               startleGapMin = 2.5f, startleGapMax = 6.5f, tremble = 0.0007f };
        m[(int)FaceMood.Anger]    = new Mood { pose = Sym(-0.0120f, 0.0100f, -0.0070f, 0.0080f, 12f, 6f, jaw: -2f), blinkRate = 0.55f, blinkSlow = 0.9f };
        m[(int)FaceMood.Sad]      = new Mood { pose = Sym(0.0090f, 0.0035f, 0.0020f, -0.0040f, 6f, 0f, 0f, -6f), blinkRate = 0.9f, blinkSlow = 1.3f };
        m[(int)FaceMood.Tired]    = new Mood { pose = Sym(-0.0020f, 0f, -0.0030f, -0.0030f, 15f, 4f, 0f, -9f), blinkRate = 1.3f, blinkSlow = 3.2f };
        m[(int)FaceMood.Sleeping] = new Mood { pose = Sym(-0.0010f, 0f, -0.0020f, -0.0020f, ClosedUpperDeg, ClosedLowerDeg, 0f, -4f, 4f), noBlink = true };
        // Confusion is deliberately lopsided: one brow cocked up, the other pulled down, eyes narrowed and glancing aside.
        var conf = Sym(0, 0, 0, 0, 3f, 1f, 9f, 0f);
        conf[0 * PerSide + InnerUp] = 0.0060f; conf[0 * PerSide + MidUp] = 0.0110f; conf[0 * PerSide + OuterUp] = 0.0080f;   // left up
        conf[1 * PerSide + InnerUp] = -0.0040f; conf[1 * PerSide + InnerIn] = 0.0040f; conf[1 * PerSide + MidUp] = -0.0030f; // right down
        m[(int)FaceMood.Confusion] = new Mood { pose = conf, blinkRate = 1.2f, blinkSlow = 1f };
        return m;
    }

    // ---------------------------------------------------------------- bones
    private class Bone
    {
        public Transform t; public Vector3 restPos, restScale; public Quaternion restRot;
        public Vector3 smoothPos, vel;
    }

    private class Side
    {
        public Bone inner, mid, outer, eye, lidUp, lidLow, corner, cheek;
        public float inwardSign;          // +1/-1: which way along avatar-right points toward the nose for this side
    }

    private readonly Side[] _s = { new Side(), new Side() };
    private Bone _center, _jaw;
    private bool _ready, _hasJawDriver;
    private System.Random _rng;

    // ---------------------------------------------------------------- state
    [SerializeField] private FaceMood _mood = FaceMood.Neutral;
    private float _moodHold;                   // seconds left before reverting to _baseMood (0 = no timer)
    private FaceMood _baseMood;
    private readonly float[] _cur = new float[PoseLen];
    private float _blink, _blinkT = -1f, _nextBlink, _blinkLen = 1f; private int _blinkQueued;
    private float _nextLook = 4f, _lookYaw, _lookPitch, _lookYawSm, _lookPitchSm, _lookYawVel, _lookPitchVel; private bool _lookActive;
    private float _headYaw, _headPitch, _headYawSm, _headPitchSm, _headYawVel, _headPitchVel; private Transform _headBone, _neckBone;
    private float _nextSaccade, _sacYaw, _sacPitch, _sacYawSm, _sacPitchSm, _sacYawVel, _sacPitchVel;
    private float _nextStartle, _startleT = 99f;
    private float _noiseSeed;

    public FaceMood CurrentMood => _mood;

    // ---- Eye darting (gagged workers): every ~10 s the eyes flick to one side, then the other, then back - nervous, looking for help.
    private bool _darting;
    private float _nextDart = 8f, _dartT = -1f, _dartFirstSign = 1f, _dartYaw, _dartYawSm, _dartYawVel;
    private const float DartGapMin = 7f, DartGapMax = 13f;      // "about every 10 seconds"
    private const float DartDegrees = 26f, DartHoldSeconds = 0.5f;

    /// <summary>Turns the intermittent left/right eye dart on or off (used for gagged workers).</summary>
    public void EnableDarting(bool on) { _darting = on; _nextDart = Range(3f, DartGapMax); _dartT = -1f; }

    /// <summary>Switch mood. holdSeconds &gt; 0 returns to the previous base mood afterwards (a flash of surprise, say).</summary>
    public void SetMood(FaceMood mood, float holdSeconds = 0f)
    {
        if (holdSeconds > 0f) { _moodHold = holdSeconds; } else { _baseMood = mood; _moodHold = 0f; }
        _mood = mood;
        ScheduleStartle();
    }

    public static FaceExpressionController Attach(GameObject avatarRoot, FaceMood mood = FaceMood.Neutral)
    {
        if (avatarRoot == null) return null;
        var c = avatarRoot.GetComponent<FaceExpressionController>();
        if (c == null) c = avatarRoot.AddComponent<FaceExpressionController>();
        c._baseMood = mood; c.SetMood(mood);
        return c;
    }

    // ---------------------------------------------------------------- setup
    private void Start()
    {
        _rng = new System.Random(System.Guid.NewGuid().GetHashCode());
        _noiseSeed = (float)_rng.NextDouble() * 100f;
        _hasJawDriver = GetComponent<GagMouthMotion>() != null;

        string[] sides = { "L", "R" };
        for (int i = 0; i < 2; i++)
        {
            var s = _s[i]; string k = sides[i];
            s.inner = Find("EyebrowInner_" + k); s.mid = Find("EyebrowMid_" + k); s.outer = Find("EyebrowOuter_" + k);
            s.eye = Find("Eye_" + k); s.lidUp = Find("EyelidUpper_" + k); s.lidLow = Find("EyelidLower_" + k);
            s.corner = Find("OuterMouth_" + k); s.cheek = Find("Cheek_" + k);
        }
        _center = Find("EyebrowCenter_M"); _jaw = Find("Jaw_M");
        _headBone = Find("Head_M")?.t; _neckBone = Find("Neck_M")?.t;
        _ready = _s[0].inner != null || _s[0].eye != null || _s[0].lidUp != null;
        if (!_ready) { enabled = false; return; }

        // Which way is "toward the nose" for each side, measured from the rig itself (independent of naming/handedness).
        Vector3 right = transform.right;
        Bone[] refs = { _s[0].mid ?? _s[0].eye, _s[1].mid ?? _s[1].eye };
        for (int i = 0; i < 2; i++)
        {
            float side = 0f;
            if (refs[i] != null && _center != null) side = Vector3.Dot(_center.t.position - refs[i].t.position, right);
            else if (refs[i] != null && refs[1 - i] != null) side = Vector3.Dot(refs[1 - i].t.position - refs[i].t.position, right);
            _s[i].inwardSign = side >= 0f ? 1f : -1f;
        }

        for (int i = 0; i < PoseLen; i++) _cur[i] = Poses[(int)_mood].pose[i];
        _nextBlink = Range(2f, BlinkGapMax); _nextSaccade = Range(SaccadeGapMin, SaccadeGapMax); ScheduleStartle();
    }

    private Bone Find(string boneName)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == boneName)
                return new Bone { t = t, restPos = t.localPosition, restRot = t.localRotation, restScale = t.localScale, smoothPos = t.localPosition };
        return null;
    }

    // ---------------------------------------------------------------- per frame
    private void LateUpdate()
    {
        if (!_ready) return;
        float dt = Time.deltaTime; float time = Time.time;
        if (dt <= 0f) return;

        if (_moodHold > 0f) { _moodHold -= dt; if (_moodHold <= 0f) { _mood = _baseMood; ScheduleStartle(); } }
        var mood = Poses[(int)_mood];

        // 1) ease the working pose toward the mood's pose
        float k = 1f - Mathf.Exp(-dt / PoseSmoothSeconds);
        for (int i = 0; i < PoseLen; i++) _cur[i] = Mathf.Lerp(_cur[i], mood.pose[i], k);

        // 2) startle spike (brief surprise on top of the base mood)
        float spike = 0f;
        if (mood.startleGapMax > 0f)
        {
            _nextStartle -= dt;
            if (_nextStartle <= 0f) { _startleT = 0f; ScheduleStartle(); }
            _startleT += dt;
            spike = _startleT < 1.2f ? Mathf.Pow(1f - _startleT / 1.2f, 1.6f) : 0f;
        }
        var sur = Poses[(int)FaceMood.Surprise].pose;

        // 3) blink
        UpdateBlink(dt, mood);

        // 4) gaze saccades (tiny flicks)
        _nextSaccade -= dt;
        if (_nextSaccade <= 0f)
        {
            _nextSaccade = Range(SaccadeGapMin, SaccadeGapMax);
            _sacYaw = Range(-SaccadeDegrees, SaccadeDegrees); _sacPitch = Range(-SaccadeDegrees * 0.6f, SaccadeDegrees * 0.6f);
        }
        _sacYawSm = Mathf.SmoothDamp(_sacYawSm, _sacYaw, ref _sacYawVel, 0.05f, Mathf.Infinity, dt);
        _sacPitchSm = Mathf.SmoothDamp(_sacPitchSm, _sacPitch, ref _sacPitchVel, 0.05f, Mathf.Infinity, dt);

        // look-around: every ~5-10 s the eyes glance somewhere, hold ~1-2 s, then return (none while asleep)
        if (mood.noBlink) { _lookYaw = 0f; _lookPitch = 0f; _headYaw = 0f; _headPitch = 0f; }
        else
        {
            _nextLook -= dt;
            if (_nextLook <= 0f)
            {
                if (_lookActive)
                {
                    _lookActive = false; _lookYaw = _lookPitch = _headYaw = _headPitch = 0f; _nextLook = Range(LookGapMin, LookGapMax);
                }
                else
                {
                    _lookActive = true; _nextLook = Range(LookHoldMin, LookHoldMax);
                    float roll = Range(0f, 1f);
                    if (roll < LookDownChance)          // glance down at the shoes
                    { _lookYaw = Range(-8f, 8f); _lookPitch = -LookDegrees * 0.8f; _headYaw = _lookYaw * 0.5f; _headPitch = -HeadDownDegrees; }
                    else if (roll < LookDownChance + LookUpChance)   // glance up at the sky
                    { _lookYaw = Range(-8f, 8f); _lookPitch = LookDegrees * 0.8f; _headYaw = _lookYaw * 0.5f; _headPitch = HeadUpDegrees; }
                    else                                // look around to one side
                    { _lookYaw = Range(-LookDegrees, LookDegrees); _lookPitch = Range(-6f, 6f); _headYaw = _lookYaw * HeadFollow; _headPitch = _lookPitch * 0.5f; }
                }
            }
        }
        _lookYawSm = Mathf.SmoothDamp(_lookYawSm, _lookYaw, ref _lookYawVel, 0.08f, Mathf.Infinity, dt);
        _lookPitchSm = Mathf.SmoothDamp(_lookPitchSm, _lookPitch, ref _lookPitchVel, 0.08f, Mathf.Infinity, dt);
        // the head turns after the eyes (slower), so the glance leads and the head follows
        _headYawSm = Mathf.SmoothDamp(_headYawSm, _headYaw, ref _headYawVel, 0.22f, Mathf.Infinity, dt);
        _headPitchSm = Mathf.SmoothDamp(_headPitchSm, _headPitch, ref _headPitchVel, 0.22f, Mathf.Infinity, dt);
        if (_headBone != null && (Mathf.Abs(_headYawSm) > 0.01f || Mathf.Abs(_headPitchSm) > 0.01f))
        {
            Vector3 hUp = transform.up, hRight = transform.right;
            if (_neckBone != null)
                _neckBone.rotation = Quaternion.AngleAxis(_headYawSm * 0.35f, hUp) * Quaternion.AngleAxis(-_headPitchSm * 0.35f, hRight) * _neckBone.rotation;
            _headBone.rotation = Quaternion.AngleAxis(_headYawSm * 0.65f, hUp) * Quaternion.AngleAxis(-_headPitchSm * 0.65f, hRight) * _headBone.rotation;
        }

        // darting: left, right, back to centre (each held DartHoldSeconds), then wait for the next one
        float dartTarget = 0f;
        if (_darting)
        {
            if (_dartT < 0f) { _nextDart -= dt; if (_nextDart <= 0f) { _dartT = 0f; _dartFirstSign = Range(0f, 1f) < 0.5f ? -1f : 1f; } }
            else
            {
                _dartT += dt;
                int phase = (int)(_dartT / DartHoldSeconds);
                if (phase == 0) dartTarget = _dartFirstSign * DartDegrees;
                else if (phase == 1) dartTarget = -_dartFirstSign * DartDegrees;
                else { dartTarget = 0f; _dartT = -1f; _nextDart = Range(DartGapMin, DartGapMax); }
            }
        }
        _dartYawSm = Mathf.SmoothDamp(_dartYawSm, dartTarget, ref _dartYawVel, 0.05f, Mathf.Infinity, dt);

        // smile: now and then, only when neutral and not gagged (a gagged mouth can't smile)
        bool canSmile = !_hasJawDriver && _mood == FaceMood.Neutral && !mood.noBlink;
        if (!canSmile) { _smileTarget = 0f; _smileHoldLeft = 0f; }
        else if (_smileTarget > 0f)
        {
            _smileHoldLeft -= dt;
            if (_smileHoldLeft <= 0f) { _smileTarget = 0f; _nextSmile = Range(SmileGapMin, SmileGapMax); }
        }
        else
        {
            _nextSmile -= dt;
            if (_nextSmile <= 0f) { _smileTarget = Range(0.7f, 1f); _smileHoldLeft = Range(SmileHoldMin, SmileHoldMax); }
        }
        _smile = Mathf.SmoothDamp(_smile, _smileTarget, ref _smileVel, 0.35f, Mathf.Infinity, dt);

        Vector3 up = transform.up, right = transform.right;
        for (int i = 0; i < 2; i++)
        {
            var s = _s[i]; int b = i * PerSide;
            float n = Mathf.PerlinNoise(_noiseSeed + i * 7.1f, time * 9f) * 2f - 1f;
            float trem = n * mood.tremble;
            Vector3 toNose = right * s.inwardSign;
            float sp(int idx) => spike * (sur[b + idx] - 0f);

            DriveBrow(s.inner, up * (_cur[b + InnerUp] + sp(InnerUp) + trem) + toNose * (_cur[b + InnerIn] + spike * sur[b + InnerIn]), dt);
            DriveBrow(s.mid,   up * (_cur[b + MidUp]   + sp(MidUp)   + trem), dt);
            DriveBrow(s.outer, up * (_cur[b + OuterUp] + sp(OuterUp) + trem), dt);

            // lids: pose (+ startle widening) blended toward fully closed by the blink amount
            float upper = Mathf.Lerp(_cur[b + LidUpper] + spike * sur[b + LidUpper], ClosedUpperDeg, _blink);
            float lower = Mathf.Lerp(_cur[b + LidLower] + spike * sur[b + LidLower] + SmileSquintDeg * _smile, ClosedLowerDeg, _blink * 0.9f);
            if (s.corner != null) DriveBrow(s.corner, up * (SmileCornerUp * _smile) - toNose * (SmileCornerOut * _smile), dt);
            if (s.cheek != null) DriveBrow(s.cheek, up * (SmileCheekUp * _smile), dt);
            RotateLid(s.lidUp, upper, true);
            RotateLid(s.lidLow, lower, false);

            // gaze
            if (s.eye != null)
            {
                float yaw = _cur[b + GazeYaw] + _sacYawSm + _dartYawSm + _lookYawSm, pitch = _cur[b + GazePitch] + _sacPitchSm + _lookPitchSm;
                var rest = s.eye.t.parent != null ? s.eye.t.parent.rotation * s.eye.restRot : s.eye.restRot;
                // yaw about avatar up; pitch about avatar right (positive pitch = look up, hence the minus)
                s.eye.t.rotation = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(-pitch, right) * rest;
            }
        }
        if (_center != null)
            DriveBrow(_center, up * (0.5f * (_cur[InnerUp] + _cur[PerSide + InnerUp]) + spike * sur[InnerUp] * 0.8f), dt);

        // jaw (only when the gag mouth motion is not already driving it)
        if (_jaw != null && !_hasJawDriver)
        {
            float jaw = _cur[JawIndex] + spike * sur[JawIndex];
            _jaw.t.localRotation = _jaw.restRot * Quaternion.AngleAxis(-jaw, Vector3.forward);   // opens about local -Z (measured on this rig)
        }
    }

    // ---------------------------------------------------------------- helpers
    private void UpdateBlink(float dt, Mood mood)
    {
        if (mood.noBlink) { _blink = Mathf.MoveTowards(_blink, 0f, dt * 6f); _blinkT = -1f; return; }
        if (_blinkT < 0f)
        {
            _nextBlink -= dt;
            if (_nextBlink <= 0f)
            {
                _blinkT = 0f; _blinkLen = Mathf.Max(0.5f, mood.blinkSlow);
                _blinkQueued = Range(0f, 1f) < DoubleBlinkChance ? 1 : 0;   // Range() recreates _rng if a recompile cleared it
                _nextBlink = Range(BlinkGapMin, BlinkGapMax) / Mathf.Max(0.2f, mood.blinkRate);
            }
            _blink = Mathf.MoveTowards(_blink, 0f, dt * 8f);
            return;
        }
        float close = BlinkCloseSeconds * _blinkLen, hold = BlinkHoldSeconds * _blinkLen, open = BlinkOpenSeconds * _blinkLen;
        _blinkT += dt;
        if (_blinkT < close) _blink = Mathf.SmoothStep(0f, 1f, _blinkT / close);
        else if (_blinkT < close + hold) _blink = 1f;
        else if (_blinkT < close + hold + open) _blink = 1f - Mathf.SmoothStep(0f, 1f, (_blinkT - close - hold) / open);
        else
        {
            _blink = 0f;
            if (_blinkQueued > 0) { _blinkQueued--; _blinkT = 0f; } else _blinkT = -1f;
        }
    }

    // Brows: offset from rest by a WORLD-space delta (converted into the parent's local space), eased.
    private void DriveBrow(Bone b, Vector3 worldDelta, float dt)
    {
        if (b == null || b.t == null) return;
        var parent = b.t.parent;
        Vector3 local = parent != null ? parent.InverseTransformVector(worldDelta) : worldDelta;
        b.smoothPos = Vector3.SmoothDamp(b.smoothPos, b.restPos + local, ref b.vel, 0.05f, Mathf.Infinity, dt);
        b.t.localPosition = b.smoothPos;
    }

    // Lids hinge about the avatar's lateral (right) axis through the bone. "Closing" direction is derived geometrically: the upper lid
    // closes when its front point moves DOWN, the lower when it moves UP.
    private void RotateLid(Bone lid, float closeDegrees, bool upper)
    {
        if (lid == null || lid.t == null) return;
        Vector3 axis = transform.right, fwd = transform.forward;
        float dirY = Vector3.Cross(axis, fwd).y;               // + : positive rotation about 'axis' lifts the front point
        float sign = upper ? (dirY > 0f ? -1f : 1f) : (dirY > 0f ? 1f : -1f);
        var rest = lid.t.parent != null ? lid.t.parent.rotation * lid.restRot : lid.restRot;
        lid.t.rotation = Quaternion.AngleAxis(sign * closeDegrees, axis) * rest;
    }

    private void ScheduleStartle()
    {
        var m = Poses[(int)_mood];
        _nextStartle = m.startleGapMax > 0f ? Range(0.5f, m.startleGapMax) : 1e9f;
    }

    private float Range(float a, float b) { if (_rng == null) _rng = new System.Random(); return a + (float)_rng.NextDouble() * (b - a); }

#if UNITY_EDITOR
    // Handy for tuning in the Inspector while the game runs.
    [ContextMenu("Mood: Fear")]       private void DbgFear()      => SetMood(FaceMood.Fear);
    [ContextMenu("Mood: Anger")]      private void DbgAnger()     => SetMood(FaceMood.Anger);
    [ContextMenu("Mood: Confusion")]  private void DbgConfusion() => SetMood(FaceMood.Confusion);
    [ContextMenu("Mood: Tired")]      private void DbgTired()     => SetMood(FaceMood.Tired);
    [ContextMenu("Mood: Sleeping")]   private void DbgSleeping()  => SetMood(FaceMood.Sleeping);
    [ContextMenu("Mood: Surprise")]   private void DbgSurprise()  => SetMood(FaceMood.Surprise);
    [ContextMenu("Mood: Neutral")]    private void DbgNeutral()   => SetMood(FaceMood.Neutral);
#endif
}
