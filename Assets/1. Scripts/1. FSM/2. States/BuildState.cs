using UnityEngine;

public class BuildState : IPlacementState
{
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;

    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;

    private ObjDataSO _currentData;

    public BuildState(
        PlacementActions actions,
        PreviewController preview,
        PlacementValidator validator,
        PlacementFinalizer finalizer,
        PlacementGrid grid,
        PlacementStateMachine fsm,
        RaycastController raycast,
        CellIndicatorController indicator )
    {
        _actions = actions;
        _preview = preview;
        _validator = validator;
        _finalizer = finalizer;
        _grid = grid;
        _fsm = fsm;
        _raycast = raycast;
        _indicator = indicator;
    }
    public bool IsPlacementState
    {
        get { return true; }
    }

    public void OnEnter()
    {
        if (_currentData == null)
        {
            return;
        }        
        _raycast.EnableRay();

        // Show preview ghost
        _preview.Show(_currentData);
    }

    public void Tick()
    {
        _raycast.Tick();

        if (_raycast.HasHit)
        {
            // Move Cell Indicator
            _indicator.ShowAtCell(_raycast.HitCell);
            // Move preview ghost
            //_preview.MoveTo(_raycast.HitPoint);

            //OLD WAY - Move preview ghost to cell center
            //Vector3 worldPos = _grid.CellToWorld(_raycast.HitCell);
            _preview.MoveTo(_grid.GetCellCenter(_raycast.HitCell));
        }
        else
        {
            _indicator.Hide();
            _preview.Hide();
        }
    }
    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.Hide();
        _preview.Hide();
    }
    public void SetBuildData(ObjDataSO data)
    {        
        // Additional setup if needed when entering BuildState
        SetData(data);
    }
    public void SetData(ObjDataSO data)
    {   
        // Store the data for use in placement logic
        _currentData = data;
    }
}