using UnityEngine;

/// <summary>
/// The three lamps on an MHE charging station (MHE_Station's child meshes DeadBattery / Charging / FullCharge),
/// lit by the battery of whatever machine is parked on it (Tad, 2026-10-17):
///   • RED    - the machine is (nearly) dead: within one game hour of a flat battery.
///   • YELLOW - charging.
///   • GREEN  - fully charged.
///   • all off - nothing on the charger.
/// A lamp is switched off by blacking out its glow with a MaterialPropertyBlock (the mesh stays, so the post
/// never shows a hole); switching it on clears the block and the authored emissive material shows again.
/// </summary>
[DisallowMultipleComponent]
public class MHEStationLights : MonoBehaviour
{
    private Renderer _red, _yellow, _green;
    private PalletJackParkingSpot _spot;
    private MaterialPropertyBlock _off;
    private int _state = -1;   // 0 off, 1 red, 2 yellow, 3 green
    private float _nextCheck;

    private void Start()
    {
        _spot = GetComponent<PalletJackParkingSpot>();
        _red = Find("DeadBattery");
        _yellow = Find("Charging");
        _green = Find("FullCharge");
        _off = new MaterialPropertyBlock();
        _off.SetColor("_EmissionColor", Color.black);
        _off.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f, 1f));
        _off.SetColor("_Color", new Color(0.12f, 0.12f, 0.12f, 1f));
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

        int state = docked == null ? 0
                  : docked.IsFull ? 3
                  : docked.HoursLeft <= EquipmentBattery.LowHours ? 1
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
        r.SetPropertyBlock(on ? null : _off);
    }
}
