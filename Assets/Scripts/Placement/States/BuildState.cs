public class BuildState : IPlacementState
{
    private ObjDataSO _currentData;
    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;

    public BuildState(
        PlacementActions actions,
        PreviewController preview,
        PlacementValidator validator,
        PlacementFinalizer finalizer,
        PlacementGrid grid,
        PlacementStateMachine fsm)
    {
        _actions = actions;
        _preview = preview;
        _validator = validator;
        _finalizer = finalizer;
        _grid = grid;
        _fsm = fsm;
    }
    public bool IsPlacementState
    {
        get { return true; }
    }

    public void OnEnter()
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

    public void OnExit()
    {
        // Hide ghost
        // Unsubscribe input
    }
    public void SetData(ObjDataSO data)
    {   // Store the data for use in placement logic
        _currentData = data;
    }

}