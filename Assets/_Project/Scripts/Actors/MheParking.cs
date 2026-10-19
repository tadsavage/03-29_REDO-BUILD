using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Where a machine is put down when an employee takes it off a charging station (Tad, 2026-10-18). Every station has three
/// made-up locations - LEFT, RIGHT and in FRONT of it ("STA-A-L", "STA-A-R", "STA-A-F") - tried in that order. A location
/// only counts as free when nothing physical is there: no other machine, no other station's pad, no wall / rack / pallet
/// collider, and it sits on the navigation mesh. With all three taken the machine goes to the nearest EMPTY staging lane
/// ("LANE 1B"). Nothing is ever parked on top of, or inside, anything else.
///
/// <see cref="NameAt"/> is the machine's "record" of where it is, like inventory: a station ("STA-A"), one of the three
/// side locations, a lane, or empty while it is in motion / somewhere unnamed.
/// </summary>
public static class MheParking
{
    public struct Spot
    {
        public Vector3 Pos;      // where the machine's transform goes
        public Vector3 Forward;  // which way it faces
        public string Name;      // "STA-A-L" / "LANE 1B"
    }

    private const float SideStep = 2.65f;        // one cell pitch: the neighbouring bay position
    private const float FrontStep = 4.6f;        // from the bay's charge spot out past its open end
    private const float NameRadius = 1.3f;       // within this of a made-up location = "at" it
    private const float JackBodyCenterOffset = 0.64f;
    private const int IgnoredLayers = (1 << 2) | (1 << 3) | (1 << 9);   // Ignore Raycast, Ground (floors), Avatars (people)

