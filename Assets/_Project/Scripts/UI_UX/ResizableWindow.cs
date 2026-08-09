using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Makes a UI Toolkit panel resizable by dragging its edges / bottom-right corner — the companion
/// to <see cref="DraggableWindow"/> (which handles moving by the title bar). Adds thin invisible
/// grab strips along the left / right / bottom edges plus a bottom-right corner grip. Dragging a
/// strip rewrites the panel's inline width/height (and left/top for the left edge) so it grows or
/// shrinks live. Grips subtly highlight on hover so the resize zones are discoverable.
///
/// Also drives an optional "scale" button that callers place themselves — typically right next to
/// the panel's own close button in its title bar, so it matches that button's size and sits on the
/// same row (see <see cref="CycleScale"/>). Unlike the edge grips — which just stretch the layout
/// box — clicking it uniformly scales the WHOLE panel (fonts, icons, everything) via a UI Toolkit
/// transform, toggling between normal (1x) and fill-screen. Callers should call
/// <see cref="ResetToNormal"/> whenever the panel is (re)shown, so it always opens at normal size
/// regardless of whatever it was left at last time it was closed.
///
/// Session-only, like DraggableWindow: everything is written as inline styles on the panel, so a
/// fresh UIDocument (new play session) starts back at the stylesheet size.
/// </summary>
public class ResizableWindow
{
    private enum Edge { Left, Right, Bottom, BottomRight }

    private readonly VisualElement _panel;
    private readonly float _minW;
    private readonly float _minH;

    private bool _resizing;
    private int _pointerId = -1;
    private Edge _edge;
    private Vector2 _pointerStart;
    private float _startLeft, _startTop, _startW, _startH;

    private bool _filled; // false = normal (1x), true = fill screen

    /// <summary>True while the user is actively dragging an edge.</summary>
    public bool IsResizing => _resizing;

    /// <summary>Fired on pointer-up after a resize, once the new inline size is committed.</summary>
    public event System.Action OnResizeEnd;

    /// <param name="titleInset">Height (px) of the title bar to keep clear of the side grips, so
    /// grabbing near the top corners still drags the window rather than resizing it.</param>
    public ResizableWindow(VisualElement panel, float minW = 240f, float minH = 180f,
        float grip = 8f, float titleInset = 34f, bool allowVerticalResize = true)
    {
        _panel = panel;
        _minW = minW;
        _minH = minH;
        if (_panel == null) return;

        AddGrip(Edge.Right,       grip, titleInset);
        AddGrip(Edge.Left,        grip, titleInset);
        if (allowVerticalResize)
        {
            AddGrip(Edge.Bottom,      grip, titleInset);
            AddGrip(Edge.BottomRight, grip, titleInset);
        }
    }

    private void AddGrip(Edge edge, float grip, float titleInset)
    {
        var h = new VisualElement { name = $"resize-{edge}" };
        h.style.position = Position.Absolute;
        h.pickingMode = PickingMode.Position;

        switch (edge)
        {
            case Edge.Right:
                h.style.top = titleInset; h.style.bottom = grip; h.style.right = 0; h.style.width = grip;
                break;
            case Edge.Left:
                h.style.top = titleInset; h.style.bottom = grip; h.style.left = 0; h.style.width = grip;
                break;
            case Edge.Bottom:
                h.style.left = grip; h.style.right = grip; h.style.bottom = 0; h.style.height = grip;
                break;
            case Edge.BottomRight:
                h.style.right = 0; h.style.bottom = 0; h.style.width = grip * 2f; h.style.height = grip * 2f;
                break;
        }

        var cursor = edge == Edge.Bottom ? CreateBuiltInCursor(2) : CreateBuiltInCursor(3);
        h.style.cursor = cursor;

        // Subtle discoverability highlight on hover (a faint royal-blue tint).
        var hot = new Color(0.35f, 0.55f, 0.95f, 0.35f);


        h.RegisterCallback<PointerEnterEvent>(_ => { if (!_resizing) h.style.backgroundColor = hot; });
        h.RegisterCallback<PointerLeaveEvent>(_ => { if (!_resizing) h.style.backgroundColor = Color.clear; });

        h.RegisterCallback<PointerDownEvent>(e => OnDown(e, edge, h));
        h.RegisterCallback<PointerMoveEvent>(OnMove);
        h.RegisterCallback<PointerUpEvent>(e => OnUp(e, h));

        _panel.Add(h); // added after content → renders on top of the panel body
    }

