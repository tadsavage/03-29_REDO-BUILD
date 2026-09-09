using UnityEngine;

/// <summary>
/// Swaps the OS mouse pointer for the blue ergonomic-cursors set, in place of the default Windows
/// arrow, for the whole game — the plain "diamond" arrow normally, and the "select" pointer whenever
/// the mouse is over something the player can actually click (see SetHoveringInteractable, called by
/// PlacementStateMachine for 3D-world objects and by GlobalButtonUX for UI Toolkit buttons).
/// Self-bootstrapping (same pattern as SystemsLogWindow, ReplenishmentService, etc.) rather than a
/// scene GameObject, so it takes effect the moment the game starts regardless of which scene loads
/// first.
///
/// Textures have to be loaded from Resources rather than referenced directly — same reason
/// TruckFillSprite lives under Resources/UI (see Multi-Vendor Order Tab notes): anything loaded at
/// runtime by path, rather than wired up in the Editor via an inspector reference, has to sit inside a
/// Resources folder or Resources.Load can't find it at all, editor or build.
/// </summary>
public static class CustomCursorService
{
    private const string DefaultCursorResourcePath = "UI/BlueArrowDiamondCursor";
    private const string SelectCursorResourcePath = "UI/SelectBlueCursor";

    private static Texture2D _defaultCursor;
    private static Texture2D _selectCursor;

    // Avoids calling Cursor.SetCursor every single frame from a per-frame hover check when nothing's
    // actually changed — redundant native cursor swaps are wasted work and can visibly flicker.
    private static bool _showingSelectCursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        _defaultCursor = Resources.Load<Texture2D>(DefaultCursorResourcePath);
        _selectCursor = Resources.Load<Texture2D>(SelectCursorResourcePath);

        if (_defaultCursor == null)
        {
            Debug.LogWarning($"[CustomCursorService] Couldn't load cursor texture at Resources/{DefaultCursorResourcePath} — leaving the default OS cursor in place.");
            return;
        }
        if (_selectCursor == null)
            Debug.LogWarning($"[CustomCursorService] Couldn't load cursor texture at Resources/{SelectCursorResourcePath} — the select cursor will never show, but the default arrow still will.");

        _showingSelectCursor = false;
        // Hotspot at the arrow's own tip (top-left of the sprite) so clicks land where the cursor
        // visually points, same convention every standard arrow cursor uses.
        Cursor.SetCursor(_defaultCursor, Vector2.zero, CursorMode.Auto);
    }

    /// <summary>Call every frame with whether the mouse is currently over something clickable — a
    /// world object (PlacementStateMachine's hover raycast) or a UI Toolkit button (GlobalButtonUX).
    /// Cheap to call unconditionally: it only actually touches the OS cursor on the frame the state
    /// changes.</summary>
    public static void SetHoveringInteractable(bool hovering)
    {
        if (_defaultCursor == null) return; // Bootstrap hasn't run yet, or the texture failed to load.
        if (hovering == _showingSelectCursor) return;
        if (hovering && _selectCursor == null) return; // nothing to switch to -- stay on the default.

        _showingSelectCursor = hovering;
        Cursor.SetCursor(hovering ? _selectCursor : _defaultCursor, Vector2.zero, CursorMode.Auto);
    }
}
