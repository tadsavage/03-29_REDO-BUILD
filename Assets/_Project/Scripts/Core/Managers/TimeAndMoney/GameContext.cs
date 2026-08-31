using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SaveLoadSystem;
using GameCore.Services;
using GameCore.Economy;
using GameCore.Events;
using GameCore.Build;
using GameCore.Inventory;

[DefaultExecutionOrder(-100)]
public class GameContext : MonoBehaviour
{
    public MoneyService MoneyService { get; private set; }
    public SimulationTimeService TimeService { get; private set; }

    [SerializeField] private TimeDriver timeDriver;

    [Header("Auto-Load")]
    [Tooltip("When enabled, quicksave.json is loaded automatically on Start when hitting Play directly. The main menu sets a FromMainMenu flag to override this with its own slot selection.")]
    [SerializeField] private bool _autoLoadQuicksaveOnStart = true;

    private void Awake()
    {
        LoadingScreenManager.Instance?.SetProgress(0.1f);

        // Ensure EventManager exists (required by all services)
        var eventManager = EventManager.Instance;
        if (eventManager == null)
        {
            GameObject eventManagerObj = new GameObject("EventManager");
            eventManager = eventManagerObj.AddComponent<EventManager>();
            //Debug.Log("[GameContext] EventManager not found in scene; created at runtime.");
        }

        // LOCKED to Clerk difficulty for equipment-first hiring model development
        int difficulty = 0; // Clerk (easy): 500k starting capital, 100% sell-back rate, 4x faster hiring replenishment
        int startingCapital = difficulty switch
        {
            0 => 500000, // Clerk — easiest, more money
            1 => 100000, // Supervisor — normal
            2 => 80000,  // Manager — hardest, tight budget
            _ => 100000
        };
        float sellBackRate = difficulty switch
        {
            0 => 1.0f,  // Clerk (easy) — full refund, no penalty
            1 => 0.75f, // Supervisor (normal) — 75% back
            2 => 0.5f,  // Manager (hard) — 50% back
            _ => 0.5f
        };

        // Create refactored services (plain C# classes)
        TimeService = new SimulationTimeService(1, 8, 0);
        MoneyService = new MoneyService(startingCapital, sellBackRate);
        var economyService = new EconomyService();
        var payrollService = new PayrollService();
        var inventoryService = new InventoryService();
        var orderService = new GameCore.Inventory.OrderService();
        var shipmentService = new GameCore.Inventory.ShipmentService();
        var workQueueSystem = new GameCore.Labor.WorkQueueSystem();
        var shipmentReceivingCoordinator = new GameCore.Inventory.ShipmentReceivingCoordinator();
        var putawayLogic = new GameCore.Inventory.PutawayLogic();
        var orderArrivalService = new GameCore.Inventory.OrderArrivalService();
        var dockScheduleService = new GameCore.Inventory.DockScheduleService();
        var marketService = new GameCore.Inventory.MarketService();
        var reputationService = new GameCore.Inventory.ReputationService();
        var brokerService = new GameCore.Inventory.BrokerService();
        var fulfillmentStatsService = new GameCore.Inventory.FulfillmentStatsService();
        var vendorEconomyService = new GameCore.Inventory.VendorEconomyService();
        var vendorPerformanceTracker = new GameCore.Inventory.VendorPerformanceTracker();
        var contractRevenueTracker = new GameCore.Inventory.ContractRevenueTracker();
        var vendorDealService = new GameCore.Inventory.VendorDealService();

        // Register services with ServiceLocator for dependency injection
        ServiceLocator.Register<SimulationTimeService>(TimeService as SimulationTimeService);
        ServiceLocator.Register<MoneyService>(MoneyService as MoneyService);
        ServiceLocator.Register<EconomyService>(economyService);
        ServiceLocator.Register<PayrollService>(payrollService);
        ServiceLocator.Register<InventoryService>(inventoryService);
        ServiceLocator.Register<GameCore.Inventory.OrderService>(orderService);
        ServiceLocator.Register<GameCore.Inventory.ShipmentService>(shipmentService);
        ServiceLocator.Register<GameCore.Labor.WorkQueueSystem>(workQueueSystem);
        ServiceLocator.Register<GameCore.Inventory.ShipmentReceivingCoordinator>(shipmentReceivingCoordinator);
        ServiceLocator.Register<GameCore.Inventory.PutawayLogic>(putawayLogic);
        ServiceLocator.Register<GameCore.Inventory.OrderArrivalService>(orderArrivalService);
        ServiceLocator.Register<GameCore.Inventory.DockScheduleService>(dockScheduleService);
        ServiceLocator.Register<GameCore.Inventory.MarketService>(marketService);
        ServiceLocator.Register<GameCore.Inventory.ReputationService>(reputationService);
        ServiceLocator.Register<GameCore.Inventory.BrokerService>(brokerService);
        ServiceLocator.Register<GameCore.Inventory.FulfillmentStatsService>(fulfillmentStatsService);
        ServiceLocator.Register<GameCore.Inventory.VendorEconomyService>(vendorEconomyService);
        ServiceLocator.Register<GameCore.Inventory.VendorPerformanceTracker>(vendorPerformanceTracker);
        ServiceLocator.Register<GameCore.Inventory.ContractRevenueTracker>(contractRevenueTracker);
        ServiceLocator.Register<GameCore.Inventory.VendorDealService>(vendorDealService);

        // Initialize services (subscribes to events, publishes initial state)
        TimeService.Initialize();
        MoneyService.Initialize();
        economyService.Initialize();
        payrollService.Initialize();
        inventoryService.Initialize();
        orderService.Initialize();
        shipmentService.Initialize();
        workQueueSystem.Initialize();
        shipmentReceivingCoordinator.Initialize();
        putawayLogic.Initialize();
        // Before OrderArrivalService: this subscribes to OrderService.OnOrderArrived to auto-place
        // each order into a dock block, and signing a contract can generate orders immediately.
        dockScheduleService.Initialize();
        // Last: it resolves OrderService/InventoryService/SimulationTimeService out of the locator,
        // so everything it depends on must already be registered AND initialized.
        orderArrivalService.Initialize();
        // Only needs OrderService and SimulationTimeService, both already registered above —
        // order relative to dockSchedule/marketService/reputation doesn't matter.
        fulfillmentStatsService.Initialize();

        // Load every SkuData asset that lives under a Resources folder (currently just the dummy
        // test SKU) so InventoryService.GetSkuData / TruckController.LoadShipment can resolve a
        // CasePrefab. The 99 Excel-imported SKUs live outside Resources and aren't picked up here —
        // that's a separate follow-up if/when those need real case visuals too.
        inventoryService.LoadSkuDatabase(Resources.LoadAll<SkuData>("Inventory/SKUs"));

        // AFTER LoadSkuDatabase, not with the other services above: MarketService seeds a price and
        // seven days of history for every SKU in the database on Initialize, and an empty database
        // at that moment means an empty market with no prices and no spot deals for the session.
        marketService.Initialize();
        // Owns every vendor's randomized Partnership Level for this playthrough. Before reputation:
        // it only needs VendorRegistry (a Resources asset), and reputation's own Initialize wires a
        // subscription to its static OnPartnershipLevelChanged event.
        vendorEconomyService.Initialize();
        vendorPerformanceTracker.Initialize();
        contractRevenueTracker.Initialize();
        // After vendorEconomyService: rolls deals off each vendor's Partnership Level, which must
        // already exist. Ticked in real seconds from TimeDriver.Update(), not from here.
        vendorDealService.Initialize();
        // Gates which vendors will deal with the player, and is the same score intended to drive
        // customer contract arrival. Subscribes to OrderService's shipped/fined/cancelled events, and
        // (as of the Vendor Partnership rework) VendorEconomyService's partnership-changed event.
        reputationService.Initialize();
        // AFTER reputation: the broker reads the score to decide whether it will deal with the
        // player at all, and resolves it out of the locator on Initialize.
        brokerService.Initialize();

        // Defensive reset in case a same-process "New Game"/reload path ever re-runs GameContext.Awake()
        // without a full domain reload — SlotAssignmentService is in-memory only (see its own doc
        // comment) and would otherwise leak assignments from a previous session into a new one.
        SlotAssignmentService.ClearAll();
        // Same reasoning: ZoneRegistry is persisted (see PlacementSystem's load path, which Imports
        // it before this could matter for a real load) but must start empty for a brand-new game.
        ZoneRegistry.ClearAll();

        // Wire timeDriver to use refactored TimeService
        timeDriver.Initialize(TimeService);

        // Create and initialize BuildService (FSM coordination)
        var grid = FindAnyObjectByType<PlacementGrid>();
        var commandHistory = new CommandHistory();
        if (grid != null && commandHistory != null)
        {
            var buildService = new BuildService(grid, commandHistory);
            buildService.Initialize();
            ServiceLocator.Register<BuildService>(buildService);
            //Debug.Log("[GameContext] BuildService registered.");
        }
        else
        {
            //Debug.LogError("[GameContext] Cannot initialize BuildService: grid or command history null.");
        }

        var fsm = FindAnyObjectByType<PlacementStateMachine>();
        if (fsm != null)
            fsm.Initialize(this);

        var ui = FindAnyObjectByType<BuildMenuUI>();
        if (ui != null) ui.Initialize(MoneyService);
        else Debug.LogError("[GameContext] BuildMenuUI not found in scene.");

        var placement = FindAnyObjectByType<PlacementSystem>();
        if (placement != null) placement.Initialize(MoneyService);
        //else Debug.LogError("[GameContext] PlacementSystem not found in scene.");

        // Wire legacy OnDayChanged event for backward compatibility (MoneyService resets daily spending)
        TimeService.OnDayChanged += () => MoneyService.ResetDailySpending();
    }

