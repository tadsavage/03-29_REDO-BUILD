using UnityEditor;
using UnityEngine;

public class GameContext : MonoBehaviour
{
    public TopRightUI_TimeMoney topRightUI;

    private void Start()
    {
        topRightUI.Initialize(MoneyService, TimeService);
    }
    public MoneyService MoneyService { get; private set; }

    public SimulationTimeService TimeService { get; private set; }

    [SerializeField] private TimeDriver timeDriver;

    private void Awake()
    {
        TimeService = new SimulationTimeService(
            startDay: 1,
            startHour: 8,
            startMinute: 0
        );

        timeDriver.Initialize(TimeService);

        MoneyService = new MoneyService(startingCapital: 100000);

        TimeService.OnHourChanged += () => MoneyService.ApplyHourlyCost();
        TimeService.OnDayChanged += () => MoneyService.ResetDailySpending();
    }
}
