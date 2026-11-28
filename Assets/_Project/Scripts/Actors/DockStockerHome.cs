using System.Collections.Generic;
using UnityEngine;
using GameCore.Actors;

/// <summary>
/// DOCK STOCKER HOME POCKETS (Tad, 2026-11-14). A dock stocker with nothing to do used to go to a random "plain floor" spot, which in a
/// building like "three doors aisle" is the corridor beyond the staging lanes - exactly where pallet jacks stage and reach trucks pick up
/// (blocks 42-52: dock stockers idled at x 4.6, z 51-54, behind lanes 3A-3E). It drove across the lanes to get there and took laps from there.
///
/// Now it waits at the doors, on the DOCK APRON (the strip between the doors and the dock end of the staging lanes):
/// - one pocket in each gap between two neighbouring doors on the same wall (one machine fits there, not two), plus one beyond each end
///   door of a wall as a fallback;
/// - the machine lies ALONG the wall, set back from it (<see cref="WallGap"/>), placed by its real outline (a dock stocker's pivot is near its
///   front: its body runs ~2.2-2.6 m behind it), so the strip in front of the lanes where cross-door trips drive stays clear;
/// - one dock stocker per pocket; a parked one that another machine is held behind moves to another pocket (EquipmentBattery make-way).
/// Block 53 showed why the last two matter: pockets in the middle of the apron sat on the only route between doors and six cross-door
/// offloads timed out behind a parked stocker (the 1 m vehicle spacing in AiNavigation.GuardStockerVehicleSpacing).
/// </summary>
public static class DockStockerHome
{
    /// <summary>Half the trailer opening plus a margin: no pocket body comes closer than this to a door's centre line.</summary>
    public const float DoorHalfOpening = 1.6f;
    /// <summary>Gap between the wall line (the door pivot) and the body near side: one grid cell (1.325 m, Tad 2026-11-20), so a parked stocker is never against the front wall.</summary>
    public const float WallGap = 0.9f;

    private struct Pocket { public Vector3 Centre, Pivot, Forward, Inward; public float CentreOff; public string Name; }

    /// <summary>MheParking.IsClear tests the machine's COLLIDER box padded by 0.15 m (a dock stocker's is ~2.0 m wide, wider than its outline),
    /// so at the wall-hugging depth it touches the wall and the door frame (block 54: no pocket was ever clear). Step the pocket away from
    /// the wall until it is clear, at most this far.</summary>
    private const float MaxPushFromWall = 1.25f, PushStep = 0.25f;

    private static readonly Dictionary<EquipmentBattery, Vector3> s_claims = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_claims.Clear();

    public static bool IsDockStocker(EquipmentBattery b)
        => b != null && b.Slot != null && !b.Slot.IsPalletJack && b.Slot.GetComponent<ReachTruckOperator>() == null;

    /// <summary>A parked stocker's "DOOR n-F" pocket is in front of a door that now has a trailer at it: it must move off the work strip.</summary>
    public static bool FrontDoorInUse(string pocketName)
    {
        if (string.IsNullOrEmpty(pocketName) || !pocketName.StartsWith("DOOR ") || !pocketName.EndsWith("-F")) return false;
        string num = pocketName.Substring(5, pocketName.Length - 7);
        foreach (var d in DockSlot.All)
            if (d != null && d.IsOccupied && d.DoorNumber.ToString() == num) return true;
        return false;
    }

    /// <summary>Forget this machine's pocket (it took a job, or is moving out of someone's way).</summary>
    public static void Release(EquipmentBattery b) { if (b != null) s_claims.Remove(b); }

