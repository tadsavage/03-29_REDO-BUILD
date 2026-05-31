using UnityEngine;

/// <summary>
/// Pulses emission between MinIntensity and MaxIntensity.
/// EmissionColor is set explicitly so the underlying material colour/texture
/// never bleeds through and changes the hue at low intensities.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class LightPulse : MonoBehaviour
{
    [SerializeField] public Color EmissionColor = Color.red;
    [SerializeField] public float MinIntensity  = 2f;
    [SerializeField] public float MaxIntensity  = 8f;
    [SerializeField] public float PulseSpeed    = 1.5f;

    private Renderer _renderer;
    private Material _matInstance;

    private void Awake()
    {
        _renderer    = GetComponent<Renderer>();
        _matInstance = _renderer.material; // per-instance copy
        _matInstance.EnableKeyword("_EMISSION");
    }

    private void Update()
    {
        float t         = (Mathf.Sin(Time.time * PulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(MinIntensity, MaxIntensity, t);

        // Force base colour so the underlying texture never bleeds through
        if (_matInstance.HasProperty("_BaseColor"))
            _matInstance.SetColor("_BaseColor", EmissionColor);

        _matInstance.SetColor("_EmissionColor",
            new Color(EmissionColor.r * intensity,
                      EmissionColor.g * intensity,
                      EmissionColor.b * intensity, 1f));
    }

    private void OnDestroy()
    {
        if (_matInstance != null)
            Destroy(_matInstance);
    }
}
