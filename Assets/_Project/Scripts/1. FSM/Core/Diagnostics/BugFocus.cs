using UnityEngine;

namespace GameCore.Diagnostics
{
    /// <summary>
    /// Where a bug HAL / the playtest bot found is reported. LOG ONLY since 2026-11-12 (Tad: "Don't pause the game anymore or zoom the camera to
    /// bugs anymore. Just let them hit the log and that's good enough"): no pause, no camera move, no quicksave and no on-screen toast. The
    /// bug goes to the in-game Systems Log (the playtest bot also writes it to its bug report files and the Editor log, see PlaytestBot.WriteBug).
    /// It used to quicksave over quicksave.json and pause on every bug, which once destroyed a real quicksave during a test run.
    /// </summary>
    public static class BugFocus
    {
        /// <summary>Kept so the callers did not have to change; <paramref name="pauseAndZoom"/> is ignored.</summary>
        public static void Show(string category, string text, bool pauseAndZoom)
        {
            SystemsLogWindow.LogWarning($"BUG: {category} - {text}");
        }
    }
}
