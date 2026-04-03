using UnityEngine;

public class RaycastPlacementState : IPlacementState
{
    private RaycastController _raycast;
    private CellIndicatorController _indicator;


    public RaycastPlacementState(RaycastController raycast, CellIndicatorController indicator)
    {
        _raycast = raycast;
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
        _indicator.Hide();
    }

    public void Tick()
    {
        
        _raycast.Tick();

        if (_raycast.HasHit)
        {
            _indicator.ShowAtCell(_raycast.HitCell);
        }
        else
        {
            _indicator.Hide();
        }
    }
}


