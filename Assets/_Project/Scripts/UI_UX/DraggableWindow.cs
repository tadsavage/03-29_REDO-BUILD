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
    private bool _dragging;
    private int _pointerId = -1;
    private Vector2 _pointerStart;
    private Vector2 _panelStart;
    private Vector2 _lastPointerPosition;
    private bool _sizeFrozen;

    /// <summary>True while the user is actively dragging the panel.</summary>
    public bool IsDragging => _dragging;

    /// <summary>Which CSS position mode this drag moves the panel with — Absolute (default) removes it
    /// from its parent's layout flow immediately, which is correct for an ordinary floating window but
    /// WRONG for a panel that's still sitting in-flow inside another layout (e.g. docked in a flex
    /// bar): switching to Absolute the instant the pointer goes down yanks it out of that flow right
    /// away, so any sibling relying on space-between/flex-grow reflows before the user has dragged
    /// anywhere. A listener can set this to Relative in its OnDragStart handler (called before OnDown
    /// finishes) to keep the panel fully in-flow — occupying its normal slot, so nothing around it
    /// moves — while still letting it slide freely via left/top offset. Reset to Absolute automatically
    /// once the drag ends, so it never leaks into the next one.</summary>
    public Position PositionMode = Position.Absolute;

    /// <summary>Fired at the very start of a drag, before position is captured — lets a listener
    /// reparent/reposition the panel first (e.g. detaching from a docked layout) so the drag picks up
    /// from the correct post-reparent position instead of jumping.</summary>
    public event System.Action OnDragStart;

    /// <summary>Fired on every pointer move while dragging, after the panel's position is updated —
    /// lets a listener track where the panel currently is (e.g. to preview a drop target).</summary>
    public event System.Action OnDragMove;

    /// <summary>Fired on pointer-up after a drag, once the panel's new inline position is set.</summary>
    public event System.Action OnDragEnd;

    public DraggableWindow(VisualElement panel, VisualElement handle, VisualElement closeButton)
    {
        _panel = panel;
        _closeButton = closeButton;
        if (_panel == null || handle == null) return;
        handle.RegisterCallback<PointerDownEvent>(OnDown);
        _panel.RegisterCallback<PointerMoveEvent>(OnMove);
        _panel.RegisterCallback<PointerUpEvent>(OnUp);
        _panel.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
    }
    /// <summary>Clear all inline position/size overrides → revert to the stylesheet spot. Call on the red X.</summary>
    public void ResetToOriginal()
    {
        if (_panel == null) return;
        _sizeFrozen = false;
        _panel.style.position = StyleKeyword.Null;
        _panel.style.left = StyleKeyword.Null;
        _panel.style.top = StyleKeyword.Null;
        _panel.style.right = StyleKeyword.Null;
        _panel.style.bottom = StyleKeyword.Null;
        _panel.style.width = StyleKeyword.Null;
        _panel.style.height = StyleKeyword.Null;
    }
    private void OnDown(PointerDownEvent e)
    {
        if (IsOverClose(e.target as VisualElement)) return;   // let the X click through
        // FreezeSize BEFORE OnDragStart, not after: it reads resolvedStyle, which only refreshes on
        // Unity's NEXT layout pass — reading it AFTER a listener (e.g. DetachFromDock) has just
        // written a new explicit width would silently read the width from BEFORE that write and
        // clobber it right back. Freezing first captures the size as it stood going into the drag,
        // which is what this method exists for (stopping a stretched panel, e.g. the roster, from
        // collapsing once it switches to Position.Absolute); anything OnDragStart sets afterward is
        // then free to stand.
        FreezeSize();
        OnDragStart?.Invoke();   // may reparent/reposition _panel, or set PositionMode, before we snapshot below
        _dragging = true;
        _pointerId = e.pointerId;
        _pointerStart = (Vector2)e.position;

        if (PositionMode == Position.Relative)
        {
            // Relative left/top are pure OFFSETS from the panel's normal in-flow position, not
            // coordinates from a parent origin — starting at zero means "no offset yet", and every
            // subsequent drag delta becomes the offset directly. This is what keeps the panel fully
            // participating in its parent's layout (nothing around it reflows) while still letting it
            // slide anywhere on screen.
            _panelStart = Vector2.zero;
        }
        else
        {
            // Prefer the panel's OWN just-written inline left/top over worldBound — worldBound is also
            // a layout-pass value with the same staleness problem as resolvedStyle above, so reading it
            // right after OnDragStart repositioned the panel (e.g. a detach-on-drag-start) would still
            // reflect wherever it was BEFORE that, and the drag would visibly jump on its first move. A
            // style value just written reads back immediately; layout-derived values do not. Only fall
            // back to worldBound when no explicit position has been set yet (a panel's very first drag).
            bool hasExplicitPos = _panel.style.left.keyword == StyleKeyword.Undefined
                                && _panel.style.top.keyword == StyleKeyword.Undefined;
            if (hasExplicitPos)
            {
                _panelStart = new Vector2(_panel.style.left.value.value, _panel.style.top.value.value);
            }
            else
            {
                Vector2 parentOrigin = _panel.parent != null ? _panel.parent.worldBound.position : Vector2.zero;
                _panelStart = _panel.worldBound.position - parentOrigin;
            }
        }

        SetPos(_panelStart);   // pin where it already is (no jump), then drag from here
        _panel.CapturePointer(_pointerId);
        e.StopPropagation();
    }
    private void OnMove(PointerMoveEvent e)
    {
        if (!_dragging || e.pointerId != _pointerId) return;
        _lastPointerPosition = (Vector2)e.position;
        Vector2 delta = _lastPointerPosition - _pointerStart;
        SetPos(_panelStart + delta);
        e.StopPropagation();
        OnDragMove?.Invoke();   // a listener may call RebaseDuringDrag() here
    }

    /// <summary>Lets a listener switch an in-progress drag to a different parent/position mode
    /// MID-DRAG, without ending it — e.g. promoting a docked-and-sliding card to a real floating
    /// window the instant it clears its dock, instead of waiting for pointer-up. Two problems this
    /// solves that a plain reparent can't:
    /// <list type="bullet">
    /// <item>Reparenting (RemoveFromHierarchy + Add elsewhere) silently drops pointer capture —
    /// confirmed empirically. Capture is reacquired immediately after `reparentAction` runs, and
    /// <see cref="OnCaptureOut"/> double-checks current capture state before treating a capture-out
    /// event as a real interruption, so the transient loss this causes is a no-op instead of ending
    /// the drag.</item>
    /// <item>Every subsequent <see cref="OnMove"/> computes the panel's position as
    /// `_panelStart + (currentPointer - _pointerStart)` relative to where the DRAG STARTED — so simply
    /// reparenting mid-drag without also re-baselining those two values would make the panel jump to
    /// (or fight against) a position computed from its pre-reparent starting point. This re-baselines
    /// both to "right now": `_panelStart` from whatever explicit left/top `reparentAction` just wrote
    /// (or, failing that, from worldBound), and `_pointerStart` from the pointer's last known
    /// position — so the very next move continues smoothly from exactly where things stand.</item>
    /// </list>
    /// `reparentAction` is expected to leave the panel with its own final post-reparent position/style
    /// already applied (mirroring what <see cref="OnDown"/> expects) — this method only fixes up the
    /// drag's internal bookkeeping around it.</summary>
    public void RebaseDuringDrag(System.Action reparentAction, Position newPositionMode)
    {
        if (!_dragging || reparentAction == null) return;

        reparentAction();
        PositionMode = newPositionMode;

        if (newPositionMode == Position.Relative)
        {
            _panelStart = Vector2.zero;
        }
        else
        {
            bool hasExplicitPos = _panel.style.left.keyword == StyleKeyword.Undefined
                                && _panel.style.top.keyword == StyleKeyword.Undefined;
            if (hasExplicitPos)
            {
                _panelStart = new Vector2(_panel.style.left.value.value, _panel.style.top.value.value);
            }
            else
            {
                Vector2 parentOrigin = _panel.parent != null ? _panel.parent.worldBound.position : Vector2.zero;
                _panelStart = _panel.worldBound.position - parentOrigin;
            }
        }
        _pointerStart = _lastPointerPosition;

        if (_panel.panel != null && _pointerId >= 0) _panel.CapturePointer(_pointerId);
    }
    private void OnUp(PointerUpEvent e)
    {
        if (!_dragging) return;
        _dragging = false;
        if (_panel.HasPointerCapture(e.pointerId)) _panel.ReleasePointer(e.pointerId);
        _pointerId = -1;
        OnDragEnd?.Invoke();
        // Reset AFTER the listener's OnDragEnd has had a chance to read what mode this drag used —
        // never leaks into the next drag unless the listener opts back in via OnDragStart.
        PositionMode = Position.Absolute;
    }
    // Fires whenever pointer capture ends — both for our own OnUp above (which has already set
    // _dragging = false by the time its ReleasePointer call triggers this, so the guard below skips
    // it) AND for capture lost any OTHER way: focus stolen mid-drag, alt-tab, the window losing
    // input focus — anything that means Unity will never receive a real PointerUpEvent. Without this,
    // an interrupted drag left _dragging stuck true forever: the panel stayed floating wherever it
    // was abandoned, OnDragEnd never fired, so a docking drag never docked and its ghost preview (if
    // any) never got cleaned out of the bar.
    //
    // ALSO fires (as a transient, expected event) from RebaseDuringDrag's reparent — recaptured
    // immediately after, so by the time this handler runs (whether that recapture happened before or
    // after this event was actually dispatched) capture is already back. Checking the CURRENT capture
    // state rather than reacting unconditionally is what tells the two cases apart without needing a
    // suppression flag or caring about dispatch timing.
    private void OnCaptureOut(PointerCaptureOutEvent e)
    {
        if (!_dragging) return;
        if (_pointerId >= 0 && _panel.HasPointerCapture(_pointerId)) return; // already recaptured — not a real interruption
        _dragging = false;
        _pointerId = -1;
        OnDragEnd?.Invoke();
        PositionMode = Position.Absolute;
    }
    // Lock the current size so switching to absolute positioning doesn't collapse a
    // stretched panel (e.g. the roster, which is sized by top+bottom).
    private void FreezeSize()
    {
        if (_sizeFrozen) return;
        _panel.style.width = _panel.resolvedStyle.width;
        _panel.style.height = _panel.resolvedStyle.height;
        _sizeFrozen = true;
    }
    private void SetPos(Vector2 p)
    {
        _panel.style.position = PositionMode;
        _panel.style.left = p.x;
        _panel.style.top = p.y;
        _panel.style.right = StyleKeyword.Auto;
        _panel.style.bottom = StyleKeyword.Auto;
    }
    private bool IsOverClose(VisualElement t)
    {
        for (var e = t; e != null; e = e.parent)
            if (e == _closeButton) return true;
        return false;
    }
}