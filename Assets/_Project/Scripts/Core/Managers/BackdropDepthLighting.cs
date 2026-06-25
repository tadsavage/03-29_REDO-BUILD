using UnityEngine;

/// <summary>
/// Theater-style "groundrow" fill lights placed between the CityBackdrop's depth layers
/// (Background / Midground / Foreground.001 / Foreground), lighting each flat from below
/// to separate the layers and add depth — like backlighting between theater set flats.
///
/// Ultra-preset only: this is pure visual flourish with a real per-frame light cost (several
/// realtime point lights per side), so it's fully disabled on Good/Toaster. Lives on the
/// GraphicsPresetManager GameObject and polls CurrentPreset (no event exists on that class to
/// hook synchronously, and polling every 0.5s is cheap and reacts fast enough for a settings
/// change). Toggles the light-rig parents' active state directly rather than going through
/// GraphicsPresetManager itself, so this stays fully self-contained — no changes needed there.
/// </summary>
public class BackdropDepthLighting : MonoBehaviour
{
    [Tooltip("One parent per CityBackdrop side, each holding that side's depth-gap fill lights.")]
    [SerializeField] private GameObject[] lightRigs;

    private GraphicsPresetManager.Preset? _lastApplied;

    private void OnEnable()
    {
        Refresh();
        InvokeRepeating(nameof(Refresh), 0.5f, 0.5f);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(Refresh));
    }

    private void Refresh()
    {
        var preset = GraphicsPresetManager.Instance != null
            ? GraphicsPresetManager.Instance.CurrentPreset
            : GraphicsPresetManager.Preset.Ultra;

        if (_lastApplied == preset) return;
        _lastApplied = preset;

        bool active = preset == GraphicsPresetManager.Preset.Ultra;
        foreach (var rig in lightRigs)
        {
            if (rig != null) rig.SetActive(active);
        }
    }
}
