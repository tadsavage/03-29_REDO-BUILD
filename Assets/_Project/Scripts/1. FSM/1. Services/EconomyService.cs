using GameCore.Economy;
using UnityEngine;
using GameCore.Services;
using GameCore.Events;
using GameCore.Events.Payloads;

namespace GameCore.Economy
{
    /// <summary>
    /// NEW SERVICE: EconomyService coordinates the Money and Time systems.
    ///
    /// RESPONSIBILITY:
    /// - Deduct hourly operating costs at minute boundaries
    /// - Track and update total hourly cost when objects are placed/deleted
    /// - Handle daily resets and time-based economy operations
    /// - Serve as the single point of economy-wide event coordination
    ///
    /// EVENT FLOW:
    /// 1. BuildService places an object → publishes OnObjectPlaced
    /// 2. EconomyService listens to OnObjectPlaced
    /// 3. EconomyService calculates new hourly cost
    /// 4. EconomyService updates MoneyService.TotalHourlyCost
    /// 5. Each minute, SimulationTimeService publishes OnMinutePassed
    /// 6. EconomyService deducts hourly cost / 60 (proportional cost per minute)
    ///
    /// DESIGN PATTERN: Mediator
    /// - Money and Time services are decoupled
    /// - EconomyService acts as the coordinator
    /// - Each system publishes events; EconomyService reacts
    /// - No circular dependencies: Money ↛ Time, Time ↛ Money
    ///
    /// INTEGRATION POINTS:
    /// - Subscribes to: Build.OnObjectPlaced, Build.OnObjectDeleted, Time.OnMinutePassed
    /// - Reads: MoneyService, SimulationTimeService, BuildService
    /// - Publishes: (none; uses existing events)
    /// </summary>
    public class EconomyService : IService
    {
        // ============ INTERNAL STATE ============
        private IMoneyService _moneyService;
        private ITimeService _timeService;
        private EventManager _eventManager;

        private int _totalHourlyCost = 0;

        // ============ LIFECYCLE ============

        public void Initialize()
        {
            Debug.Log("[EconomyService] Initializing...");

            _eventManager = EventManager.Instance;
            if (_eventManager == null)
            {
                Debug.LogError("[EconomyService] EventManager not found.");
                return;
            }

            _moneyService = ServiceLocator.Get<MoneyService>();
            if (_moneyService == null)
            {
                Debug.LogError("[EconomyService] MoneyService not found.");
                return;
            }

            _timeService = ServiceLocator.Get<SimulationTimeService>();
            if (_timeService == null)
            {
                Debug.LogError("[EconomyService] SimulationTimeService not found.");
                return;
            }

            _eventManager.Subscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnObjectPlaced);
            _eventManager.Subscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnObjectDeleted);
            _eventManager.Subscribe<SimulationTimeData>(GameEvents.Time.OnMinutePassed, OnMinutePassed);
            _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);

            Debug.Log("[EconomyService] Initialized.");
        }

        public void Shutdown()
        {
            Debug.Log("[EconomyService] Shutting down...");

            if (_eventManager != null)
            {
                _eventManager.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnObjectPlaced);
                _eventManager.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnObjectDeleted);
                _eventManager.Unsubscribe<SimulationTimeData>(GameEvents.Time.OnMinutePassed, OnMinutePassed);
                _eventManager.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            }

            Debug.Log("[EconomyService] Shut down complete.");
        }

        // ============ EVENT HANDLERS ============

        private void OnObjectPlaced(string eventId, PlacedObject placedObject)
        {
            if (placedObject == null || placedObject.data == null)
            {
                return;
            }

            _totalHourlyCost += placedObject.data.hourlyCost;
            Debug.Log($"[EconomyService] Object placed. New hourly cost: ${_totalHourlyCost:N0}");
        }

        private void OnObjectDeleted(string eventId, PlacedObject deletedObject)
        {
            if (deletedObject == null || deletedObject.data == null)
            {
                return;
            }

            _totalHourlyCost = Mathf.Max(0, _totalHourlyCost - deletedObject.data.hourlyCost);
            Debug.Log($"[EconomyService] Object deleted. New hourly cost: ${_totalHourlyCost:N0}");
        }

        private void OnMinutePassed(string eventId, GameCore.Events.Payloads.SimulationTimeData timeData)
        {
            if (_moneyService == null || _totalHourlyCost == 0)
            {
                return;
            }

            int minuteCost = Mathf.CeilToInt(_totalHourlyCost / 60f);

            if (minuteCost > 0)
            {
                _moneyService.RemoveCapital(minuteCost, "Hourly Operating Cost");
            }
        }

        private void OnDayChanged(string eventId, int newDay)
        {
            Debug.Log($"[EconomyService] New day: {newDay}. Daily spending reset.");
        }
    }
}
