// METADATA file_path: Assets/3. UI/7. EmployeeUI/DraggableWindow.cs
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Makes a UI Toolkit panel draggable by a handle (its title bar), with session-only
/// position memory — no PlayerPrefs, ever:
///   • Drag → the panel follows the pointer; its position is written as INLINE styles.
///   • Close via toggle (F2/F3) → the inline styles persist, so it reopens exactly
///     where you left it.
///   • Close via the red X → call <see cref="ResetToOriginal"/>: the inline styles are
///     cleared, so it snaps back to the original stylesheet position next time.
///   • New play session → a fresh UIDocument means fresh elements with no inline
///     overrides, so everything starts at its pristine stylesheet spot.
///
/// Clicking the close button never starts a drag.
/// </summary>
public class DraggableWindow
{
    private readonly VisualElement _panel;
    private readonly VisualElement _closeButton;

    private bool    _dragging;
    private int     _pointerId = -1;
    private Vector2 _pointerStart;
    private Vector2 _panelStart;
    private bool    _sizeFrozen;

    public DraggableWindow(VisualElement panel, VisualElement handle, VisualElement closeButton)
    {
        _panel       = panel;
        _closeButton = closeButton;

        if (_panel == null || handle == null) return;
        handle.RegisterCallback<PointerDownEvent>(OnDown);
        _panel.RegisterCallback<PointerMoveEvent>(OnMove);
        _panel.RegisterCallback<PointerUpEvent>(OnUp);
    }

    /// <summary>Clear all inline position/size overrides → revert to the stylesheet spot. Call on the red X.</summary>
    public void ResetToOriginal()
    {
        if (_panel == null) return;
        _sizeFrozen = false;
        _panel.style.position = StyleKeyword.Null;
        _panel.style.left   = StyleKeyword.Null;
        _panel.style.top    = StyleKeyword.Null;
        _panel.style.right  = StyleKeyword.Null;
        _panel.style.bottom = StyleKeyword.Null;
        _panel.style.width  = StyleKeyword.Null;
        _panel.style.height = StyleKeyword.Null;
    }

    private void OnDown(PointerDownEvent e)
    {
        if (IsOverClose(e.target as VisualElement)) return;   // let the X click through

        _dragging   = true;
        _pointerId  = e.pointerId;
        _pointerStart = (Vector2)e.position;

        FreezeSize();
        Vector2 parentOrigin = _panel.parent != null ? _panel.parent.worldBound.position : Vector2.zero;
        _panelStart = _panel.worldBound.position - parentOrigin;
        SetPos(_panelStart);   // pin where it already is (no jump), then drag from here

        _panel.CapturePointer(_pointerId);
        e.StopPropagation();
    }

    private void OnMove(PointerMoveEvent e)
    {
        if (!_dragging || e.pointerId != _pointerId) return;
        Vector2 delta = (Vector2)e.position - _pointerStart;
        SetPos(_panelStart + delta);
        e.StopPropagation();
    }

    private void OnUp(PointerUpEvent e)
    {
        if (!_dragging) return;
        _dragging = false;
        if (_panel.HasPointerCapture(e.pointerId)) _panel.ReleasePointer(e.pointerId);
        _pointerId = -1;
    }

    // Lock the current size so switching to absolute positioning doesn't collapse a
    // stretched panel (e.g. the roster, which is sized by top+bottom).
    private void FreezeSize()
    {
        if (_sizeFrozen) return;
        _panel.style.width  = _panel.resolvedStyle.width;
        _panel.style.height = _panel.resolvedStyle.height;
        _sizeFrozen = true;
    }

    private void SetPos(Vector2 p)
    {
        _panel.style.position = Position.Absolute;
        _panel.style.left   = p.x;
        _panel.style.top    = p.y;
        _panel.style.right  = StyleKeyword.Auto;
        _panel.style.bottom = StyleKeyword.Auto;
    }

    private bool IsOverClose(VisualElement t)
    {
        for (var e = t; e != null; e = e.parent)
            if (e == _closeButton) return true;
        return false;
    }
}
