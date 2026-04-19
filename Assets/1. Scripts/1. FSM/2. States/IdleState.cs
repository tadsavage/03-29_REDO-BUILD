using UnityEngine;

public class IdleState : IPlacementState
{
    public bool IsPlacementState => false;

    public void OnEnter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Tick()
    {
        // Idle does nothing — FSM handles transitions
    }

    public void OnExit()
    {
        // Nothing to clean up
    }
}
