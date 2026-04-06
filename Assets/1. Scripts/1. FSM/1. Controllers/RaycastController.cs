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

    // Used to detect cell changes (for audio, events, etc.)
    private Vector2Int _lastHitCell;

    // Whether raycasting is active
    private bool _enabled;

    // =========================================================
    //  ENABLE / DISABLE
    // =========================================================
    public void EnableRay() => _enabled = true;

    public void DisableRay()
    {
        _enabled = false;

        // Immediately hide line renderer
        if (_line != null)
            _line.enabled = false;

        HasHit = false;
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

        // ---------------------------------------------------------
        // RAYCAST FROM MOUSE POSITION
        // ---------------------------------------------------------
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, _groundMask))
        {
            HasHit = true;
            HitPoint = hit.point;

            // Convert world hit to grid cell
            HitCell = _grid.WorldToCell(hit.point);

            // ================================
            // CELL CHANGE EVENT (audio, etc.)
            // ================================
            if (HitCell != _lastHitCell)
                AudioManager.Play("NewCell");

            _lastHitCell = HitCell;
        }
        else
        {
            HasHit = false;
        }

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

        // Slight offset to avoid z‑fighting with camera plane
        Vector3 start = _camera.transform.position - _camera.transform.up * 0.01f;

        _line.SetPosition(0, start);
        _line.SetPosition(1, HitPoint);
    }
}
