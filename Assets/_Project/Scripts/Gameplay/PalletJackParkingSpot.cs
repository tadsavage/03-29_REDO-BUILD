using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A 3x1-cell painted bay (build menu: Vehicles → Pallet Jack Parking) where order selectors leave their pallet
/// jack when an order is staged, instead of abandoning it in the lane mouth where it blocks everyone (Tad,
/// 2026-10-16). Later a battery charger can live here and charge the parked jack (see FUTURE_IDEAS.md).
///
/// The bay's long axis is the prefab's local X (its footprint is (-1,0)(0,0)(1,0) around the root cell). A
/// selector reserves a free bay before driving to it; the reservation becomes "parked" on arrival and is
/// dropped the moment any selector claims that jack again. A bay also reads as taken while any pallet jack is
/// physically standing in it (covers save/load and jacks placed straight onto it).
/// </summary>
public class PalletJackParkingSpot : MonoBehaviour
{
    /// <summary>PltJack_FULL's body runs 1.26 m in front of the operator anchor to 2.54 m behind it, so the
    /// anchor sits this far ahead of the body's middle.</summary>
    private const float JackBodyCenterOffset = 0.64f;
    private const float OccupiedRadius = 1.6f;
    private const float StaleReservationDistance = 4f;

    private static readonly List<PalletJackParkingSpot> s_all = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_all.Clear();

    private MHEOperatorSlot _reservedFor;

    public static IReadOnlyList<PalletJackParkingSpot> All => s_all;

    /// <summary>A real station whose centre is within <paramref name="radius"/> of <paramref name="point"/> (flat), or null.</summary>
    public static PalletJackParkingSpot StationWithin(Vector3 point, float radius)
    {
        foreach (var s in s_all)
            if (s != null && s.IsReal && Flat(point - s.Center).magnitude < radius) return s;
        return null;
    }

    private void OnEnable() { if (!s_all.Contains(this)) s_all.Add(this); }
    private void OnDisable() => s_all.Remove(this);

    /// <summary>Only a real placed bay counts (the build-menu hover ghost is the same prefab).</summary>
    private bool IsReal
    {
        get
        {
            var po = GetComponent<PlacedObject>();
            return po != null && po.data != null && isActiveAndEnabled;
        }
    }

    private Vector3 Center => transform.position;

    /// <summary>The bay's long axis, pointing at the charger post. Read from the model rather than assumed: the
    /// first bay ran along local X, Tad's MHE_Station runs along local Z with the post at +Z and the AlphaID letter
    /// at the open end (2026-10-17 - jacks were parking sideways across the new bays).</summary>
    private Vector3 Axis
    {
        get
        {
            if (!_axisResolved)
            {
                _axisResolved = true;
                var mf = GetComponent<MeshFilter>();
                Vector3 size = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : new Vector3(1f, 0f, 0f);
                _localAxis = size.z > size.x ? Vector3.forward : Vector3.right;
                var letter = transform.Find("AlphaID");
                if (letter != null && Vector3.Dot(letter.localPosition, _localAxis) > 0.05f) _localAxis = -_localAxis;   // post is away from the letter
            }
            return transform.TransformDirection(_localAxis);
        }
    }
    private bool _axisResolved;
    private Vector3 _localAxis = Vector3.right;

    /// <summary>Where the jack's operator anchor goes, and which way it faces: always nose-in toward the charger
    /// post, which sits at the bay's +X end (the MHE_Charger model).</summary>
    public void ParkPose(Vector3 jackForward, out Vector3 origin, out Vector3 forward)
    {
        forward = Axis; forward.y = 0f; forward.Normalize();
        origin = Center + forward * JackBodyCenterOffset;
    }

    private bool IsFreeFor(MHEOperatorSlot jack)
    {
        if (!IsReal) return false;
        if (_reservedFor != null && _reservedFor != jack)
        {
            // A machine driving here to CHARGE holds its bay the whole way (it reserves from across the building,
            // so the distance test below would drop it at once and every machine piled onto one bay - 2026-10-17).
            var rb = _reservedFor.GetComponent<EquipmentBattery>();
            if (rb != null && rb.OnChargeTrip) return false;
            // A reservation whose jack has since gone somewhere else is stale (it was driven off without
            // releasing, or the save was reloaded).
            if (Flat(_reservedFor.transform.position - Center).magnitude < StaleReservationDistance) return false;
            _reservedFor = null;
        }
        // Any machine standing in the bay takes it (reach trucks and dock stockers charge here too).
        foreach (var b in EquipmentBattery.All)
        {
            if (b == null || b.Slot == null || b.Slot == jack) continue;
            if (Flat(b.BodyCentre - Center).magnitude < OccupiedRadius) return false;
        }
        return true;
    }

    public void Reserve(MHEOperatorSlot jack) => _reservedFor = jack;

    public void Release(MHEOperatorSlot jack)
    {
        if (_reservedFor == jack) _reservedFor = null;
    }

    /// <summary>Called when a selector takes a jack: whatever bay it was parked in is free again.</summary>
    public static void ReleaseJack(MHEOperatorSlot jack)
    {
        foreach (var s in s_all) if (s != null) s.Release(jack);
    }

    public static PalletJackParkingSpot FindNearestFree(Vector3 from, MHEOperatorSlot jack)
    {
        PalletJackParkingSpot best = null;
        float bestD = float.MaxValue;
        foreach (var s in s_all)
        {
            if (s == null || !s.IsFreeFor(jack)) continue;
            float d = Flat(s.Center - from).magnitude;
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    /// <summary>Nearest real station, free or not (a machine with nowhere to charge parks beside one).</summary>
    public static PalletJackParkingSpot FindNearest(Vector3 from)
    {
        PalletJackParkingSpot best = null;
        float bestD = float.MaxValue;
        foreach (var s in s_all)
        {
            if (s == null || !s.IsReal) continue;
            float d = Flat(s.Center - from).magnitude;
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    /// <summary>Where a machine stands to charge here and which way it faces (nose toward the charger post).
    /// <paramref name="beside"/> = no free station: the same spot shifted off to the side of the bay.</summary>
    public void ChargePose(MHEOperatorSlot machine, bool beside, out Vector3 origin, out Vector3 forward)
    {
        forward = Axis; forward.y = 0f; forward.Normalize();
        // A pallet jack's anchor sits ahead of its body middle; other machines are centred on the bay.
        origin = Center + (machine != null && machine.IsPalletJack ? forward * JackBodyCenterOffset : Vector3.zero);
        if (beside) origin += Vector3.Cross(Vector3.up, forward).normalized * 2.4f;
    }

    public bool IsFree(MHEOperatorSlot machine) => IsFreeFor(machine);

    /// <summary>The charging station (every bay is an MHE_Station charger) a machine is standing in, or null.
    /// <paramref name="bodyCentre"/> is the middle of the machine's body, not its operator anchor.</summary>
    public static PalletJackParkingSpot ChargerAt(Vector3 bodyCentre)
    {
        foreach (var s in s_all)
            if (s != null && s.IsReal && Flat(bodyCentre - s.Center).magnitude < OccupiedRadius) return s;
        return null;
    }

    /// <summary>Middle of a pallet jack's body (its forks trail behind the operator anchor).</summary>
    public static Vector3 JackBodyCentre(Transform jack) => jack.position - jack.forward * JackBodyCenterOffset;

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