    private static string Letter(PalletJackParkingSpot st)
    {
        var l = st != null ? st.GetComponent<MHEStationLetter>() : null;
        return l != null && !string.IsNullOrEmpty(l.Letter) ? l.Letter : "?";
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// <summary>The three made-up locations of a station for this machine: name + pose, in the order they are tried.</summary>
    private static Spot[] StationLocations(EquipmentBattery who, PalletJackParkingSpot st)
    {
        st.ChargePose(who.Slot, false, out Vector3 origin, out Vector3 fwd);
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        string a = Letter(st);
        float y = who.transform.position.y;
        Vector3 At(Vector3 p) { p.y = y; return p; }
        return new[]
        {
            new Spot { Pos = At(origin - right * SideStep), Forward = fwd, Name = $"STA-{a}-L" },
            new Spot { Pos = At(origin + right * SideStep), Forward = fwd, Name = $"STA-{a}-R" },
            new Spot { Pos = At(origin - fwd * FrontStep),  Forward = fwd, Name = $"STA-{a}-F" },
        };
    }

    private static Vector3 BodyCentre(EquipmentBattery who, Vector3 pos, Vector3 fwd)
        => who.Slot != null && who.Slot.IsPalletJack ? pos - fwd * JackBodyCenterOffset : pos;

    /// <summary>Is the footprint of <paramref name="who"/> standing at <paramref name="pos"/> facing <paramref name="fwd"/> clear of
    /// everything: walkable, not on another station, no other machine, no collider of any wall / rack / pallet?</summary>
    public static bool IsClear(EquipmentBattery who, Vector3 pos, Vector3 fwd)
    {
        Vector3 body = BodyCentre(who, pos, fwd);

        // On the navigation mesh (the machine has to be able to drive there, and a spot inside a wall or rack isn't).
        if (!NavMesh.SamplePosition(body, out var hit, 0.7f, who.AgentAreaMask) || Flat(hit.position - body).magnitude > 0.6f)
            return false;

        // Another station's pad.
        if (PalletJackParkingSpot.StationWithin(body, NameRadius + 0.3f) != null) return false;

        // Another machine.
        foreach (var o in EquipmentBattery.All)
        {
            if (o == null || o == who) continue;
            if (Flat(o.BodyCentre - body).magnitude < 2.4f) return false;
        }

        // Walls, racks, pallets, anything solid or trigger.
        Vector3 half = who.BodyHalfExtents + new Vector3(0.15f, 0f, 0.15f);
        var cols = Physics.OverlapBox(body + Vector3.up * (half.y + 0.05f), half, Quaternion.LookRotation(fwd.sqrMagnitude > 0.0001f ? fwd : Vector3.forward),
                                      ~IgnoredLayers, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            if (c == null) continue;
            if (c.GetComponentInParent<EquipmentBattery>() == who) continue;
            if (c.GetComponentInParent<EmployeeIdentity>() != null) continue;      // people step aside
            if (c.GetComponentInParent<PalletJackParkingSpot>() != null) continue; // the station pad itself
            return false;
        }
        return true;
    }

    /// <summary>First free location for <paramref name="who"/> off <paramref name="bay"/>: left, right, front, else the nearest empty lane.</summary>
    public static bool TryFindSpot(EquipmentBattery who, PalletJackParkingSpot bay, out Spot spot)
    {
        spot = default;
        if (who == null || who.Slot == null) return false;

        if (bay != null)
            foreach (var s in StationLocations(who, bay))
                if (IsClear(who, s.Pos, s.Forward)) { spot = s; return true; }

        return TryFindEmptyLane(who, bay != null ? bay.transform.position : who.transform.position, out spot);
    }

    private static bool TryFindEmptyLane(EquipmentBattery who, Vector3 from, out Spot spot)
    {
        spot = default;
        float best = float.MaxValue;
        foreach (var (door, lane) in LaneNamingService.AllLanes())
        {
            var slots = LaneNamingService.GetLane(door, lane);
            if (slots == null || slots.Count == 0) continue;
            if (!LaneNamingService.TryGetLaneGeometry(door, lane, out var geo)) continue;
            if (!LaneNamingService.TryGetSlotWorldPos(slots[slots.Count - 1].Cell, out Vector3 far)) continue;
            Vector3 near = geo.EntryPoint;

            float d = Flat(near - from).magnitude;
            if (d >= best) continue;

            // The WHOLE lane has to be empty: no pallets in it, nobody else parked in it.
            Vector3 mid = (near + far) * 0.5f;
            float halfLen = Flat(far - near).magnitude * 0.5f + 0.7f;
            var cols = Physics.OverlapBox(new Vector3(mid.x, who.transform.position.y + 0.9f, mid.z), new Vector3(0.6f, 0.8f, halfLen),
                                          Quaternion.LookRotation(Flat(geo.DepthAxis).sqrMagnitude > 0.0001f ? Flat(geo.DepthAxis).normalized : Vector3.forward),
                                          ~IgnoredLayers, QueryTriggerInteraction.Collide);
            bool empty = true;
            foreach (var c in cols)
            {
                if (c == null) continue;
                if (c.GetComponentInParent<EquipmentBattery>() == who) continue;
                if (c.GetComponentInParent<EmployeeIdentity>() != null) continue;
                // Floor tiles and lane markings are not obstacles; anything else is.
                if (c.GetComponentInParent<PlacedObject>() is PlacedObject po && po.data != null && po.data.isFloor) continue;
                empty = false; break;
            }
            if (!empty) continue;
            if (EquipmentBattery.All.Any(o => o != null && o != who && Flat(o.BodyCentre - far).magnitude < 3f)) continue;

            Vector3 fwd = Flat(geo.DepthAxis).sqrMagnitude > 0.0001f ? Flat(geo.DepthAxis).normalized : Vector3.forward;
            Vector3 pos = far; pos.y = who.transform.position.y;
            if (who.Slot != null && who.Slot.IsPalletJack) pos += fwd * JackBodyCenterOffset;
            if (!IsClear(who, pos, fwd)) continue;

            best = d;
            spot = new Spot { Pos = pos, Forward = fwd, Name = $"LANE {ZoneRegistry.DisplayPrefix(door)}{lane}" };
        }
        return best < float.MaxValue;
    }

    /// <summary>A random place to wait with nothing to do: plain warehouse floor - not an aisle (no racking within three cells), not a
    /// staging lane, not a walkway or MHE lane, nowhere near a door - and physically clear (see <see cref="IsClear"/>).</summary>
    public static bool TryFindIdleSpot(EquipmentBattery who, out Spot spot, bool relaxed = false)
    {
        spot = default;
        var grid = Object.FindAnyObjectByType<PlacementGrid>();
        if (grid == null || who == null) return false;

        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        for (int i = 0; i < 120; i++)
        {
            var cell = new Vector2Int(Random.Range(0, grid.Width), Random.Range(0, grid.Height));
            if (!IsPlainFloor(grid, cell)) continue;
            if (LaneNamingService.TryGetSlot(cell, out _)) continue;
            // Aisles and doorways are off limits - unless nothing else is free (relaxed): a full machine must still get off its charger.
            if (!relaxed && NearCategory(grid, cell, 3, c => c == "Racking")) continue;
            if (!relaxed && NearDoor(grid, cell, 5)) continue;

            Vector3 pos = grid.GetCellCenter(cell);
            pos.y = who.transform.position.y;
            Vector3 fwd = dirs[Random.Range(0, dirs.Length)];
            if (who.Slot != null && who.Slot.IsPalletJack) pos += fwd * JackBodyCenterOffset;
            if (!IsClear(who, pos, fwd)) continue;

            spot = new Spot { Pos = pos, Forward = fwd, Name = $"PARK {cell.x},{cell.y}" };
            return true;
        }
        return false;
    }

    // Only bare building floor: a foundation under a plain floor tile, nothing else stacked there.
    private static bool IsPlainFloor(PlacementGrid grid, Vector2Int cell)
    {
        if (!grid.IsInsideGrid(cell)) return false;
        var list = grid.GetObjectsInCell(cell);
        if (list == null || list.Count == 0) return false;
        bool foundation = false;
        foreach (var e in list)
        {
            if (e.instance == null || !e.instance.activeSelf || e.data == null) continue;
            if (e.data.category == "Foundation" || e.data.category == "Grounds") { foundation = true; continue; }
            if (e.data.isFloor && e.data.objName == "Floor Tile") continue;
            return false;   // a lane, walkway, MHE lane, rack, wall, pallet, light... : not plain floor
        }
        return foundation;
    }

    private static bool NearCategory(PlacementGrid grid, Vector2Int cell, int radius, System.Func<string, bool> match)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                var c = new Vector2Int(cell.x + dx, cell.y + dy);
                if (!grid.IsInsideGrid(c)) continue;
                var list = grid.GetObjectsInCell(c);
                if (list == null) continue;
                foreach (var e in list)
                    if (e.instance != null && e.instance.activeSelf && e.data != null && match(e.data.category)) return true;
            }
        return false;
    }

    private static bool NearDoor(PlacementGrid grid, Vector2Int cell, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                var c = new Vector2Int(cell.x + dx, cell.y + dy);
                if (!grid.IsInsideGrid(c)) continue;
                var list = grid.GetObjectsInCell(c);
                if (list == null) continue;
                foreach (var e in list)
                    if (e.instance != null && e.instance.activeSelf && e.data != null && e.data.replacesWalls) return true;   // man door, rollup, shipping door
            }
        return false;
    }

    /// <summary>The machine's record of where it is: "STA-A" on a charger, "STA-A-L/R/F" on one of a station's three locations,
    /// "LANE 1B" in a lane, or "" in motion / unnamed.</summary>
    public static string NameAt(EquipmentBattery who)
    {
        if (who == null || who.Slot == null) return "";
        var onStation = PalletJackParkingSpot.ChargerAt(who.BodyCentre);
        if (onStation != null) return $"STA-{Letter(onStation)}";

        foreach (var st in PalletJackParkingSpot.All)
        {
            if (st == null) continue;
            foreach (var s in StationLocations(who, st))
                if (Flat(BodyCentre(who, s.Pos, s.Forward) - who.BodyCentre).magnitude < NameRadius) return s.Name;
        }

        // In a lane tile?
        var grid = Object.FindAnyObjectByType<PlacementGrid>();
        if (grid != null && LaneNamingService.TryGetSlot(grid.WorldToCell(who.BodyCentre), out var slot))
            return $"LANE {ZoneRegistry.DisplayPrefix(slot.DoorNumber)}{slot.Lane}";
        return "";
    }
}
