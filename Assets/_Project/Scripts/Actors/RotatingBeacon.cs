using UnityEngine;

/// <summary>
/// Police-siren style rotating beacon in orange, for the amber safety lights on the MHE (dock stocker, reach truck).
/// No real Light: an invisible beam direction sweeps a full 360 degrees every <see cref="secondsPerTurn"/> and the lamp's
/// EMISSION brightens as that beam points at the camera, then dims as it turns away, so it reads as a rotating reflector.
/// Drives a MaterialPropertyBlock only (no material instances, SRP-batcher friendly), no allocation per frame.
/// </summary>
[DisallowMultipleComponent]
public class RotatingBeacon : MonoBehaviour
{
    [Tooltip("Seconds for one full 360 degree sweep.")]
    [SerializeField] private float secondsPerTurn = 1f;
    [Tooltip("Emission when the beam points away from the camera.")]
    [SerializeField] private float dimIntensity = 0.15f;
    [Tooltip("Emission (HDR, so bloom picks it up) when the beam points at the camera.")]
    [SerializeField] private float peakIntensity = 5f;
    [Tooltip("Higher = a narrower, sharper flash.")]
    [SerializeField] private float beamSharpness = 3f;
    [SerializeField] private Color emissionColor = new Color(1f, 0.38f, 0f, 1f);
    [Tooltip("Also spin this transform around its up axis (cosmetic; the lamp box itself is symmetric).")]
    [SerializeField] private bool spinTransform = true;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    Renderer[] _renderers;
    MaterialPropertyBlock _mpb;
    Camera _cam;
    float _phase;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
        _phase = Random.value * 360f;   // fleet beacons shouldn't all flash in unison
    }

    void LateUpdate()
    {
        if (_renderers == null || _renderers.Length == 0) return;
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

        float turn = secondsPerTurn <= 0.01f ? 1f : secondsPerTurn;
        float angle = (Time.time * 360f / turn + _phase) % 360f;
        if (spinTransform) transform.localRotation = Quaternion.Euler(0f, angle, 0f);

        // Beam direction in world space (horizontal, sweeping around the world up axis).
        Vector3 beam = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
        Vector3 toCam = _cam.transform.position - transform.position;
        toCam.y = 0f;
        float facing = toCam.sqrMagnitude > 1e-4f ? Mathf.Max(0f, Vector3.Dot(beam, toCam.normalized)) : 1f;
        float k = Mathf.Pow(facing, beamSharpness);
        float intensity = Mathf.Lerp(dimIntensity, peakIntensity, k);

        // _mpb is not serialized: after a script recompile in Play _renderers survives but this comes back null, and the NullReferenceException
        // fired every frame (a 15 GB Editor log, 2026-11-12). Rebuild it.
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        _mpb.SetColor(EmissionId, emissionColor * intensity);
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_mpb);
    }
}
