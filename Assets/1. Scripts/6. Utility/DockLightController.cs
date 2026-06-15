using UnityEngine;

/// <summary>
/// Manages the red/green indicator lights on a ShippingDoor.
/// Finds "GreenLight" and "RedLight" by name anywhere in children.
/// Default: green on, red off. Uses MaterialPropertyBlock so shared
/// materials are never modified — all doors stay independent.
/// </summary>
public class DockLightController : MonoBehaviour
{
    private Renderer _greenInt;
    private Renderer _redInt;
    private Renderer _greenExt;
    private Renderer _redExt;
    private Color    _greenEmission;
    private Color    _redEmission;

    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "GreenLight-INT" && _greenInt == null) _greenInt = t.GetComponent<Renderer>();
            if (t.name == "RedLight-INT"   && _redInt   == null) _redInt   = t.GetComponent<Renderer>();
            if (t.name == "GreenLight-EXT" && _greenExt == null) _greenExt = t.GetComponent<Renderer>();
            if (t.name == "RedLight-EXT"   && _redExt   == null) _redExt   = t.GetComponent<Renderer>();
        }

        if (_greenInt == null) Debug.LogWarning($"[DockLightController] GreenLight-INT not found on {name}.");
        if (_redInt   == null) Debug.LogWarning($"[DockLightController] RedLight-INT not found on {name}.");
        if (_greenExt == null) Debug.LogWarning($"[DockLightController] GreenLight-EXT not found on {name}.");
        if (_redExt   == null) Debug.LogWarning($"[DockLightController] RedLight-EXT not found on {name}.");

        _greenEmission = ReadEmission(_greenInt, Color.green);
        _redEmission   = ReadEmission(_redInt,   Color.red);

        SetOccupied(false); // default: green on, red off
    }

    /// <summary>
    /// false = door available (green on, red off).
    /// true  = truck docked   (green off, red on).
    /// </summary>
    public void SetOccupied(bool occupied)
    {
        WriteEmission(_greenInt, occupied ? _greenEmission : Color.black); 
        WriteEmission(_redInt,   occupied ? Color.black : _redEmission);
        WriteEmission(_greenExt, occupied ? Color.black : _greenEmission);
        WriteEmission(_redExt,   occupied ? _redEmission : Color.black);
        Debug.Log($"[DockLightController] SetOccupied({occupied}) on {name}.");
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
