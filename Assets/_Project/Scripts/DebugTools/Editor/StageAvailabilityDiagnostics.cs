using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.DebugTools
{
    /// <summary>
    /// TEMPORARY diagnostic for "why isn't Stage N offered in the Work Queue dropdown?".
    ///
    /// The Work Queue builds its stage list from LaneNamingService.AllLanes(), so a door with no
    /// REGISTERED lane tiles is never evaluated and never explains itself — it just isn't there. That
    /// is invisible in-game, because a lane tile keeps its last drawn label even on a pass where it
    /// failed to register, so the floor can still read "1A-1" while the lane doesn't exist as far as
    /// staging is concerned. This dump separates those two worlds: what tiles are on the floor vs.
    /// what LaneNamingService actually resolved, plus the four selectability gates per door.
    ///
    /// Run from Tools/Diagnostics WHILE IN PLAY MODE (lane state is built at runtime only).
    /// DELETE once the staging-lane question is settled.
    /// </summary>
    public static class StageAvailabilityDiagnostics
    {
        private const string OutPath =
            @"C:\Users\MURILLO\AppData\Local\Temp\claude\C--Users-MURILLO--claude\e197961c-5f3f-403d-abea-399e27004f86\scratchpad\stage_diagnostic.txt";

        [MenuItem("Tools/Diagnostics/Dump Stage Availability")]
        public static void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== STAGE AVAILABILITY DIAGNOSTIC ===");
            if (!Application.isPlaying)
                sb.AppendLine("!! NOT IN PLAY MODE — lane geometry is runtime-only, everything below will be empty. !!");

            ServiceLocator.TryGet<InventoryService>(out var inv);
            ServiceLocator.TryGet<OrderService>(out var orderService);
            sb.AppendLine($"InventoryService: {(inv == null ? "NULL" : "ok")}   OrderService: {(orderService == null ? "NULL" : "ok")}");

            DumpDocks(sb);
            DumpLaneTiles(sb);
            DumpRegisteredLanes(sb, inv);
            DumpGates(sb, orderService, inv);

            string text = sb.ToString();
            Debug.Log(text);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                File.WriteAllText(OutPath, text);
                Debug.Log($"[StageDiagnostic] Written to {OutPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[StageDiagnostic] Could not write file: {e.Message}");
            }
        }

        /// <summary>Docks and their numbers. A dock whose DoorNumber is 0 can never own lanes:
        /// Recompute only indexes doors with DoorNumber > 0.</summary>
        private static void DumpDocks(StringBuilder sb)
        {
            var docks = DockSlot.All;
            sb.AppendLine($"\n-- DOCKS ({(docks == null ? 0 : docks.Count)}) --");
            if (docks == null || docks.Count == 0)
            {
                sb.AppendLine("  none — LaneNamingService.Recompute() bails out early, so NO lanes exist.");
                return;
            }
            foreach (var d in docks.Where(d => d != null).OrderBy(d => d.DoorNumber))
            {
                string flag = d.DoorNumber <= 0 ? "   <-- UNNUMBERED: cannot own lanes" : "";
                sb.AppendLine($"  Door {d.DoorNumber}  occupied={d.IsOccupied}  pos={d.transform.position}{flag}");
            }
        }

        /// <summary>Every lane tile physically on the floor, and whether LaneNamingService actually
        /// resolved it to an address. Tiles present but unresolved are the silent failure.</summary>
        private static void DumpLaneTiles(StringBuilder sb)
        {
            var byOwner = new Dictionary<string, int>();
            var unresolved = new List<string>();
            int total = 0;

            foreach (var po in PlacedObjectRegistry.All)
            {
                if (po == null || !po.gameObject.activeInHierarchy) continue;
                if (po.transform.Find("LaneNo") == null) continue;

                total++;
                var cell = new Vector2Int(po.gridX, po.gridY);
                bool resolved = LaneNamingService.TryGetSlot(cell, out var slot);
                string owner = string.IsNullOrEmpty(po.customData) ? "<none>" : po.customData;
                string key = resolved ? $"owner '{owner}' -> {slot.DoorNumber}{slot.Lane}" : $"owner '{owner}' -> UNRESOLVED";
                byOwner.TryGetValue(key, out int n);
                byOwner[key] = n + 1;

                if (!resolved && unresolved.Count < 40)
                    unresolved.Add($"    cell={cell} customData='{owner}' pos={po.transform.position}");
            }

            sb.AppendLine($"\n-- LANE TILES ON FLOOR ({total}) --");
            if (total == 0) sb.AppendLine("  none found (no Flr-ShipLane tiles placed / registered).");
            foreach (var kv in byOwner.OrderBy(k => k.Key))
                sb.AppendLine($"  {kv.Value,4} tiles   {kv.Key}");

            if (unresolved.Count > 0)
            {
                sb.AppendLine("  UNRESOLVED tiles (on the floor, but not addressable — these are invisible to staging;");
                sb.AppendLine("  note their painted label is stale and may still read like a valid lane):");
                foreach (var line in unresolved) sb.AppendLine(line);
            }
        }

        /// <summary>What AllLanes() reports — this is the ONLY set the Work Queue considers.</summary>
        private static void DumpRegisteredLanes(StringBuilder sb, InventoryService inv)
        {
            var lanes = LaneNamingService.AllLanes();
            sb.AppendLine($"\n-- REGISTERED LANES ({lanes.Count}) — the Work Queue sees only these --");
            if (lanes.Count == 0)
            {
                sb.AppendLine("  none. Every stage dropdown will be empty.");
                return;
            }
            foreach (var (door, lane) in lanes)
            {
                var cfg = LaneConfigRegistry.Get(door, lane);
                var laneSlots = LaneNamingService.GetLane(door, lane);
                bool picking = inv == null || inv.LaneAllowsPicking(door, lane);
                bool inbound = StagingLaneAssignmentService.LaneHasInboundStock(inv, door, lane);
                string why = !picking ? "  SKIPPED (Inbound-only)"
                           : inbound ? "  SKIPPED (holding received pallets)"
                           : "  usable for outbound";
                sb.AppendLine($"  {door}{lane}  slots={laneSlots.Count}  usage={cfg.Usage}  order={cfg.Order}{why}");

                // Slot 1 MUST be the end nearest the dock door — it's the end staging fills from and
                // the end loading picks from. If it isn't, every lane-entry calculation runs backwards.
                var dock = DockSlot.All?.FirstOrDefault(d => d != null && d.DoorNumber == door);
                if (dock == null || laneSlots.Count < 2) continue;
                if (!LaneNamingService.TryGetSlotWorldPos(laneSlots[0].Cell, out var firstPos)) continue;
                if (!LaneNamingService.TryGetSlotWorldPos(laneSlots[laneSlots.Count - 1].Cell, out var lastPos)) continue;

                float dFirst = Vector3.Distance(dock.transform.position, firstPos);
                float dLast = Vector3.Distance(dock.transform.position, lastPos);
                string verdict = dFirst <= dLast ? "OK" : "*** INVERTED: slot 1 is the FAR end ***";
                sb.AppendLine($"      slot {laneSlots[0].Slot} is {dFirst:F2}m from the door, " +
                              $"slot {laneSlots[laneSlots.Count - 1].Slot} is {dLast:F2}m — {verdict}");
            }

            var doors = lanes.Select(l => l.door).Distinct().OrderBy(d => d).ToList();
            sb.AppendLine($"  Doors with at least one registered lane: {string.Join(", ", doors)}");
            var docked = DockSlot.All?.Where(d => d != null && d.DoorNumber > 0).Select(d => d.DoorNumber).OrderBy(n => n).ToList()
                         ?? new List<int>();
            var missing = docked.Except(doors).ToList();
            if (missing.Count > 0)
                sb.AppendLine($"  *** Doors with NO registered lanes (never offered, never explained): {string.Join(", ", missing)} ***");
        }

        /// <summary>The four gates, per door, per customer that currently has an order.</summary>
        private static void DumpGates(StringBuilder sb, OrderService orderService, InventoryService inv)
        {
            sb.AppendLine("\n-- SELECTABILITY GATES --");
            if (orderService == null)
            {
                sb.AppendLine("  OrderService missing — cannot evaluate.");
                return;
            }

            var doors = LaneNamingService.AllLanes().Select(l => l.door).Distinct().OrderBy(d => d).ToList();
            var customers = orderService.ActiveOrders
                .Select(o => (o.CustomerId, o.CustomerName))
                .Distinct()
                .ToList();

            sb.AppendLine($"  Active orders: {orderService.ActiveOrders.Count()}");
            foreach (var o in orderService.ActiveOrders)
                sb.AppendLine($"    {o.OrderId}  {o.CustomerName}  status={o.Status}  door={o.AssignedDoorNumber}  lane='{o.AssignedLane}'");

            foreach (var (customerId, customerName) in customers)
            {
                sb.AppendLine($"  For customer {customerName} ({customerId}):");
                foreach (int d in doors)
                {
                    bool ok = StagingLaneAssignmentService.IsStageSelectableFor(orderService, inv, d, customerId, out string why);
                    sb.AppendLine($"    Stage {d}: {(ok ? "AVAILABLE" : "blocked — " + why)}");
                }
            }

            sb.AppendLine("  Inbound trucks docked:");
            bool any = false;
            foreach (var truck in Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None))
            {
                if (truck == null || truck.IsOutbound || truck.DockedAt == null) continue;
                any = true;
                sb.AppendLine($"    inbound truck at door {truck.DockedAt.DoorNumber}");
            }
            if (!any) sb.AppendLine("    none");
        }
    }
}
