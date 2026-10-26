using System.Collections.Generic;

/// <summary>
/// Tracks whether a text-entry modal (e.g. Rack Setup, Lane Setup) is currently open. Global
/// number-row hotkeys (1–5 panel toggles) consult this and stand down while such a modal is up —
/// otherwise typing a digit into the modal both fires the panel toggle AND steals focus from the
/// field being typed into (which is why the Max Stack field silently refused input).
///
/// Ref-counted so overlapping modals behave (each pushes on open, pops on close).
/// </summary>
public static class UIModalGuard
{
    private static readonly HashSet<object> _open = new();
    // Optional "is this modal really still on screen?" check per holder. A holder whose check returns
    // false is swept, so a Push that was never matched by a Pop (modal hidden by some other path, or
    // pushed while it had nothing to show) can't keep every hotkey and world click dead.
    private static readonly Dictionary<object, System.Func<bool>> _validators = new();

    /// <summary>True while any registered text-entry modal is open.</summary>
    public static bool IsCapturing
    {
        get
        {
            if (_open.Count == 0) return false;
            // A destroyed MonoBehaviour that never popped (scene change, teardown) would otherwise keep
            // every guarded hotkey AND world click dead for the rest of the run. Unity's == null is true
            // for destroyed objects, so sweep those out before answering.
            _open.RemoveWhere(o => o is UnityEngine.Object uo && uo == null);
            _open.RemoveWhere(o => _validators.TryGetValue(o, out var v) && v != null && !SafeCheck(v));
            return _open.Count > 0;
        }
    }

    /// <summary>Names of the modals currently holding input, for diagnostics.</summary>
    public static string Describe()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var o in _open) sb.Append(o == null ? "null" : o.GetType().Name).Append(' ');
        return sb.ToString().Trim();
    }

    private static bool SafeCheck(System.Func<bool> v) { try { return v(); } catch { return false; } }

    public static void Push(object modal, System.Func<bool> isStillOpen = null)
    {
        if (modal == null) return;
        _open.Add(modal);
        if (isStillOpen != null) _validators[modal] = isStillOpen;
    }
    public static void Pop(object modal)
    {
        if (modal == null) return;
        _open.Remove(modal);
        _validators.Remove(modal);
    }

    // Statics survive domain reloads in the Editor, and entries are plain `object` so a destroyed
    // MonoBehaviour can't be detected and swept. Exiting Play with a modal still open would therefore
    // leave a permanent entry here and kill every guarded hotkey for the rest of the session. Clear
    // the set on each play-mode start so a stale entry can never outlive the run that created it.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _open.Clear();
        _validators.Clear();
        // A build plays MainMenu -> Main in one process; nothing open in the previous scene can still
        // be open in the next one, so start every scene with a clean slate.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) { _open.Clear(); _validators.Clear(); }
    }
}
