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
