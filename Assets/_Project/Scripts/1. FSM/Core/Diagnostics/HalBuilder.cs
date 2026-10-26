using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Diagnostics
{
    /// <summary>
    /// HAL 9000 starting a company from nothing. Launched by "INITIATE HAL 9000" on the main menu (PlayerPrefs HalAutoStart):
    /// the game loads as a brand-new game, then this builds the warehouse through the same PlaceCommand the player's clicks
    /// use (so the validator, costs and undo history are all real), stocks the slots, hires a crew and hands over to the bot.
    ///
    /// The layout is a cut-down copy of a reference save (HalBlueprint). Cash is the limit, not a script: each placement
    /// checks the balance first and the rack tail waits behind the vehicles. Every step is announced in the Systems Log.
    /// </summary>
    public class HalBuilder : MonoBehaviour
    {
        public static HalBuilder Instance { get; private set; }
        public string Status = "";

        PlacementGrid _grid; PlacementValidator _validator; PlacementFinalizer _finalizer; PlacementStateMachine _fsm;
        MoneyService _money; ObjDataRegistry _registry;
        int _failed, _placed;
        HalLayout _layout;

        // ── launch hook: the game scene loads after the menu sets the flag ──
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Pressing Play inside the editor on the game scene never raises sceneLoaded for that first scene, so check once here too.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterFirstScene() => OnSceneLoaded(default, LoadSceneMode.Single);

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance != null) return;
            if (PlayerPrefs.GetInt("HalAutoStart", 0) != 1) return;
            if (Object.FindAnyObjectByType<GameContext>() == null) return;   // still in the menu
            PlayerPrefs.SetInt("HalAutoStart", 0); PlayerPrefs.Save();
            var go = new GameObject("HalBuilder");
            Instance = go.AddComponent<HalBuilder>();
        }

        IEnumerator Start()
        {
            // Wait for the new game to finish booting (loading screen gone, services registered).
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 120f)
            {
                if (ServiceLocator.TryGet<MoneyService>(out _money) && ServiceLocator.TryGet<InventoryService>(out _) &&
                    ServiceLocator.TryGet<ShipmentService>(out _) && Object.FindAnyObjectByType<PlacementGrid>() != null &&
                    Object.FindAnyObjectByType<PlacementStateMachine>() != null && Object.FindAnyObjectByType<PlacementStateMachine>().History != null &&
                    Time.realtimeSinceStartup - t0 > 8f)
                    break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(2f);

            _grid = FindAnyObjectByType<PlacementGrid>();
            _validator = FindAnyObjectByType<PlacementValidator>();
            _finalizer = FindAnyObjectByType<PlacementFinalizer>();
            _fsm = FindAnyObjectByType<PlacementStateMachine>();
            var buildMenu = FindAnyObjectByType<BuildMenuUI>();
            _registry = buildMenu != null ? buildMenu.registry : Resources.FindObjectsOfTypeAll<ObjDataRegistry>().FirstOrDefault();
            if (_grid == null || _validator == null || _finalizer == null || _fsm == null || _registry == null || _money == null)
            { SystemsLogWindow.LogHal("HAL 9000: cannot start - the placement systems are not available."); yield break; }

            HideWelcome();
            Time.timeScale = 1f;
            var s = PlaytestBot.Ensure().S;
            SystemsLogWindow.LogHal($"HAL 9000 initiated. New company, starting capital ${_money.CurrentCapital:N0}. Building the warehouse.");

            _layout = HalLayout.Load(s.layoutName);
            if (_layout == null) { SystemsLogWindow.LogHal("HAL 9000: no layout files found in Resources/HalLayouts - cannot build."); yield break; }
            SystemsLogWindow.LogHal($"HAL 9000: layout \"{_layout.name}\" selected.");
            int cashAtStart = _money.CurrentCapital;
            var steps = _layout.steps.Select(x => (x.phase, x.id, x.x, x.y, x.rot, x.data)).ToList();

            // Phase 0-2: shell, walls and doors, shipping lane
            for (int phase = 0; phase <= 2; phase++)
            {
                yield return PlacePhase(steps.Where(x => x.phase == phase).ToList(), PhaseName(phase), 0);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            // 3: guard shack (trucks cannot arrive without one)
            yield return PlacePhase(steps.Where(x => x.phase == 3).ToList(), "guard shack", 0);

            // 4a: ground-level racks, then equipment, then the upper levels with whatever cash is left
            var racks = steps.Where(x => x.phase == 4).ToList();
            var ground = racks.Where(r => RackLevel(r.data) == 0).ToList();
            var upper = racks.Where(r => RackLevel(r.data) > 0).ToList();
            yield return PlaceRacks(ground, "ground-level racks", 0);
            yield return PlaceEquipment(s);
            yield return PlaceRacks(upper, "upper rack levels", s.buildReserveCash);

            var others = steps.Where(x => x.phase == 5).ToList();
            if (others.Count > 0) yield return PlacePhase(others, "other items", s.buildReserveCash);

            FinishRacking();
            yield return new WaitForSecondsRealtime(1f);
            AssignPickSlots();

            yield return Hire(s);

            int spent = cashAtStart - _money.CurrentCapital;
            SystemsLogWindow.LogHal($"HAL 9000: build complete. {_placed} objects placed ({_failed} refused). Spent ${spent:N0}, ${_money.CurrentCapital:N0} left. Handing over to the operations bot.");
            Status = "built";
            if (s.buildOnly)
            {
                SystemsLogWindow.LogHal("HAL 9000: build-only mode. Not recording and not running the bot. Edit the warehouse, then save it from the HAL 9000 tab.");
                Destroy(gameObject);
                yield break;
            }
            Archive(cashAtStart, spent);

            var bot = PlaytestBot.Ensure();
            bot.StartBlock();
            Destroy(gameObject);
        }

        static string PhaseName(int p) => p == 0 ? "foundation" : p == 1 ? "walls and doors" : "shipping lane";

        static int RackLevel(string data)
        {
            var p = (data ?? "").Split('|');
            return p.Length > 3 && int.TryParse(p[3], out int l) ? l : 0;
        }

        void HideWelcome()
        {
            var topBar = FindAnyObjectByType<TopBarUI>(FindObjectsInactive.Include);
            var backdrop = topBar != null ? topBar.GetComponent<UIDocument>()?.rootVisualElement?.Q("welcome-backdrop") : null;
            if (backdrop != null) backdrop.style.display = DisplayStyle.None;
            FindAnyObjectByType<WelcomeOverlayManager>(FindObjectsInactive.Include)?.Dismiss();
        }

        // ── placement ──

        bool TryPlace(int id, int x, int y, int rot, string note = null)
        {
            var data = _registry.GetByID(id);
            if (data == null) { _failed++; return false; }
            float deg = rot * 90f;
            var offsets = data.GetFootprintOffsets(-deg);
            var root = new Vector2Int(x, y);
            if (!_money.CanAfford(data.cost)) { _failed++; return false; }
            if (!_validator.IsValidPlacement(root, offsets, data)) { _failed++; return false; }
            _fsm.History.Push(new PlaceCommand(_grid, _finalizer, root, offsets, data, deg, _money));
            _placed++;
            return true;
        }

        IEnumerator PlacePhase(List<(int phase, int id, int x, int y, int rot, string data)> list, string label, int reserve)
        {
            int ok = 0, bad = 0;
            foreach (var st in list)
            {
                if (_money.CurrentCapital - (_registry.GetByID(st.id)?.cost ?? 0) < reserve) { bad++; continue; }
                if (TryPlace(st.id, st.x, st.y, st.rot)) ok++; else bad++;
                if ((ok + bad) % 8 == 0) yield return null;
            }
            SystemsLogWindow.LogHal($"HAL 9000: {label}: {ok} placed{(bad > 0 ? $", {bad} refused" : "")}. Cash ${_money.CurrentCapital:N0}.");
            yield return null;
        }

        IEnumerator PlaceRacks(List<(int phase, int id, int x, int y, int rot, string data)> list, string label, int reserve)
        {
            var det = FindAnyObjectByType<RackCollectionDetector>();
            if (det != null) det.enabled = false;   // we apply the aisle data ourselves; no ghosts, chevrons or setup modal
            yield return PlacePhase(list, label, reserve);
            if (det != null) det.enabled = true;
        }

        // Equipment comes from the layout. If the exact cell is taken, search outward.
        IEnumerator PlaceEquipment(PlaytestBotSettings s)
        {
            int ok = 0;
            foreach (var e in _layout.equipment)
            {
                int count = e.count;
                if (!string.IsNullOrEmpty(e.countSetting))
                {
                    var f = typeof(PlaytestBotSettings).GetField(e.countSetting);
                    if (f != null && f.FieldType == typeof(int)) count = (int)f.GetValue(s);
                }
                ok += PlaceNear(e.id, e.x, e.y, e.rot, count);
            }
            SystemsLogWindow.LogHal($"HAL 9000: equipment: {ok} placed. Cash ${_money.CurrentCapital:N0}.");
            yield return null;
        }

        int PlaceNear(int id, int cx, int cy, int rot, int count)
        {
            int made = 0;
            for (int n = 0; n < count; n++)
            {
                bool done = false;
                for (int r = 0; r <= 6 && !done; r++)
                    for (int dx = -r; dx <= r && !done; dx++)
                        for (int dy = -r; dy <= r && !done; dy++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                            if (TryPlace(id, cx + dx, cy + dy, rot)) { made++; done = true; }
                        }
            }
            return made;
        }

        // ── aisle data for the racks we placed ──

        void FinishRacking()
        {
            var racks = PlacedObjectRegistry.All.Where(p => p != null && p.data != null && p.data.category == "Racking").ToList();
            var steps = _layout.steps.Where(x => x.phase == 4).ToList();
            int applied = 0;
            foreach (var cell in racks.GroupBy(p => new Vector2Int(p.gridX, p.gridY)))
            {
                var spots = steps.Where(x => x.x == cell.Key.x && x.y == cell.Key.y).OrderBy(x => RackLevel(x.data)).ToList();
                var placed = cell.OrderBy(p => p.transform.position.y).ToList();
                for (int i = 0; i < placed.Count && i < spots.Count; i++)
                {
                    RackSaveCodec.RestoreOnto(placed[i], spots[i].data);
                    placed[i].customData = spots[i].data;
                    applied++;
                }
            }
            var init = FindAnyObjectByType<AisleInitializer>();
            if (init != null) init.RefreshAllRackLabelsAfterLoad();
            SystemsLogWindow.LogHal($"HAL 9000: aisle 1 commissioned ({applied} rack positions labelled).");
        }

        void AssignPickSlots()
        {
            if (!ServiceLocator.TryGet<InventoryService>(out var inv)) return;
            var skus = ItemAvailability.Build().Where(r => r.VendorId != null && r.CasesPerPallet > 0).Select(r => r.SkuId).Distinct().ToList();
            var picks = SlotRegistry.PickSlots.Select(p => p.Address).OrderBy(a => a).ToList();
            if (skus.Count == 0 || picks.Count == 0) { SystemsLogWindow.LogHal("HAL 9000: no pick slots or SKUs available to assign yet."); return; }
            for (int i = 0; i < picks.Count; i++) SlotAssignmentService.Assign(picks[i], skus[i % skus.Count]);
            SystemsLogWindow.LogHal($"HAL 9000: {picks.Count} pick slots assigned across {Mathf.Min(skus.Count, picks.Count)} items.");
        }

        // ── crew ──

        IEnumerator Hire(PlaytestBotSettings s)
        {
            var hs = HiringService.Instance;
            if (hs == null) { SystemsLogWindow.LogHal("HAL 9000: no hiring service - crew not hired."); yield break; }
            if (hs.Roster.Count == 0) hs.RefreshRoster();
            yield return null;
            var want = new (EmployeeRole role, int n)[]
            {
                (EmployeeRole.DockStockerOperator, s.startDockStockers), (EmployeeRole.ReachTruckOperator, s.startReachTruckOps),
                (EmployeeRole.Receiver, s.startReceivers), (EmployeeRole.OrderSelector, s.startSelectors),
            };
            foreach (var (role, n) in want)
                for (int i = 0; i < n; i++)
                {
                    var cand = hs.Roster.Where(c => c.role == role).OrderBy(c => c.minWage).FirstOrDefault();
                    if (cand == null) { hs.RefreshRoster(); yield return null; cand = hs.Roster.Where(c => c.role == role).OrderBy(c => c.minWage).FirstOrDefault(); }
                    if (cand == null) { SystemsLogWindow.LogHal($"HAL 9000: no {role} candidates on the board."); break; }
                    var rec = hs.Hire(cand);
                    SystemsLogWindow.LogHal($"HAL 9000: hired {rec?.employeeName ?? "?"} as {role}.");
                    yield return new WaitForSecondsRealtime(0.4f);
                }

            // Safety net: make sure every machine operator is actually on their way to a free machine of their type.
            yield return new WaitForSecondsRealtime(3f);
            foreach (var id in FindObjectsByType<EmployeeIdentity>())
            {
                if (id == null || id.Record == null) continue;
                var role = id.Record.role;
                if (role != EmployeeRole.DockStockerOperator && role != EmployeeRole.ReachTruckOperator) continue;
                var nav = id.GetComponent<AiNavigation>();
                if (nav == null || nav.IsSeekingEquipment || id.GetComponentInParent<MHEOperatorSlot>() != null) continue;
                string kind = role == EmployeeRole.DockStockerOperator ? "Dockstocker" : "ReachTruck";
                var slot = FindObjectsByType<MHEOperatorSlot>().FirstOrDefault(m => m.name.StartsWith(kind) && !m.IsOccupied && m.AcceptsOperator);
                if (slot != null) { nav.SeekEquipment(slot); SystemsLogWindow.LogHal($"HAL 9000: sending {id.Record.employeeName} to the {kind}."); }
            }
        }

        void Archive(int cashStart, int spent)
        {
            try
            {
                string dir = PlaytestBot.Ensure().S.ResolveReportDir();
                System.IO.Directory.CreateDirectory(dir);
                string line = $"{System.DateTime.Now:yyyy-MM-dd HH:mm},{cashStart},{spent},{_placed},{_failed},{PlacedObjectRegistry.All.Count()}\n";
                string f = System.IO.Path.Combine(dir, "builds.csv");
                if (!System.IO.File.Exists(f)) System.IO.File.WriteAllText(f, "when,startCash,buildSpend,placed,refused,objectsInWorld\n");
                System.IO.File.AppendAllText(f, line);
            }
            catch (System.Exception e) { Debug.LogWarning("[HalBuilder] archive failed: " + e.Message); }
        }
    }
}
