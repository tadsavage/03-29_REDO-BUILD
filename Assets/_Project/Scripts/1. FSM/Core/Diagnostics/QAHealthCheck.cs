#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;

namespace GameCore.Diagnostics
{
    /// <summary>
    /// DEV-ONLY soak-test helper (Claude QA pass, 2026-10-17). <see cref="Run"/> scans the live game for the
    /// classes of bug Tad asked to hunt and returns a report; <see cref="SoakTick"/> keeps a test warehouse busy
    /// (money top-up, purchase orders when stock runs low, test orders, releasing them). Called from the Unity
    /// MCP bridge; nothing in the game calls it. Excluded from release builds.
    ///
    /// Checks: lane pallets with no record / no task / an order that no longer exists; duplicate inventory links;
    /// floating pallets (nothing under them); pallets inside each other; lane records with no pallet object;
    /// Assigned tasks that have gone stale; trucks tilted, sunk, overlapping, or stalled mid-route; capital that
    /// changed without a ledger entry; reputation changes that don't add up; partnership values out of range.
    /// </summary>
    public static class QAHealthCheck
    {
        private static bool _hooked;
        private static readonly List<string> _repLog = new();
        private static int _lastRepScore = int.MinValue;
        private static int? _moneyBaseline;
        private static readonly Dictionary<string, float> _lastSatisfaction = new();
        private static readonly Dictionary<TruckController, (Vector3 pos, float movedAt)> _truckTrack = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _hooked = false; _repLog.Clear(); _lastRepScore = int.MinValue; _moneyBaseline = null;
            _lastSatisfaction.Clear(); _truckTrack.Clear(); CheatTotal = 0;
        }

        /// <summary>Rough economy report: lifetime income/expense by category, plus cheat cash.</summary>
        public static string Economy()
        {
            if (!ServiceLocator.TryGet<MoneyService>(out var m)) return "no money service";
            var sb = new StringBuilder($"capital={m.CurrentCapital} cheat={CheatTotal}\nINCOME: ");
            sb.Append(string.Join(", ", m.LifetimeIncome.Where(kv => kv.Value != 0).Select(kv => $"{kv.Key}={kv.Value}")));
            sb.Append("\nEXPENSE: ");
            sb.Append(string.Join(", ", m.LifetimeExpenses.Where(kv => kv.Value != 0).Select(kv => $"{kv.Key}={kv.Value}")));
            sb.Append($"\nnet={LedgerNet(m)}");
            return sb.ToString();
        }