    private static void BodyOf(EquipmentBattery who, out float halfW, out float halfL, out float centreOff)
    {
        halfW = 0.6f; halfL = 1.6f; centreOff = -1.0f;   // a dock stocker, measured 2026-11-14 (x -0.6..0.6, z -2.6..0.6)
        if (who == null || who.Slot == null) return;
        var fp = MachineClearance.FootprintOf(who.Slot);
        if (fp.MaxZ - fp.MinZ < 0.5f) return;
        halfW = (fp.MaxX - fp.MinX) * 0.5f;
        halfL = (fp.MaxZ - fp.MinZ) * 0.5f;
        centreOff = (fp.MinZ + fp.MaxZ) * 0.5f;
    }

    private static List<Pocket> BuildPockets(Vector3 from, EquipmentBattery who)
    {
        BodyOf(who, out float halfW, out float halfL, out float centreOff);
        var doors = new List<DockSlot>();
        foreach (var d in DockSlot.All) if (d != null && d.isActiveAndEnabled) doors.Add(d);

        var list = new List<(float dist, Pocket p)>();
        Vector3 inwardDir = Vector3.forward;
        void Add(Vector3 centre, Vector3 fwd, string name)
        {
            var p = new Pocket { Centre = centre, Forward = fwd, Pivot = centre - fwd * centreOff, Inward = inwardDir, CentreOff = centreOff, Name = name };
            Vector3 dd = centre - from; dd.y = 0f;
            list.Add((dd.magnitude, p));
        }

        foreach (var a in doors)
        {
            Vector3 pa = a.transform.position;
            Vector3 n = a.transform.forward; n.y = 0f;
            if (n.sqrMagnitude < 0.0001f) continue;
            n.Normalize();
            Vector3 t = Vector3.Cross(Vector3.up, n);   // along the wall
            Vector3 inward = n * (WallGap + halfW);
            inwardDir = n;

            // The nearest door on each side along the same wall (same facing, same wall line).
            DockSlot left = null, right = null;
            float bestL = float.MaxValue, bestR = float.MaxValue;
            foreach (var b in doors)
            {
                if (b == a) continue;
                Vector3 nb = b.transform.forward; nb.y = 0f;
                if (nb.sqrMagnitude < 0.0001f || Vector3.Dot(nb.normalized, n) < 0.95f) continue;
                Vector3 d = b.transform.position - pa; d.y = 0f;
                if (Mathf.Abs(Vector3.Dot(d, n)) > 1f) continue;
                float along = Vector3.Dot(d, t);
                if (along > 0f && along < bestR) { bestR = along; right = b; }
                if (along < 0f && -along < bestL) { bestL = -along; left = b; }
            }

            // RIGHT IN FRONT OF THE DOOR (Tad, 2026-11-22): lying along the wall, centred on the opening, set back WallGap. First choice: the
            // stocker is where its door's work happens and out of the other doors' way. A trailer docking here does not need this strip.
            // Not while a trailer is docked / claimed at this door: that strip is where the stockers drive in and out of it.
            if (!a.IsOccupied)
            {
                Vector3 front = pa + inward; front.y = pa.y;
                Add(front, t, $"DOOR {a.DoorNumber}-F");
            }
            // Between this door and its right-hand neighbour (each gap once): one machine, if its body fits clear of both openings.
            if (right != null && bestR - 2f * DoorHalfOpening >= 2f * halfL)
            {
                Vector3 mid = pa + t * (bestR * 0.5f) + inward;
                mid.y = pa.y;
                Add(mid, t, $"DOOR {a.DoorNumber}|{right.DoorNumber}");
            }
            // Beyond an end door of the wall (fallback): body just clear of the opening.
            if (left == null)
            {
                Vector3 c = pa - t * (DoorHalfOpening + halfL) + inward; c.y = pa.y;
                Add(c, -t, $"DOOR {a.DoorNumber}-L");
            }
            if (right == null)
            {
                Vector3 c = pa + t * (DoorHalfOpening + halfL) + inward; c.y = pa.y;
                Add(c, t, $"DOOR {a.DoorNumber}-R");
            }
        }
        // In front of a door first, then the gaps between two doors, then the end fallbacks; each by distance.
        int Rank(string n) => n.EndsWith("-F") ? 0 : n.Contains("|") ? 1 : 2;
        list.Sort((x, y) =>
        {
            int rx = Rank(x.p.Name), ry = Rank(y.p.Name);
            if (rx != ry) return rx.CompareTo(ry);
            return x.dist.CompareTo(y.dist);
        });
        var res = new List<Pocket>(list.Count);
        foreach (var e in list) res.Add(e.p);
        return res;
    }

