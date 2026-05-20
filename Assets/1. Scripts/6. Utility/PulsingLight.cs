using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class EmissionPulse : MonoBehaviour
{
    [Header("Emission Settings")]
    public Color emissionColor = Color.red;
    public float minIntensity = 0.5f;
    public float maxIntensity = 5f;
    public float pulseSpeed = 2f;

    Renderer _renderer;
    MaterialPropertyBlock _mpb;
    static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f; // 0–1
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(EmissionColorID, emissionColor * intensity);
        _renderer.SetPropertyBlock(_mpb);
    }
}
