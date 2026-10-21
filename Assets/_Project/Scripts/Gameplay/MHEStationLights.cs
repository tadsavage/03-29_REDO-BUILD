using UnityEngine;

/// <summary>
/// The three lamps on an MHE charging station (MHE_Station's child meshes DeadBattery / Charging / FullCharge),
/// lit by the charge level of whatever machine is parked on it (Tad, 2026-10-20):
///   • RED    - under 50% charge.
///   • YELLOW - 50% or more, still charging.
///   • GREEN  - 100%, fully charged.
///   • all off - nothing on the charger.
/// A lamp is switched off by swapping its authored glowing material for one shared dark "off" material (the mesh
/// stays, so the post never shows a hole) and switched on by putting the authored material back. (It used to black
/// out the glow with a MaterialPropertyBlock, but the base colour of that block was not applied reliably, so the
/// three off lamps came out three different shades.)
/// </summary>
[DisallowMultipleComponent]
public class MHEStationLights : MonoBehaviour
{
    private const string OffMaterialPath = "Resource_SOs/MHE_LampOff";
    /// <summary>Charge fraction below which the lamp is red (0.5 = 50%).</summary>
    private const float RedBelow = 0.5f;

    private Renderer _red, _yellow, _green;
    private PalletJackParkingSpot _spot;
    private Material _offMat;
    private readonly System.Collections.Generic.Dictionary<Renderer, Material> _onMat = new System.Collections.Generic.Dictionary<Renderer, Material>();
    private int _state = -1;   // 0 off, 1 red, 2 yellow, 3 green
    private float _nextCheck;

    private void Start()
    {
        _spot = GetComponent<PalletJackParkingSpot>();
        _red = Find("DeadBattery");
        _yellow = Find("Charging");
        _green = Find("FullCharge");

        // Remember each lamp's authored (glowing) material before anything swaps it.
        foreach (var r in new[] { _red, _yellow, _green })
            if (r != null) _onMat[r] = r.sharedMaterial;

        _offMat = Resources.Load<Material>(OffMaterialPath);
        if (_offMat == null && _red != null && _red.sharedMaterial != null)
        {
            // Asset missing: build a plain dark material from the lamps' own shader so "off" still reads as off.
            _offMat = new Material(_red.sharedMaterial.shader) { name = "MHE_LampOff (runtime)" };
            var dark = new Color(0.10f, 0.10f, 0.11f, 1f);
            if (_offMat.HasProperty("_BaseColor")) _offMat.SetColor("_BaseColor", dark);
            if (_offMat.HasProperty("_Color")) _offMat.SetColor("_Color", dark);
            _offMat.DisableKeyword("_EMISSION");
        }
    }

    private Renderer Find(string child)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == child) return t.GetComponent<Renderer>();
        return null;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 0.25f;

        EquipmentBattery docked = null;
        foreach (var b in EquipmentBattery.All)
            if (b != null && _spot != null && b.Station == _spot) { docked = b; break; }

        // By the parked machine's charge level (Tad, 2026-10-20): under 50% red, 50% or more yellow, full green.
        int state = docked == null ? 0
                  : docked.IsFull ? 3
                  : docked.Charge01 < RedBelow ? 1
                  : 2;
        if (state == _state) return;
        _state = state;
        Set(_red, state == 1);
        Set(_yellow, state == 2);
        Set(_green, state == 3);
    }

    private void Set(Renderer r, bool on)
    {
        if (r == null) return;
        r.SetPropertyBlock(null);   // clear any block left by the old approach
        if (on) { if (_onMat.TryGetValue(r, out var lit) && lit != null) r.sharedMaterial = lit; }
        else if (_offMat != null) r.sharedMaterial = _offMat;
    }
}
