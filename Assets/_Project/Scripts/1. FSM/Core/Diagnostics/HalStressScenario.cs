using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;

namespace GameCore.Diagnostics
{
    /// <summary>
    /// HAL STRESS SCENARIO (Tad, 2026-11-12): "a better test environment that has inventory to ship on actual large case-pick orders".
    /// One click on the HAL tab (TEST SCENARIO) turns whatever warehouse is loaded into a repeatable stress test:
    ///   1. every SKU is stocked (TestRackStocker.FillRacks: a pick face plus reserve pallets, real pallets, free of charge),
    ///   2. N LARGE case-pick orders arrive (default 6 x 38-50 lines x 4-9 cases = 150-450 cases each: the shape of a real bulk order, which
    ///      is split into several planned pallets and keeps three selectors and their jacks busy for hours),
    ///   3. they are released to the staging lanes the same way the Scheduler's RELEASE ORDERS does,
    ///   4. the game is saved to a slot, so INITIATE HAL 9000 with PlaytestBotSettings.startFromSlot = that slot starts every block from
    ///      the SAME stocked warehouse and the SAME orders (blocks become comparable).
    /// Orders are sized against what is on hand (pick faces PLUS reserve, minus what open orders already owe), so every line can be filled
    /// and the replenishment loop (reserve -> pick face) is part of the test instead of a stock-out. Lines stay under 45% of a pallet so none
    /// turns into a pallet pick: this is a CASE-pick test.
    /// </summary>
    public static class HalStressScenario
    {
        public struct Spec
        {
            public int PalletsPerSku;   // pallets of each SKU in the building (face + reserve)
            public int Orders;          // large orders to create
            public int MinLines, MaxLines;
            public int MinCases, MaxCases;   // per line
            public bool Release;        // release them to the staging lanes straight away
            public int SaveToSlot;      // 1-8 = save the game there when done, 0 = don't

            public static Spec Default => new Spec { PalletsPerSku = 4, Orders = 6, MinLines = 38, MaxLines = 50, MinCases = 4, MaxCases = 9, Release = true, SaveToSlot = 8 };
        }

        /// <summary>The values the HAL tab's sliders edit.</summary>
        public static Spec Current = Spec.Default;

        public struct Result { public int Pallets, Orders, Lines, Cases, Released, WaitingForLane; public string Note; }

        public static Result Build(Spec s)
        {
            var r = new Result();
            if (!Application.isPlaying) { r.Note = "Enter Play mode and load a warehouse first."; return r; }
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null
                || !ServiceLocator.TryGet<OrderService>(out var orders) || orders == null
                || !ServiceLocator.TryGet<SimulationTimeService>(out var time) || time == null)
            { r.Note = "Inventory / order / time service not available."; return r; }

            var customers = CustomerRegistry.Load();
            if (customers == null) { r.Note = "No CustomerRegistry asset."; return r; }

            // 1. Stock.
            var stock = TestRackStocker.FillRacks(Mathf.Max(2, s.PalletsPerSku));
            r.Pallets = stock.Pallets;

            // 2. Large orders, sized against everything on hand minus what open orders already owe.
            var avail = new Dictionary<string, int>();
            foreach (var sku in inv.AllSkus)
            {
                if (sku == null || sku.Ti <= 0 || sku.Hi <= 0 || sku.SellValue <= 0f) continue;
                int onHand = inv.TotalOnHand(sku.SkuId);
                if (onHand > 0) avail[sku.SkuId] = onHand;
            }
            foreach (var o in orders.ActiveOrders)
                foreach (var li in o.LineItems)
                    if (avail.ContainsKey(li.SkuId)) avail[li.SkuId] -= li.QuantityRemaining;

            var ids = new List<string>();
            for (int n = 0; n < s.Orders; n++)
            {
                var customer = customers.GetRandom();
                if (customer == null) break;
                var order = new OrderData(customer.CustomerId, customer.CompanyName, $"{customer.CompanyName} Distribution Center",
                                          time.Day, time.Day + OrderGenerator.DefaultDueDaysOut, time.Minute);
                int lines = Random.Range(s.MinLines, s.MaxLines + 1);
                foreach (var skuId in avail.Where(kv => kv.Value >= s.MinCases).Select(kv => kv.Key).OrderBy(_ => Random.value).Take(lines).ToList())
                {
                    var sku = inv.GetSkuData(skuId);
                    if (sku == null) continue;
                    int cap = Mathf.Max(s.MinCases, Mathf.FloorToInt(sku.Ti * sku.Hi * 0.45f));   // never a pallet pick
                    int qty = Mathf.Min(Random.Range(s.MinCases, s.MaxCases + 1), cap, avail[skuId]);
                    if (qty <= 0) continue;
                    avail[skuId] -= qty;
                    order.LineItems.Add(new OrderLineItem(skuId, qty, Mathf.RoundToInt(sku.BuyValue), Mathf.RoundToInt(sku.SellValue)));
                    r.Cases += qty;
                }
                if (order.LineItems.Count == 0) break;   // out of stock
                orders.ReceiveOrder(order);
                ids.Add(order.OrderId);
                r.Lines += order.LineItems.Count;
            }
            r.Orders = ids.Count;

            // 3. Release, one at a time so one failure can't block the rest (same call as the Scheduler's RELEASE ORDERS).
            string lastFail = null;
            if (s.Release)
                foreach (var id in ids)
                {
                    if (orders.ReleaseOrdersToStages(new List<string> { id }, out string why, out bool wait, out _)) { r.Released++; if (wait) r.WaitingForLane++; }
                    else lastFail = why;
                }

            // 4. Save to a slot for startFromSlot.
            string saved = "";
            if (s.SaveToSlot >= 1 && s.SaveToSlot <= 8 && SaveLoadSystem.SaveManager.Instance != null)
            {
                SaveLoadSystem.SaveManager.Instance.SaveToSlot(s.SaveToSlot - 1, "HAL stress scenario");
                saved = $" Saved to slot {s.SaveToSlot} (set \"Start from saved game\" to {s.SaveToSlot}).";
            }

            r.Note = $"{stock.Pallets} pallet(s) stocked for {stock.SkusStocked} SKU(s); {r.Orders} large order(s), {r.Lines} lines, {r.Cases} cases; " +
                     (s.Release ? $"{r.Released}/{ids.Count} released" + (r.WaitingForLane > 0 ? $" ({r.WaitingForLane} waiting for a lane)" : "") + (lastFail != null ? $", last failure: {lastFail}" : "") + "." : "not released.") + saved;
            Debug.LogWarning("[HalStressScenario] " + r.Note);
            return r;
        }
    }
}
