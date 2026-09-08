using UnityEngine;
using GameCore.Build;

/// <summary>
/// Hover inspection state. Shows object details when hovering, allows no placement.
/// Inherits from PlacementStateBase for standardized service access and event handling.
/// </summary>
public class IdleState : PlacementStateBase
{
    public override bool IsPlacementState => false;

    private RaycastController _raycast;
    private TopBarUI _topBarUI;

    private RaycastController raycast => _raycast != null ? _raycast : _raycast = Object.FindAnyObjectByType<RaycastController>();
    private TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();

    public override void OnEnter()
    {
        base.OnEnter(); // Initialize event manager and services

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        topBarUI?.SetState(GetType().Name);

        raycast?.EnableRay();
        raycast?.ResetHitData();
    }

    public override void Update()
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

    public override void OnExit()
    {
        // Nothing to clean up
    }
}
