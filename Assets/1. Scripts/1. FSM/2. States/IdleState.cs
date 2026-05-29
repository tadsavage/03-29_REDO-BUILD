using UnityEngine;

public class IdleState : IPlacementState
{
    public bool IsPlacementState => false;

    private RaycastController _raycast;
    private TopBarUI _topBarUI;
    
    private RaycastController raycast => _raycast != null ? _raycast : _raycast = Object.FindAnyObjectByType<RaycastController>();
    private TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();

    public void OnEnter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        topBarUI?.SetState(GetType().Name);

        raycast?.EnableRay();
        raycast?.ResetHitData();
    }

    public void Tick()
    {
        var ray = raycast;
        if (ray == null) return;

        ray.Tick();
        if (ray.HasHit)
        {
            Vector2Int cell = ray.HitCell;
            topBarUI?.SetCell(cell.x, cell.y);
        }
    }

    public void OnExit()
    {
        // Nothing to clean up
    }
}