        // Static events outlive a Play session (no domain reload), so the handler is a named method that is
        // removed before being added — otherwise every session stacked another copy and logged each change twice.
        private static void OnRep(int score, int delta, string reason)
        {
            string flag = (_lastRepScore != int.MinValue && score != Mathf.Clamp(_lastRepScore + delta, 0, 1000))
                ? $"  !! expected {_lastRepScore}+{delta}" : "";
            _repLog.Add($"rep {score} ({(delta >= 0 ? "+" : "")}{delta}) {reason}{flag}");
            _lastRepScore = score;
        }

        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            ReputationService.OnReputationChanged -= OnRep;
            ReputationService.OnReputationChanged += OnRep;
        }

        private static int LedgerNet(MoneyService m) =>
            m.LifetimeIncome.Values.Sum() - m.LifetimeExpenses.Values.Sum();

        private static bool Carried(GameObject g) =>
            g.GetComponentInParent<MHEOperatorSlot>() != null || g.GetComponentInParent<TruckController>() != null ||
            g.GetComponentInParent<GameCore.Actors.OrderSelectionTaskDriver>() != null || g.GetComponentInParent<EmployeeIdentity>() != null;

        /// <summary>Deterministic, line-per-fact summary of the persistent game state. Take one before a save and one after
        /// the load and diff them: any line that differs is something save/load lost or changed (2026-10-22).</summary>
        public static string Snapshot()
        {
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || !ServiceLocator.TryGet<WorkQueueSystem>(out var wq) ||
                !ServiceLocator.TryGet<OrderService>(out var orders))
                return "services not ready";
            ServiceLocator.TryGet<MoneyService>(out var money);
            ServiceLocator.TryGet<ReputationService>(out var rep);
            var lines = new List<string>
            {
                $"money {money?.CurrentCapital}",
                $"rep {rep?.Score}",
                $"slotAssignments {SlotAssignmentService.AllAssignments.Count}",
            };
            foreach (var g in LocationStatusRegistry.Export().GroupBy(e => (LocationStatus)e.status))
                lines.Add($"locStatus {g.Key} {g.Count()}");
            foreach (var o in orders.ActiveOrders)
                lines.Add($"order {o.OrderNumber} {o.Status} picked={o.TotalUnitsPicked}/{o.TotalUnits} door={o.AssignedDoorNumber} lane={o.EffectiveStagingDoor}{o.AssignedLane} rel={o.ReleasedToFloor} ret={o.ReturnsMode}");
            foreach (var g in wq.Tasks.Where(t => t.Status != WorkTaskStatus.Complete && t.Status != WorkTaskStatus.Cancelled)
                                      .GroupBy(t => $"{t.Type} {t.Status}"))
                lines.Add($"tasks {g.Key} {g.Count()}");
            var recs = inv.AllPallets.Values.ToList();
            lines.Add($"records {recs.Count} cases={recs.Sum(r => r.Quantity)}");
            foreach (var g in recs.GroupBy(r => r.SkuId)) lines.Add($"stock {g.Key} pallets={g.Count()} cases={g.Sum(r => r.Quantity)}");
            int linked = recs.Count(r => PalletMasterLink.Find(r.PalletId) != null);
            lines.Add($"recordsWithObject {linked}");
            foreach (var g in Object.FindObjectsByType<OutboundPalletBuilder>()
                                    .Where(p => p != null && !string.IsNullOrEmpty(p.OrderId))
                                    .GroupBy(p => $"{p.OrderId.Substring(0, 6)} {p.Stage}"))
                lines.Add($"outbound {g.Key} pallets={g.Count()} cases={g.Sum(p => p.TotalCases)}");
            foreach (var t in Object.FindObjectsByType<TruckController>().OrderBy(t => t.name))
                lines.Add($"truck {(t.IsOutbound ? "OUT" : "IN")} {t.CurrentState} door={(t.DockedAt != null ? t.DockedAt.DoorNumber : 0)} pallets={t.GetComponentsInChildren<PalletData>(true).Length}");
            lines.Add($"mheOccupied {Object.FindObjectsByType<MHEOperatorSlot>().Count(s => s.IsOccupied)}");
            lines.Sort(System.StringComparer.Ordinal);
            return string.Join("\n", lines);
        }

        /// <summary>Rack slots whose LocationData names a pallet that is not physically standing there, and records with
        /// no pallet object anywhere. These are the "ghost pallet" cases a reach truck keeps failing to grab.</summary>
        public static string Ghosts()
        {
            if (!ServiceLocator.TryGet<InventoryService>(out var inv)) return "services not ready";
            var sb = new StringBuilder();
            foreach (var loc in Object.FindObjectsByType<LocationData>())
            {
                if (loc == null || string.IsNullOrEmpty(loc.PalletId)) continue;
                var link = PalletMasterLink.Find(loc.PalletId);
                if (link == null) { sb.Append($"SLOT {loc.Address} names {loc.PalletId.Substring(0, 8)} - no object\n"); continue; }
                Vector3 a = link.transform.position, b = loc.WorldPosition; a.y = b.y = 0f;
                float d = Vector3.Distance(a, b);
                if (d > 0.9f) sb.Append($"SLOT {loc.Address} names {loc.PalletId.Substring(0, 8)} - object is {d:F1} m away at {link.transform.position} (parent {link.transform.parent?.name})\n");
            }
            foreach (var r in inv.AllPallets.Values)
                if (PalletMasterLink.Find(r.PalletId) == null)
                    sb.Append($"RECORD {r.PalletId.Substring(0, 8)} {r.SkuId} x{r.Quantity} cell={r.CurrentLocation} lane={r.StagingLaneId} - no object\n");
            return sb.Length == 0 ? "no ghosts" : sb.ToString();
        }

        public static string Run()
        {
            Hook();
            var sb = new StringBuilder();
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || !ServiceLocator.TryGet<WorkQueueSystem>(out var wq) ||
                !ServiceLocator.TryGet<OrderService>(out var orders))
                return "services not ready";
            ServiceLocator.TryGet<MoneyService>(out var money);
            ServiceLocator.TryGet<ReputationService>(out var rep);
            ServiceLocator.TryGet<OrderArrivalService>(out var arrivals);
            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            float nowReal = Time.realtimeSinceStartup;
            int issues = 0;

            sb.Append($"t={Time.time:F0} ts={Time.timeScale} $={money?.CurrentCapital} rep={rep?.Score}\n");

            // ── money: capital must move only with the ledger ────────────────────────────────────
            if (money != null)
            {
                int offset = money.CurrentCapital - LedgerNet(money);
                if (_moneyBaseline == null) _moneyBaseline = offset;
                else if (offset != _moneyBaseline)
                {
                    sb.Append($"MONEY-DRIFT capital moved {offset - _moneyBaseline.Value:+#;-#;0} without a ledger entry\n");
                    issues++; _moneyBaseline = offset;
                }
            }
            if (rep != null && _lastRepScore == int.MinValue) _lastRepScore = rep.Score;
            foreach (var l in _repLog) { sb.Append(l).Append('\n'); if (l.Contains("!!")) issues++; }
            _repLog.Clear();

            // ── partnership ─────────────────────────────────────────────────────────────────────
            if (arrivals != null)
                foreach (var s in arrivals.Signed)
                {
                    if (s.SatisfactionPercent < 0f || s.SatisfactionPercent > 100f) { sb.Append($"PARTNERSHIP-RANGE {s.ContractId} {s.SatisfactionPercent:F1}\n"); issues++; }
                    if (_lastSatisfaction.TryGetValue(s.ContractId, out float prev) && Mathf.Abs(prev - s.SatisfactionPercent) > 0.01f)
                        sb.Append($"partnership {s.ContractId} {prev:F1} -> {s.SatisfactionPercent:F1} (delivered {s.OrdersDelivered}, late {s.OrdersLate})\n");
                    _lastSatisfaction[s.ContractId] = s.SatisfactionPercent;
                }

            // ── pallets ─────────────────────────────────────────────────────────────────────────
            var live = new HashSet<string>(wq.Tasks.Where(t => t.Status != WorkTaskStatus.Cancelled && t.Status != WorkTaskStatus.Complete && !string.IsNullOrEmpty(t.PalletId)).Select(t => t.PalletId));
            var seen = new HashSet<GameObject>();
            var pals = new List<GameObject>();
            foreach (var p in Object.FindObjectsByType<PalletMasterLink>()) if (seen.Add(p.gameObject)) pals.Add(p.gameObject);
            foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>()) if (seen.Add(p.gameObject)) pals.Add(p.gameObject);
            foreach (var p in Object.FindObjectsByType<PalletData>()) if (seen.Add(p.gameObject)) pals.Add(p.gameObject);
            pals.RemoveAll(g => g == null || Carried(g));
            var bb = new Bounds[pals.Count]; var hasB = new bool[pals.Count];
            for (int i = 0; i < pals.Count; i++)
            {
                var rs = pals[i].GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                bb[i] = b; hasB[i] = true;
            }
            var locs = Object.FindObjectsByType<LocationData>();
            float PD(Bounds a, Bounds c) => Vector2.Distance(new Vector2(a.center.x, a.center.z), new Vector2(c.center.x, c.center.z));

            for (int i = 0; i < pals.Count; i++)
            {
                var g = pals[i];
                var cell = grid.WorldToCell(g.transform.position);
                string addr = LaneNamingService.AddressAt(cell);
                string where = addr ?? cell.ToString();
                var links = g.GetComponents<PalletMasterLink>();
                var link = links.Length > 0 ? links[0] : null;
                var ob = g.GetComponent<OutboundPalletBuilder>();
                if (links.Length > 1) { sb.Append($"DUPLINK {g.name} @{where}\n"); issues++; }
                if (addr != null)
                {
                    if (ob != null)
                    {
                        if (!orders.ActiveOrders.Any(o => o.OrderId == ob.OrderId)) { sb.Append($"ORPHAN-OUTBOUND {addr} (order {ob.OrderId} gone)\n"); issues++; }
                    }
                    else
                    {
                        var rec = link != null ? inv.GetPallet(link.PalletId) : null;
                        if (rec == null) { sb.Append($"NO-RECORD lane pallet {addr} {g.name}\n"); issues++; }
                        else if (!live.Contains(rec.PalletId)) { sb.Append($"NO-TASK lane pallet {addr} sku={rec.SkuId} load={rec.LoadId}\n"); issues++; }
                    }
                }
                if (!hasB[i]) continue;
                float bottom = bb[i].min.y;
                bool supported = bottom < 1.25f;
                for (int j = 0; j < pals.Count && !supported; j++)
                    if (j != i && hasB[j] && PD(bb[i], bb[j]) < 0.6f && bb[j].max.y > bottom - 0.12f && bb[j].max.y < bottom + 0.06f) supported = true;
                if (!supported)
                    foreach (var l in locs)
                    {
                        var lp = l.transform.position;
                        if (Vector2.Distance(new Vector2(lp.x, lp.z), new Vector2(g.transform.position.x, g.transform.position.z)) < 0.9f && Mathf.Abs(lp.y - bottom) < 0.3f) { supported = true; break; }
                    }
                if (!supported) { sb.Append($"FLOATING {g.name} @{where} bottom={bottom:F2}\n"); issues++; }
                for (int j = i + 1; j < pals.Count; j++)
                {
                    if (!hasB[j]) continue;
                    float vo = Mathf.Min(bb[j].max.y, bb[i].max.y) - Mathf.Max(bb[j].min.y, bb[i].min.y);
                    if (PD(bb[i], bb[j]) < 0.45f && vo > 0.15f) { sb.Append($"OVERLAP {g.name} & {pals[j].name} @{where}\n"); issues++; }
                }
            }
            foreach (var r in inv.GetAllPallets())
                if (!string.IsNullOrEmpty(r.StagingLaneId) && PalletMasterLink.Find(r.PalletId) == null)
                { sb.Append($"GHOST-RECORD {r.PalletId.Substring(0, 8)} sku={r.SkuId} lane={r.StagingLaneId}\n"); issues++; }

            // ── tasks ───────────────────────────────────────────────────────────────────────────
            var byType = new SortedDictionary<string, int>();
            foreach (var t in wq.Tasks)
            {
                if (t.Status == WorkTaskStatus.Cancelled || t.Status == WorkTaskStatus.Complete) continue;
                string k = t.Type + ":" + t.Status;
                byType[k] = byType.TryGetValue(k, out int n) ? n + 1 : 1;
                if (t.Status == WorkTaskStatus.Assigned && nowReal - t.AssignedAtRealtime > 240f)
                { sb.Append($"STALE-ASSIGNED {t.Type} {t.Description} {nowReal - t.AssignedAtRealtime:F0}s\n"); issues++; }
            }
            sb.Append("tasks: ").Append(string.Join(" ", byType.Select(kv => $"{kv.Key}={kv.Value}"))).Append('\n');
            sb.Append("orders: ").Append(string.Join(" ", orders.ActiveOrders.GroupBy(o => o.Status).Select(gr => $"{gr.Key}={gr.Count()}"))).Append('\n');

            // ── trucks ──────────────────────────────────────────────────────────────────────────
            var trucks = Object.FindObjectsByType<TruckController>();
            foreach (var tr in trucks)
            {
                if (tr == null) continue;
                var p = tr.transform.position;
                float tilt = Vector3.Angle(Vector3.up, tr.transform.up);
                if (tilt > 4f) { sb.Append($"TRUCK-TILT {tr.name} {tilt:F1}deg state={tr.CurrentState}\n"); issues++; }
                if (p.y < -0.5f) { sb.Append($"TRUCK-SUNK {tr.name} y={p.y:F2}\n"); issues++; }
                bool parked = tr.CurrentState == TruckController.TruckState.Docked || tr.CurrentState == TruckController.TruckState.Idle ||
                              tr.CurrentState == TruckController.TruckState.Queuing || tr.CurrentState == TruckController.TruckState.GuardCheck || tr.IsInSideLot;
                if (_truckTrack.TryGetValue(tr, out var prev))
                {
                    if (Vector3.Distance(prev.pos, p) > 0.2f) _truckTrack[tr] = (p, Time.time);
                    else if (!parked && Time.time - prev.movedAt > 90f)
                    { sb.Append($"TRUCK-STALLED {tr.name} state={tr.CurrentState} {Time.time - prev.movedAt:F0}s at {p:F1}\n"); issues++; }
                }
                else _truckTrack[tr] = (p, Time.time);
                sb.Append($"truck {tr.name} {tr.CurrentState}{(tr.IsOutbound ? " OUT" : " IN")} dock={(tr.AssignedDock != null ? tr.AssignedDock.name : "-")} pallets={tr.TotalPalletsAtDock}\n");
            }
            for (int i = 0; i < trucks.Length; i++)
                for (int j = i + 1; j < trucks.Length; j++)
                {
                    if (trucks[i] == null || trucks[j] == null) continue;
                    Vector3 a = trucks[i].transform.position, c = trucks[j].transform.position;
                    if (Vector2.Distance(new Vector2(a.x, a.z), new Vector2(c.x, c.z)) < 2.5f)
                    { sb.Append($"TRUCK-OVERLAP {trucks[i].name} & {trucks[j].name} at {a:F1}\n"); issues++; }
                }

            sb.Append("ISSUES=").Append(issues);
            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // Soak driver: keep the loop busy
        // ══════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Total cash injected by the soak driver this session (should stay 0 if the loop is profitable).</summary>
        public static int CheatTotal;

        public static string SoakTick(int targetOpenOrders = 3, int minPalletsPerSku = 3, int maxPendingPOs = 2)
        {
            var sb = new StringBuilder();
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || !ServiceLocator.TryGet<OrderService>(out var orders) ||
                !ServiceLocator.TryGet<ShipmentService>(out var ship) || !ServiceLocator.TryGet<MoneyService>(out var money))
                return "services not ready";

            // Balance check first: the business pays its own way. Cash is injected only to stop a bankruptcy, and
            // every injection is reported so the economy result stays honest.
            if (money.CurrentCapital < 5000) { money.AddCapital(50000, "QA Cheat"); CheatTotal += 50000; sb.Append("CHEAT +$50k (near bankrupt); "); }

            // Restock: SKUs that have a pick slot, low on pallets, no PO already coming.
            int pending = ship.PendingShipments.Count(s => s.Status == ShipmentData.ShipmentStatus.InTransit);
            var coming = new HashSet<string>(ship.PendingShipments.Where(s => s.Status == ShipmentData.ShipmentStatus.InTransit)
                                                                  .SelectMany(s => s.LineItems).Select(li => li.SkuId));
            var skus = SlotAssignmentService.AllAssignments.Values.Distinct().ToList();
            var low = skus.Where(id => !coming.Contains(id) && inv.GetAllPallets().Count(r => r.SkuId == id) < minPalletsPerSku).ToList();
            if (low.Count > 0 && pending < maxPendingPOs)
            {
                var items = new List<ShipmentLineItem>();
                foreach (var id in low.Take(4))
                {
                    var sku = inv.GetSkuData(id);
                    if (sku == null) continue;
                    int cases = Mathf.Max(1, sku.Ti * sku.Hi);
                    for (int k = 0; k < 3 && items.Count < 12; k++)
                        items.Add(new ShipmentLineItem(id, cases, Mathf.Max(1, Mathf.RoundToInt(sku.BuyValue)), sku.ShelfLifeDays));
                }
                if (items.Count > 0)
                {
                    // The PLAYER path: goods are paid for when ordered, like a real PO from the Purchasing panel.
                    int day = ServiceLocator.TryGet<SimulationTimeService>(out var clock) ? clock.Day : 1;
                    var po = ship.CreatePlayerPurchaseOrder(Random.Range(100000, 999999).ToString(), "QA", "QA Supply", items, day);
                    sb.Append($"PO {items.Count} pallets ${po?.TotalCost} ({string.Join(",", low.Take(4))}); ");
                }
            }

            // Orders: keep a few open.
            int open = orders.ActiveOrders.Count(o => o.Status != OrderData.OrderStatus.Shipped && o.Status != OrderData.OrderStatus.Cancelled);
            var tools = Object.FindAnyObjectByType<ToolsWindowController>(FindObjectsInactive.Include);
            if (open < targetOpenOrders && tools != null)
            {
                string made = tools.CreateSizedTestOrder(Random.value < 0.5f ? 1 : 0, 1);
                sb.Append($"order: {made}; ");
            }

            // Release anything waiting.
            foreach (var o in orders.ActiveOrders.Where(orders.IsAwaitingRelease).ToList())
            {
                bool ok = orders.ReleaseOrdersToStages(new List<string> { o.OrderId }, out string why, out bool waiting, out string note);
                sb.Append($"release {o.OrderNumber ?? o.OrderId.Substring(0, 6)} {(ok ? "ok" : "FAILED " + why)}{(waiting ? " (waiting for lane)" : "")}; ");
            }
            return sb.Length == 0 ? "soak: nothing to do" : "soak: " + sb;
        }
    }
}
#endif
