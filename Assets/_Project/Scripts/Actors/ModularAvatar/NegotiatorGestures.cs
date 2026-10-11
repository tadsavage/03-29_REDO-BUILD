using UnityEngine;

/// <summary>
/// Drives the customer's arms and torso by CODE (Tad, 2026-10-11: no angry-wave or hand-to-chin clips exist), on top of a
/// rig whose Animator is switched off - so the arms are posed from scratch every frame.
///
/// Each arm is a two-bone aim: the shoulder is rotated until the upper arm points along a target direction, then the elbow
/// until the forearm does. Aiming is done in WORLD space from the avatar's own forward/right/up, so it does not depend on
/// how the bones happen to be oriented in the FBX. Gestures are blended by weight, and the weights follow "heat" - how close
/// the customer is to refusing the deal: calm (idle + an occasional wave), thinking (hand to chin), gesticulating, furious.
///
/// Runs on UNSCALED time: the Contracts window pauses the game, and the customer must keep moving anyway.
/// </summary>
[DefaultExecutionOrder(-200)]   // runs BEFORE FaceExpressionController, so the bind pose is restored before the face adds its offsets
public class NegotiatorGestures : MonoBehaviour
{
    /// <summary>score / budget of the offer on the table (see NegotiationSession.Appetite).</summary>
    public float Heat;

    // Bands, on the same scale as the DEAL HEAT gauge.
    const float ThinkStart = 0.92f, GestureStart = 1.12f, AngryStart = 1.32f;

    Transform _root, _chest, _spine;
    Arm _armR, _armL;
    Quaternion _chest0, _spine0;
    bool _ready;

    float _wThink, _wGesture, _wAngry, _wWave;
    float _waveLeft, _nextWave = 4f;
    float _forceAngryLeft, _forceWaveLeft;
    float _phase;

    class Arm
    {
        public Transform Shoulder, Elbow, Wrist;
        public Quaternion Shoulder0, Elbow0;
        public float Side;          // +1 / -1 along the avatar's right axis (which way is "outward")
        public bool IsRightHand;    // the character's right hand does the chin pose
    }

    // Bind pose of every bone. With the Animator off nothing resets the rig between frames, and the face controller ADDS rotation
    // to the head/neck/lids each frame - left alone that compounds until the head hangs on its chest. So every frame starts
    // from this snapshot.
    Transform[] _all;
    Vector3[] _pos0, _scale0;
    Quaternion[] _rot0;

    void Awake()
    {
        _root = transform;
        _all = GetComponentsInChildren<Transform>(true);
        _pos0 = new Vector3[_all.Length]; _scale0 = new Vector3[_all.Length]; _rot0 = new Quaternion[_all.Length];
        for (int i = 0; i < _all.Length; i++) { _pos0[i] = _all[i].localPosition; _rot0[i] = _all[i].localRotation; _scale0[i] = _all[i].localScale; }
        _chest = Find("Chest_M"); _spine = Find("Spine1_M");
        _armR = MakeArm("R", true); _armL = MakeArm("L", false);
        _ready = _armR != null && _armL != null;
        if (_chest != null) _chest0 = _chest.localRotation;
        if (_spine != null) _spine0 = _spine.localRotation;
        _phase = Random.value * 10f;
    }

    Arm MakeArm(string suffix, bool right)
    {
        var sh = Find("Shoulder_" + suffix); var el = Find("Elbow_" + suffix); var wr = Find("Wrist_" + suffix);
        if (sh == null || el == null || wr == null) return null;
        float side = Mathf.Sign(Vector3.Dot(sh.position - _root.position, _root.right));
        if (Mathf.Approximately(side, 0f)) side = right ? 1f : -1f;
        return new Arm { Shoulder = sh, Elbow = el, Wrist = wr, Shoulder0 = sh.localRotation, Elbow0 = el.localRotation, Side = side, IsRightHand = right };
    }

