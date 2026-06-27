using System.Collections;
using UnityEngine;
using SaveLoadSystem;
using GameCore.Services;
using GameCore.Economy;
using GameCore.Events;
using GameCore.Build;

[DefaultExecutionOrder(-100)]
public class GameContext : MonoBehaviour
{
    public MoneyService MoneyService { get; private set; }
    public SimulationTimeService TimeService { get; private set; }

    [SerializeField] private TimeDriver timeDriver;

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
            Debug.Log("[GameContext] EventManager not found in scene; created at runtime.");
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

        // Register services with ServiceLocator for dependency injection
        ServiceLocator.Register<SimulationTimeService>(TimeService as SimulationTimeService);
        ServiceLocator.Register<MoneyService>(MoneyService as MoneyService);
        ServiceLocator.Register<EconomyService>(economyService);
        ServiceLocator.Register<PayrollService>(payrollService);
        ServiceLocator.Register<InventoryService>(inventoryService);

        // Initialize services (subscribes to events, publishes initial state)
        TimeService.Initialize();
        MoneyService.Initialize();
        economyService.Initialize();
        payrollService.Initialize();
        inventoryService.Initialize();

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
            Debug.Log("[GameContext] BuildService registered.");
        }
        else
        {
            Debug.LogError("[GameContext] Cannot initialize BuildService: grid or command history null.");
        }

        var fsm = FindAnyObjectByType<PlacementStateMachine>();
        if (fsm != null)
            fsm.Initialize(this);

        var ui = FindAnyObjectByType<BuildMenuUI>();
        if (ui != null) ui.Initialize(MoneyService);
        else Debug.LogError("[GameContext] BuildMenuUI not found in scene.");

        var placement = FindAnyObjectByType<PlacementSystem>();
        if (placement != null) placement.Initialize(MoneyService);
        else Debug.LogError("[GameContext] PlacementSystem not found in scene.");

        // Wire legacy OnDayChanged event for backward compatibility (MoneyService resets daily spending)
        TimeService.OnDayChanged += () => MoneyService.ResetDailySpending();
    }

    private void Start()
    {
        var placement = FindAnyObjectByType<PlacementSystem>();
        var grid = FindAnyObjectByType<PlacementGrid>();

        LoadingScreenManager.Instance?.SetProgress(0.25f);

        bool isNewGame = PlayerPrefs.GetInt("IsNewGame", 0) == 1;
        if (isNewGame)
        {
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
            int loadSlotIndex = PlayerPrefs.GetInt("LoadSlotIndex", -1);
            if (loadSlotIndex >= 0 && SaveManager.Instance != null)
                SaveManager.Instance.LoadFromSlot(loadSlotIndex);
            else
            {
                string saveName = PlayerPrefs.GetString("LastSaveName", "quicksave");
                placement.LoadGame(saveName);
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
        else
            Debug.LogWarning("[GameContext] _yardFloorTile not assigned — skipping yard floor mesh.");

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
        else
            Debug.LogWarning("[GameContext] NavMeshManager not found — agents may not navigate.");
    }
}
