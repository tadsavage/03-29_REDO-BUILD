using UnityEngine;
using UnityEngine.InputSystem;

public class RaycastController : MonoBehaviour
{
    // =========================================================
    //  CONFIGURATION
    // =========================================================
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private PlacementGrid _grid;

    [Header("Debug")]
    [SerializeField] private LineRenderer _line;
    [SerializeField] private bool _visualizeRay = true;

    // =========================================================
    //  PUBLIC HIT DATA
    // =========================================================
    public bool HasHit { get; private set; }
    public Vector3 HitPoint { get; private set; }
    public Vector2Int HitCell { get; private set; }

    // NEW: Object hit by the mouse ray
    public GameObject HitObject { get; private set; }

    private Vector2Int _lastHitCell;
    private bool _enabled;

    // =========================================================
    //  ENABLE / DISABLE
    // =========================================================
    public void EnableRay() => _enabled = true;

    public void DisableRay()
    {
        _enabled = false;

        if (_line != null)
            _line.enabled = false;

        HasHit = false;
        HitObject = null;
    }

    // =========================================================
    //  INITIALIZATION
    // =========================================================
    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_line != null)
            _line.enabled = false;
    }

    // =========================================================
    //  MAIN UPDATE (CALLED FROM FSM)
    // =========================================================
    public void Tick()
    {
        if (!_enabled)
            return;

        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // ---------------------------------------------------------
        // 1. RAYCAST FOR GROUND (grid placement)
        // ---------------------------------------------------------
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _groundMask))
        {
            HasHit = true;
            HitPoint = hit.point;
            HitCell = _grid.WorldToCell(hit.point);

            if (HitCell != _lastHitCell)
                AudioManager.Play("NewCell");

            _lastHitCell = HitCell;
        }
        else
        {
            HasHit = false;
        }

        // ---------------------------------------------------------
        // 2. RAYCAST FOR OBJECTS (free-moving, vehicles, etc.)
        // ---------------------------------------------------------
        if (Physics.Raycast(ray, out RaycastHit objHit, 100f))
            HitObject = objHit.collider.gameObject;
        else
            HitObject = null;

        DrawRay();
    }

    // =========================================================
    //  RAY VISUALIZATION
    // =========================================================
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

        Vector3 start = _camera.transform.position - _camera.transform.up * 0.01f;

        _line.SetPosition(0, start);
        _line.SetPosition(1, HitPoint);
    }

    // =========================================================
    //  PUBLIC: Raycast at a specific grid cell center
    // =========================================================
    public GameObject RaycastCellCenter(Vector2Int cell)
    {
        Vector3 world = _grid.GetCellCenter(cell) + Vector3.up * 5f;
        Ray ray = new Ray(world, Vector3.down);

        return RaycastFrom(ray, 10f);
    }

    // =========================================================
    //  PRIVATE: Shared raycast logic
    // =========================================================
    private GameObject RaycastFrom(Ray ray, float distance)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, distance))
            return hit.collider.gameObject;

        return null;
    }
}
