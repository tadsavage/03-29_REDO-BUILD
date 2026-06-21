using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pushes the active CavityVolumeComponent's blended values onto Cavity_Material06-07
/// every frame, so Volumes (global or local, e.g. BuildModeOverride) can tune/fade the
/// cavity effect's intensity/radius/sharpness/multiplier the same way the built-in post
/// stack blends Depth of Field. The FullScreenPassRendererFeature keeps rendering with
/// this one material throughout — only its float properties change.
///
/// Setup: add this component anywhere persistent in the scene (e.g. alongside
/// GraphicsPresetManager) and assign Cavity_Material06-07.mat to 'cavityMaterial'.
/// </summary>
[ExecuteAlways]
public class CavityVolumeDriver : MonoBehaviour
{
    [Tooltip("Assign Cavity_Material06-07.mat here — the same material referenced by the FullScreenPassRendererFeature.")]
    [SerializeField] private Material cavityMaterial;

    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    private static readonly int RadiusID = Shader.PropertyToID("_Radius");
    private static readonly int SharpnessID = Shader.PropertyToID("_Sharpness");
    private static readonly int MultiplierID = Shader.PropertyToID("_Multiplier");

    private void Update()
    {
        if (cavityMaterial == null) return;

        // Use TryGet on the stack for safety
        if (VolumeManager.instance.stack.GetComponent<CavityVolumeComponent>() is CavityVolumeComponent cavity && cavity.active)
        {
            cavityMaterial.SetFloat(IntensityID, cavity.intensity.value);
            cavityMaterial.SetFloat(RadiusID, cavity.radius.value);
            cavityMaterial.SetFloat(SharpnessID, cavity.sharpness.value);
            cavityMaterial.SetFloat(MultiplierID, cavity.multiplier.value);
        }
    }
}
