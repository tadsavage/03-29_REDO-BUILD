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
    private Vector3 Axis => transform.right;

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
            // A reservation whose jack has since gone somewhere else is stale (it was driven off without
            // releasing, or the save was reloaded).
            if (Flat(_reservedFor.transform.position - Center).magnitude < StaleReservationDistance) return false;
            _reservedFor = null;
        }
        foreach (var slot in FindObjectsByType<MHEOperatorSlot>())
        {
            if (slot == null || slot == jack || !slot.IsPalletJack) continue;
            Vector3 body = slot.transform.position - slot.transform.forward * JackBodyCenterOffset;
            if (Flat(body - Center).magnitude < OccupiedRadius) return false;
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

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
