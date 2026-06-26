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

        // Hourly cost pooled per ObjDataSO.GL_Line (e.g. "Doors", "MHE Costs", "Groundskeeping"),
        // so deduction can be attributed to the correct FinancialBreakdownPanel row/tooltip via
        // FinanceCategory.ForGLLine. Replaces the old single anonymous int.
        private readonly System.Collections.Generic.Dictionary<string, int> _hourlyByGLLine = new();

        /// <summary>Read-only view of the current hourly-cost-by-GL_Line pool — debug/diagnostic use.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, int> HourlyByGLLine => _hourlyByGLLine;

        // Fractional dollars owed per GL_Line, carried minute-to-minute (mirrors
        // SimulationTimeService's own _fractionalMinutes pattern). REQUIRED — without this,
        // Mathf.CeilToInt(hourlyAmount / 60f) rounds every category up to a $1/minute FLOOR
        // regardless of its real hourly rate, so any two small/cheap GL_Lines (e.g. a handful
        // of $5/hr doors vs $20/hr racking) both hit the same $1/minute floor and accumulate
        // IDENTICAL lifetime totals over time — which is exactly the "every row shows the same
        // number" bug. Accumulating the true fractional cost and only charging whole dollars
        // once they're actually owed fixes this.
        private readonly System.Collections.Generic.Dictionary<string, float> _fractionalByGLLine = new();

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

        private static string GLLineOf(ObjDataSO data) =>
            string.IsNullOrEmpty(data.GL_Line) ? data.category : data.GL_Line;

        // Employee prefabs carry a PlacedObject component too (for hover-popup/inspection), so
        // they self-register with PlacedObjectRegistry exactly like a placed building the moment
        // they're instantiated — even though they never go through PlaceCommand. Without this
        // guard, every hired employee's body double-charges its legacy ObjDataSO.hourlyCost on
        // top of PayrollService's own EmployeeRecord.hourlyWage deduction. Mirrors the same
        // Worker/Staff exclusion PlacementSystem.cs already uses for the save-data spawn path —
        // wages for these are fully owned by PayrollService, never by hourly object cost.
        private static bool IsWageTracked(ObjDataSO data) =>
            data.category != "Worker" && data.category != "Staff";

        private void OnObjectPlaced(string eventId, PlacedObject placedObject)
        {
            if (placedObject == null || placedObject.data == null || !IsWageTracked(placedObject.data))
            {
                return;
            }

            string glLine = GLLineOf(placedObject.data);
            _hourlyByGLLine.TryGetValue(glLine, out int current);
            _hourlyByGLLine[glLine] = current + placedObject.data.hourlyCost;
        }

        private void OnObjectDeleted(string eventId, PlacedObject deletedObject)
        {
            if (deletedObject == null || deletedObject.data == null || !IsWageTracked(deletedObject.data))
            {
                return;
            }

            string glLine = GLLineOf(deletedObject.data);
            if (_hourlyByGLLine.TryGetValue(glLine, out int current))
                _hourlyByGLLine[glLine] = Mathf.Max(0, current - deletedObject.data.hourlyCost);
        }

        private void OnMinutePassed(string eventId, GameCore.Events.Payloads.SimulationTimeData timeData)
        {
            if (_moneyService == null || _hourlyByGLLine.Count == 0)
            {
                return;
            }

            var moneyService = _moneyService as MoneyService;
            if (moneyService == null)
            {
                return;
            }

            foreach (var kvp in _hourlyByGLLine)
            {
                if (kvp.Value <= 0) continue;

                _fractionalByGLLine.TryGetValue(kvp.Key, out float owed);
                owed += kvp.Value / 60f;

                int wholeDollars = Mathf.FloorToInt(owed);
                _fractionalByGLLine[kvp.Key] = owed - wholeDollars;

                if (wholeDollars <= 0) continue;

                moneyService.RemoveCapital(wholeDollars, FinanceCategory.ForGLLine(kvp.Key), kvp.Key);

                // Bookkeeping only (not a second deduction). This is RECURRING hourly upkeep,
                // not a one-time purchase — it belongs in "Total Hourly Expenses" on the Spent
                // Today panel, not the "Purchases" list (that's one-time costs only, recorded by
                // MoneyService.Deduct() instead — see PlaceCommand/MoveCommand/DeleteCommand).
                moneyService.RecordHourlySpend(wholeDollars);
            }
        }

        private void OnDayChanged(string eventId, int newDay)
        {
            Debug.Log($"[EconomyService] New day: {newDay}. Daily spending reset.");
            ChargeLease();
        }

        /// <summary>
        /// Daily lease/mortgage on the plot: $1 per 10 grid tiles, charged once per in-game day
        /// (not hourly, like everything else — lease/rent is conventionally billed per period,
        /// not per hour). Quick, simple stand-in pending a real lease/property system — easy to
        /// remove or replace wholesale later.
        /// </summary>
        private void ChargeLease()
        {
            if (_moneyService == null) return;

            var grid = UnityEngine.Object.FindAnyObjectByType<PlacementGrid>();
            if (grid == null) return;

            int totalTiles = grid.Width * grid.Height;
            int leaseCost = totalTiles / 10;
            if (leaseCost <= 0) return;

            _moneyService.RemoveCapital(leaseCost, FinanceCategory.LeaseMortgage);
        }

        /// <summary>
        /// Re-scans PlacedObjectRegistry and rebuilds the hourly-cost-by-GL_Line pool from
        /// scratch. MUST be called after a save loads or the scene starts with pre-placed
        /// objects — those are instantiated directly (PlacementSystem.SpawnFromSave, or objects
        /// placed by hand in the Editor scene), never through PlaceCommand, so OnObjectPlaced
        /// never fires for them and they'd otherwise be invisible to hourly-cost tracking.
        /// Mirrors PlacementGrid.RebuildFromRegistry — call alongside it at the same sites.
        /// </summary>
        public void RebuildFromRegistry()
        {
            _hourlyByGLLine.Clear();

            foreach (var placed in PlacedObjectRegistry.All)
            {
                if (placed == null || placed.data == null || !IsWageTracked(placed.data)) continue;

                string glLine = GLLineOf(placed.data);
                _hourlyByGLLine.TryGetValue(glLine, out int current);
                _hourlyByGLLine[glLine] = current + placed.data.hourlyCost;
            }
        }
    }
}
