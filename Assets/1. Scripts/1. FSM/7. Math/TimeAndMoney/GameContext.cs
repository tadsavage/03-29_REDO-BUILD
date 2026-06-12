using System.Collections;
using UnityEngine;
using SaveLoadSystem;

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
        TimeService = new SimulationTimeService(1, 8, 0);
        timeDriver.Initialize(TimeService);

        int difficulty = PlayerPrefs.GetInt("Difficulty", 0);
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
        MoneyService = new MoneyService(startingCapital, sellBackRate);

        var fsm = FindAnyObjectByType<PlacementStateMachine>();
        if (fsm != null)
            fsm.Initialize(this);

        var ui = FindAnyObjectByType<BuildMenuUI>();
        if (ui != null) ui.Initialize(MoneyService);
        else Debug.LogError("[GameContext] BuildMenuUI not found in scene.");

        var placement = FindAnyObjectByType<PlacementSystem>();
        if (placement != null) placement.Initialize(MoneyService);
        else Debug.LogError("[GameContext] PlacementSystem not found in scene.");

        TimeService.OnHourChanged += () => MoneyService.ApplyHourlyCost();
        TimeService.OnDayChanged += () => MoneyService.ResetDailySpending();
    }

    private void Start()
    {
        var placement = FindAnyObjectByType<PlacementSystem>();
        var grid = FindAnyObjectByType<PlacementGrid>();

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

    // Fills every empty cell with a yard floor tile, spread across frames (1 column = 1 frame),
    // then triggers a single NavMesh bake when done.
    private IEnumerator PopulateYardFloors(PlacementGrid grid)
    {
        var finalizer = FindAnyObjectByType<PlacementFinalizer>();
        if (finalizer == null)
        {
            Debug.LogWarning("[GameContext] PlacementFinalizer not found — skipping yard floor population.");
            SyncAndBake(grid);
            yield break;
        }

        // Guard against an uninitialized grid (e.g. a launch where the load early-returned
        // because the save file was missing — _cells would still be null here).
        grid.EnsureInitialized();

        Vector2Int[] offsets = _yardFloorTile.GetFootprintOffsets(0f);

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                var cell = new Vector2Int(x, y);
                // Only place if no floor tile already exists (safe for loaded saves)
                var list = grid.GetObjectsInCell(cell);
                bool hasFloor = false;
                if (list != null)
                    foreach (var e in list)
                        if (e.data?.isFloor == true) { hasFloor = true; break; }

                if (!hasFloor)
                    finalizer.FinalizePlacement(cell, offsets, _yardFloorTile, 0f, null, true);
            }
            yield return null; // one frame per column keeps the hitch short
        }

        SyncAndBake(grid);
    }

    private void SyncAndBake(PlacementGrid grid)
    {
        if (grid != null)
            grid.RebuildFromRegistry();

        if (NavMeshManager.Instance != null && !NavMeshManager.IsReady)
            NavMeshManager.Instance.BakeSynchronous();
        else if (NavMeshManager.Instance == null)
            Debug.LogWarning("[GameContext] NavMeshManager not found — agents may not navigate.");
    }
}
