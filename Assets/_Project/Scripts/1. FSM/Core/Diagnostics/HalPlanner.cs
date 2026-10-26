using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;

namespace GameCore.Diagnostics
{
    /// <summary>One change HAL made between blocks and what it hoped to fix. `scoreAfter` is filled in when the next block ends,
    /// which is what makes this file the learning record: change, reason, and whether the score moved.</summary>
    [Serializable]
    public class HalChange
    {
        public int block;                 // the block this change was made BEFORE (it is judged by that block's result)
        public string action, reason;
        public float scoreBefore, scoreAfter;
        public int cashSpent;
        public bool measured;
    }

    [Serializable] public class HalLedger { public List<HalChange> changes = new(); }

    /// <summary>Per-block samples of how busy each job is and how much work is waiting. Sampled once per game hour by the bot's health check.</summary>
    public static class HalMetrics
    {
        public static int Samples;
        public static float BacklogReceive, BacklogPutaway, BacklogPick, BacklogLoad, BacklogOrders, WaitingTrucks;
        public static float UtilReceiver, UtilReach, UtilDock, UtilSelector;
        public static int HeadReceiver, HeadReach, HeadDock, HeadSelector;

        public static void Reset()
        {
            Samples = 0;
            BacklogReceive = BacklogPutaway = BacklogPick = BacklogLoad = BacklogOrders = WaitingTrucks = 0f;
            UtilReceiver = UtilReach = UtilDock = UtilSelector = 0f;
        }

        static bool InRole(WorkTaskType t, EmployeeRole role) => role switch
        {
            EmployeeRole.Receiver => t == WorkTaskType.Receive,
            EmployeeRole.ReachTruckOperator => t == WorkTaskType.Putaway || t == WorkTaskType.Replenish || t == WorkTaskType.PalletPick,
            EmployeeRole.DockStockerOperator => t == WorkTaskType.Load,
            EmployeeRole.OrderSelector => t == WorkTaskType.OrderSelect || t == WorkTaskType.Return,
            _ => false
        };

        /// <summary>An employee standing in the warehouse. The portrait booth keeps a LIVE COPY of every employee far off the map, each with
        /// a real Record; counting or firing those doubled the headcount and let the planner remove the real person (block 3, 2026-10-24).</summary>
        public static bool IsRealEmployee(EmployeeIdentity e)
            => e != null && e.Record != null && e.gameObject.activeInHierarchy && e.transform.position.y > -50f && e.transform.position.sqrMagnitude < 400000f
               && !e.name.StartsWith("LiveFeed") && e.GetComponentInParent<EmployeePhotoBooth>() == null;

        public static int Headcount(EmployeeRole role)
            => UnityEngine.Object.FindObjectsByType<EmployeeIdentity>()
                .Count(e => IsRealEmployee(e) && e.Record.role == role && e.Record.status == EmploymentStatus.Active);

        public static void Sample()
        {
            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var wq)) return;
            Samples++;
            int Avail(Func<WorkTaskType, bool> f) => wq.Tasks.Count(t => t.Status == WorkTaskStatus.Available && f(t.Type));
            BacklogReceive += Avail(t => t == WorkTaskType.Receive);
            BacklogPutaway += Avail(t => t == WorkTaskType.Putaway || t == WorkTaskType.Replenish);
            BacklogPick += Avail(t => t == WorkTaskType.PalletPick);
            BacklogLoad += Avail(t => t == WorkTaskType.Load);
            BacklogOrders += Avail(t => t == WorkTaskType.OrderSelect);

            int waiting = 0;
            foreach (var tr in UnityEngine.Object.FindObjectsByType<TruckController>())
                if (tr != null && (tr.CurrentState == TruckController.TruckState.Queuing || tr.CurrentState == TruckController.TruckState.GuardCheck || tr.IsInSideLot)) waiting++;
            WaitingTrucks += waiting;

