using UnityEngine;

public class IdleState : IPlacementState
{
    public bool IsPlacementState => false;

    RaycastController _raycast = Object.FindFirstObjectByType<RaycastController>();

    public void OnEnter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Object.FindAnyObjectByType<TopBarUI>().SetState(GetType().Name);

        _raycast.EnableRay();
        _raycast.ResetHitData();
    }

    public void Tick()
    {
        // Idle does nothing
    }

    public void OnExit()
    {
        // Nothing to clean up
    }
}
