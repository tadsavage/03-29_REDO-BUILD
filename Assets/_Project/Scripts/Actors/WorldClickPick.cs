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
        int n = Physics.RaycastNonAlloc(ray, Buffer, 500f, ~0, QueryTriggerInteraction.Ignore);
        best = default;
        float bestDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var h = Buffer[i];
            var c = h.collider;
            if (c == null) continue;
            if (_groundLayer >= 0 && c.gameObject.layer == _groundLayer) continue;
            if (c.GetComponentInParent<PalletJackParkingSpot>() != null && c.GetComponentInParent<EquipmentBattery>() == null) continue;
            if (h.distance < bestDist) { bestDist = h.distance; best = h; found = true; }
        }
        return found;
    }

    /// <summary>True when the nearest real thing under the ray belongs to <paramref name="owner"/> (the collider is on it or a child).</summary>
    public static bool Hits(Ray ray, Transform owner)
    {
        if (!Raycast(ray, out var hit)) return false;
        return hit.collider.transform == owner || hit.collider.transform.IsChildOf(owner);
    }
}
