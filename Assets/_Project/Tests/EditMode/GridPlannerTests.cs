using NUnit.Framework;
using UnityEngine;
using GameCore.Navigation;

namespace GameCore.Tests
{
    /// <summary>The grid planner on synthetic grids: pure data, no scene. Mirrors the checks run outside Unity while it was written.</summary>
    public class GridPlannerTests
    {
        private static NavGrid G(int w, int h) => new NavGrid(w, h, 1.325f, Vector3.zero);
        private static Vector3 C(NavGrid g, int x, int z) => g.CellCenter(x, z, 0f);
        private static MachineProfile Small => new MachineProfile { Name = "small", Front = 0.3f, Back = 0.3f, HalfWidth = 0.3f };
        private static MachineProfile Jack(float hw) => new MachineProfile { Name = "jack", Front = 0.6f, Back = 2.55f, HalfWidth = hw, CanReverse = true };

        [Test]
        public void OpenFloor_IsAStraightLine()
        {
            var g = G(20, 20);
            var r = new GridPlanner(g).Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 12, 2));
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(2, r.Waypoints.Count);
            Assert.AreEqual(10f, r.Cost, 0.01f);
        }

        [Test]
        public void Wall_WithAGap_RoutesThroughTheGap()
        {
            var g = G(20, 20);
            for (int z = 0; z < 20; z++) if (z != 15) g.SetStaticBlocked(10, z, true);
            var r = new GridPlanner(g).Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 18, 2));
            Assert.IsTrue(r.Ok);
            Assert.IsTrue(r.Steps.Exists(s => s.X == 10 && s.Z == 15));
        }

        [Test]
        public void SealedWall_IsNoPath_AndBlockedGoalIsReported()
        {
            var g = G(20, 20);
            for (int z = 0; z < 20; z++) g.SetStaticBlocked(10, z, true);
            var p = new GridPlanner(g);
            Assert.AreEqual(PlanStatus.NoPath, p.Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 18, 2)).Status);
            Assert.AreEqual(PlanStatus.GoalBlocked, p.Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 10, 2)).Status);
        }

        [Test]
        public void MachineWidth_DecidesWhetherAOneCellGapIsPassable()
        {
            var g = G(30, 12);
            for (int z = 0; z < 12; z++) if (z != 5) g.SetStaticBlocked(10, z, true);
            var p = new GridPlanner(g);
            Assert.IsTrue(p.Plan(Jack(0.6f), C(g, 2, 5), NavGrid.East, C(g, 20, 5)).Ok);
            Assert.IsFalse(p.Plan(Jack(0.9f), C(g, 2, 5), NavGrid.East, C(g, 20, 5)).Ok);
        }

        [Test]
        public void OneWayAisle_IsNeverEnteredAgainstTheFlow()
        {
            var g = G(30, 12);
            for (int x = 5; x <= 14; x++) { g.SetOneWay(x, 5, NavGrid.East); g.SetStaticBlocked(x, 4, true); g.SetStaticBlocked(x, 6, true); }
            var p = new GridPlanner(g);
            Assert.IsTrue(p.Plan(Small, C(g, 2, 5), NavGrid.East, C(g, 20, 5)).Ok);
            var west = p.Plan(Small, C(g, 20, 5), NavGrid.West, C(g, 2, 5));
            Assert.IsFalse(west.Steps.Exists(s => s.X >= 6 && s.X <= 13 && s.Z == 5));
        }

        [Test]
        public void GoalHeading_IsHonoured_AndDynamicBlocksReroute()
        {
            var g = G(20, 20);
            var p = new GridPlanner(g);
            var r = p.Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 10, 10), NavGrid.South);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(NavGrid.South, r.Steps[r.Steps.Count - 1].Heading);

            g.SetDynamicBlocked(6, 2, true);
            var around = p.Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 12, 2));
            Assert.IsTrue(around.Ok);
            Assert.IsFalse(around.Steps.Exists(s => s.X == 6 && s.Z == 2));
            g.ClearDynamic();
            Assert.AreEqual(10f, p.Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 12, 2)).Cost, 0.01f);
        }

        [Test]
        public void RouteCorners_AreReportedWhereTheMachineTurns()
        {
            var g = G(20, 20);
            var p = new GridPlanner(g);
            // An L: the pivot happens in place, so the corner must not be dropped (found in Play: a 31-step route reported 2 corners).
            var r = p.Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 10, 8));
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(3, r.Waypoints.Count);
            // A reverse after a forward move is a cusp even though the heading does not change.
            var dead = G(30, 12);
            for (int x = 5; x <= 14; x++) { dead.SetStaticBlocked(x, 4, true); dead.SetStaticBlocked(x, 6, true); }
            for (int z = 0; z < 12; z++) dead.SetStaticBlocked(15, z, true);
            var back = new GridPlanner(dead).Plan(Jack(0.6f), C(dead, 12, 5), NavGrid.East, C(dead, 2, 5), NavGrid.East);
            Assert.IsTrue(back.Ok);
            Assert.GreaterOrEqual(back.Waypoints.Count, 2);
        }

        [Test]
        public void Smoothing_CutsOpenFloorStaircases_ButNeverThroughWallsOrCostCells()
        {
            var open = G(40, 40);
            var r = new GridPlanner(open).Plan(Jack(0.6f), C(open, 2, 2), NavGrid.East, C(open, 30, 20));
            Assert.AreEqual(2, r.Smoothed.Count);

            var walled = G(40, 40);
            for (int z = 0; z < 40; z++) if (z < 15 || z > 16) walled.SetStaticBlocked(15, z, true);
            var w = new GridPlanner(walled).Plan(Small, C(walled, 2, 2), NavGrid.East, C(walled, 30, 30));
            Assert.IsTrue(w.Ok);
            Assert.GreaterOrEqual(w.Smoothed.Count, 3);
            for (int i = 1; i < w.Smoothed.Count; i++)
                for (int k = 0; k <= 200; k++)
                {
                    Vector3 a = w.Smoothed[i - 1], b = w.Smoothed[i], q = Vector3.Lerp(a, b, k / 200f);
                    var c = walled.WorldToCell(q);
                    Assert.IsFalse(walled.IsBlocked(c.x, c.y), $"smoothed leg {i} crosses a blocked cell at {c}");
                }

            // Aisle and aisle-mouth cells (NoSmooth) always keep the lattice, however open they look.
            var aisle = G(40, 40);
            for (int x = 0; x < 40; x++) for (int z = 0; z < 40; z++) aisle.SetNoSmooth(x, z, true);
            var ar = new GridPlanner(aisle).Plan(Small, C(aisle, 2, 2), NavGrid.East, C(aisle, 30, 20));
            Assert.AreEqual(ar.Waypoints.Count, ar.Smoothed.Count);

            // The rack-clearance cost ring no longer stops smoothing; the 0.25 m outline margin does the protecting.
            var ring = G(40, 40);
            for (int x = 0; x < 40; x++) for (int z = 0; z < 40; z++) ring.SetCost(x, z, 0.5f);
            Assert.AreEqual(2, new GridPlanner(ring).Plan(Small, C(ring, 2, 2), NavGrid.East, C(ring, 30, 20)).Smoothed.Count);
        }

        [Test]
        public void AisleTraffic_JackFollowsTheFlow_ReachTruckMayGoEitherWay()
        {
            var g = G(30, 14);
            for (int z = 0; z < 14; z++) if (z < 5 || z > 8) g.SetStaticBlocked(10, z, true);
            for (int x = 7; x <= 13; x++) for (int z = 5; z <= 8; z++) g.SetAisle(x, z, 1, NavGrid.East);
            var p = new GridPlanner(g);
            var jack = Jack(0.6f); jack.FollowAisleTraffic = true;
            Assert.IsTrue(p.Plan(jack, C(g, 2, 6), NavGrid.East, C(g, 22, 6)).Ok, "with the traffic");
            Assert.IsFalse(p.Plan(jack, C(g, 22, 6), NavGrid.West, C(g, 2, 6)).Ok, "against the traffic");
            Assert.IsTrue(p.Plan(Jack(0.6f), C(g, 22, 6), NavGrid.West, C(g, 2, 6)).Ok, "a reach truck is not bound by it");
            jack.AllowAisleReverse = true;
            Assert.IsTrue(p.Plan(jack, C(g, 22, 6), NavGrid.West, C(g, 2, 6)).Ok, "reversing only when the profile allows it");
        }

        [Test]
        public void Smoothing_KeepsTheFinalApproach_WhenAnArrivalHeadingIsRequired()
        {
            var g = G(40, 40);
            var r = new GridPlanner(g).Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 30, 20), NavGrid.South);
            int n = r.Waypoints.Count, m = r.Smoothed.Count;
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0f, (r.Smoothed[m - 1] - r.Waypoints[n - 1]).sqrMagnitude, 1e-4f);
            Assert.AreEqual(0f, (r.Smoothed[m - 2] - r.Waypoints[n - 2]).sqrMagnitude, 1e-4f);
        }

        [Test]
        public void AvoidAisles_DockStockersHaveNoRouteThroughAnAisle()
        {
            var g = G(30, 14);
            for (int z = 0; z < 14; z++) if (z < 5 || z > 8) g.SetStaticBlocked(10, z, true);   // the only way across is the aisle
            for (int x = 7; x <= 13; x++) for (int z = 5; z <= 8; z++) g.SetAisle(x, z, 1, NavGrid.East);
            var p = new GridPlanner(g);
            var ds = Small; ds.AvoidAisles = true;
            Assert.IsFalse(p.Plan(ds, C(g, 2, 6), NavGrid.East, C(g, 22, 6)).Ok, "a dock stocker never uses an aisle");
            Assert.IsTrue(p.Plan(Small, C(g, 2, 6), NavGrid.East, C(g, 22, 6)).Ok, "anything else may");
            // With another way round it takes it.
            var g2 = G(30, 14);
            for (int x = 7; x <= 13; x++) for (int z = 5; z <= 8; z++) g2.SetAisle(x, z, 1, NavGrid.East);
            var r = new GridPlanner(g2).Plan(ds, C(g2, 2, 6), NavGrid.East, C(g2, 22, 6));
            Assert.IsTrue(r.Ok);
            Assert.IsFalse(r.Steps.Exists(s => g2.IsAisle(s.X, s.Z)));
        }

        [Test]
        public void PlanNearest_MovesAnUnusableGoalToTheNearestFittingCell_AndSaysHowFar()
        {
            var g = G(30, 20);
            for (int x = 14; x <= 16; x++) for (int z = 8; z <= 12; z++) g.SetStaticBlocked(x, z, true);   // a rack block
            var p = new GridPlanner(g);
            Vector3 inRack = C(g, 15, 10);
            Assert.AreEqual(PlanStatus.GoalBlocked, p.Plan(Small, C(g, 2, 10), NavGrid.East, inRack).Status);
            var r = p.PlanNearest(Small, C(g, 2, 10), NavGrid.East, inRack, -1, 3, out float moved);
            Assert.IsTrue(r.Ok);
            Assert.Greater(moved, 0f);
            Assert.LessOrEqual(moved, 3f * g.CellSize * 1.5f);
            var end = r.Steps[r.Steps.Count - 1];
            Assert.IsFalse(g.IsBlocked(end.X, end.Z));
            // Nothing within reach: still refused, not silently planned somewhere far away.
            var none = p.PlanNearest(Small, C(g, 2, 10), NavGrid.East, inRack, -1, 0, out float m0);
            Assert.AreEqual(PlanStatus.GoalBlocked, none.Status);
            Assert.AreEqual(0f, m0);
        }

        [Test]
        public void BlockOutline_BlocksOnlyTheCellsTheRectangleTouches_AndRoutesGoRoundIt()
        {
            var g = G(40, 30);
            Vector3 c = g.CellCenter(20, 15);
            int n = g.BlockOutline(c.x, c.z, 0f, 1f, 1.9f, 0.6f);   // a jack-sized outline pointing north
            Assert.GreaterOrEqual(n, 3);
            Assert.IsTrue(g.IsDynamicBlocked(20, 15) && g.IsDynamicBlocked(20, 16) && g.IsDynamicBlocked(20, 14));
            Assert.IsFalse(g.IsDynamicBlocked(20, 20));
            Assert.IsFalse(g.IsDynamicBlocked(24, 15));

            var p = new GridPlanner(g);
            var r = p.Plan(Small, C(g, 5, 15), NavGrid.East, C(g, 35, 15));
            Assert.IsTrue(r.Ok);
            Assert.IsFalse(r.Steps.Exists(s => g.IsDynamicBlocked(s.X, s.Z)));
            Assert.Greater(r.Cost, 30f);

            // Pointing east it blocks along x instead.
            var e = G(40, 30);
            e.BlockOutline(c.x, c.z, 1f, 0f, 1.9f, 0.6f);
            Assert.IsTrue(e.IsDynamicBlocked(19, 15) && e.IsDynamicBlocked(21, 15));
            Assert.IsFalse(e.IsDynamicBlocked(20, 18));
        }

        [Test]
        public void StartInsideABlockedCell_StillPlansOut()
        {
            var g = G(20, 20);
            g.SetStaticBlocked(2, 2, true);
            Assert.IsTrue(new GridPlanner(g).Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 10, 2)).Ok);
        }
    }
}
