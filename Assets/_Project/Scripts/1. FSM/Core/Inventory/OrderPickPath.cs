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
    public static LocationData ChooseSlot(string skuId, int remaining, out int takeQty, long minKey = long.MinValue)
    {
        LocationData best = null;
        int bestQty = 0;

        foreach (var address in SlotAssignmentService.GetSlotsForSku(skuId))
        {
            if (!LocationRegistry.TryGet(address, out var candidate)) continue;
            if (candidate.Quantity <= 0) continue;
            // ASCENDING ONLY (Tad, 2026-10-18): a selector never picks going the wrong way down the aisle, so a slot
            // earlier in the sequence than where this trip has already been is not a candidate.
            if (SequenceKey(candidate.Address) < minKey) continue;

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
    /// SERPENTINE PICK SEQUENCE (Tad, 2026-10-17): a selector walks aisle 1 with the bays ascending, turns
    /// around and walks aisle 2 with the bays descending, aisle 3 ascending again, and so on - never back and
    /// forth inside an aisle. Within a bay the lower level comes first, then position (in walking direction).
    /// Lower key = visited earlier. Addresses that don't parse (AA-BB-LP) sort last.
    /// </summary>
    public static long SequenceKey(string address)
    {
        if (string.IsNullOrEmpty(address)) return long.MaxValue;
        var parts = address.Split('-');
        if (parts.Length < 3 || !int.TryParse(parts[0], out int aisle) || !int.TryParse(parts[1], out int bay) || parts[2].Length < 1)
            return long.MaxValue;
        char lc = parts[2][0];
        int level = char.IsDigit(lc) ? lc - '0' : 10 + char.ToUpperInvariant(lc) - 'A';
        int pos = parts[2].Length > 1 && char.IsDigit(parts[2][1]) ? parts[2][1] - '0' : 0;
        // EVERY aisle ascends (Tad, 2026-10-18): the jack always points the way the bays count up and never turns around
        // inside an aisle; after the last bay it goes round to the low end of the next aisle. (It used to alternate
        // direction by aisle, which made the jack U-turn at the end of every second aisle.)
        return ((long)aisle * 1000 + bay) * 1000 + level * 10 + pos;
    }

    /// <summary>
    /// The next pick of an order: across every line item that still has cases to pick, the slot that comes
    /// EARLIEST in the serpentine sequence. Picking always moves forward through the building this way instead
    /// of following line-item order (which zigzagged up and down the aisles). <paramref name="wantFor"/> says how
    /// many cases a line still needs from a case picker (0 = skip).
    /// </summary>
    public static bool ChooseNext(OrderData order, System.Func<OrderLineItem, int> wantFor,
                                  out LocationData location, out OrderLineItem lineItem, out int takeQty,
                                  long minKey = long.MinValue)
    {
        location = null; lineItem = null; takeQty = 0;
        if (order == null) return false;
        long bestKey = long.MaxValue;
        foreach (var li in order.LineItems)
        {
            if (li == null || li.IsFullyPicked) continue;
            int want = wantFor != null ? wantFor(li) : li.QuantityRemaining;
            if (want <= 0) continue;
            var slot = ChooseSlot(li.SkuId, want, out int qty, minKey);
            if (slot == null) continue;
            long key = SequenceKey(slot.Address);
            if (location == null || key < bestKey)
            {
                bestKey = key; location = slot; lineItem = li; takeQty = qty;
            }
        }
        return location != null;
    }

    /// <summary>
    /// PICKLIST for the pallet a selector is about to start (Tad, 2026-10-18): the items and case counts that will
    /// go on it, in walking order, until it CUBES OUT (about one reference pallet, ~70 cu ft - the same
    /// 1/(Ti*Hi)-per-case proxy OutboundPalletBuilder.FillFraction uses). Call it when the pallet starts: the
    /// cases already on earlier pallets of this trip are picked by then, so <paramref name="wantFor"/> (what is
    /// still owed to this selector) already excludes them. Each line is capped by the stock actually sitting in its
    /// assigned pick faces. Same ordering as ChooseNext, so the plan and the real route agree; it is a forecast, so
    /// a shortage or another selector's reservation can still change what actually goes on.
    /// </summary>
    public static List<KeyValuePair<string, int>> PlanPallet(OrderData order, System.Func<OrderLineItem, int> wantFor,
                                                             System.Func<string, SkuData> getSku, long minKey = long.MinValue)
    {
        var plan = new List<KeyValuePair<string, int>>();
        if (order == null) return plan;

        var candidates = new List<(long key, string sku, int want, float cost)>();
        foreach (var li in order.LineItems)
        {
            if (li == null || li.IsFullyPicked) continue;
            int want = wantFor != null ? wantFor(li) : li.QuantityRemaining;
            if (want <= 0) continue;
            var slot = ChooseSlot(li.SkuId, want, out _, minKey);
            if (slot == null) continue;

            int stock = 0;
            foreach (var address in SlotAssignmentService.GetSlotsForSku(li.SkuId))
                if (SequenceKey(address) >= minKey && LocationRegistry.TryGet(address, out var s)) stock += Mathf.Max(0, s.Quantity);
            want = Mathf.Min(want, stock);
            if (want <= 0) continue;

            var sku = getSku != null ? getSku(li.SkuId) : null;
            int perPallet = sku != null ? Mathf.Max(1, sku.Ti * sku.Hi) : 1;
            candidates.Add((SequenceKey(slot.Address), li.SkuId, want, 1f / perPallet));
        }
        candidates.Sort((a, b) => a.key.CompareTo(b.key));

        float fill = 0f;
        foreach (var c in candidates)
        {
            // The case that carries the pallet to 1.0 still goes on it (the driver checks IsCubedOut AFTER adding).
            int room = Mathf.Max(1, Mathf.CeilToInt((1f - fill) / c.cost - 0.0001f));
            int take = Mathf.Min(c.want, room);
            plan.Add(new KeyValuePair<string, int>(c.sku, take));
            fill += take * c.cost;
            if (fill >= 1f - 0.0001f) break;
        }
        return plan;
    }

    /// <summary>
    /// The pick faces this order will be worked through, in serpentine walking order — one entry per line item
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
        path.Sort((a, b) => SequenceKey(a).CompareTo(SequenceKey(b)));
        return path;
    }
}
