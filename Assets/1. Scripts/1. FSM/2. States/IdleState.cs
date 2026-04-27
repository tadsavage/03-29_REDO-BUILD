using UnityEngine;

public class IdleState : IPlacementState
{
    public bool IsPlacementState => false;

    public void OnEnter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Object.FindAnyObjectByType<TopBarUI>().SetState(GetType().Name);
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
