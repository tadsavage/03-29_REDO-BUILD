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

        // FIX: Initialize the State Machine
        var fsm = FindAnyObjectByType<PlacementStateMachine>();
        if (fsm != null)
        {
            fsm.Initialize(this);
        }

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

        // Bake the NavMesh if LoadGame didn't already do it (no save file, or manually-placed Editor objects).
        // ApplySaveData calls BakeSynchronous itself, which sets IsReady = true — skip the second bake.
        if (NavMeshManager.Instance != null && !NavMeshManager.IsReady)
            NavMeshManager.Instance.BakeSynchronous();
        else if (NavMeshManager.Instance == null)
            Debug.LogWarning("[GameContext] NavMeshManager not found — agents may not navigate.");
    }
}
