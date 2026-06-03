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
        ui.Initialize(MoneyService);

        var placement = FindAnyObjectByType<PlacementSystem>();
        placement.Initialize(MoneyService);

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

            // ── Starting camera: full-playfield overview at ~40° downward angle ──
            // Field spans X:-18→+18, Z:-8→+18. At pitch=40° and Y=15, the look-at
            // ground point is 15/tan(40°)≈17.9 units forward, so Z=-13 centres the
            // field in view. We also update the clamp values in case the scene still
            // stores the old Inspector-serialised defaults (6 / -8).
            var cam = FindAnyObjectByType<FreeLookCamera>();
            if (cam != null)
            {
                cam.heightMax = 20f;
                cam.Z_Min     = -15f;
                cam.transform.position    = new Vector3(0f, 15f, -13f);
                cam.transform.eulerAngles = new Vector3(40f, 0f, 0f);
            }

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
                string saveName = PlayerPrefs.GetString("LastSaveName", "autosave");
                placement.LoadGame(saveName);
            }

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
