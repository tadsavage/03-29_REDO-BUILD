using UnityEditor;
using UnityEngine;

public class GameContext : MonoBehaviour
{
    //public TopRightUI_TimeMoney topRightUI; DONT NEED RIGHT NOW, JUST TESTING




    private void Start()
    {
        //topRightUI.Initialize(MoneyService, TimeService);
    }
    public MoneyService MoneyService { get; private set; }
    public SimulationTimeService TimeService { get; private set; }
    [SerializeField] private TimeDriver timeDriver;

    [SerializeField] private PlacementGrid grid;
    public PlacementGrid Grid => grid;


    private void Awake()
    {
        TimeService = new SimulationTimeService(
            startDay: 1,
            startHour: 8,
            startMinute: 0
        );

        timeDriver.Initialize(TimeService);

        MoneyService = new MoneyService(startingCapital: 100000);

        var ui = FindAnyObjectByType<BuildMenuUI>();
        ui.Initialize(MoneyService);

        TimeService.OnHourChanged += () => MoneyService.ApplyHourlyCost();
        TimeService.OnDayChanged += () => MoneyService.ResetDailySpending();
    }
}
