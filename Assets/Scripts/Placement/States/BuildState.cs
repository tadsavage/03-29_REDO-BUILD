using UnityEngine;
using UnityEngine.UIElements;

public class BuildState : IState
{
    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly StateMachine _fsm;

    public BuildState(
        PlacementActions actions,
        PreviewController preview,
        PlacementValidator validator,
        PlacementFinalizer finalizer,
        PlacementGrid grid,
        StateMachine fsm)
    {
        _actions = actions;
        _preview = preview;
        _validator = validator;
        _finalizer = finalizer;
        _grid = grid;
        _fsm = fsm;
    }

    public void Enter()
    {
        // Show ghost
        // Subscribe to place input
    }

    public void Tick()
    {
        // Update ghost position
        // Validate placement
        // Detect drag start (optional later)
    }

    public void Exit()
    {
        // Hide ghost
        // Unsubscribe input
    }
}
