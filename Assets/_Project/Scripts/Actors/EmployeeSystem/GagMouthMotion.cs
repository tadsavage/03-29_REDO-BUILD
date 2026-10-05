using UnityEngine;

/// <summary>
/// Muffled-speech jaw motion for an employee wearing a gag: the mouth works slowly and repeatedly, as if trying to talk through it.
/// Added by EmployeeSpawner when the assembled head part's variant contains "gag" (e.g. Female_Head_Gagged).
///
/// Why procedural: the animation clips hold the jaw at one constant value each (Walking holds "Jaw Close" = 1.03, every other clip 0),
/// so ordinary characters get an accidental jaw twitch whenever the Animator crossfades between walking and standing/turning. This
/// component takes over the jaw bone completely after the Animator has run (LateUpdate), which also removes that twitch for gagged
/// characters, and plays a deliberate, much larger and slower open/close pattern instead.
///
/// Pattern: bursts of 1-3 "groans". Each groan eases the jaw open and shut over roughly one second (sine eased, then SmoothDamp'ed so it
/// never snaps), with a different strength each time; a short pause follows the burst. Runs on scaled game time, so it follows the sim
/// speed buttons and freezes when the game is paused.
///
/// Measured on the Avatar 2.0 head: the jaw opens by rotating Jaw_M about its LOCAL -Z axis; -15 deg moves the jaw 2.1 cm down.
/// </summary>
[DisallowMultipleComponent]
public class GagMouthMotion : MonoBehaviour
{
    // Tuning lives in the shared GagMouthSettings asset (Resources/Resource_AvatarSystemAssets/GagMouthSettings) so it can be edited in the
    // Inspector, live. If the asset is missing a temporary default instance is used, so the component still works.
    private static GagMouthSettings _settings;
    private static GagMouthSettings S
    {
        get
        {
            if (_settings == null)
            {
                _settings = Resources.Load<GagMouthSettings>("Resource_AvatarSystemAssets/GagMouthSettings");
                if (_settings == null) _settings = ScriptableObject.CreateInstance<GagMouthSettings>();
            }
            return _settings;
        }
    }
    private static float RestOpenDegrees   => S.restOpenDegrees;
    private static float MaxOpenDegrees    => S.maxOpenDegrees;
    private static float MinStrength       => S.minStrength;
    private static float GroanSecondsMin   => S.groanSecondsMin;
    private static float GroanSecondsMax   => Mathf.Max(S.groanSecondsMin, S.groanSecondsMax);
    private static int   GroansPerBurstMin => S.groansPerBurstMin;
    private static int   GroansPerBurstMax => Mathf.Max(S.groansPerBurstMin, S.groansPerBurstMax);
    private static float PauseSecondsMin   => S.pauseSecondsMin;
    private static float PauseSecondsMax   => Mathf.Max(S.pauseSecondsMin, S.pauseSecondsMax);
    private static float WobbleDegrees     => S.wobbleDegrees;
    private static float Smoothing         => S.smoothing;

    private Transform _jaw;
    private Quaternion _rest;        // the jaw's neutral local rotation (captured while the Animator is in Idle, where Jaw Close = 0)
    private bool _haveRest;

    private float _groanT, _groanLen, _strength, _pause;
    private int _groansLeft;
    private float _open, _openVel, _wobble, _wobbleVel, _wobbleTarget;
    private System.Random _rng;

    public static void Attach(GameObject avatarRoot)
    {
        if (avatarRoot == null || avatarRoot.GetComponent<GagMouthMotion>() != null) return;
        avatarRoot.AddComponent<GagMouthMotion>();
    }

    private void Start()
    {
        var anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (anim != null && anim.avatar != null && anim.avatar.isHuman) _jaw = anim.GetBoneTransform(HumanBodyBones.Jaw);
        if (_jaw == null)
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Jaw_M") { _jaw = t; break; }
        if (_jaw == null) { enabled = false; return; }

        // Different phase per employee so a crowd of gagged workers doesn't groan in unison.
        _rng = new System.Random(System.Guid.NewGuid().GetHashCode());   // random per employee (Object.GetInstanceID is obsolete in Unity 6)
        _pause = Range(0.1f, PauseSecondsMax);
    }

    private void LateUpdate()
    {
        if (_jaw == null) return;

        // Neutral jaw: captured on the first frame (avatar just spawned in Idle). Later frames ignore whatever the clip wrote.
        if (!_haveRest) { _rest = _jaw.localRotation; _haveRest = true; }

        float dt = Time.deltaTime;
        float target = 0f;

        if (_pause > 0f)
        {
            _pause -= dt;
            if (_pause <= 0f) BeginBurst();
        }
        else
        {
            _groanT += dt;
            float u = _groanT / _groanLen;
            if (u >= 1f)
            {
                if (--_groansLeft > 0) BeginGroan();
                else { _pause = Range(PauseSecondsMin, PauseSecondsMax); }
            }
            else
            {
                // Eased bell: 0 -> 1 -> 0 over the groan, flatter at the top so the mouth lingers open for a beat.
                float bell = Mathf.Sin(u * Mathf.PI);
                target = _strength * (bell * bell * (3f - 2f * bell));
            }
        }

        _open   = Mathf.SmoothDamp(_open, target, ref _openVel, Smoothing, Mathf.Infinity, dt);
        _wobble = Mathf.SmoothDamp(_wobble, _wobbleTarget * _open, ref _wobbleVel, 0.2f, Mathf.Infinity, dt);

        // Replace (not add to) the clip's jaw pose: opens about -Z, with a little sideways drift about Y.
        _jaw.localRotation = _rest
                             * Quaternion.AngleAxis(-(RestOpenDegrees + _open * (MaxOpenDegrees - RestOpenDegrees)), Vector3.forward)
                             * Quaternion.AngleAxis(_wobble * WobbleDegrees, Vector3.up);
    }

    private void BeginBurst()
    {
        if (_rng == null) _rng = new System.Random(System.Guid.NewGuid().GetHashCode());
        _groansLeft = _rng.Next(GroansPerBurstMin, GroansPerBurstMax + 1);
        BeginGroan();
    }

    private void BeginGroan()
    {
        _groanT = 0f;
        _groanLen = Range(GroanSecondsMin, GroanSecondsMax);
        _strength = Range(MinStrength, 1f);
        _wobbleTarget = Range(-1f, 1f);
    }

    // _rng is not serialized, so a script recompile during Play Mode leaves it null on live avatars; recreate it instead of throwing every frame.
    private float Range(float a, float b)
    {
        if (_rng == null) _rng = new System.Random(System.Guid.NewGuid().GetHashCode());
        return a + (float)_rng.NextDouble() * (b - a);
    }
}
