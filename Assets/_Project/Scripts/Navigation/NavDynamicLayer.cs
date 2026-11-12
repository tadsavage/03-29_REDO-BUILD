using UnityEngine;
using GameCore.Actors;

namespace GameCore.Navigation
{
    /// <summary>
    /// The DYNAMIC half of the grid: what is standing on the floor right now that the static layer cannot know about.
    ///   - machines with nobody aboard (a parked jack, a reach truck left in an aisle): they never move, and a disabled NavMeshAgent made
    ///     them invisible to every other machine's avoidance (the reach truck driving through a parked jack, 2026-11-11);
    ///   - pallets standing on a floor (staged lanes, dock, aisles).
    /// MANNED machines are deliberately NOT here: they move, and the queue / 1 m buffer / right-of-way code already deals with them. Blocking
    /// a moving machine's cells would plan routes round where it WAS.
    /// Refreshed on demand, at plan time (a leg planned now sees the floor as it is now), never per frame.
    /// </summary>
    public static class NavDynamicLayer
    {
        /// <summary>Rebuild the dynamic layer from the live scene. <paramref name="self"/> (the machine being planned for) is never blocked
        /// by its own outline. Returns the number of cells blocked.</summary>
        /// <param name="machineInflate">Grow each unmanned machine's blocked outline by this many metres: the 1 m vehicle buffer
        /// (<see cref="MachineClearance.VehicleSpacingMeters"/>) the scripted moves enforce, so a route is planned that the guard will not
        /// then hold. A machine already inside that buffer of <paramref name="self"/> (a jack parked between two others) is NOT grown, or the
        /// machine could never leave its own parking row: the guard lets a vehicle inside the buffer move away.</param>
        public static int Refresh(NavGrid grid, MHEOperatorSlot self, float machineInflate = 0f)
        {
            if (grid == null) return 0;
            grid.ClearDynamic();
            int cells = 0;

            foreach (var m in MachineClearance.AllMachines())
            {
                if (m == null || m == self || m.IsOccupied) continue;
                float inflate = machineInflate > 0f && self != null && MachineClearance.OutlineGap(self, m) >= machineInflate ? machineInflate : 0f;
                cells += BlockMachine(grid, m, inflate);
            }

            float half = MachineClearance.PalletHalfSize;
            foreach (var p in MachineClearance.AllFloorPallets())
            {
                if (p == null) continue;
                Vector3 f = p.forward; f.y = 0f;
                cells += grid.BlockOutline(p.position.x, p.position.z, f.x, f.z, half, half);
            }
            return cells;
        }

        /// <summary>One machine's real outline (the same footprint MachineClearance tests) as blocked cells.</summary>
        public static int BlockMachine(NavGrid grid, MHEOperatorSlot m, float inflate = 0f)
        {
            var f = MachineClearance.FootprintOf(m);
            Transform t = m.transform;
            Vector3 fwd = t.forward; fwd.y = 0f;
            Vector3 right = t.right; right.y = 0f;
            // The outline is not centred on the pivot (a jack's forks trail behind it): find its middle, then its half sizes.
            Vector3 centre = t.position + fwd.normalized * ((f.MinZ + f.MaxZ) * 0.5f) + right.normalized * ((f.MinX + f.MaxX) * 0.5f);
            return grid.BlockOutline(centre.x, centre.z, fwd.x, fwd.z, (f.MaxZ - f.MinZ) * 0.5f, (f.MaxX - f.MinX) * 0.5f, inflate);
        }
    }
}
