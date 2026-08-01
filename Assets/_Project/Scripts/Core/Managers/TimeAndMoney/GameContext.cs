using System.Collections;
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

    [Header("Yard Floor")]
    [Tooltip("Zero-cost floor tile that fills the entire grid at the start of a new game.")]
    [SerializeField] private ObjDataSO _yardFloorTile;

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
        int difficulty = 0; // Clerk (easy): 120k starting capital, 100% sell-back rate, 4x faster hiring replenishment
        int startingCapital = difficulty switch
        {
            0 => 120000, // Clerk — easiest, more money
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
        // Last: it resolves OrderService/InventoryService/SimulationTimeService out of the locator,
        // so everything it depends on must already be registered AND initialized.
        orderArrivalService.Initialize();

        // Load every SkuData asset that lives under a Resources folder (currently just the dummy
        // test SKU) so InventoryService.GetSkuData / TruckController.LoadShipment can resolve a
        // CasePrefab. The 99 Excel-imported SKUs live outside Resources and aren't picked up here —
        // that's a separate follow-up if/when those need real case visuals too.
        inventoryService.LoadSkuDatabase(Resources.LoadAll<SkuData>("Inventory/SKUs"));

        // Defensive reset in case a same-process "New Game"/reload path ever re-runs GameContext.Awake()
        // without a full domain reload — SlotAssignmentService is in-memory only (see its own doc
        // comment) and would otherwise leak assignments from a previous session into a new one.
        SlotAssignmentService.ClearAll();

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

            // Populate every cell with a zero-cost yard floor tile, then bake NavMesh
            if (_yardFloorTile != null && grid != null)
                StartCoroutine(PopulateYardFloors(grid));
            else
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

            // Guarantee the yard-floor baseline even on load. The yard tiles are runtime-only
            // (never part of the saved scene), so any launch that loads instead of starting a
            // new game must re-fill them — otherwise the field comes up as a bare grid.
            // PopulateYardFloors skips cells that already carry a floor, so loaded floors/foundations
            // are preserved and only the empty remainder is filled.
            if (_yardFloorTile != null && grid != null)
                StartCoroutine(PopulateYardFloors(grid));
            else
                SyncAndBake(grid);
        }
    }

    private bool _isPopulatingYardFloors;

    // One merged mesh + one collider standing in for what used to be up to 10,000 individually
    // Instantiate()'d yard tile GameObjects — see YardFloorMeshBuilder. Rebuilt (not reused)
    // every call since which cells are "bare yard" vs covered by a real floor can change
    // between loads.
    private GameObject _yardFloorMeshObject;

    // Rebuilds the yard floor carpet (every empty cell, minus anything with a real
    // floor/ground/foundation already there), then triggers a single NavMesh bake. Public so
    // PlacementSystem can re-run this after every load (F9 quickload, slot load) — yard tiles
    // aren't saved to disk (see PlacementSystem.BuildSaveData), so they must be regenerated
    // every time the world is rebuilt, not just on the initial scene Start. Kept as an
    // IEnumerator for call-site compatibility (callers StartCoroutine/yield this), even though
    // the merged-mesh build itself completes synchronously — no more frame-budget spreading
    // needed now that this isn't 10,000 individual Instantiate + placement-pipeline calls.
    public IEnumerator PopulateYardFloors(PlacementGrid grid)
    {
        // Guard against two fills running concurrently (e.g. the initial GameContext.Start
        // fallback overlapping with a fill triggered by ApplySaveData in the same frame) —
        // running twice would double up the mesh.
        if (_isPopulatingYardFloors) yield break;
        _isPopulatingYardFloors = true;

        // Guard against an uninitialized grid (e.g. a launch where the load early-returned
        // because the save file was missing — _cells would still be null here).
        grid.EnsureInitialized();

        LoadingScreenManager.Instance?.SetProgress(0.5f);

        if (_yardFloorMeshObject != null)
        {
            Destroy(_yardFloorMeshObject);
            _yardFloorMeshObject = null;
        }

        if (_yardFloorTile != null)
            _yardFloorMeshObject = YardFloorMeshBuilder.Build(grid, _yardFloorTile, transform);
        //else
        //    Debug.LogWarning("[GameContext] _yardFloorTile not assigned — skipping yard floor mesh.");

        SyncAndBake(grid);
        _isPopulatingYardFloors = false;
    }

    private void SyncAndBake(PlacementGrid grid)
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
