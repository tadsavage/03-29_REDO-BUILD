using UnityEngine;
using GameCore.Actors;

namespace GameCore.Navigation
{
    /// <summary>Builds a <see cref="NavGrid"/> from the PlacementGrid. Runs on demand (build, load, layout change), never per frame.</summary>
    public static class NavGridBuilder
    {
        public struct Options
        {
            /// <summary>Extra cost to enter a free cell that touches a blocked one: "keep your distance from the rack" as data.</summary>
            public float ClearanceCost;
            /// <summary>A cell with no floor or foundation under it is off the building floor and blocked (found in Play 2026-11-11: only
            /// ~8% of the 100x100 grid has floor, and a grid that treats the empty yard as open drives machines off the slab).
            /// Dock stockers entering a trailer are NOT on this layer; that leg stays with the load / unload choreography.</summary>
            public bool RequireFloor;
            public static Options Default => new Options { ClearanceCost = 0.75f, RequireFloor = true };
        }

        /// <summary>A cell is blocked when a REAL (non-buffer) placed object in it is solid by <see cref="MachineClearance.IsSolidData"/>
        /// (racks, walls, barriers, furniture). Floors, lane tiles, doors and buffer reservations stay open. This is conservative: a thin
        /// wall panel blocks its whole 1.325 m cell, where MachineClearance tests the real outline. Floor pallets and parked machines are
        /// NOT here: they are dynamic reservations (<see cref="NavGrid.SetDynamicBlocked"/>).</summary>
        public static NavGrid Build(PlacementGrid grid, Options? options = null)
        {
            var opt = options ?? Options.Default;
            var nav = new NavGrid(grid.Width, grid.Height, grid.CellSize, grid.Origin);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    var list = grid.GetObjectsInCell(new Vector2Int(x, z));
                    bool hasFloor = false, solid = false;
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var e = list[i];
                            if (e.isBuffer || e.instance == null || !e.instance.activeInHierarchy || e.data == null) continue;
                            if (e.data.isFloor || e.data.category == "Foundation") hasFloor = true;
                            if (MachineClearance.IsSolidData(e.data)) solid = true;
                        }
                    }
                    if (solid || (opt.RequireFloor && !hasFloor)) nav.SetStaticBlocked(x, z, true);
                }
            }

            if (opt.ClearanceCost > 0f)
            {
                // Read blocked state first, then write costs, so a cost ring never feeds back into itself.
                var ring = new System.Collections.Generic.List<Vector2Int>();
                for (int x = 0; x < nav.Width; x++)
                    for (int z = 0; z < nav.Height; z++)
                    {
                        if (nav.IsStaticBlocked(x, z)) continue;
                        bool touches = false;
                        for (int dx = -1; dx <= 1 && !touches; dx++)
                            for (int dz = -1; dz <= 1 && !touches; dz++)
                                if ((dx != 0 || dz != 0) && nav.InBounds(x + dx, z + dz) && nav.IsStaticBlocked(x + dx, z + dz)) touches = true;
                        if (touches) ring.Add(new Vector2Int(x, z));
                    }
                foreach (var c in ring) nav.AddCost(c.x, c.y, opt.ClearanceCost);
            }
            return nav;
        }

        /// <summary>The machine's real outline from its renderers (the same footprint MachineClearance tests), as a profile.
        /// Pivot-relative: Front/Back along the machine's local +Z/-Z, HalfWidth the larger side of its local X.</summary>
        public static MachineProfile ProfileOf(MHEOperatorSlot m, bool canReverse)
        {
            var f = MachineClearance.FootprintOf(m);
            return new MachineProfile
            {
                Name = m != null ? m.name : "machine",
                Front = Mathf.Max(0.1f, f.MaxZ),
                Back = Mathf.Max(0.1f, -f.MinZ),
                HalfWidth = Mathf.Max(0.3f, Mathf.Max(-f.MinX, f.MaxX)),
                CanReverse = canReverse,
            };
        }
    }

    /// <summary>Holds the live grid and planner. Statics survive between Play sessions (Domain Reload is off), so it is cleared explicitly.</summary>
    public static class NavGridService
    {
        public static NavGrid Grid { get; private set; }
        public static GridPlanner Planner { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Grid = null; Planner = null; }

        public static NavGrid Rebuild(PlacementGrid placement, NavGridBuilder.Options? options = null)
        {
            if (placement == null) return null;
            Grid = NavGridBuilder.Build(placement, options);
            Planner = new GridPlanner(Grid);
            return Grid;
        }
    }
}
