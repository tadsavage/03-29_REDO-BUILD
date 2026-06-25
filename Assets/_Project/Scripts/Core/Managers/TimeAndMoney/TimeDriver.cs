using GameCore.Economy;
using UnityEngine;

public class TimeDriver : MonoBehaviour
{
    public SimulationTimeService TimeService { get; private set; }

    public void Initialize(SimulationTimeService service)
    {
        TimeService = service;
    }

    private void Update()
    {
        // TimeService is wired by GameContext.Awake(). Guard against the brief window
        // before that runs, and against private state being reset by a play-mode domain
        // reload (which doesn't re-run Awake), so we don't spam NullReferenceExceptions.
        if (TimeService == null) return;
        TimeService.Tick(Time.deltaTime);
    }
}