    private void Start()
    {
        var placement = FindAnyObjectByType<PlacementSystem>();
        var grid = FindAnyObjectByType<PlacementGrid>();

        LoadingScreenManager.Instance?.SetProgress(0.25f);

        bool isNewGame = PlayerPrefs.GetInt("IsNewGame", 0) == 1;
        bool fromMainMenu = PlayerPrefs.GetInt("FromMainMenu", 0) == 1;
        int loadSlotIndex = PlayerPrefs.GetInt("LoadSlotIndex", -1);

        // Consume the FromMainMenu flag immediately so it doesn't leak into
        // subsequent direct-Play sessions.
        PlayerPrefs.SetInt("FromMainMenu", 0);
        PlayerPrefs.Save();

        // When auto-load is enabled and we're NOT coming from the main menu
        // (i.e. the user hit Play directly in the editor), force the quicksave
        // path and ignore any stale PlayerPrefs from a previous session.
        if (_autoLoadQuicksaveOnStart && !fromMainMenu)
        {
            if (isNewGame)
            {
                Debug.Log("[GameContext.Start] Auto-load: overriding stale IsNewGame flag (not from main menu).");
                isNewGame = false;
            }
            if (loadSlotIndex >= 0)
            {
                Debug.Log($"[GameContext.Start] Auto-load: overriding stale LoadSlotIndex={loadSlotIndex} (not from main menu).");
                loadSlotIndex = -1;
            }
        }

        Debug.Log($"[GameContext.Start] FromMainMenu={fromMainMenu}, IsNewGame={isNewGame}, LoadSlotIndex={loadSlotIndex}");

        if (isNewGame)
        {
            Debug.Log("[GameContext.Start] New game path: starting fresh.");
            PlayerPrefs.SetInt("IsNewGame", 0);
            PlayerPrefs.Save();

            string playerName = PlayerPrefs.GetString("PlayerName", "Boss");
            var welcomeOverlay = FindAnyObjectByType<WelcomeOverlayManager>(FindObjectsInactive.Include);
            if (welcomeOverlay != null)
            {
                welcomeOverlay.gameObject.SetActive(true);
                welcomeOverlay.Show(playerName);
            }

            // Rebuild the grid registry and bake NavMesh. The yard ground is now a static
            // authored plane in the scene, so there's nothing to populate here.
            SyncAndBake(grid);
        }
        else
        {
            // loadSlotIndex is -1 for the default continue/autosave case. Numbered slots
            // (explicitly picked from Resume Shift) still go through SaveManager directly;
            // everything else calls PlacementSystem.QuickLoad(), the exact same method F9
            // uses, so boot-load can never diverge from in-game quickload.
            if (loadSlotIndex >= 0 && SaveManager.Instance != null)
            {
                Debug.Log($"[GameContext.Start] Loading from slot {loadSlotIndex} via SaveManager.");
                SaveManager.Instance.LoadFromSlot(loadSlotIndex);
            }
            else
            {
                Debug.Log("[GameContext.Start] Loading quicksave via PlacementSystem.QuickLoad().");
                if (placement != null)
                    placement.QuickLoad();
                else
                    Debug.LogError("[GameContext.Start] PlacementSystem not found — cannot load quicksave.");
            }

            // Rebuild the grid registry and bake NavMesh even on load. The yard ground is a
            // static authored plane in the scene now, so there's no per-load repopulation step.
            SyncAndBake(grid);
        }
    }

    /// <summary>
    /// Rebuilds the placement grid from the object registry, seeds economy tracking, and
    /// triggers a NavMesh bake. Called on both new-game start and after any load (F9 quickload,
    /// slot load) so the world is always in a consistent state.
    /// </summary>
    public void SyncAndBake(PlacementGrid grid)
    {
        if (grid != null)
            grid.RebuildFromRegistry();

        // Seed hourly-cost tracking from whatever's already on the grid (manually-placed scene
        // content, or objects this load just restored) — these never went through PlaceCommand,
        // so EconomyService's event-driven tracking would otherwise never see them.
        ServiceLocator.Get<EconomyService>()?.RebuildFromRegistry();

        // Dismiss loading screen the moment the bake starts — navmesh builds off-thread
        // just like it did before the loading screen existed. Agents wait on OnNavMeshReady
        // internally, so nothing breaks. This restores the original startup speed.
        LoadingScreenManager.Instance?.SetProgress(0.9f);
        LoadingScreenManager.Instance?.CompleteAndHide();

        if (NavMeshManager.Instance != null)
            NavMeshManager.Instance.BakeImmediate();
        //else
            //Debug.LogWarning("[GameContext] NavMeshManager not found — agents may not navigate.");
    }
}
