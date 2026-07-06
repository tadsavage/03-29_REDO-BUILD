/// <summary>
/// Contract for UI panels that can be toggled open/closed by the UIKeyBindingManager.
/// Implement this on any UI that should respect the keybinding exclusivity system
/// (only one keybind UI open at a time).
/// </summary>
public interface IUIPanel
{
    /// <summary>Open/activate this UI panel.</summary>
    void Show();

    /// <summary>Close/deactivate this UI panel.</summary>
    void Hide();

    /// <summary>True if this panel is currently visible/open.</summary>
    bool IsOpen { get; }
}
