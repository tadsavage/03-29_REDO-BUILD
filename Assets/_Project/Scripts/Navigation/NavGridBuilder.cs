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
            /// <summary>Import rack aisles (cells between racks, their travel direction) from the live racks.</summary>
            public bool ImportAisles;
            public static Options Default => new Options { ClearanceCost = 0.75f, RequireFloor = true, ImportAisles = true };
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

            if (opt.ImportAisles) ImportAisles(nav);

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

        /// <summary>How far from a rack face an aisle can reach, in cells. Aisles here are 3-4 cells wide (three-door layout: 4 cells between rack rows);
        /// a rack facing open floor must not turn the whole floor into an aisle.</summary>
        public const int AisleDepthCells = 4;
        /// <summary>Cells either side of an aisle's ends treated as its mouth (no smoothing across them).</summary>
        public const int AisleMouthCells = 2;

        /// <summary>Aisles from the live ground racks (PlacedObject.rackAisle / rackBay / rackAisleFacing, set when the aisle is initialised):
        /// from every ground rack cell, walk out in the rack's aisle-facing direction until something blocked, marking aisle cells. Travel
        /// direction is toward higher bay numbers (same side, so the vector runs along the aisle, never across it) and is the SAME definition
        /// the jack driver uses. An aisle whose direction can't be worked out is still marked (no smoothing) with travel unknown.</summary>
        private static void ImportAisles(NavGrid nav)
        {
            var byAisle = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<PlacedObject>>();
            foreach (var po in PlacedObjectRegistry.All)
            {
                if (po == null || po.data == null || po.data.category != "Racking" || !po.isRackLive || po.rackAisle < 0 || po.rackLevelIndex > 0) continue;
                if (!byAisle.TryGetValue(po.rackAisle, out var list)) byAisle[po.rackAisle] = list = new System.Collections.Generic.List<PlacedObject>();
                list.Add(po);
            }

            foreach (var kv in byAisle)
            {
                int travel = TravelHeading(kv.Value);
                foreach (var po in kv.Value)
                {
                    Vector3 f = po.rackAisleFacing; f.y = 0f;
                    if (f.sqrMagnitude < 1e-4f) continue;
                    int hf = NavGrid.HeadingOf(f);
                    var offsets = po.data.GetFootprintOffsets(-po.rotation * 90f);
                    int core = Mathf.Min(po.data.CoreFootprintCellCount, offsets.Length);
                    for (int i = 0; i < core; i++)
                    {
                        int x = po.gridX + offsets[i].x, z = po.gridY + offsets[i].y;
                        for (int k = 1; k <= AisleDepthCells; k++)
                        {
                            int cx = x + NavGrid.DX[hf] * k, cz = z + NavGrid.DZ[hf] * k;
                            if (!nav.InBounds(cx, cz) || nav.IsStaticBlocked(cx, cz)) break;
                            nav.SetAisle(cx, cz, kv.Key, travel);
                        }
                    }
                }
                if (travel < 0) continue;
                // Mouths: the cells just beyond each end of the aisle, along its run.
                var mouth = new System.Collections.Generic.List<Vector2Int>();
                for (int x = 0; x < nav.Width; x++)
                    for (int z = 0; z < nav.Height; z++)
                    {
                        if (nav.GetAisleId(x, z) != kv.Key) continue;
                        for (int s = 0; s < 2; s++)
                        {
                            int d = s == 0 ? travel : (travel + 2) & 3;
                            for (int k = 1; k <= AisleMouthCells; k++)
                            {
                                int mx = x + NavGrid.DX[d] * k, mz = z + NavGrid.DZ[d] * k;
                                if (!nav.InBounds(mx, mz) || nav.IsStaticBlocked(mx, mz)) break;
                                if (nav.GetAisleId(mx, mz) == kv.Key) continue;   // still inside the aisle
                                mouth.Add(new Vector2Int(mx, mz));
                            }
                        }
                    }
                foreach (var m in mouth) nav.SetNoSmooth(m.x, m.y, true);
            }
        }

        // Toward higher bay numbers, from the side of the aisle with the widest bay span (same parity = same side of the aisle).
        private static int TravelHeading(System.Collections.Generic.List<PlacedObject> racks)
        {
            int best = -1, bestSpan = 1;
            for (int parity = 0; parity < 2; parity++)
            {
                PlacedObject lo = null, hi = null;
                foreach (var r in racks)
                {
                    if (r.rackBay < 0 || (r.rackBay & 1) != parity) continue;
                    if (lo == null || r.rackBay < lo.rackBay) lo = r;
                    if (hi == null || r.rackBay > hi.rackBay) hi = r;
                }
                if (lo == null || hi == null || hi.rackBay - lo.rackBay < 2 || hi.rackBay - lo.rackBay <= bestSpan) continue;
                Vector3 v = hi.transform.position - lo.transform.position; v.y = 0f;
                if (v.sqrMagnitude < 0.01f) continue;
                bestSpan = hi.rackBay - lo.rackBay;
                best = NavGrid.HeadingOf(v);
            }
            return best;
        }

        /// <summary>The machine's real outline from its renderers (the same footprint MachineClearance tests), as a profile.
        /// Pivot-relative: Front/Back along the machine's local +Z/-Z, HalfWidth the larger side of its local X.</summary>
        public static MachineProfile ProfileOf(MHEOperatorSlot m, bool canReverse, bool followAisleTraffic = false)
        {
            var f = MachineClearance.FootprintOf(m);
            return new MachineProfile
            {
                Name = m != null ? m.name : "machine",
                Front = Mathf.Max(0.1f, f.MaxZ),
                Back = Mathf.Max(0.1f, -f.MinZ),
                HalfWidth = Mathf.Max(0.3f, Mathf.Max(-f.MinX, f.MaxX)),
                CanReverse = canReverse,
                FollowAisleTraffic = followAisleTraffic,
            };
        }
    }

    /// <summary>Holds the live grid and planner. Statics survive between Play sessions (Domain Reload is off), so it is cleared explicitly.</summary>
    public static class NavGridService
    {
        public static NavGrid Grid { get; private set; }
        public static GridPlanner Planner { get; private set; }

        private static PlacementGrid s_placement;
        private static int s_builtCount = -1;
        private static float s_builtAt = -1000f;
        private const string JackPrefKey = "NavGrid.Jacks";
        /// <summary>The grid is rebuilt when the placed-object count changes or it is this old (real seconds). A rebuild takes ~2 ms.</summary>
        public const float MaxAgeSeconds = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Grid = null; Planner = null; s_placement = null; s_builtCount = -1; s_builtAt = -1000f; }

        /// <summary>FLAG (default off): drive the pallet jack's open-floor legs on the grid planner instead of the jack's NavMeshAgent.
        /// Persisted in PlayerPrefs so it survives Play sessions; flip it with the NavGridDebug context menu or from code.</summary>
        public static bool UseForJacks
        {
            get { try { return PlayerPrefs.GetInt(JackPrefKey, 0) == 1; } catch { return false; } }
            set { try { PlayerPrefs.SetInt(JackPrefKey, value ? 1 : 0); } catch { } }
        }

        private const string DsPrefKey = "NavGrid.DS";
        /// <summary>FLAG (default off): drive the dock stocker's cross-dock travel legs on the grid planner instead of its NavMeshAgent.</summary>
        public static bool UseForDockStockers
        {
            get { try { return PlayerPrefs.GetInt(DsPrefKey, 0) == 1; } catch { return false; } }
            set { try { PlayerPrefs.SetInt(DsPrefKey, value ? 1 : 0); } catch { } }
        }

        private const string RtPrefKey = "NavGrid.RT";
        /// <summary>FLAG (default off): drive the reach truck's travel legs on the grid planner instead of its NavMeshAgent.</summary>
        public static bool UseForReachTrucks
        {
            get { try { return PlayerPrefs.GetInt(RtPrefKey, 0) == 1; } catch { return false; } }
            set { try { PlayerPrefs.SetInt(RtPrefKey, value ? 1 : 0); } catch { } }
        }

        /// <summary>One line saying which machines drive on the grid and which on the NavMesh, for block logs: a playtest block is only
        /// comparable with another if you know which nav it ran on.</summary>
        public static string FlagSummary()
            => $"nav: jacks={(UseForJacks ? "GRID" : "navmesh")}, reach trucks={(UseForReachTrucks ? "GRID" : "navmesh")}, dock stockers={(UseForDockStockers ? "GRID" : "navmesh")}";

        public static NavGrid Rebuild(PlacementGrid placement, NavGridBuilder.Options? options = null)
        {
            if (placement == null) return null;
            Grid = NavGridBuilder.Build(placement, options);
            Planner = new GridPlanner(Grid);
            s_placement = placement;
            s_builtCount = PlacedObjectRegistry.Count;
            s_builtAt = Time.unscaledTime;
            return Grid;
        }

        /// <summary>A planner on an up-to-date grid, or null when there is no PlacementGrid in the scene.</summary>
        public static GridPlanner EnsurePlanner()
        {
            if (s_placement == null) s_placement = Object.FindAnyObjectByType<PlacementGrid>();
            if (s_placement == null) return null;
            if (Grid == null || PlacedObjectRegistry.Count != s_builtCount || Time.unscaledTime - s_builtAt > MaxAgeSeconds)
                Rebuild(s_placement);
            return Planner;
        }
    }
}
