using System.Linq;

namespace GameCore.Inventory
{
    /// <summary>
    /// Derives which customer (if any) currently "owns" a staging lane, purely by scanning active
    /// orders for one whose AssignedDoorNumber/AssignedLane match and hasn't shipped/cancelled yet --
    /// no separate reservation dictionary to keep in sync (same "always re-derivable from current
    /// game state" philosophy as DoorAssignmentService). A lane frees up automatically the moment
    /// its last order there ships. For now a lane holds exactly one customer's orders at a time, per
    /// Tad's spec -- multi-stop trailers (several customers sharing a lane) are a later feature.
    /// </summary>
    public static class StagingLaneAssignmentService
    {
        public static string GetOwningCustomerId(OrderService orderService, int door, string lane)
        {
            if (orderService == null || string.IsNullOrEmpty(lane)) return null;
            var owner = orderService.ActiveOrders.FirstOrDefault(o =>
                o.AssignedDoorNumber == door && o.AssignedLane == lane &&
                o.Status != OrderData.OrderStatus.Shipped && o.Status != OrderData.OrderStatus.Cancelled);
            return owner?.CustomerId;
        }

        /// <summary>True if this lane is free, or already owned by the given customer (so more of
        /// their orders can be added to it).</summary>
        public static bool IsLaneAvailableFor(OrderService orderService, int door, string lane, string customerId)
        {
            var owner = GetOwningCustomerId(orderService, door, lane);
            return owner == null || owner == customerId;
        }
    }
}
