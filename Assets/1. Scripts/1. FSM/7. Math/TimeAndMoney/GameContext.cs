using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameContext : MonoBehaviour
{
    public MoneyService MoneyService { get; private set; }
    public SimulationTimeService TimeService { get; private set; }

    [SerializeField] private TimeDriver timeDriver;

    private void Awake()
    {
        TimeService = new SimulationTimeService(1, 8, 0);
        timeDriver.Initialize(TimeService);

        MoneyService = new MoneyService(startingCapital: 100000);

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
        placement.LoadGame();

        // Sync the grid with any objects already in the scene (e.g. manually placed in Editor)
        var grid = FindAnyObjectByType<PlacementGrid>();
        if (grid != null)
            grid.RebuildFromRegistry();

        // NOW bake the NavMesh — all placed objects (obstacles, floors, walls) are live.
        // NavMeshManager will fire OnNavMeshReady when done, which unblocks all AiNavigation agents.
        if (NavMeshManager.Instance != null)
            NavMeshManager.Instance.BakeImmediate();
        else
            Debug.LogWarning("[GameContext] NavMeshManager not found — agents may not navigate.");
    }
}
