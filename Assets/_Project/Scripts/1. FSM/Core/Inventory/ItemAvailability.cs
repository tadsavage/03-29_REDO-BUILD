using System.Collections.Generic;
using System.Linq;
using GameCore.Economy;
using GameCore.Services;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>OK = nothing to worry about (white). PotentialScratch = short on the shelf, but a PO lands before the
    /// order is due (yellow). Insufficient = short, with nothing arriving in time (red).</summary>
    public enum AvailabilityStatus { Ok, PotentialScratch, Insufficient }

    /// <summary>One SKU's line on the Item Listing report.</summary>
    public class ItemAvailabilityRow
    {
        public SkuData Sku;
        public string SkuId;
        public string Description;
        public Sprite Icon;
        public int OnHand;          // cases on hand
        public int Inbound;         // cases still to arrive on open inbound POs
        public int Pending;         // cases still owed to outbound orders
        public int Net;             // OnHand - Pending + Inbound  (the "potential scratch" figure)
        public AvailabilityStatus Status;
        public float PurchasePrice; // cheapest vendor cost per case today
        public float SalePrice;     // what we sell a case for
        public string Vendor;       // the vendor with that cheapest cost
        public string VendorId;     // that vendor's id (null when nobody sells it)
        public int CasesPerPallet;  // one full pallet of this SKU (0 if unknown)
    }

    /// <summary>
    /// Builds the per-SKU availability picture: stock, inbound POs, outbound demand and the red/yellow/white verdict.
    /// Shared by the Item Listing report and the "SKUs with insufficient balance" total on Warehouse Capacity, so the two
    /// can never disagree.
    ///
    /// VERDICT (walks the outbound orders by due date, cumulatively):
    ///   - the orders up to this one fit in what is on the shelf              -> fine
    ///   - they don't, but shelf + POs that ARRIVE BEFORE this order is due do -> PotentialScratch (yellow)
    ///   - they don't even with those POs                                       -> Insufficient (red)
    /// No orders at all (including 0 on hand / 0 on order) is always Ok.
    /// </summary>
    public static class ItemAvailability
    {
        private const int MinutesPerDay = 1440;

        public static List<ItemAvailabilityRow> Build()
        {
            var rows = new List<ItemAvailabilityRow>();
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return rows;
            ServiceLocator.TryGet<OrderService>(out var orders);
            ServiceLocator.TryGet<ShipmentService>(out var shipments);
            ServiceLocator.TryGet<MarketService>(out var market);
            ServiceLocator.TryGet<VendorEconomyService>(out var economy);
            ServiceLocator.TryGet<SimulationTimeService>(out var time);

            long nowMin = time != null ? (long)time.Day * MinutesPerDay + time.Hour * 60 + time.Minute : 0L;

            // On hand, per SKU (every pallet record, wherever it is standing).
            var onHand = new Dictionary<string, int>();
            foreach (var p in inv.GetAllPallets())
            {
                if (string.IsNullOrEmpty(p.SkuId)) continue;
                onHand.TryGetValue(p.SkuId, out int n);
                onHand[p.SkuId] = n + p.Quantity;
            }

            // Outbound demand per SKU: (due minute, cases still owed). Orders already staged/loaded have their stock out of
            // the building's inventory, so only orders still waiting to be picked count.
            var demand = new Dictionary<string, List<(long due, int qty)>>();
            if (orders != null)
            {
                // An order the first load pass already marked Staged/Loading/Loaded can STILL owe cases (selectors are mid-trip, or later trips
                // are queued) - the status alone said "done" and hid that demand. Those orders count while pick work for them is live.
                var liveWorkOrders = new HashSet<string>();
                if (ServiceLocator.TryGet<GameCore.Labor.WorkQueueSystem>(out var queue) && queue != null)
                    foreach (var task in queue.Tasks)
                    {
                        if (string.IsNullOrEmpty(task.OrderId)) continue;
                        if (task.Type != GameCore.Labor.WorkTaskType.OrderSelect && task.Type != GameCore.Labor.WorkTaskType.PalletPick) continue;
                        if (task.Status == GameCore.Labor.WorkTaskStatus.Complete || task.Status == GameCore.Labor.WorkTaskStatus.Cancelled) continue;
                        liveWorkOrders.Add(task.OrderId);
                    }

                foreach (var o in orders.ActiveOrders)
                {
                    if (o == null || o.ReturnsMode) continue;
                    if (o.Status == OrderData.OrderStatus.Shipped || o.Status == OrderData.OrderStatus.Cancelled) continue;
                    bool stillPicking = o.Status == OrderData.OrderStatus.Pending || o.Status == OrderData.OrderStatus.PartiallyPicked ||
                                        o.Status == OrderData.OrderStatus.Backorder;
                    if (!stillPicking && !liveWorkOrders.Contains(o.OrderId)) continue;
                    long due = ((long)o.DueDay + 1) * MinutesPerDay;   // end of the due day
                    foreach (var li in o.LineItems)
                    {
                        if (li == null || string.IsNullOrEmpty(li.SkuId)) continue;
                        int rem = li.QuantityRemaining;
                        if (rem <= 0) continue;
                        if (!demand.TryGetValue(li.SkuId, out var list)) demand[li.SkuId] = list = new List<(long, int)>();
                        list.Add((due, rem));
                    }
                }
            }

            // Inbound per SKU: (arrival minute, cases not yet received) on every open PO.
            var inbound = new Dictionary<string, List<(long eta, int qty)>>();
            if (shipments != null)
            {
                foreach (var s in shipments.PendingShipments)
                {
                    if (s == null) continue;
                    if (s.Status != ShipmentData.ShipmentStatus.InTransit && s.Status != ShipmentData.ShipmentStatus.Receiving &&
                        s.Status != ShipmentData.ShipmentStatus.Delayed) continue;
                    long eta = (long)s.ArrivalDayNumber * MinutesPerDay + s.ArrivalTimeMinute;
                    if (eta < nowMin) eta = nowMin;   // already due: it is "here any moment"
                    foreach (var li in s.LineItems)
                    {
                        if (li == null || li.Dropped || string.IsNullOrEmpty(li.SkuId)) continue;
                        int rem = li.Quantity - li.ReceivedQuantity;
                        if (rem <= 0) continue;
                        if (!inbound.TryGetValue(li.SkuId, out var list)) inbound[li.SkuId] = list = new List<(long, int)>();
                        list.Add((eta, rem));
                    }
                }
            }

            // Which SKUs each vendor sells right now (computed once per vendor, not once per SKU).
            var vendors = new List<(string id, string name, HashSet<string> skus)>();
            var registry = VendorRegistry.Load();
            if (registry != null)
            {
                foreach (var v in registry.AllVendors)
                {
                    if (v == null) continue;
                    var set = new HashSet<string>();
                    if (economy != null)
                    {
                        foreach (var e in economy.GetAvailableCatalogue(v.VendorId))
                            if (e?.Sku != null) set.Add(e.Sku.SkuId);
                    }
                    else
                    {
                        foreach (var e in v.Catalogue) if (e?.Sku != null) set.Add(e.Sku.SkuId);
                    }
                    if (set.Count > 0) vendors.Add((v.VendorId, v.DisplayName, set));
                }
            }

            foreach (var sku in inv.AllSkus)
            {
                if (sku == null) continue;
                string id = sku.SkuId;
                onHand.TryGetValue(id, out int have);
                demand.TryGetValue(id, out var dem);
                inbound.TryGetValue(id, out var inb);

                int pending = dem != null ? dem.Sum(d => d.qty) : 0;
                int incoming = inb != null ? inb.Sum(i => i.qty) : 0;

                var status = AvailabilityStatus.Ok;
                if (dem != null)
                {
                    int cumulative = 0;
                    foreach (var d in dem.OrderBy(d => d.due))
                    {
                        cumulative += d.qty;
                        if (cumulative <= have) continue;                       // the shelf covers it
                        int arrivingInTime = inb != null ? inb.Where(i => i.eta <= d.due).Sum(i => i.qty) : 0;
                        if (cumulative <= have + arrivingInTime) status = AvailabilityStatus.PotentialScratch;
                        else { status = AvailabilityStatus.Insufficient; break; }
                    }
                }

                // Cheapest vendor today.
                string bestVendorId = null;
                float bestCost = float.MaxValue; string bestVendor = "—";
                foreach (var v in vendors)
                {
                    if (!v.skus.Contains(id)) continue;
                    float cost = economy != null ? economy.GetEffectiveCost(v.id, sku, market) : sku.BuyValue;
                    if (cost < bestCost) { bestCost = cost; bestVendor = v.name; bestVendorId = v.id; }
                }
                if (bestCost == float.MaxValue) bestCost = market != null ? market.CurrentPrice(sku) : sku.BuyValue;

                rows.Add(new ItemAvailabilityRow
                {
                    Sku = sku,
                    SkuId = id,
                    Description = sku.ItemDescription,
                    Icon = sku.Icon,
                    OnHand = have,
                    Inbound = incoming,
                    Pending = pending,
                    Net = have + incoming - pending,
                    Status = status,
                    PurchasePrice = bestCost,
                    SalePrice = sku.SellValue,
                    Vendor = bestVendor,
                    VendorId = bestVendorId,
                    CasesPerPallet = orders != null ? orders.FullPalletCases(id) : 0,
                });
            }
            return rows;
        }
    }
}
