using System;

/// <summary>
/// One switch for every hover tooltip (the cursor-following UI cards and the Scheduler's own).
/// F1 toggles <see cref="Enabled"/> (see TopBarUI). While the player is carrying a tile in the Scheduler the
/// tooltips are held back as well - the carried tile follows the mouse and a card popping up under it gets in
/// the way of every slot you are trying to read. The Scheduler registers <see cref="CarryingTile"/>.
/// </summary>
public static class TooltipSettings
{
    /// <summary>Master switch (F1). Session only.</summary>
    public static bool Enabled = true;

    /// <summary>Set by the Scheduler: true while a tile is in hand.</summary>
    public static Func<bool> CarryingTile;

    public static bool Blocked => !Enabled || (CarryingTile != null && CarryingTile());

    // The project runs without a domain reload on entering Play, so statics survive between sessions: start each
    // one with tooltips on and no stale carry predicate from the last session's panel.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        Enabled = true;
        CarryingTile = null;
    }
}
