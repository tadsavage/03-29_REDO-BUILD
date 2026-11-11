using UnityEngine;

/// <summary>
/// Converts REAL (scaled) time into GAME time, in one place. The clock runs at <see cref="GameCore.Economy.SimulationTimeService.GameSecondsPerRealSecond"/>
/// game seconds per real second at 1x (20, like Skyrim, since 2026-11-10; it was 60). Anything that means "game minutes" but is counted with
/// <c>Time.deltaTime</c> / <c>Time.time</c> (which are scaled REAL seconds) must go through here, or it drifts whenever that rate changes.
/// Movement, animation, physics and "real seconds" rules do NOT: they stay on Time.deltaTime.
/// </summary>
public static class GameClock
{
    /// <summary>Game minutes per scaled real second (20 game s per real s = 1/3).</summary>
    public static float MinutesPerSecond => GameCore.Economy.SimulationTimeService.GameSecondsPerRealSecond / 60f;

    /// <summary>Game minutes that passed this frame (Time.deltaTime already carries the HUD speed).</summary>
    public static float DeltaMinutes => Time.deltaTime * MinutesPerSecond;

    /// <summary>Scaled real seconds (a Time.time difference) to game minutes.</summary>
    public static float ToMinutes(float scaledSeconds) => scaledSeconds * MinutesPerSecond;
}
