namespace GameCore.Economy
{
    using GameCore.Services;

    /// <summary>
    /// Interface defining the contract for in-game time simulation.
    ///
    /// RATIONALE:
    /// - Separates time management from simulation logic
    /// - Allows swapping time implementations (real-time, scaled, paused)
    /// - Enables multiple time sources if needed (game time + UI time)
    /// - Makes time queries explicit and testable
    ///
    /// CORE RESPONSIBILITIES:
    /// - Simulate in-game time (minutes, hours, days)
    /// - Run at configurable time scale (pause, 1x, 2x, 3x)
    /// - Track elapsed game time
    /// - Publish time tick events
    ///
    /// TIME SCALE:
    /// - 0.0 = paused
    /// - 1.0 = normal speed (1 real-second = 1 in-game minute)
    /// - 2.0 = double speed (1 real-second = 2 in-game minutes)
    /// - 3.0 = triple speed
    ///
    /// EVENT EMISSION:
    /// - Publishes GameEvents.Time.OnMinutePassed every game minute
    /// - Publishes GameEvents.Time.OnHourChanged when hour increments
    /// - Publishes GameEvents.Time.OnDayChanged at midnight (00:00)
    /// - Publishes GameEvents.Time.OnTimeScaleChanged when speed changes
    /// </summary>
    public interface ITimeService : IService
    {
        // ============ TIME QUERIES ============

        /// <summary>Get the current in-game minute (0-59).</summary>
        int Minute { get; }

        /// <summary>Get the current in-game hour (0-23).</summary>
        int Hour { get; }

        /// <summary>Get the current in-game day number (increments at midnight).</summary>
        int Day { get; }

        /// <summary>Get the current time as a formatted string (HH:MM, e.g., "14:30").</summary>
        string TimeString { get; }

        /// <summary>Get total elapsed in-game minutes since game start.</summary>
        long TotalMinutesElapsed { get; }

        // ============ TIME SCALE CONTROL ============

        /// <summary>Get the current time scale multiplier (0.0 paused, 1.0 normal, 2.0+ accelerated).</summary>
        float TimeScale { get; }

        /// <summary>Set the time scale multiplier (0.0 to 3.0). Publishes OnTimeScaleChanged event.</summary>
        void SetTimeScale(float scale);

        // ============ TIME EVENTS ============

        /// <summary>Check if a minute just passed (used for event timing).</summary>
        bool IsMinutePassed { get; }

        /// <summary>Check if an hour just changed.</summary>
        bool IsHourChanged { get; }

        /// <summary>Check if a new day just started (midnight).</summary>
        bool IsDayChanged { get; }
    }
}
