using UnityEngine;
using UnityEngine.InputSystem;

public class RaycastController : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private PlacementGrid _grid;

    [Header("Debug")]
    [SerializeField] private LineRenderer _line;
    [SerializeField] private bool _visualizeRay = true;

    public bool HasHit { get; private set; }
    public Vector3 HitPoint { get; private set; }
    public Vector2Int HitCell { get; private set; }

    private bool _enabled;

    public void EnableRay() => _enabled = true;
    public void DisableRay() => _enabled = false;
    
    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_line != null)
            _line.enabled = false;
    }

    public void Tick()
    {
        if (!_enabled)
        {
            HasHit = false;
            if (_line != null) _line.enabled = false;
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
        DrawRay();
    }
    private void DrawRay()
    {
        if (!_visualizeRay || _line == null)
            return;

        if (!HasHit)
        {
            _line.enabled = false;
            return;
        }

        _line.enabled = true;
        _line.positionCount = 2;
        Vector3 start = _camera.transform.position - _camera.transform.up * .01f;
        _line.SetPosition(0, start);
        _line.SetPosition(1, HitPoint);
    }
}
