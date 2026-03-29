public class DeleteState : IState
{
    private readonly PlacementActions _actions;
    private readonly PlacementFinalizer _finalizer;
    private readonly StateMachine _fsm;

    public DeleteState(
        PlacementActions actions,
        PlacementFinalizer finalizer,
        StateMachine fsm)
    {
        _actions = actions;
        _finalizer = finalizer;
        _fsm = fsm;
    }

    public void Enter()
    {
        // Highlight deletable objects
        // Subscribe to delete click
    }

    public void Tick()
    {
        // Raycast to object under cursor
        // Show highlight
    }

    public void Exit()
    {
        // Remove highlight
        // Unsubscribe input
    }
}
