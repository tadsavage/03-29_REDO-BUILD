using UnityEngine;

public class AppUIDemoManager : MonoBehaviour
{
    public MoneyService MoneyService { get; private set; }
    public SimulationTimeService TimeService { get; private set; }

    private void Awake()
    {
        TimeService = new SimulationTimeService(1, 10, 30);
        MoneyService = new MoneyService(150250);
        MoneyService.AddHourlyCost(120);
    }

    private void Update()
    {
        TimeService.Tick(Time.deltaTime);
    }
}
