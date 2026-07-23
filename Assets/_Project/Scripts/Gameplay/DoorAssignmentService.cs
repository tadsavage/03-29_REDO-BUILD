using System.Linq;
using UnityEngine;
using GameCore.Inventory;

/// <summary>
/// "Guard shack" routing: decides which outbound door a customer's order is sent to. Deterministic
/// (hash of customer id modulo the currently available outbound-capable doors) rather than a
/// persisted registry — same philosophy as SlotAssignmentService, always re-derivable from current
/// game state instead of needing its own save data. Called once per order, at creation
/// (OrderService.ReceiveOrder), and the result is stored on OrderData.AssignedDoorNumber so it can't
/// drift later if the player adds or removes docks — only brand-new orders see a layout change.
/// </summary>
public static class DoorAssignmentService
{
    /// <summary>Picks an outbound-capable door for the given customer. Returns 0 if no lane
    /// currently allows outbound picking (e.g. no docks placed yet, or all placed lanes are
    /// Inbound-only) — callers should treat 0 as "not yet routable".</summary>
    public static int AssignDoorForCustomer(string customerId, InventoryService inventoryService)
    {
        if (string.IsNullOrEmpty(customerId) || inventoryService == null) return 0;

        var outboundDoors = LaneNamingService.AllLanes()
            .Where(l => inventoryService.LaneAllowsPicking(l.door, l.lane))
            .Select(l => l.door)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (outboundDoors.Count == 0) return 0;

        int index = Mathf.Abs(customerId.GetHashCode()) % outboundDoors.Count;
        return outboundDoors[index];
    }
}
