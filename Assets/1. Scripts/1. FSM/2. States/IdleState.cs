public class IdleState : IPlacementState
{
    public bool IsPlacementState
    {
        get { return false; }
    }

    public void OnEnter() { }
    public void Tick() { }
    public void OnExit() { UnityEngine.Debug.Log("IdleState.OnExit fired"); }
}

