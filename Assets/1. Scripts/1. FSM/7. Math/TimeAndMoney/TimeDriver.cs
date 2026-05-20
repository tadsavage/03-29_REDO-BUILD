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
        TimeService.Tick(Time.deltaTime);
    }
}
