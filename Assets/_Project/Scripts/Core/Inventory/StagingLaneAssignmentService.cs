using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        /// <summary>True if this order still has goods physically occupying its staging lane, and so
        /// still owns it. Once a truck has been LOADED the pallets are aboard and the lane is bare —
        /// holding it until the player gets around to closing the order out (Shipped) is what silently
        /// starved every other customer of stages: four doors read as "already assigned to another
        /// customer" purely because of long-finished loads, leaving only the one door an inbound
        /// trailer was using. Cancelled orders never had goods there to begin with.</summary>
        private static bool StillOccupiesItsLane(OrderData o) =>
            o.Status != OrderData.OrderStatus.Loaded &&
            o.Status != OrderData.OrderStatus.Shipped &&
            o.Status != OrderData.OrderStatus.Cancelled;

        public static string GetOwningCustomerId(OrderService orderService, int door, string lane)
        {
            if (orderService == null || string.IsNullOrEmpty(lane)) return null;
            var owner = orderService.ActiveOrders.FirstOrDefault(o =>
                o.AssignedDoorNumber == door && o.AssignedLane == lane && StillOccupiesItsLane(o));
            return owner?.CustomerId;
        }

        /// <summary>True if this lane is free, or already owned by the given customer (so more of
        /// their orders can be added to it).</summary>
        public static bool IsLaneAvailableFor(OrderService orderService, int door, string lane, string customerId)
        {
            var owner = GetOwningCustomerId(orderService, door, lane);
            return owner == null || owner == customerId;
        }

        // ── STAGE-level (whole door) assignment ──────────────────────────────────────────────────
        // The player now assigns a "Stage" — every staging lane belonging to one door, treated as a
        // single pool — rather than an individual lane. Staging fills the door's first lane (A), and
        // overflows into B, then C, as each fills up. Ownership therefore has to be reckoned per DOOR:
        // if a customer is staging into Stage 1, the whole of Stage 1 is theirs to overflow across,
        // and no other customer may be assigned to it.

        /// <summary>The customer currently occupying any lane of this door's Stage, or null if the
        /// whole Stage is free.</summary>
        public static string GetOwningCustomerIdForDoor(OrderService orderService, int door)
        {
            if (orderService == null) return null;
            var owner = orderService.ActiveOrders.FirstOrDefault(o =>
                o.AssignedDoorNumber == door && !string.IsNullOrEmpty(o.AssignedLane) &&
                StillOccupiesItsLane(o));
            return owner?.CustomerId;
        }

        /// <summary>True if this whole Stage is free, or already owned by the given customer.</summary>
        public static bool IsStageAvailableFor(OrderService orderService, int door, string customerId)
        {
            var owner = GetOwningCustomerIdForDoor(orderService, door);
            return owner == null || owner == customerId;
        }

        /// <summary>
        /// True if any lane of this door's Stage is currently holding INBOUND stock, or is about to.
        ///
        /// Two signals, both meaning "putaway owns this door right now":
        ///   • Received pallets sitting in its lanes. InventoryService only tracks inbound stock —
        ///     staged OUTBOUND pallets are OutboundPalletBuilder objects placed straight into the
        ///     world and deliberately never added to _palletsByLocation (see OutboundOccupiedCells).
        ///     So a non-empty GetPalletsAtLocation on a lane cell means inbound goods, specifically.
        ///   • An inbound trailer docked at the door. Its cargo is headed for these very lanes, so
        ///     the stage is spoken for even though the pallets haven't landed yet.
        ///
        /// Offering such a door as an outbound stage is how a lane ends up double-assigned — the
        /// selector stages onto tiles the dock stocker is still filling.
        /// </summary>
        /// <summary>True if this ONE lane currently holds inbound stock. InventoryService only tracks
        /// inbound pallets — staged outbound pallets are OutboundPalletBuilder objects placed straight
        /// into the world and never added to _palletsByLocation (see OutboundOccupiedCells) — so a
        /// non-empty cell here means received goods awaiting putaway, specifically.</summary>
        public static bool LaneHasInboundStock(InventoryService inv, int door, string lane)
        {
            if (inv == null) return false;
            foreach (var slot in LaneNamingService.GetLane(door, lane))
            {
                var pallets = inv.GetPalletsAtLocation(slot.Cell);
                if (pallets != null && pallets.Count > 0) return true;
            }
            return false;
        }

        /// <summary>True if an INBOUND trailer is docked at this door. Its cargo is headed for these
        /// lanes, so the whole stage is spoken for even though the pallets haven't landed yet. This is
        /// the only genuinely door-WIDE inbound signal; stock sitting in a lane is per-lane and handled
        /// by LanesInStage instead.</summary>
        public static bool HasInboundTruckDocked(int door)
        {
            foreach (var truck in Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None))
            {
                if (truck == null || truck.IsOutbound) continue;
                if (truck.DockedAt != null && truck.DockedAt.DoorNumber == door) return true;
            }
            return false;
        }

        /// <summary>The full gate the Work Queue dropdown uses, with the reason it failed so an empty
        /// dropdown can explain itself instead of just being blank.</summary>
        public static bool IsStageSelectableFor(OrderService orderService, InventoryService inv, int door,
                                                string customerId, out string reason)
        {
            reason = null;

            if (HasInboundTruckDocked(door))
            {
                reason = "an inbound trailer is docked there";
                return false;
            }

            var usable = LanesInStage(inv, door);
            if (usable.Count == 0)
            {
                reason = "no lane is free for outbound picking (Inbound-only, or all holding received pallets)";
                return false;
            }

            var owner = GetOwningCustomerIdForDoor(orderService, door);
            if (owner != null && owner != customerId)
            {
                reason = "already assigned to another customer";
                return false;
            }

            return true;
        }

        public static bool IsStageSelectableFor(OrderService orderService, InventoryService inv, int door, string customerId)
            => IsStageSelectableFor(orderService, inv, door, customerId, out _);

        /// <summary>Every pickable lane of this door's Stage, in fill order (A, then B, then C…).
        /// This ordering IS the overflow order.</summary>
        public static List<string> LanesInStage(InventoryService inv, int door)
        {
            return LaneNamingService.AllLanes()
                .Where(l => l.door == door)
                .Where(l => inv == null || inv.LaneAllowsPicking(l.door, l.lane))
                // Skip lanes currently holding received pallets. This USED to reject the entire door
                // if any single lane had inbound stock, which made whole stages vanish from the
                // dropdown while most of their lanes were empty. Per-lane is both less blunt and more
                // correct: overflow simply never targets an occupied lane.
                .Where(l => !LaneHasInboundStock(inv, l.door, l.lane))
                .Select(l => l.lane)
                .OrderBy(l => l)
                .ToList();
        }

        /// <summary>The lane a newly-released order should start staging into: the first lane of the
        /// Stage that still has room. If every lane is full we still hand back the first one rather
        /// than failing the release — the order stages there when space frees up, and the driver's
        /// own overflow walk re-resolves at drop time anyway.</summary>
        public static bool TryResolveStartLane(OrderService orderService, InventoryService inv, int door, out string lane)
        {
            lane = null;
            var lanes = LanesInStage(inv, door);
            if (lanes.Count == 0) return false;

            foreach (var candidate in lanes)
            {
                if (inv != null && inv.TryFindStagingSlotInLane(door, candidate, out _)) { lane = candidate; return true; }
            }
            lane = lanes[0];
            return true;
        }
    }
}
