using UnityEngine;

public class RaycastPlacementState : IPlacementState
{
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;
    private readonly PlacementGrid _grid;

    public RaycastPlacementState(RaycastController raycast, CellIndicatorController indicator, PlacementGrid grid)
    {
        _raycast = raycast;
        _indicator = indicator;
        _grid = grid;
    }

    public bool IsPlacementState => true;

    public void OnEnter()
    {
        _raycast.EnableRay();
    }

    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.Hide();
    }

    public void Tick()
    {
        _raycast.Tick();

        if (_raycast.HasHit)
        {
            Vector2Int cell = _raycast.HitCell;

            _indicator.ShowCells(
                cell,
                new Vector2Int[] { Vector2Int.zero },
                _grid,
                true
            );
        }
        else
        {
            _indicator.Hide();
        }
    }
}
