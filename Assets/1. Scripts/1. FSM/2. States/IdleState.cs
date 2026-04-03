using UnityEngine;

public class IdleState : IPlacementState
{
    public bool IsPlacementState
    {
        get { return false; }
    }

    public void OnEnter() 
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    public void Tick() { }
    public void OnExit() { }
}

