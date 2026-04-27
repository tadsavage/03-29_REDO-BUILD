using UnityEditor;
using UnityEngine;

public class GameContext : MonoBehaviour
{
    //public TopRightUI_TimeMoney topRightUI; DONT NEED RIGHT NOW, JUST TESTING

    private void Start()
    {
        //topRightUI.Initialize(MoneyService, TimeService);
        var placement = FindAnyObjectByType<PlacementSystem>();
        placement.LoadGame();
    }
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

}
