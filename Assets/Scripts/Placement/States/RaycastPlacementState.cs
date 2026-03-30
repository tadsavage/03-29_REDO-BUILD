using UnityEngine;

public class RaycastPlacementState : IPlacementState
{
    private RaycastController _raycast;
    private CellIndicatorController _indicator;


    public RaycastPlacementState(RaycastController raycast, CellIndicatorController indicator)
    {
        _raycast = raycast;
        _indicator = indicator;
        _indicator = indicator;
    }

    public bool IsPlacementState { get { return true; } }

    public void OnEnter()
    {
        _raycast.EnableRay();
    }

    public void OnExit()
    {
        _raycast.DisableRay();
    }

    public void Tick()
    {

        _raycast.Tick();

        if (_raycast.HasHit)
        {

            Vector2Int cell = _raycast.HitCell;
            _indicator.ShowAtCell(cell);
        }
        else
        {
            _indicator.Hide();
        }


    }
}


