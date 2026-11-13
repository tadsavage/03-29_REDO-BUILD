using NUnit.Framework;
using GameCore.Inventory;

namespace GameCore.Tests
{
    /// <summary>Which orders count as "never released": the ones whose stamped door must not be saved (see OrderService.NeverReleased).</summary>
    public class OrderReleaseStateTests
    {
        private static OrderData Order() => new OrderData("c1", "Customer", "1 Test Way", 1, 3, 0);

        [Test]
        public void AFreshPendingOrderIsNeverReleased()
        {
            var o = Order();
            Assert.IsTrue(OrderService.NeverReleased(o));
            o.AssignedDoorNumber = 2;   // stamped from its dock appointment: still never released
            Assert.IsTrue(OrderService.NeverReleased(o));
        }

        [Test]
        public void ReleasedOrOnALaneOrBeyondPendingIsNotNeverReleased()
        {
            var released = Order(); released.ReleasedToFloor = true;
            Assert.IsFalse(OrderService.NeverReleased(released));

            var onLane = Order(); onLane.AssignedLane = "B";
            Assert.IsFalse(OrderService.NeverReleased(onLane));

            var picking = Order(); picking.Status = OrderData.OrderStatus.PartiallyPicked;
            Assert.IsFalse(OrderService.NeverReleased(picking));

            Assert.IsFalse(OrderService.NeverReleased(null));
        }
    }
}