    /// <summary>Every pocket for this machine: Pos = where its PIVOT goes, Forward = along the wall, Name = "DOOR 1|2" / "DOOR 3-R".</summary>
    public static List<MheParking.Spot> Pockets(Vector3 from, EquipmentBattery who = null)
    {
        var res = new List<MheParking.Spot>();
        foreach (var p in BuildPockets(from, who)) res.Add(new MheParking.Spot { Pos = p.Pivot, Forward = p.Forward, Name = p.Name });
        return res;
    }

    /// <summary>The first free, clear, reachable pocket for <paramref name="who"/>, claimed for it (Pos = pivot target). <paramref name="avoid"/>:
    /// a pocket centre to skip (the one it is moving out of). False when none is free.</summary>
    public static bool TryClaimPocket(EquipmentBattery who, out MheParking.Spot spot, Vector3? avoid = null)
    {
        spot = default;
        if (who == null) return false;
        var stale = new List<EquipmentBattery>();
        foreach (var kv in s_claims) if (kv.Key == null || kv.Key == who || kv.Key.IsBusy) stale.Add(kv.Key);
        foreach (var k in stale) s_claims.Remove(k);

        foreach (var p in BuildPockets(who.BodyCentre, who))
        {
            if (avoid.HasValue && Flat(p.Centre - avoid.Value).magnitude < 1.5f) continue;
            bool taken = false;
            foreach (var kv in s_claims) if (Flat(kv.Value - p.Centre).magnitude < 1.0f) { taken = true; break; }
            if (taken) continue;
            // MheParking.IsClear treats a dock stocker's position as its body centre, so test the body where it will actually stand,
            // as close to the wall as it is clear (see MaxPushFromWall).
            for (float push = 0f; push <= MaxPushFromWall + 0.001f; push += PushStep)
            {
                Vector3 c = p.Centre + p.Inward * push;
                if (!MheParking.IsClear(who, c, p.Forward)) continue;
                s_claims[who] = c;
                spot = new MheParking.Spot { Pos = c - p.Forward * p.CentreOff, Forward = p.Forward, Name = p.Name };
                return true;
            }
        }
        return false;
    }

    /// <summary>Is a pocket free and clear for <paramref name="who"/> right now? (Claims nothing.)</summary>
    public static bool AnyPocketFree(EquipmentBattery who)
    {
        if (!TryClaimPocket(who, out _)) return false;
        Release(who);
        return true;
    }

    /// <summary>The pocket centre this machine has claimed, if any.</summary>
    public static bool TryGetClaim(EquipmentBattery who, out Vector3 centre) => s_claims.TryGetValue(who, out centre);

    /// <summary>No traffic knowledge any more (2026-11-20 rework): a restless dock stocker always keeps still.</summary>
    public static bool OtherTrafficNear(EquipmentBattery who, Vector3 pos, float radius) => true;

    /// <summary>A machine's travel leg has stalled behind this parked stocker for at least <paramref name="minHeldReal"/> real seconds
    /// (<see cref="GameCore.Actors.MheTravel.IsHoldingUp"/>). Was a stub (false) after the 2026-11-20 rework, so a stocker parked across the
    /// apron never made way (block 1101: 65 battery-trip stalls behind one).</summary>
    public static bool SomeoneWaitingOn(EquipmentBattery who, float minHeldReal) => GameCore.Actors.MheTravel.IsHoldingUp(who, minHeldReal);

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
