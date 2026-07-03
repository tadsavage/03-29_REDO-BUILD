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

    /// <summary>True while any registered text-entry modal is open.</summary>
    public static bool IsCapturing => _open.Count > 0;

    public static void Push(object modal) { if (modal != null) _open.Add(modal); }
    public static void Pop(object modal)  { if (modal != null) _open.Remove(modal); }
}
