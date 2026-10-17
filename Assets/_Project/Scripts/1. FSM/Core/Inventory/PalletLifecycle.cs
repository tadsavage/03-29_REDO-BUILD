using System.Linq;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Where a pallet is in the building's "digestive system" (Tad, 2026-10-17).
    ///
    ///   Unknown   - offloaded into a staging lane (or still on the inbound trailer), not received yet.
    ///   Available - received; free for putaway, a replenishment or an order. Also a racked pallet with no claim on it.
    ///   InTransit - on forks, a selector, a pallet jack or a reach truck: moving.
    ///   Reserved  - claimed by exactly one task (replenishment, pallet pick) or a pick-face pallet that released
    ///               orders are picking from. Nothing else may take it.
    ///   Staged    - standing in a staging lane for an order.
    ///   Loading   - lifted by the loader on its way into the trailer.
    ///   Shipped   - set down in an outbound trailer. It never comes back out, and it is the only state billing pays for.
    /// </summary>
    public enum PalletLifecycle { Unknown, Available, InTransit, Reserved, Staged, Loading, Shipped }

    /// <summary>
    /// Works out a pallet's <see cref="PalletLifecycle"/> from what is actually true right now - its parent, its order
    /// tag, its inventory record and the live work queue - instead of a stored field that has to be kept in step by
    /// every system that touches a pallet. That is deliberate: this project has paid twice already for two stores of
    /// the same fact drifting apart (LocationData vs LocationStatusRegistry). It also means the status is correct the
    /// moment a save finishes loading, with nothing extra to persist.
    /// </summary>
    public static class PalletLifecycleResolver
    {
        public static PalletLifecycle Resolve(GameObject pallet)
        {
            if (pallet == null) return PalletLifecycle.Unknown;

            // Outbound freight (case-picked or a whole pallet picked for an order).
            var outbound = pallet.GetComponent<OutboundPalletBuilder>();
            if (outbound != null && !string.IsNullOrEmpty(outbound.OrderId))
            {
                switch (outbound.Stage)
                {
                    case OutboundPalletBuilder.PalletStage.Loaded:
                    case OutboundPalletBuilder.PalletStage.Shipped: return PalletLifecycle.Shipped;
                    case OutboundPalletBuilder.PalletStage.Loading: return PalletLifecycle.Loading;
                }
                var truck = pallet.GetComponentInParent<TruckController>();
                if (truck != null) return truck.IsOutbound ? PalletLifecycle.Shipped : PalletLifecycle.Unknown;
                if (!outbound.IsOnFloor || outbound.Stage == OutboundPalletBuilder.PalletStage.Moving) return PalletLifecycle.InTransit;
                return PalletLifecycle.Staged;
            }

            // Still on an inbound trailer: not ours to count yet.
            if (pallet.GetComponentInParent<TruckController>() != null) return PalletLifecycle.Unknown;

            // Riding any vehicle or person.
            if (!OutboundPalletBuilder.IsOnFloorTransform(pallet.transform)) return PalletLifecycle.InTransit;

            var link = pallet.GetComponent<PalletMasterLink>();
            ServiceLocator.TryGet<InventoryService>(out var inv);
            var record = link != null && inv != null ? inv.GetPallet(link.PalletId) : null;
            if (record == null) return PalletLifecycle.Unknown;
            return ForRecord(record);
        }

        /// <summary>Status of a pallet that is standing still (rack, lane) with an inventory record.</summary>
        public static PalletLifecycle ForRecord(PalletMasterRecord record)
        {
            if (record == null) return PalletLifecycle.Unknown;
            if (string.IsNullOrEmpty(record.LoadId)) return PalletLifecycle.Unknown;   // offloaded, not received
            if (IsReservedByTask(record.PalletId, out _)) return PalletLifecycle.Reserved;
            if (IsPickFaceWithDemand(record)) return PalletLifecycle.Reserved;
            return PalletLifecycle.Available;
        }

        /// <summary>
        /// True if a live replenishment or pallet pick has claimed this pallet. Those are the claims that make a
        /// pallet Reserved: a Putaway task does not (a received pallet waiting for putaway is still Available, and an
        /// order may take it straight out of the lane, which is how TryFindStagedPalletAwaitingPutaway works).
        /// </summary>
        public static bool IsReservedByTask(string palletId, out WorkTask task, string exceptTaskId = null)
        {
            task = null;
            if (string.IsNullOrEmpty(palletId)) return false;
            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var wq) || wq == null) return false;
            task = wq.Tasks.FirstOrDefault(t =>
                t.PalletId == palletId && t.TaskId != exceptTaskId &&
                (t.Type == WorkTaskType.Replenish || t.Type == WorkTaskType.PalletPick) &&
                t.Status != WorkTaskStatus.Complete && t.Status != WorkTaskStatus.Cancelled);
            return task != null;
        }

        /// <summary>A pallet in a pick slot that a released, unfinished order still needs cases of.</summary>
        private static bool IsPickFaceWithDemand(PalletMasterRecord record)
        {
            var pick = LocationRegistry.PickLocations?.FirstOrDefault(l => l != null && l.PalletId == record.PalletId);
            if (pick == null) return false;
            if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return false;
            foreach (var order in orders.ActiveOrders)
            {
                if (order == null || !order.ReleasedToFloor) continue;
                if (order.Status == OrderData.OrderStatus.Shipped || order.Status == OrderData.OrderStatus.Cancelled) continue;
                foreach (var li in order.LineItems)
                    if (li.SkuId == record.SkuId && orders.SelectableRemaining(order, li) > 0) return true;
            }
            return false;
        }

        public static string Label(PalletLifecycle s) => s switch
        {
            PalletLifecycle.Unknown   => "UNKNOWN",
            PalletLifecycle.Available => "AVAILABLE",
            PalletLifecycle.InTransit => "IN TRANSIT",
            PalletLifecycle.Reserved  => "RESERVED",
            PalletLifecycle.Staged    => "STAGED",
            PalletLifecycle.Loading   => "LOADING",
            PalletLifecycle.Shipped   => "SHIPPED",
            _ => s.ToString().ToUpperInvariant(),
        };
    }
}
