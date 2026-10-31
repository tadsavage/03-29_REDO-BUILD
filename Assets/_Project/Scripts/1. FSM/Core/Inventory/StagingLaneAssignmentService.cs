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
                o.UsesLane(door, lane) && StillOccupiesItsLane(o));
            return owner?.CustomerId;
        }

        /// <summary>
        /// Picks the lane a newly released batch stages into: searches OUTWARD from the order's dock
        /// door — that door's lanes in letter order (2A, 2B, 2C…), then the nearest doors either side
        /// (1, then 3, then 4…) — and takes the first lane that is completely empty. A door is skipped
        /// when an inbound trailer is docked there or when another customer already holds any of its
        /// lanes (one customer per stage). A lane counts as empty only if it is pickable for outbound,
        /// holds no received pallets, has none arriving, has no staged OUTBOUND pallets (which
        /// InventoryService does not track), and is not already claimed by another order's lane.
        /// When <c>palletsNeeded</c> is given the lane must also have room for that many pallets.
        /// </summary>
        public static bool TryFindLaneForOrder(OrderService orderService, InventoryService inv, int dockDoor,
                                               string customerId, out int stageDoor, out string lane,
                                               ICollection<(int door, string lane)> plannedLanes = null,
                                               IDictionary<int, string> plannedDoorOwners = null,
                                               int palletsNeeded = 0)
        {
            stageDoor = 0;
            lane = null;
            foreach (var (door, candidate) in EnumerateEmptyLanes(orderService, inv, dockDoor, customerId, plannedLanes, plannedDoorOwners))
            {
                // The WHOLE order must fit: skip an empty lane that is too short for it and keep looking.
                if (palletsNeeded > 0 && inv != null && inv.CountFreeStagingSlotsInLane(door, candidate) < palletsNeeded) continue;
                stageDoor = door;
                lane = candidate;
                return true;
            }
            // (Sharing a lane with another customer is a last resort and lives in TryFindLanesForOrder, AFTER the empty lanes have been
            // spread across: a shared lane must never beat using several empty ones.)
            return false;
        }

        /// <summary>
        /// Finds the lane(s) a released batch stages into. Prefers ONE lane with room for the whole
        /// order; when none is big enough it takes several completely empty lanes, nearest first, until
        /// their combined room covers <paramref name="palletsNeeded"/> - or until there are no more, in
        /// which case the caller releases only what fits. Returns false when no lane is free at all.
        /// </summary>
        public static bool TryFindLanesForOrder(OrderService orderService, InventoryService inv, int dockDoor,
                                                string customerId, int palletsNeeded,
                                                ICollection<(int door, string lane)> plannedLanes,
                                                IDictionary<int, string> plannedDoorOwners,
                                                out List<(int door, string lane)> lanes)
        {
            lanes = new List<(int door, string lane)>();
            if (TryFindLaneForOrder(orderService, inv, dockDoor, customerId, out int d1, out string l1,
                                    plannedLanes, plannedDoorOwners, palletsNeeded))
            {
                lanes.Add((d1, l1));
                return true;
            }

            int total = 0;
            foreach (var (door, candidate) in EnumerateEmptyLanes(orderService, inv, dockDoor, customerId, plannedLanes, plannedDoorOwners))
            {
                int room = inv != null ? inv.CountFreeStagingSlotsInLane(door, candidate) : 0;
                if (room <= 0) continue;
                lanes.Add((door, candidate));
                total += room;
                if (palletsNeeded > 0 && total >= palletsNeeded) break;
            }
            // Still short after every empty lane: top up from lanes that are shared with another customer (last resort).
            if (palletsNeeded <= 0 || total < palletsNeeded)
            {
                foreach (var (door, candidate) in EnumerateSharedLanes(orderService, inv, dockDoor, plannedLanes))
                {
                    if (lanes.Contains((door, candidate))) continue;
                    int room = inv != null ? inv.CountFreeStagingSlotsInLane(door, candidate) : 0;
                    if (room <= 0) continue;
                    lanes.Add((door, candidate));
                    total += room;
                    if (palletsNeeded > 0 && total >= palletsNeeded) break;
                }
            }
            return lanes.Count > 0;
        }

        /// <summary>
        /// Where one selector trip of <paramref name="pallets"/> pallets stages: the order's primary lane or the
        /// next lane of its own door first, then any lane the order already uses (at any door), then the nearest
        /// completely empty lane at another door (same door rules as a release: no inbound trailer docked, no
        /// other customer holding it). The found lane is the caller's to record via AddOverflowLane.
        /// </summary>
        public static bool TryFindTripLane(OrderService orderService, InventoryService inv, OrderData order, int pallets,
                                           out int door, out string lane)
        {
            door = order != null ? order.EffectiveStagingDoor : 0;
            lane = null;
            if (order == null || inv == null) return false;
            if (pallets < 1) pallets = 1;

            if (door > 0 && inv.TryFindStagingLaneForPallets(door, order.AssignedLane, pallets, out lane)) return true;

            foreach (var used in order.AllStagingLanes())
            {
                if (!inv.LaneAllowsPicking(used.door, used.lane)) continue;
                if (inv.CountFreeStagingSlotsInLane(used.door, used.lane) < pallets) continue;
                door = used.door; lane = used.lane;
                return true;
            }

            foreach (var (d, candidate) in EnumerateEmptyLanes(orderService, inv, order.EffectiveStagingDoor, order.CustomerId, null, null))
            {
                if (inv.CountFreeStagingSlotsInLane(d, candidate) < pallets) continue;
                door = d; lane = candidate;
                return true;
            }
            // No shared-lane last resort (block 1023): staging into a lane another customer is using put this order's
            // pallets in front of theirs, and the loader stopped on the foreign pallet forever. No empty lane = no trip.
            lane = null;
            return false;
        }

        /// <summary>Every completely empty, usable lane, in the order a release should consider them:
        /// the dock door's own lanes first (A, B, C...), then the nearest doors either side.</summary>
        private static IEnumerable<(int door, string lane)> EnumerateEmptyLanes(OrderService orderService, InventoryService inv,
                                               int dockDoor, string customerId,
                                               ICollection<(int door, string lane)> plannedLanes,
                                               IDictionary<int, string> plannedDoorOwners)
        {
            if (orderService == null) yield break;

            var allLanes = LaneNamingService.AllLanes();
            var doors = allLanes.Select(l => l.door).Distinct().ToList();
            if (doors.Count == 0) yield break;

            // Nearest first; on a tie the lower-numbered door wins (dock 2 -> 2, 1, 3, 0/4...).
            int anchor = dockDoor > 0 ? dockDoor : doors.Min();
            var ordered = doors.OrderBy(d => System.Math.Abs(d - anchor)).ThenBy(d => d).ToList();

            foreach (int door in ordered)
            {
                // An inbound trailer at this door holds back only the lanes it will actually fill, not the whole door.
                var reserved = LanesReservedForInbound(orderService, inv, door);

                // Exclusivity is per LANE, not per door (Tad, 2026-10-25): one customer per lane, but a door's lanes can belong to different
                // customers (1A one customer, 1B another). A lane another order uses already fails LaneIsCompletelyEmpty below, and lanes
                // earlier customers in this planned batch took are skipped via plannedLanes. The old door-wide rule meant that with two
                // doors only two customers could stage at once, however many lanes sat empty.
                foreach (var candidate in allLanes.Where(l => l.door == door).Select(l => l.lane).OrderBy(l => l))
                {
                    if (reserved.Contains(candidate)) continue;
                    if (plannedLanes != null && plannedLanes.Contains((door, candidate))) continue;
                    if (!LaneIsCompletelyEmpty(orderService, inv, door, candidate)) continue;
                    yield return (door, candidate);
                }
            }
        }

        /// <summary>
        /// LAST RESORT lanes: a lane that still has room but is not completely empty, because another customer's staged pallets or another
        /// order's claim are in it. Nearest door first, then letter order. Never offered when an inbound trailer is docked at the door, when
        /// received stock is in the lane (putaway owns it), when inbound is about to land there, or when the lane is not pickable for outbound.
        /// The caller checks room. Mixed lanes are fine here: the loader digs out the pallets it needs.
        /// </summary>
        private static IEnumerable<(int door, string lane)> EnumerateSharedLanes(OrderService orderService, InventoryService inv, int dockDoor,
                                               ICollection<(int door, string lane)> plannedLanes)
        {
            if (orderService == null) yield break;
            var allLanes = LaneNamingService.AllLanes();
            var doors = allLanes.Select(l => l.door).Distinct().ToList();
            if (doors.Count == 0) yield break;
            int anchor = dockDoor > 0 ? dockDoor : doors.Min();
            foreach (int door in doors.OrderBy(d => System.Math.Abs(d - anchor)).ThenBy(d => d))
            {
                var reserved = LanesReservedForInbound(orderService, inv, door);
                foreach (var candidate in allLanes.Where(l => l.door == door).Select(l => l.lane).OrderBy(l => l))
                {
                    if (reserved.Contains(candidate)) continue;
                    if (plannedLanes != null && plannedLanes.Contains((door, candidate))) continue;
                    if (inv != null && !inv.LaneAllowsPicking(door, candidate)) continue;
                    if (LaneHasInboundStock(inv, door, candidate)) continue;
                    if (GameCore.Labor.TrailerOffloadController.IsLanePendingInbound(door, candidate)) continue;
                    yield return (door, candidate);
                }
            }
        }

        /// <summary>A lane nothing is using or about to use — see <see cref="TryFindLaneForOrder"/>.</summary>
        public static bool LaneIsCompletelyEmpty(OrderService orderService, InventoryService inv, int door, string lane)
        {
            if (inv != null && !inv.LaneAllowsPicking(door, lane)) return false;
            if (LaneHasInboundStock(inv, door, lane)) return false;
            if (GameCore.Labor.TrailerOffloadController.IsLanePendingInbound(door, lane)) return false;
            if (inv != null && inv.LaneHasStagedOutboundPallets(door, lane)) return false;
            if (GetOwningCustomerId(orderService, door, lane) != null) return false; // claimed by another order
            return true;
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
                StillOccupiesItsLane(o) && o.AllStagingLanes().Any(l => l.door == door));
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

        /// <summary>DIFFICULTY HOOK (Tad, 2026-10-25). On (default): while an inbound trailer is docked at a door, the lanes it will fill are held back from
        /// outbound automatically, and the rest of the door's lanes stay available. Off (a harder mode, later): nothing is held back, so the player has to
        /// keep inbound and outbound apart themselves.</summary>
        public static bool AutoReserveLanesForInbound = true;

        /// <summary>Pallets still to come off the inbound trailer(s) docked at this door (a line item is one pallet; dropped and already
        /// received lines don't count). A docked trailer with no shipment data counts as one pallet.</summary>
        public static int InboundPalletsToCome(int door)
        {
            int total = 0;
            foreach (var truck in Object.FindObjectsByType<TruckController>())
            {
                if (truck == null || truck.IsOutbound || truck.DockedAt == null || truck.DockedAt.DoorNumber != door) continue;
                var po = truck.AssignedShipment;
                if (po == null) { total += 1; continue; }
                foreach (var li in po.LineItems)
                    if (li != null && !li.Dropped && li.ReceivedQuantity < li.Quantity) total++;
            }
            return total;
        }

        /// <summary>
        /// The lanes at this door an inbound trailer needs, and so are not offered to outbound. Worked out the way the dock stocker picks them:
        /// the door's lanes in letter order, skipping lanes that don't take inbound and lanes an outbound order already holds, taking each lane's
        /// free room until the pallets still on the trailer are covered. A 2-pallet trailer at a 5-lane door holds back one lane, not the door.
        /// </summary>
        public static HashSet<string> LanesReservedForInbound(OrderService orderService, InventoryService inv, int door)
        {
            var reserved = new HashSet<string>();
            if (!AutoReserveLanesForInbound) return reserved;
            int pallets = InboundPalletsToCome(door);
            if (pallets <= 0) return reserved;
            foreach (var (d, lane) in LaneNamingService.AllLanes())
            {
                if (d != door) continue;
                if (inv != null && !inv.LaneAcceptsPutaway(d, lane)) continue;
                if (orderService != null && GetOwningCustomerId(orderService, d, lane) != null) continue;   // outbound holds it: the offloader skips it too
                int room = inv != null ? inv.CountFreeStagingSlotsInLane(d, lane) : 1;
                if (room <= 0) continue;
                reserved.Add(lane);
                pallets -= room;
                if (pallets <= 0) break;
            }
            return reserved;
        }

        public static bool IsLaneReservedForInbound(OrderService orderService, InventoryService inv, int door, string lane)
            => LanesReservedForInbound(orderService, inv, door).Contains(lane);

        /// <summary>True if an INBOUND trailer is docked at this door. Its cargo is headed for some of these
        /// lanes; see <see cref="LanesReservedForInbound"/> for which. Kept for callers that only need to
        /// know a trailer is present - lane choices use the per-lane reservation instead.</summary>
        public static bool HasInboundTruckDocked(int door)
        {
            foreach (var truck in Object.FindObjectsByType<TruckController>())
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

            // (An inbound trailer docked here no longer closes the whole door: LanesInStage leaves out just the lanes it will fill.)
            var usable = LanesInStage(inv, door);
            if (usable.Count == 0)
            {
                reason = HasInboundTruckDocked(door)
                    ? "an inbound trailer is docked there and needs its lanes"
                    : "no lane is free for outbound picking (Inbound-only, or all holding received pallets)";
                return false;
            }

            // Per LANE, not per door: the stage is offered while this customer can still have a lane to themselves, or - as a last resort -
            // while some lane still has room (sharing it with another customer; the loader digs out what it needs).
            bool haveOwnLane = usable.Any(l => IsLaneAvailableFor(orderService, door, l, customerId));
            if (!haveOwnLane && !(inv != null && usable.Any(l => inv.CountFreeStagingSlotsInLane(door, l) > 0)))
            {
                reason = "every lane there is held by another customer and none has room";
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
            // Lanes a docked inbound trailer will fill are held back from outbound (the order service isn't to hand here, so an outbound
            // order's own claim is not consulted: at worst one extra lane stays held, never one too few).
            var reservedForInbound = LanesReservedForInbound(null, inv, door);
            return LaneNamingService.AllLanes()
                .Where(l => l.door == door)
                .Where(l => !reservedForInbound.Contains(l.lane))
                .Where(l => inv == null || inv.LaneAllowsPicking(l.door, l.lane))
                // Skip lanes currently holding received pallets. This USED to reject the entire door
                // if any single lane had inbound stock, which made whole stages vanish from the
                // dropdown while most of their lanes were empty. Per-lane is both less blunt and more
                // correct: overflow simply never targets an occupied lane.
                .Where(l => !LaneHasInboundStock(inv, l.door, l.lane))
                // Also skip a lane the dock stocker has already committed to but not yet physically
                // dropped into (GameCore.Labor.TrailerOffloadController._pendingDrops) — without this
                // there's a race window between "DS chose this lane" and "pallet lands" where an
                // outbound release could land in the same lane the DS is mid-insertion into.
                .Where(l => !GameCore.Labor.TrailerOffloadController.IsLanePendingInbound(l.door, l.lane))
                .Select(l => l.lane)
                .OrderBy(l => l)
                .ToList();
        }

        /// <summary>The lane a newly-released order should start staging into: the first lane of the
        /// Stage that still has room. If every lane is full we still hand back the first one rather
        /// than failing the release — the order stages there when space frees up, and the driver's
        /// own overflow walk re-resolves at drop time anyway.</summary>
        public static bool TryResolveStartLane(OrderService orderService, InventoryService inv, int door, out string lane, string customerId = null)
        {
            lane = null;
            var lanes = LanesInStage(inv, door);
            if (lanes.Count == 0) return false;

            // A lane this customer can have to themselves (empty or already theirs) beats one shared with another customer.
            if (customerId != null)
                foreach (var candidate in lanes)
                    if (IsLaneAvailableFor(orderService, door, candidate, customerId) && inv != null && inv.TryFindStagingSlotInLane(door, candidate, out _))
                    { lane = candidate; return true; }

            foreach (var candidate in lanes)
            {
                if (inv != null && inv.TryFindStagingSlotInLane(door, candidate, out _)) { lane = candidate; return true; }
            }
            lane = lanes[0];
            return true;
        }
    }
}
