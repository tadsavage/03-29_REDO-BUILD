public class DeleteState : IPlacementState
{
    private readonly PlacementActions _actions;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementStateMachine _fsm;

    public DeleteState(
        PlacementActions actions,
        PlacementFinalizer finalizer,
        PlacementStateMachine fsm)
    {
        _actions = actions;
        _finalizer = finalizer;
        _fsm = fsm;
    }

    public bool IsPlacementState
    {
        get { return true; }
    }

    public void OnEnter()
    {
        // Highlight deletable objects
        // Subscribe to delete input
    }

    public void Tick()
    {
        // Raycast to object under cursor
        // Show highlight
    }

    public void OnExit()
    {
        // Remove highlight
        // Unsubscribe input
    }
}