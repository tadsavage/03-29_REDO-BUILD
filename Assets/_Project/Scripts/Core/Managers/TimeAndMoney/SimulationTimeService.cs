using GameCore.Economy;
using UnityEngine;
using GameCore.Services;
using GameCore.Events;
using GameCore.Events.Payloads;

namespace GameCore.Economy
{
    /// <summary>
    /// Refactored SimulationTimeService: Implements ITimeService with event publishing.
    ///
    /// MAJOR CHANGES FROM ORIGINAL:
    /// 1. Implements IService interface (Initialize/Shutdown)
    /// 2. Publishes events on every minute, hour, and day change
    /// 3. Tracks minute boundaries for event timing
    /// 4. Manages time scale (pause, 1x, 2x, 3x)
    /// 5. No direct dependencies on MoneyService; uses events instead
    ///
    /// TIME SCALE:
    /// - Real-time behavior: 1 real-second = 1 in-game minute
    /// - Then scaled by TimeScale (0 = pause, 2 = double speed)
    /// - Example: At 2x speed, 1 real-second = 2 in-game minutes
    ///
    /// RESPONSIBILITY:
    /// - Advance in-game time every frame
    /// - Track current hour, minute, day
    /// - Detect transitions (minute, hour, day)
    /// - Publish time events for other systems
    /// - Control time scale (pause, speedup)
    ///
    /// INTEGRATION POINTS:
    /// - Publishes: GameEvents.Time.OnMinutePassed, OnHourChanged, OnDayChanged, OnTimeScaleChanged
    /// - Used by: EconomyService (hourly cost timing), MoneyService (daily reset), UI (time display)
    ///
    /// THREAD SAFETY: Not thread-safe. Assumes single-threaded game loop (Update called once per frame).
    /// </summary>
    public class SimulationTimeService : ITimeService
    {
        // ============ INTERNAL STATE ============
        private int _currentHour;
        private int _currentMinute;
        private int _currentDay;
        private float _timeScale = 1.0f;
        private float _fractionalMinutes = 0f;

        private int _previousHour;
        private int _previousMinute;
        private int _previousDay;

        private EventManager _eventManager;

        // ============ LEGACY EVENTS (Backward Compatibility) ============
        // Match original SimulationTimeService event signatures for backward compatibility
        public event System.Action OnMinuteChanged;
        public event System.Action OnHourChanged;
        public event System.Action OnDayChanged;
        public event System.Action OnTimeChanged;

        // ============ PROPERTIES (ITimeService) ============
        public int Hour => _currentHour;
        public int Minute => _currentMinute;
        public int Day => _currentDay;
        public float TimeScale => _timeScale;
        public string TimeString => $"{_currentHour:00}:{_currentMinute:00}";

        public long TotalMinutesElapsed
        {
            get
            {
                long totalMinutes = (long)(_currentDay - 1) * 24 * 60 + _currentHour * 60 + _currentMinute;
                return totalMinutes;
            }
        }

        public bool IsMinutePassed { get; private set; }
        public bool IsHourChanged { get; private set; }
        public bool IsDayChanged { get; private set; }

        // ============ CONSTRUCTOR ============
        public SimulationTimeService(int startDay = 1, int startHour = 8, int startMinute = 0)
        {
            _currentDay = startDay;
            _currentHour = startHour;
            _currentMinute = startMinute;
            _previousDay = startDay;
            _previousHour = startHour;
            _previousMinute = startMinute;
        }

        // ============ LIFECYCLE ============

        public void Initialize()
        {
            Debug.Log($"[SimulationTimeService] Initializing. Starting time: {TimeString}, Day {_currentDay}");

            _eventManager = EventManager.Instance;
            if (_eventManager == null)
            {
                Debug.LogError("[SimulationTimeService] EventManager not found.");
                return;
            }

            SimulationTimeData timeData = new SimulationTimeData
            {
                Hour = _currentHour,
                Minute = _currentMinute,
                Day = _currentDay
            };
            _eventManager.Publish(GameEvents.Time.OnMinutePassed, timeData);

            Debug.Log("[SimulationTimeService] Initialized.");
        }

        public void Shutdown()
        {
            Debug.Log("[SimulationTimeService] Shutting down...");
            Debug.Log("[SimulationTimeService] Shut down complete.");
        }

        /// <summary>
        /// Advance time by deltaTime seconds. Must be called from game loop (e.g., GameContext or TimeDriver).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_eventManager == null)
            {
                return;
            }

            float scaledDeltaSeconds = deltaTime * _timeScale;
            float deltaMinutes = scaledDeltaSeconds;
            _fractionalMinutes += deltaMinutes;

            if (_fractionalMinutes >= 1.0f)
            {
                int minutesToAdd = (int)_fractionalMinutes;
                _fractionalMinutes -= minutesToAdd;

                AdvanceTime(minutesToAdd);
                DetectTransitions();
            }
        }

        // ============ TIME CONTROL ============

        public void SetTimeScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0f, 3f);

            if (_timeScale == scale)
            {
                return;
            }

            _timeScale = scale;

            Debug.Log($"[SimulationTimeService] Time scale changed to {_timeScale}x");
            _eventManager?.Publish(GameEvents.Time.OnTimeScaleChanged, _timeScale);
        }

        // ============ TIME ADVANCEMENT ============

        private void AdvanceTime(int minutesAmount)
        {
            _previousHour = _currentHour;
            _previousMinute = _currentMinute;
            _previousDay = _currentDay;

            _currentMinute += minutesAmount;

            if (_currentMinute >= 60)
            {
                int hoursToAdd = _currentMinute / 60;
                _currentMinute = _currentMinute % 60;

                _currentHour += hoursToAdd;

                if (_currentHour >= 24)
                {
                    int daysToAdd = _currentHour / 24;
                    _currentHour = _currentHour % 24;

                    _currentDay += daysToAdd;

                    Debug.Log($"[SimulationTimeService] New day: {_currentDay}");
                }
            }
        }

        private void DetectTransitions()
        {
            IsMinutePassed = false;
            IsHourChanged = false;
            IsDayChanged = false;

            if (_currentMinute != _previousMinute)
            {
                IsMinutePassed = true;

                SimulationTimeData timeData = new SimulationTimeData
                {
                    Hour = _currentHour,
                    Minute = _currentMinute,
                    Day = _currentDay
                };
                _eventManager?.Publish(GameEvents.Time.OnMinutePassed, timeData);
                OnMinuteChanged?.Invoke();
                OnTimeChanged?.Invoke();
            }

            if (_currentHour != _previousHour)
            {
                IsHourChanged = true;
                _eventManager?.Publish(GameEvents.Time.OnHourChanged, _currentHour);
                OnHourChanged?.Invoke();
                OnTimeChanged?.Invoke();
            }

            if (_currentDay != _previousDay)
            {
                IsDayChanged = true;
                _eventManager?.Publish(GameEvents.Time.OnDayChanged, _currentDay);
                OnDayChanged?.Invoke();
                OnTimeChanged?.Invoke();
            }
        }

        // ============ LEGACY METHODS (Backward Compatibility) ============
        public float TimeSpeed
        {
            get => _timeScale;
            set => SetTimeScale(value);
        }
    }
}
