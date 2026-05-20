using UnityEngine;

public class IdleState : IPlacementState
{
    public bool IsPlacementState => false;

    RaycastController _raycast = Object.FindFirstObjectByType<RaycastController>();
    private TopBarUI _topBarUI;
    private TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();

    public void OnEnter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        topBarUI?.SetState(GetType().Name);

        _raycast.EnableRay();
        _raycast.ResetHitData();
    }

    public void Tick()
    {
        _raycast.Tick();
        if (_raycast.HasHit)
        {
            Vector2Int cell = _raycast.HitCell;
            topBarUI?.SetCell(cell.x, cell.y);
        }
    }

    public void OnExit()
    {
        // Nothing to clean up
    }
}
