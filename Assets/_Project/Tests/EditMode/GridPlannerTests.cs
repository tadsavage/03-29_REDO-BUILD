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
        public void StartInsideABlockedCell_StillPlansOut()
        {
            var g = G(20, 20);
            g.SetStaticBlocked(2, 2, true);
            Assert.IsTrue(new GridPlanner(g).Plan(Small, C(g, 2, 2), NavGrid.East, C(g, 10, 2)).Ok);
        }
    }
}
