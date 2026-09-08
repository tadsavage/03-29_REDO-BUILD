using System.Collections.Generic;
using UnityEngine;
using GameCore.Inventory;

/// <summary>
/// Where a selector will go to pick an order, worked out from current stock.
///
/// This exists so the Work Queue can show a not-yet-released order's first and last pick faces
/// without re-implementing the selector's slot choice. That rule used to live only inside
/// OrderSelectionTaskDriver.TryFindBestPickLocation; a second copy in the UI would have started
/// agreeing and quietly stopped, showing the player a route the selector never takes. The driver now
/// calls ChooseSlot too, so there is exactly one rule.
/// </summary>
public static class OrderPickPath
{
    /// <summary>
    /// The pick face a selector will take this line item from: the first assigned slot holding enough
    /// to satisfy it outright, else whichever assigned slot holds the most (the selector tops up from
    /// a second location on its next pass). Null if no assigned slot has any stock at all.
    /// </summary>
    public static LocationData ChooseSlot(string skuId, int remaining, out int takeQty)
    {
        LocationData best = null;
        int bestQty = 0;

        foreach (var address in SlotAssignmentService.GetSlotsForSku(skuId))
        {
            if (!LocationRegistry.TryGet(address, out var candidate)) continue;
            if (candidate.Quantity <= 0) continue;

            if (candidate.Quantity >= remaining)
            {
                best = candidate;
                bestQty = remaining;
                break;
            }
            if (candidate.Quantity > bestQty)
            {
                best = candidate;
                bestQty = candidate.Quantity;
            }
        }

        takeQty = best == null ? 0 : Mathf.Min(bestQty, remaining);
        return best;
    }

    /// <summary>
    /// The pick faces this order will be worked through, in line-item order — one entry per line item
    /// that still needs cases and has stock somewhere. Empty if nothing on the order can be picked.
    ///
    /// A PROJECTION against stock as it stands right now, not a simulation: it doesn't decrement stock
    /// between line items, and it doesn't model the top-up second visit the driver makes when one slot
    /// can't fill a line item on its own (the driver re-scans after every pick, so its real route can
    /// revisit or add faces). For "where does this order start and end", which is what the Work Queue
    /// shows, that difference doesn't change the answer; don't reuse this to drive actual picking.
    /// </summary>
    public static List<string> Project(OrderData order)
    {
        var path = new List<string>();
        if (order == null) return path;

        foreach (var li in order.LineItems)
        {
            if (li == null || li.IsFullyPicked) continue;
            var slot = ChooseSlot(li.SkuId, li.QuantityRemaining, out _);
            if (slot != null) path.Add(slot.Address);
        }
        return path;
    }
}
