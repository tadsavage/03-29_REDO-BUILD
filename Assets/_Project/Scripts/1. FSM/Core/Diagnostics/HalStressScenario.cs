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
            public bool Release;        // release them to the staging lanes straight away (off by default: HAL releases orders itself, at the right time)
            public bool ClearLanes;     // first un-release every Pending order with nothing picked, so the staging lanes start with no commitments
            public int SaveToSlot;      // 1-8 = save the game there when done, 0 = don't

            public static Spec Default => new Spec { PalletsPerSku = 4, Orders = 6, MinLines = 38, MaxLines = 50, MinCases = 4, MaxCases = 9, Release = false, ClearLanes = true, SaveToSlot = 8 };
        }

        /// <summary>The values the HAL tab's sliders edit.</summary>
        public static Spec Current = Spec.Default;

        public struct Result { public int Pallets, Orders, Lines, Cases, Released, WaitingForLane; public string Note; }

        /// <summary>Un-releases every Pending order that has nothing picked: no lane, no door, not released, and its released tasks (selector and
        /// pallet-pick work filed for the lane) cancelled, then frees any reservation no live task still holds. Orders already being picked or
        /// staged are left alone. Returns how many orders were un-released.</summary>
        public static int ClearLaneCommitments(OrderService orders)
        {
            if (orders == null || !ServiceLocator.TryGet<WorkQueueSystem>(out var wq) || wq == null) return 0;
            int n = 0;
            foreach (var o in orders.ActiveOrders.ToList())
            {
                if (o == null || o.Status != OrderData.OrderStatus.Pending || o.TotalUnitsPicked > 0) continue;
                if (!o.ReleasedToFloor && string.IsNullOrEmpty(o.AssignedLane) && o.AssignedDoorNumber == 0) continue;
                foreach (var t in orders.LiveTasksForOrder(o.OrderId).ToList())
                    if (t.Type == WorkTaskType.OrderSelect || t.Type == WorkTaskType.PalletPick) t.Status = WorkTaskStatus.Cancelled;
                o.AssignedDoorNumber = 0;
                o.StagingDoorNumber = 0;
                o.ReleasedToFloor = false;
                o.PlannedPallets = 0;
                o.AssignedLane = null;
                o.OverflowLanes.Clear();
                // An unreleased order carries one Open placeholder task. Cancelling everything and leaving none made the order unreleasable:
                // IsAwaitingRelease needs a live placeholder, so HAL never released any of them (block 39, 2026-11-12).
                orders.EnsureAwaitingReleaseTask(o);
                n++;
            }
            if (n > 0)
            {
                // Same sweep a load does: a Reserved slot with no live task behind it is released.
                var claimed = new HashSet<string>();
                foreach (var t in wq.Tasks)
                {
                    if (t.Status == WorkTaskStatus.Complete || t.Status == WorkTaskStatus.Cancelled) continue;
                    if (!string.IsNullOrEmpty(t.ToLocation)) claimed.Add(t.ToLocation);
                    if (!string.IsNullOrEmpty(t.FromLocation)) claimed.Add(t.FromLocation);
                }
                LocationStatusRegistry.ReleaseUnclaimedReservations(claimed);
            }
            return n;
        }

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

            // 0. Clean staging lanes: a saved game can carry orders released to lanes (each commits 2 tiers in its lane until it is picked), which
            //    made lanes that hold no pallet at all read as full and paused offloading ("no room in any staging lane", Tad 2026-11-12).
            int unreleased = s.ClearLanes ? ClearLaneCommitments(orders) : 0;

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

            r.Note = (unreleased > 0 ? $"{unreleased} order(s) un-released (lanes cleared); " : "") +
                     $"{stock.Pallets} pallet(s) stocked for {stock.SkusStocked} SKU(s); {r.Orders} large order(s), {r.Lines} lines, {r.Cases} cases; " +
                     (s.Release ? $"{r.Released}/{ids.Count} released" + (r.WaitingForLane > 0 ? $" ({r.WaitingForLane} waiting for a lane)" : "") + (lastFail != null ? $", last failure: {lastFail}" : "") + "." : "not released.") + saved;
            Debug.LogWarning("[HalStressScenario] " + r.Note);
            return r;
        }
    }
}
