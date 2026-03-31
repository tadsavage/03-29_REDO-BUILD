using UnityEngine;
using UnityEngine.InputSystem;

public class RaycastController : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private PlacementGrid _grid;

    public bool HasHit { get; private set; }
    public Vector3 HitPoint { get; private set; }
    public Vector2Int HitCell { get; private set; }

    private bool _enabled;

    public void EnableRay() => _enabled = true;
    public void DisableRay() => _enabled = false;

    public void Tick()
    {
        if (!_enabled)
        {
            HasHit = false;
            return;
        }

        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _groundMask))
        {
            HasHit = true;
            HitPoint = hit.point;
            HitCell = _grid.WorldToCell(hit.point);
        }
        else
        {
            HasHit = false;
        }
    }
}
