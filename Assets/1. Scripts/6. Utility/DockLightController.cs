using UnityEngine;

/// <summary>
/// Manages the red/green indicator lights on a ShippingDoor.
/// Finds "GreenLight" and "RedLight" by name anywhere in children.
/// Default: green on, red off. Uses MaterialPropertyBlock so shared
/// materials are never modified — all doors stay independent.
/// </summary>
public class DockLightController : MonoBehaviour
{
    private Renderer _green;
    private Renderer _red;
    private Color    _greenEmission;
    private Color    _redEmission;

    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "GreenLight" && _green == null) _green = t.GetComponent<Renderer>();
            if (t.name == "RedLight"   && _red   == null) _red   = t.GetComponent<Renderer>();
        }

        if (_green == null) Debug.LogWarning($"[DockLightController] GreenLight not found on {name}.");
        if (_red   == null) Debug.LogWarning($"[DockLightController] RedLight not found on {name}.");

        _greenEmission = ReadEmission(_green, Color.green);
        _redEmission   = ReadEmission(_red,   Color.red);

        SetOccupied(false); // default: green on, red off
    }

    /// <summary>
    /// false = door available (green on, red off).
    /// true  = truck docked   (green off, red on).
    /// </summary>
    public void SetOccupied(bool occupied)
    {
        WriteEmission(_green, occupied ? Color.black : _greenEmission);
        WriteEmission(_red,   occupied ? _redEmission : Color.black);
    }

    private static Color ReadEmission(Renderer r, Color fallback)
    {
        if (r == null) return fallback;
        var mat = r.sharedMaterial;
        return (mat != null && mat.HasProperty("_EmissionColor"))
            ? mat.GetColor("_EmissionColor")
            : fallback;
    }

    private void WriteEmission(Renderer r, Color color)
    {
        if (r == null) return;
        _mpb.SetColor("_EmissionColor", color);
        r.SetPropertyBlock(_mpb);
    }
}
