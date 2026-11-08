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

    // Modals that must also block the WORLD (hover, clicks, pinned cards), not just hotkeys: the pause menu, its settings overlay and the
    // Save/Load window. Pushed with PushBlocking. Hotkeys and the camera already stand down for every modal via IsCapturing.
    private static readonly HashSet<object> _blocking = new();
    private static float _blockedUntil;
    /// <summary>Real-time seconds the world stays blocked AFTER the last world-blocking modal closes. A click that closes a menu must never
    /// also land on whatever is behind it (a press on the frame the menu hides, a double-click, a held button).</summary>
    public const float WorldGraceSeconds = 0.3f;

    /// <summary>True while a world-blocking modal is open, and for <see cref="WorldGraceSeconds"/> after the last one closes. World picking
    /// (RaycastController, UIInputGuard) treats the pointer as over UI whenever this is true.</summary>
    public static bool IsBlockingWorld
    {
        get
        {
            if (_blocking.Count > 0)
            {
                _ = IsCapturing;   // drops destroyed holders and holders whose "still open" check failed from _open
                if (_blocking.RemoveWhere(o => !_open.Contains(o)) > 0) _blockedUntil = UnityEngine.Time.unscaledTime + WorldGraceSeconds;
                if (_blocking.Count > 0) return true;
            }
            return UnityEngine.Time.unscaledTime < _blockedUntil;
        }
    }

    /// <summary>Like <see cref="Push"/>, and the modal also blocks the 3D world while it is open (see <see cref="IsBlockingWorld"/>).</summary>
    public static void PushBlocking(object modal, System.Func<bool> isStillOpen = null)
    {
        if (modal == null) return;
        Push(modal, isStillOpen);
        _blocking.Add(modal);
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
        if (_blocking.Remove(modal)) _blockedUntil = UnityEngine.Time.unscaledTime + WorldGraceSeconds;
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
        _blocking.Clear();
        _blockedUntil = 0f;
        // A build plays MainMenu -> Main in one process; nothing open in the previous scene can still
        // be open in the next one, so start every scene with a clean slate.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) { _open.Clear(); _validators.Clear(); _blocking.Clear(); _blockedUntil = 0f; }
    }
}
