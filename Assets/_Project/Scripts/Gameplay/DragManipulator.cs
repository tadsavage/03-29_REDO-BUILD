using UnityEngine;
using UnityEngine.UIElements;

public class DragManipulator : PointerManipulator
{
    private Vector2 _startPosition;
    private bool _isDragging;

    public DragManipulator(VisualElement target)
    {
        this.target = target;
        _isDragging = false;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (_isDragging) return;

        // Start dragging
        _startPosition = evt.localPosition;
        target.CapturePointer(evt.pointerId);
        _isDragging = true;
        
        target.BringToFront();
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_isDragging || !target.HasPointerCapture(evt.pointerId)) return;

        Vector2 delta = (Vector2)evt.localPosition - _startPosition;
        
        // Update position using translate to avoid layout recalculation
        float newX = target.resolvedStyle.left + delta.x;
        float newY = target.resolvedStyle.top + delta.y;
        
        target.style.left = newX;
        target.style.top = newY;

        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_isDragging || !target.HasPointerCapture(evt.pointerId)) return;

        target.ReleasePointer(evt.pointerId);
    }

    private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        _isDragging = false;
    }
}
