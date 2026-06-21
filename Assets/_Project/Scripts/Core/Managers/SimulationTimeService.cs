using System;

public class SimulationTimeService
{
    public int Minute { get; private set; }
    public int Hour { get; private set; }
    public int Day { get; private set; }

    public float TimeScale { get; private set; } = 1f;

    public event Action OnMinuteChanged;
    public event Action OnHourChanged;
    public event Action OnDayChanged;
    public event Action OnTimeChanged; // fires on ANY change

    private float accumulatedSeconds;

    public SimulationTimeService(int startDay = 1, int startHour = 8, int startMinute = 0)
    {
        Day = startDay;
        Hour = startHour;
        Minute = startMinute;
    }

    public void SetTimeScale(float scale)
    {
        TimeScale = scale;
    }

    public void Tick(float deltaTime)
    {
        accumulatedSeconds += deltaTime * TimeScale;

        // 1 in-game minute = 1 real-time second (for prototyping)
        while (accumulatedSeconds >= 1f)
        {
            accumulatedSeconds -= 1f;
            AdvanceMinute();
        }
    }

    private void AdvanceMinute()
    {
        Minute++;

        OnMinuteChanged?.Invoke();
        OnTimeChanged?.Invoke();

        if (Minute >= 60)
        {
            Minute = 0;
            AdvanceHour();
        }
    }

    private void AdvanceHour()
    {
        Hour++;

        OnHourChanged?.Invoke();
        OnTimeChanged?.Invoke();

        if (Hour >= 24)
        {
            Hour = 0;
            AdvanceDay();
        }
    }

    private void AdvanceDay()
    {
        Day++;

        OnDayChanged?.Invoke();
        OnTimeChanged?.Invoke();
    }
}

