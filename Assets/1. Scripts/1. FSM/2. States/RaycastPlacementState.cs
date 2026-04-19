using UnityEngine;
using System.Collections.Generic;

public class RaycastPlacementState : IPlacementState
{
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;
    private readonly PlacementGrid _grid;

    // Reusable buffer (no allocations)
    private readonly List<Vector2Int> _singleCell = new(1);

    public bool IsPlacementState => true;

    public RaycastPlacementState(
        RaycastController raycast,
        CellIndicatorController indicator,
        PlacementGrid grid)
    {
        _raycast = raycast;
        _indicator = indicator;
        _grid = grid;
    }

    public void OnEnter()
    {
        _raycast.EnableRay();
        _indicator.UseBuildMode(); // neutral mode
    }

    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.ClearAll();
    }

    public void Tick()
    {
        _raycast.Tick();

        if (!_raycast.HasHit)
        {
            _indicator.ClearAll();
            //return;
        }

        Vector2Int cell = _raycast.HitCell;

        _singleCell.Clear();
        _singleCell.Add(cell);

        _indicator.ShowCells(
        _singleCell,
        cell => true   // always valid in raycast hover mode
);
    }
}