            foreach (var role in new[] { EmployeeRole.Receiver, EmployeeRole.ReachTruckOperator, EmployeeRole.DockStockerOperator, EmployeeRole.OrderSelector })
            {
                int head = Headcount(role);
                int busy = wq.Tasks.Where(t => t.Status == WorkTaskStatus.Assigned && InRole(t.Type, role)).Select(t => t.AssignedToEmployeeGuid).Distinct().Count();
                // The dock stocker's main work (offloading inbound trailers) is not a queue task, so count a machine that is
                // being driven by the offload/load controllers as busy too. Without this they read 2-4% busy and the planner let one go.
                if (role == EmployeeRole.DockStockerOperator)
                    foreach (var slot in UnityEngine.Object.FindObjectsByType<MHEOperatorSlot>())
                        if (slot != null && slot.IsOccupied && DockEquipmentCommandeerRegistry.IsCommandeered(slot)) busy++;
                busy = Mathf.Min(busy, Mathf.Max(1, head));
                float u = head > 0 ? Mathf.Clamp01(busy / (float)head) : 0f;
                switch (role)
                {
                    case EmployeeRole.Receiver: UtilReceiver += u; HeadReceiver = head; break;
                    case EmployeeRole.ReachTruckOperator: UtilReach += u; HeadReach = head; break;
                    case EmployeeRole.DockStockerOperator: UtilDock += u; HeadDock = head; break;
                    default: UtilSelector += u; HeadSelector = head; break;
                }
            }
        }

        public static float Avg(float sum) => Samples > 0 ? sum / Samples : 0f;
    }

    /// <summary>
    /// The between-blocks manager. After each block it reads how busy each job was and how much work waited, picks the single
    /// biggest problem and makes ONE change: hire, fire, add or sell a machine, build or remove an expansion (a second dock door...).
    /// Every "up" has a "down": a role that stays mostly idle for two blocks in a row is trimmed. All changes are written to the ledger.
    /// Thresholds come from the Balance tab.
    /// </summary>
    public static class HalPlanner
    {
        static string LedgerPath(PlaytestBotSettings s) => Path.Combine(s.ResolveReportDir(), "changes.json");

        public static HalLedger LoadLedger(PlaytestBotSettings s)
        {
            try { var f = LedgerPath(s); if (File.Exists(f)) return JsonUtility.FromJson<HalLedger>(File.ReadAllText(f)) ?? new HalLedger(); }
            catch (Exception e) { Debug.LogWarning("[HalPlanner] ledger read failed: " + e.Message); }
            return new HalLedger();
        }

        static void SaveLedger(PlaytestBotSettings s, HalLedger l)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(LedgerPath(s))); File.WriteAllText(LedgerPath(s), JsonUtility.ToJson(l, true)); }
            catch (Exception e) { Debug.LogWarning("[HalPlanner] ledger write failed: " + e.Message); }
        }

        /// <summary>Called when a block finishes: judge the change made before it.</summary>
        public static void RecordOutcome(PlaytestBotSettings s, PlaytestBlockResult finished)
        {
            var l = LoadLedger(s);
            var open = l.changes.LastOrDefault(c => !c.measured && c.block == finished.block);
            if (open != null) { open.scoreAfter = finished.score; open.measured = true; SaveLedger(s, l); }
        }

        /// <summary>Called when a block starts (and a previous one exists): decide, act, log.</summary>
        public static void PlanAndApply(PlaytestBotSettings s, List<PlaytestBlockResult> history, int nextBlock)
        {
            if (!s.plannerEnabled || history.Count == 0) return;
            if (!ServiceLocator.TryGet<MoneyService>(out var money)) return;
            var last = history[history.Count - 1];
            var prev = history.Count > 1 ? history[history.Count - 2] : null;

            // Candidate problems, ranked by how far past its threshold each is.
            var ups = new List<(float sev, string what, string why, Func<int> act)>();
            void Up(EmployeeRole role, string label, float util, float backlog, int head)
            {
                if (head <= 0) return;
                if (util >= s.addUtilPercent / 100f && backlog >= s.addBacklog)
                    ups.Add((util + backlog / 10f, $"hire a {label}", $"{label}s were {util:P0} busy with {backlog:F1} jobs waiting on average", () => HalActions.Hire(role)));
            }
            Up(EmployeeRole.Receiver, "receiver", last.utilReceiver, last.backlogReceive, last.headReceiver);
            Up(EmployeeRole.ReachTruckOperator, "reach truck operator", last.utilReach, last.backlogPutaway + last.backlogPick, last.headReach);
            Up(EmployeeRole.DockStockerOperator, "dock stocker operator", last.utilDock, last.backlogLoad, last.headDock);
            Up(EmployeeRole.OrderSelector, "order selector", last.utilSelector, last.backlogOrders, last.headSelector);
            if (last.waitingTrucks >= s.addWaitingTrucks)
                ups.Add((last.waitingTrucks, "build a second dock door", $"{last.waitingTrucks:F1} trucks were waiting for a door on average", () => HalActions.AddPack("Second dock door")));

            var downs = new List<(float sev, string what, string why, Func<int> act)>();
            void Down(EmployeeRole role, string label, float util, float prevUtil, int head)
            {
                if (head <= 1 || prev == null) return;
                if (last.ordersShipped < 5 || prev.ordersShipped < 5) return;   // never trim the crew on the strength of a block that barely shipped anything
                float th = s.removeUtilPercent / 100f;
                if (util <= th && prevUtil <= th)
                    downs.Add((th - util, $"let one {label} go", $"{label}s were only {util:P0} busy, and {prevUtil:P0} the block before", () => HalActions.Fire(role)));
            }
            Down(EmployeeRole.Receiver, "receiver", last.utilReceiver, prev?.utilReceiver ?? 1f, last.headReceiver);
            Down(EmployeeRole.ReachTruckOperator, "reach truck operator", last.utilReach, prev?.utilReach ?? 1f, last.headReach);
            Down(EmployeeRole.DockStockerOperator, "dock stocker operator", last.utilDock, prev?.utilDock ?? 1f, last.headDock);
            Down(EmployeeRole.OrderSelector, "order selector", last.utilSelector, prev?.utilSelector ?? 1f, last.headSelector);
            if (last.doors > 1 && prev != null && last.waitingTrucks < 0.1f && prev.waitingTrucks < 0.1f && last.utilDock <= s.removeUtilPercent / 100f)
                downs.Add((0.5f, "remove the second dock door", "no trucks waited for two blocks and the dock crew was mostly idle", () => HalActions.RemovePack("Second dock door")));

            (float sev, string what, string why, Func<int> act)? pick = null;
            if (ups.Count > 0) pick = ups.OrderByDescending(u => u.sev).First();
            else if (s.plannerMayRemove && downs.Count > 0) pick = downs.OrderByDescending(d => d.sev).First();
            if (pick == null)
            {
                SystemsLogWindow.LogHal("HAL 9000: no changes this block. Nothing is over or under its limits.");
                return;
            }

            int before = money.CurrentCapital;
            int did = pick.Value.act();
            int spent = before - money.CurrentCapital;
            if (did <= 0)
            {
                SystemsLogWindow.LogHal($"HAL 9000: wanted to {pick.Value.what} ({pick.Value.why}) but could not.");
                return;
            }
            var l = LoadLedger(s);
            l.changes.Add(new HalChange { block = nextBlock, action = pick.Value.what, reason = pick.Value.why, scoreBefore = last.score, cashSpent = spent });
            SaveLedger(s, l);
            SystemsLogWindow.LogHal($"HAL 9000: decided to {pick.Value.what}. {Cap(pick.Value.why)}. Cash change {-spent:+#,0;-#,0;0}.");
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    }

    /// <summary>The things the planner can actually do, through the same commands the player's clicks use.</summary>
    public static class HalActions
    {
        static PlacementGrid _grid; static PlacementValidator _validator; static PlacementFinalizer _finalizer; static PlacementStateMachine _fsm;
        static ObjDataRegistry _registry;

        static bool Ready()
        {
            _grid = UnityEngine.Object.FindAnyObjectByType<PlacementGrid>();
            _validator = UnityEngine.Object.FindAnyObjectByType<PlacementValidator>();
            _finalizer = UnityEngine.Object.FindAnyObjectByType<PlacementFinalizer>();
            _fsm = UnityEngine.Object.FindAnyObjectByType<PlacementStateMachine>();
            var menu = UnityEngine.Object.FindAnyObjectByType<BuildMenuUI>();
            _registry = menu != null ? menu.registry : Resources.FindObjectsOfTypeAll<ObjDataRegistry>().FirstOrDefault();
            return _grid && _validator && _finalizer && _fsm && _registry != null && ServiceLocator.TryGet<MoneyService>(out _);
        }

        static bool Place(int id, int x, int y, int rot)
        {
            ServiceLocator.TryGet<MoneyService>(out var money);
            var data = _registry.GetByID(id);
            if (data == null || money == null || !money.CanAfford(data.cost)) return false;
            float deg = rot * 90f;
            var offsets = data.GetFootprintOffsets(-deg);
            var root = new Vector2Int(x, y);
            if (!_validator.IsValidPlacement(root, offsets, data)) return false;
            _fsm.History.Push(new PlaceCommand(_grid, _finalizer, root, offsets, data, deg, money));
            return true;
        }

        static bool PlaceNear(int id, int cx, int cy, int rot)
        {
            for (int r = 0; r <= 8; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == r && Place(id, cx + dx, cy + dy, rot)) return true;
            return false;
        }

        static bool Delete(PlacedObject po)
        {
            if (po == null) return false;
            ServiceLocator.TryGet<MoneyService>(out var money);
            var bd = po.GetComponent<BuildingData>();
            if (bd == null) return false;
            _fsm.History.Push(new DeleteCommand(bd.gameObject, _grid, _finalizer, money, 0.3f, 0f, 0f, 0f));
            return true;
        }

        static HalLayout Layout() => HalLayout.Load(PlaytestBot.Ensure().S.layoutName);

        // ── people ──
        public static int Hire(EmployeeRole role)
        {
            if (!Ready()) return 0;
            // A driver needs a machine of their kind that is free.
            int machineId = role == EmployeeRole.ReachTruckOperator ? 61 : role == EmployeeRole.DockStockerOperator ? 28 : 0;
            if (machineId != 0 && !FreeMachines(machineId).Any())
            {
                var e = Layout()?.equipment.FirstOrDefault(q => q.id == machineId);
                if (e == null || !PlaceNear(machineId, e.x, e.y, e.rot)) return 0;
            }
            var hs = HiringService.Instance;
            if (hs == null) return 0;
            if (hs.Roster.Count == 0) hs.RefreshRoster();
            var cand = hs.Roster.Where(c => c.role == role).OrderBy(c => c.minWage).FirstOrDefault();
            if (cand == null) { hs.RefreshRoster(); cand = hs.Roster.Where(c => c.role == role).OrderBy(c => c.minWage).FirstOrDefault(); }
            if (cand == null) return 0;
            return hs.Hire(cand) != null ? 1 : 0;
        }

        public static int Fire(EmployeeRole role)
        {
            if (!Ready()) return 0;
            var who = UnityEngine.Object.FindObjectsByType<EmployeeIdentity>()
                .Where(e => HalMetrics.IsRealEmployee(e) && e.Record.role == role && e.Record.status == EmploymentStatus.Active)
                .OrderBy(e => e.Record.skill).FirstOrDefault();   // the least skilled goes
            if (who == null || HalMetrics.Headcount(role) <= 1) return 0;   // never remove the last one
            EmployeeTerminationService.Terminate(who);
            // Their machine is now spare: sell it if that kind has another.
            int machineId = role == EmployeeRole.ReachTruckOperator ? 61 : role == EmployeeRole.DockStockerOperator ? 28 : 0;
            if (machineId != 0) SellSpareMachine(machineId);
            return 1;
        }

        static IEnumerable<MHEOperatorSlot> FreeMachines(int id)
            => UnityEngine.Object.FindObjectsByType<MHEOperatorSlot>()
                .Where(s => s != null && !s.IsOccupied && s.GetComponent<PlacedObject>()?.data != null && s.GetComponent<PlacedObject>().data.id == id);

        static void SellSpareMachine(int id)
        {
            var all = UnityEngine.Object.FindObjectsByType<MHEOperatorSlot>()
                .Where(s => s != null && s.GetComponent<PlacedObject>()?.data != null && s.GetComponent<PlacedObject>().data.id == id).ToList();
            if (all.Count <= 1) return;
            var spare = all.FirstOrDefault(s => !s.IsOccupied) ?? all.Last();
            Delete(spare.GetComponent<PlacedObject>());
        }

        // ── buildings ──
        public static int AddPack(string name)
        {
            if (!Ready()) return 0;
            var pack = Layout()?.packs?.FirstOrDefault(p => p.name == name);
            if (pack == null) return 0;
            int placed = 0;
            foreach (var st in pack.steps.OrderBy(s => s.phase)) if (Place(st.id, st.x, st.y, st.rot)) placed++;
            return placed;
        }

        public static int RemovePack(string name)
        {
            if (!Ready()) return 0;
            var pack = Layout()?.packs?.FirstOrDefault(p => p.name == name);
            if (pack == null) return 0;
            int removed = 0;
            // Reverse build order: lane, door and walls first, the slab last.
            foreach (var st in pack.steps.OrderByDescending(s => s.phase))
            {
                var po = PlacedObjectRegistry.All.FirstOrDefault(p => p != null && p.data != null && p.data.id == st.id && p.gridX == st.x && p.gridY == st.y);
                if (po != null && Delete(po)) removed++;
            }
            return removed;
        }
    }
}