    Transform Find(string boneName)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == boneName) return t;
        return null;
    }

    /// <summary>A short burst of full anger regardless of the gauge (the customer just said no).</summary>
    public void BurstAngry(float seconds = 2.6f) { _forceAngryLeft = seconds; }

    /// <summary>A friendly wave now (a deal was struck).</summary>
    public void BurstWave(float seconds = 2.6f) { _forceWaveLeft = seconds; _waveLeft = seconds; }

    void LateUpdate()
    {
        if (!_ready) return;
        for (int i = 0; i < _all.Length; i++)
        {
            if (_all[i] == null) continue;
            _all[i].localPosition = _pos0[i]; _all[i].localRotation = _rot0[i]; _all[i].localScale = _scale0[i];
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        float t = Time.unscaledTime + _phase;

        if (_forceAngryLeft > 0f) _forceAngryLeft -= dt;
        if (_forceWaveLeft > 0f) _forceWaveLeft -= dt;

        // Weights follow heat; a forced anger burst lifts the angry weight whatever the gauge says.
        float think = Band(Heat, ThinkStart, GestureStart);
        float gest = Band(Heat, GestureStart, AngryStart);
        float angry = Mathf.Clamp01((Heat - AngryStart) / 0.12f);
        if (_forceAngryLeft > 0f) angry = 1f;
        float k = 1f - Mathf.Exp(-5f * dt);
        _wThink = Mathf.Lerp(_wThink, angry > 0.5f ? 0f : think, k);
        _wGesture = Mathf.Lerp(_wGesture, angry > 0.5f ? 0f : gest, k);
        _wAngry = Mathf.Lerp(_wAngry, angry, k);

        // Occasional wave while calm.
        bool calm = Heat < ThinkStart && _forceAngryLeft <= 0f;
        if (_waveLeft > 0f) _waveLeft -= dt;
        else if (calm)
        {
            _nextWave -= dt;
            if (_nextWave <= 0f) { _waveLeft = 2.2f; _nextWave = Random.Range(6f, 11f); }
        }
        float waveTarget = (_waveLeft > 0f && (calm || _forceWaveLeft > 0f)) ? 1f : 0f;
        _wWave = Mathf.Lerp(_wWave, waveTarget, 1f - Mathf.Exp(-7f * dt));

        float wRest = Mathf.Clamp01(1f - _wThink - _wGesture - _wAngry);

        PoseArm(_armR, t, wRest, true);
        PoseArm(_armL, t, wRest, false);

        // Breathing and, when angry, a shaking torso.
        if (_chest != null)
            _chest.localRotation = _chest0 * Quaternion.Euler(Mathf.Sin(t * 1.3f) * 0.8f, Mathf.Sin(t * 0.6f) * 1.2f + Mathf.Sin(t * 13f) * 2.2f * _wAngry, Mathf.Sin(t * 11f) * 1.6f * _wAngry);
        if (_spine != null)
            _spine.localRotation = _spine0 * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * 0.6f);
    }

    static float Band(float v, float a, float b)
    {
        // 1 inside [a, b], ramping in and out over a small margin so gestures cross-fade instead of snapping.
        const float m = 0.05f;
        return Mathf.Clamp01((v - (a - m)) / (2f * m)) * Mathf.Clamp01(((b + m) - v) / (2f * m));
    }

    // Targets are written for the CHARACTER'S RIGHT arm in (outward, up, forward) terms; the left arm mirrors the outward axis.
    void PoseArm(Arm arm, float t, float wRest, bool isRight)
    {
        arm.Shoulder.localRotation = arm.Shoulder0;
        arm.Elbow.localRotation = arm.Elbow0;

        Vector3 outward = _root.right * arm.Side, up = _root.up, fwd = _root.forward;
        Vector3 Dir(float o, float y, float z) => (outward * o + up * y + fwd * z).normalized;

        // Resting: arms hanging, elbows soft.
        Vector3 uRest = Dir(0.12f, -1f, 0.02f), fRest = Dir(0.05f, -0.92f, 0.38f);

        // Thinking: right hand to the chin, left forearm across the body supporting that elbow.
        Vector3 uThink, fThink;
        if (isRight) { uThink = Dir(0.10f, -0.90f, 0.42f); fThink = Dir(-0.30f, 0.55f, 0.62f); }
        else         { uThink = Dir(0.10f, -0.92f, 0.25f); fThink = Dir(-0.90f, 0.12f, 0.45f); }

        // Gesticulating: both forearms up in front, hands chopping side to side.
        float osc = Mathf.Sin(t * 5.5f + (isRight ? 0f : 1.6f));
        Vector3 uGest = Dir(0.45f, -0.72f, 0.40f);
        Vector3 fGest = Dir(0.05f + osc * 0.40f, 0.52f + Mathf.Cos(t * 7f) * 0.18f, 0.85f);

        // Furious: arms thrown up and flailing.
        float flail = Mathf.Sin(t * 12f + (isRight ? 0f : 2.2f));
        Vector3 uAng = Dir(0.88f, 0.50f + Mathf.Sin(t * 6f) * 0.15f, 0.10f);
        Vector3 fAng = Dir(0.20f + flail * 0.85f, 1f, 0.12f);

        // Friendly wave: only the right hand.
        Vector3 uWave = uRest, fWave = fRest;
        float waveW = isRight ? _wWave : 0f;
        if (isRight) { uWave = Dir(0.78f, 0.32f, 0.22f); fWave = Dir(0.05f + Mathf.Sin(t * 9f) * 0.55f, 1f, 0.15f); }

        Vector3 u = uRest * wRest + uThink * _wThink + uGest * _wGesture + uAng * _wAngry;
        Vector3 f = fRest * wRest + fThink * _wThink + fGest * _wGesture + fAng * _wAngry;
        u = Vector3.Lerp(u.normalized, uWave, waveW);
        f = Vector3.Lerp(f.normalized, fWave, waveW);

        Aim(arm.Shoulder, arm.Elbow.position - arm.Shoulder.position, u.normalized);
        Aim(arm.Elbow, arm.Wrist.position - arm.Elbow.position, f.normalized);
    }

    static void Aim(Transform bone, Vector3 current, Vector3 target)
    {
        if (current.sqrMagnitude < 1e-8f) return;
        bone.rotation = Quaternion.FromToRotation(current.normalized, target) * bone.rotation;
    }
}
