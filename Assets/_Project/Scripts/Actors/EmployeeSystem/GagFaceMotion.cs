using UnityEngine;

/// <summary>
/// Frightened / startled face for an employee wearing a gag: brows lift and knit, eyes go wide, with a constant faint tremble and now and
/// then a sharp "surprise" spike. Added by EmployeeSpawner next to <see cref="GagMouthMotion"/> when the head variant contains "gag".
///
/// Drives the face rig's eyebrow bones (EyebrowInner/Mid/Outer_L/R, EyebrowCenter_M) and eye bones (Eye_L/R). The head meshes are weighted
/// to those bones (brows blend Inner->Mid->Outer along their length; each eye piece follows its one Eye bone), so moving a bone moves the
/// geometry. Only local POSITION and SCALE are written (rotation is left to the Animator, which owns the humanoid eye bones), and offsets
/// are defined in world directions (up / toward the nose) and converted into each bone's local space, so no bone-axis assumptions are needed.
///
/// Expressions (blended, then SmoothDamp'ed so nothing snaps):
///   Fear     - constant, slowly breathing between ~45% and 85%: inner brows up and pulled together, outer brows slightly down (worried "^"),
///              eyes a little wide.
///   Surprise - spikes every 2.5-6.5 s, decays over ~1.2 s: every brow high, eyes very wide.
/// Runs on scaled game time (freezes when paused), random phase per employee.
/// </summary>
[DisallowMultipleComponent]
public class GagFaceMotion : MonoBehaviour
{
    // Tuning (meters / multipliers) ---------------------------------------------------------------------------
    private const float FearInnerUp      = 0.0110f;
    private const float FearInnerIn      = 0.0065f;   // toward the nose
    private const float FearMidUp        = 0.0050f;
    private const float FearOuterDown    = 0.0025f;
    private const float FearEyeScale     = 0.14f;     // +14% at full fear

    private const float SurpriseBrowUp   = 0.0150f;   // every brow, on top of fear
    private const float SurpriseEyeScale = 0.24f;
    private const float SurpriseEyeUp    = 0.0030f;

    private const float FearLow = 0.45f, FearHigh = 0.85f, FearBreathSeconds = 3.2f;
    private const float SpikeGapMin = 2.5f, SpikeGapMax = 6.5f, SpikeSeconds = 1.2f;
    private const float Tremble = 0.0007f;            // brow jitter amplitude
    private const float Smoothing = 0.06f;

    private class Bone
    {
        public Transform t; public Vector3 restPos, restScale;
        public float side;        // +1 = character's left (+X in avatar space), -1 = right, 0 = centre
        public Vector3 vel;       // SmoothDamp velocity (position)
        public Vector3 smoothPos;
    }

    private Bone[] _inner = new Bone[2], _mid = new Bone[2], _outer = new Bone[2], _eye = new Bone[2];
    private Bone _center;
    private bool _ready;
    private System.Random _rng;
    private float _breathPhase, _nextSpike, _spikeT = 99f, _fearSm, _fearVel, _surSm, _surVel, _noiseSeed;

    public static void Attach(GameObject avatarRoot)
    {
        if (avatarRoot == null || avatarRoot.GetComponent<GagFaceMotion>() != null) return;
        avatarRoot.AddComponent<GagFaceMotion>();
    }

    private void Start()
    {
        _rng = new System.Random(System.Guid.NewGuid().GetHashCode());
        _breathPhase = (float)_rng.NextDouble() * 6.28f;
        _noiseSeed = (float)_rng.NextDouble() * 100f;
        _nextSpike = Range(0.5f, SpikeGapMax);

        for (int i = 0; i < 2; i++)
        {
            string s = i == 0 ? "L" : "R";
            float side = i == 0 ? 1f : -1f;
            _inner[i] = Find("EyebrowInner_" + s, side);
            _mid[i]   = Find("EyebrowMid_" + s, side);
            _outer[i] = Find("EyebrowOuter_" + s, side);
            _eye[i]   = Find("Eye_" + s, side);
        }
        _center = Find("EyebrowCenter_M", 0f);
        _ready = _inner[0] != null || _eye[0] != null;
        if (!_ready) enabled = false;
    }

    private Bone Find(string boneName, float side)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == boneName)
                return new Bone { t = t, restPos = t.localPosition, restScale = t.localScale, side = side, smoothPos = t.localPosition };
        return null;
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        float dt = Time.deltaTime;
        float time = Time.time;

        // Fear breathes; surprise spikes at random intervals.
        float fearTarget = Mathf.Lerp(FearLow, FearHigh, 0.5f + 0.5f * Mathf.Sin(time * 6.28f / FearBreathSeconds + _breathPhase));
        _nextSpike -= dt;
        if (_nextSpike <= 0f) { _spikeT = 0f; _nextSpike = Range(SpikeGapMin, SpikeGapMax); }
        _spikeT += dt;
        // sharp attack, slow release: 1 at the start, easing to 0
        float spike = _spikeT < SpikeSeconds ? Mathf.Pow(1f - _spikeT / SpikeSeconds, 1.6f) : 0f;

        _fearSm = Mathf.SmoothDamp(_fearSm, fearTarget, ref _fearVel, 0.25f, Mathf.Infinity, dt);
        _surSm  = Mathf.SmoothDamp(_surSm, spike, ref _surVel, 0.05f, Mathf.Infinity, dt);
        float f = _fearSm, s = Mathf.Clamp01(_surSm);

        Vector3 up = Vector3.up;
        Vector3 right = transform.right;   // avatar-space +X (character's left side bones sit at +X)

        for (int i = 0; i < 2; i++)
        {
            Vector3 toNose = -right * (i == 0 ? 1f : -1f);   // left brow (+X) moves toward -X
            float n = Mathf.PerlinNoise(_noiseSeed + i * 7.1f, time * 9f) * 2f - 1f;
            Drive(_inner[i], up * (FearInnerUp * f + SurpriseBrowUp * s + n * Tremble) + toNose * (FearInnerIn * f), dt);
            Drive(_mid[i],   up * (FearMidUp * f + SurpriseBrowUp * s + n * Tremble), dt);
            Drive(_outer[i], up * (-FearOuterDown * f + SurpriseBrowUp * s * 0.8f + n * Tremble), dt);

            var eye = _eye[i];
            if (eye != null)
            {
                float en = Mathf.PerlinNoise(_noiseSeed + 20f + i * 3.3f, time * 12f) * 2f - 1f;
                float scale = 1f + FearEyeScale * f + SurpriseEyeScale * s + en * 0.012f;
                eye.t.localScale = eye.restScale * scale;
                Drive(eye, up * (SurpriseEyeUp * s), dt);
            }
        }
        if (_center != null) Drive(_center, up * (FearInnerUp * 0.5f * f + SurpriseBrowUp * s), dt);
    }

    // Offset a bone by a WORLD-space delta from its rest position (converted into the parent's local space), eased.
    private void Drive(Bone b, Vector3 worldDelta, float dt)
    {
        if (b == null || b.t == null) return;
        var parent = b.t.parent;
        Vector3 localDelta = parent != null ? parent.InverseTransformVector(worldDelta) : worldDelta;
        Vector3 target = b.restPos + localDelta;
        b.smoothPos = Vector3.SmoothDamp(b.smoothPos, target, ref b.vel, Smoothing, Mathf.Infinity, dt);
        b.t.localPosition = b.smoothPos;
    }

    private float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
}
