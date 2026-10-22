using System;
using UnityEngine;

/// <summary>
/// Shared "what did the player click in the world" raycast for people and machines. A plain Physics.Raycast returns only
/// the single CLOSEST collider, so anything thin lying over the target (floor tiles, a charging bay's pad, trigger zones, a
/// contact-shadow quad) silently swallowed the click and the info card / battery panel only showed up some of the time.
/// This skips triggers, the Ground layer and charging-bay pads and returns the nearest real thing.
/// </summary>
public static class WorldClickPick
{
    private static readonly RaycastHit[] Buffer = new RaycastHit[48];
    private static int _groundLayer = -2;

    public static bool Raycast(Ray ray, out RaycastHit best)
    {
        if (_groundLayer == -2) _groundLayer = LayerMask.NameToLayer("Ground");
        // RaycastNonAlloc does NOT keep the nearest hits when the buffer fills: it drops arbitrary ones, so a ray through
        // a loaded rack (hundreds of case/pallet colliders) lost the employee/machine at random - the "only sometimes
        // opens" bug. If the buffer is full, fall back to RaycastAll, which returns everything.
        // Triggers ARE queried: every employee's click collider is a trigger (the worker prefabs' BoxCollider), so ignoring
        // triggers outright made every worker unclickable. Triggers that don't belong to a click target are skipped below.
        RaycastHit[] hits = Buffer;
        int n = Physics.RaycastNonAlloc(ray, Buffer, 500f, ~0, QueryTriggerInteraction.Collide);
        if (n >= Buffer.Length)
        {
            hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
            n = hits.Length;
        }
        best = default;
        float bestDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            var c = h.collider;
            if (c == null) continue;
            if (c.isTrigger && !IsClickTarget(c)) continue;
            if (_groundLayer >= 0 && c.gameObject.layer == _groundLayer) continue;
            if (c.GetComponentInParent<PalletJackParkingSpot>() != null && c.GetComponentInParent<EquipmentBattery>() == null) continue;
            if (h.distance < bestDist) { bestDist = h.distance; best = h; found = true; }
        }
        return found;
    }

    /// <summary>A trigger collider counts only when it belongs to something the player clicks: a person or a machine.
    /// Every other trigger (zones, sensors, a truck's cargo box) stays click-through.</summary>
    private static bool IsClickTarget(Collider c) =>
        c.GetComponentInParent<EmployeeClickHandler>() != null
        || c.GetComponentInParent<MHEOperatorSlot>() != null
        || c.GetComponentInParent<EquipmentBattery>() != null;

    /// <summary>True when the nearest real thing under the ray belongs to <paramref name="owner"/> (the collider is on it or a child).</summary>
    public static bool Hits(Ray ray, Transform owner)
    {
        if (!Raycast(ray, out var hit)) return false;
        return hit.collider.transform == owner || hit.collider.transform.IsChildOf(owner);
    }
}