    // ── Scale handle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Cycles the panel through normal (1x) → large (1.5x) → fill-screen → back to normal. Wire a
    /// button's `clicked` event to this — callers own the button's placement/size/styling (typically
    /// cloning their close button's dimensions so the two sit flush together on the title bar).
    /// </summary>
    /// <summary>
    /// Draws the classic "restore/maximize" glyph — two overlapping square outlines — as children of
    /// a scale button, sized relative to the button's own square dimensions. Callers own the button
    /// itself (size, background, hover states); this only fills in its icon.
    /// </summary>
    public static void AddStackedSquaresGlyph(VisualElement button, float buttonSize, Color lineColor)
    {
        float sq = buttonSize * 0.44f;
        float thick = Mathf.Max(1.4f, buttonSize * 0.05f);
        float shift = buttonSize * 0.16f;
        float baseOffset = (buttonSize - sq) / 2f;

        VisualElement MakeSquare(float left, float top)
        {
            var sqEl = new VisualElement { pickingMode = PickingMode.Ignore };
            sqEl.style.position = Position.Absolute;
            sqEl.style.left = left;
            sqEl.style.top = top;
            sqEl.style.width = sq;
            sqEl.style.height = sq;
            sqEl.style.borderTopWidth = sqEl.style.borderBottomWidth =
                sqEl.style.borderLeftWidth = sqEl.style.borderRightWidth = thick;
            sqEl.style.borderTopColor = sqEl.style.borderBottomColor =
                sqEl.style.borderLeftColor = sqEl.style.borderRightColor = lineColor;
            sqEl.style.borderTopLeftRadius = sqEl.style.borderTopRightRadius =
                sqEl.style.borderBottomLeftRadius = sqEl.style.borderBottomRightRadius = 2f;
            return sqEl;
        }

        // Back square (dimmer, upper-right) peeking out from behind the front one (lower-left) —
        // the two-window "restore" silhouette.
        var back = MakeSquare(baseOffset + shift, baseOffset - shift);
        back.style.opacity = 0.55f;
        var front = MakeSquare(baseOffset - shift, baseOffset + shift);

        button.Add(back);
        button.Add(front);
    }

    public void CycleScale()
    {
        _filled = !_filled;
        ApplyScale(_filled ? ComputeFillScreenScale() : 1f);
    }

    /// <summary>Forces the panel back to normal size without touching an already-normal panel's
    /// position (so it doesn't jump on every open). Call this whenever the panel is (re)shown.</summary>
    public void ResetToNormal()
    {
        if (!_filled) return;
        _filled = false;
        ApplyScale(1f);
    }

    /// <summary>
    /// The true screen bounds to fill, NOT the panel's immediate parent — some panels sit inside an
    /// overlay that's deliberately shortened to leave the bottom HUD strip clickable (see
    /// ContractsPanel/WorkQueuePanel's overlay.style.bottom), and fill-screen should cover the whole
    /// screen edge-to-edge regardless, same as it would for a panel with no such reservation. Falls
    /// back to the immediate parent if the panel isn't attached to a live UI Toolkit panel yet.
    /// </summary>
    private (float w, float h) GetFullScreenSize()
    {
        var root = _panel.panel?.visualTree;
        if (root != null && root.resolvedStyle.width > 0f && root.resolvedStyle.height > 0f)
            return (root.resolvedStyle.width, root.resolvedStyle.height);

        var parent = _panel.parent;
        float w = parent != null ? parent.resolvedStyle.width : _panel.resolvedStyle.width;
        float h = parent != null ? parent.resolvedStyle.height : _panel.resolvedStyle.height;
        return (w, h);
    }

