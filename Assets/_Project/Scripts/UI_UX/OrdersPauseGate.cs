using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Pause Game while working on Orders" — the Dev Settings checkbox lives here. When Enabled,
/// Time.timeScale drops to 0 the moment the FIRST of PurchasingPanel / ContractsPanel opens, and is
/// restored to whatever it was (respecting the TopBar's speed buttons) once the LAST one closes.
/// Ref-counted like UIModalGuard, so having both panels open at once doesn't resume time when only
/// one of them is closed. Entirely inert while Enabled is false — Push() is then a no-op, so nothing
/// about the panels' own open/close behavior changes.
/// </summary>
public static class OrdersPauseGate
{
    private const string EnabledPrefsKey = "DevSettings_PauseWhileOrdersUI";

    private static readonly HashSet<object> _open = new();
    private static float _timeScaleBeforePause = 1f;

    public static bool Enabled { get; private set; } = PlayerPrefs.GetInt(EnabledPrefsKey, 0) == 1;

    /// <summary>Flips the setting and persists it. Turning it off while a tracked panel is still open
    /// immediately hands time control back to whatever set it before the pause, rather than leaving
    /// the game paused with no checkbox left to un-pause it.</summary>
    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        PlayerPrefs.SetInt(EnabledPrefsKey, enabled ? 1 : 0);

        if (!enabled && _open.Count > 0)
        {
            Time.timeScale = _timeScaleBeforePause;
            _open.Clear();
        }
    }

    /// <summary>Call from a tracked panel's Show(). No-ops entirely while Enabled is false.</summary>
    public static void Push(object requester)
    {
        if (requester == null || !Enabled) return;

        if (_open.Count == 0)
        {
            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
        }
        _open.Add(requester);
    }

    /// <summary>Call from a tracked panel's Hide(). Safe to call even if Push() never ran for this
    /// requester (e.g. the checkbox was off when it opened) — Remove is then just a no-op.</summary>
    public static void Pop(object requester)
    {
        if (requester == null) return;

        bool wasTracked = _open.Remove(requester);
        if (wasTracked && _open.Count == 0)
            Time.timeScale = _timeScaleBeforePause;
    }

    // Statics survive domain reloads in the Editor, and entries are plain `object` so a destroyed
    // panel can't be detected and swept. Exiting Play with a panel still open would otherwise leave a
    // stale entry (and a paused Time.timeScale) for the rest of the session — clear it on every
    // play-mode start, same reasoning as UIModalGuard.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _open.Clear();
        Enabled = PlayerPrefs.GetInt(EnabledPrefsKey, 0) == 1;
    }
}