    private float ComputeFillScreenScale()
    {
        float baseW = _panel.resolvedStyle.width;
        float baseH = _panel.resolvedStyle.height;
        // Positive checks, not <= 0f: resolvedStyle can be NaN on an element that hasn't been through
        // a layout pass yet, and NaN <= 0f is false, so a negated check would let it slip through.
        if (!(baseW > 0f) || !(baseH > 0f)) return 1f;

        var (availW, availH) = GetFullScreenSize();
        if (availW <= 0f) availW = baseW;
        if (availH <= 0f) availH = baseH;

        const float margin = 0.98f; // slim breathing room so borders don't clip against the edges
        return Mathf.Max(1f, Mathf.Min(availW / baseW, availH / baseH) * margin);
    }

    /// <summary>
    /// Uniformly scales the panel (fonts and icons included, since it's a render transform of the
    /// whole subtree) and re-centers it against the full screen so growing never pushes it off-screen.
    /// The underlying layout width/height — and therefore the edge-grip drag math — stays untouched;
    /// only the visual transform changes.
    /// </summary>
    private void ApplyScale(float scale)
    {
        float baseW = _panel.resolvedStyle.width;
        float baseH = _panel.resolvedStyle.height;
        var (availW, availH) = GetFullScreenSize();

        _panel.style.position = Position.Absolute;
        _panel.style.right = StyleKeyword.Auto;
        _panel.style.bottom = StyleKeyword.Auto;

        if (availW > 0f && availH > 0f && baseW > 0f && baseH > 0f)
        {
            float scaledW = baseW * scale;
            float scaledH = baseH * scale;
            _panel.style.left = Mathf.Max(0f, (availW - scaledW) / 2f);
            _panel.style.top = Mathf.Max(0f, (availH - scaledH) / 2f);
        }

        _panel.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(Length.Percent(0), Length.Percent(0)));
        _panel.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
    }

    private static StyleCursor CreateBuiltInCursor(int cursorId)
    {
        var cursor = new UnityEngine.UIElements.Cursor();
        var field = typeof(UnityEngine.UIElements.Cursor).GetField("defaultCursorId", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) return default;
        object boxedCursor = cursor;
        field.SetValue(boxedCursor, cursorId);
        return new StyleCursor((UnityEngine.UIElements.Cursor)boxedCursor);
    }


    private void OnDown(PointerDownEvent e, Edge edge, VisualElement grip)
    {
        _resizing = true;
        _edge = edge;
        _pointerId = e.pointerId;
        _pointerStart = (Vector2)e.position;

        var rs = _panel.resolvedStyle;
        _startLeft = rs.left;
        _startTop = rs.top;
        _startW = rs.width;
        _startH = rs.height;

        // Pin to explicit absolute box so width/height edits don't fight stretch anchoring.
        _panel.style.position = Position.Absolute;
        _panel.style.left = _startLeft;
        _panel.style.top = _startTop;
        _panel.style.right = StyleKeyword.Auto;
        _panel.style.bottom = StyleKeyword.Auto;
        _panel.style.width = _startW;
        _panel.style.height = _startH;

        grip.CapturePointer(_pointerId);
        e.StopPropagation();
    }

    private void OnMove(PointerMoveEvent e)
    {
        if (!_resizing || e.pointerId != _pointerId) return;
        Vector2 d = (Vector2)e.position - _pointerStart;

        switch (_edge)
        {
            case Edge.Right:
                _panel.style.width = Mathf.Max(_minW, _startW + d.x);
                break;
            case Edge.Bottom:
                _panel.style.height = Mathf.Max(_minH, _startH + d.y);
                break;
            case Edge.BottomRight:
                _panel.style.width = Mathf.Max(_minW, _startW + d.x);
                _panel.style.height = Mathf.Max(_minH, _startH + d.y);
                break;
            case Edge.Left:
                float newW = Mathf.Max(_minW, _startW - d.x);
                // Keep the right edge fixed: left moves only as far as the width actually shrank.
                _panel.style.left = _startLeft + (_startW - newW);
                _panel.style.width = newW;
                break;
        }
        e.StopPropagation();
    }

    private void OnUp(PointerUpEvent e, VisualElement grip)
    {
        if (!_resizing) return;
        _resizing = false;
        if (grip.HasPointerCapture(e.pointerId)) grip.ReleasePointer(e.pointerId);
        grip.style.backgroundColor = Color.clear;
        _pointerId = -1;
        OnResizeEnd?.Invoke();
    }
}
