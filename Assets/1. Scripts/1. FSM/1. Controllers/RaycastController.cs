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

    [Header("Object Ray Debug")]
    [SerializeField] private bool _debugObjectRay = true;
    [SerializeField] private Color _objectRayColor = Color.cyan;
    [SerializeField] private Color _objectHitColor = Color.magenta;

    [Header("Cell Ray Debug")]
    [SerializeField] private bool _debugCellRay = true;
    [SerializeField] private Color _cellRayColor = Color.yellow;
    [SerializeField] private Color _cellHitColor = Color.green;

    public bool HasHit { get; private set; }
    public Vector3 HitPoint { get; private set; }
    public Vector2Int HitCell { get; private set; }
    public GameObject HitObject { get; private set; }

    private Vector2Int _lastHitCell;
    private bool _enabled;

    public void EnableRay() => _enabled = true;

    public void DisableRay()
    {
        _enabled = false;

        if (_line != null)
            _line.enabled = false;

        HasHit = false;
        HitObject = null;
    }

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_line != null)
            _line.enabled = false;
    }

    public void Tick()
    {
        if (!_enabled) {
            return; }

        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // ---------------------------------------------------------
        // 1. Ground raycast (grid placement)
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
        // 2. Object raycast (no mask)
        // ---------------------------------------------------------
        if (Physics.Raycast(ray, out RaycastHit objHit, 100f))
            HitObject = objHit.collider.gameObject;
        else
            HitObject = null;

        DrawRay(ray);

        // ---------------------------------------------------------
        // Debug object ray
        // ---------------------------------------------------------
        if (_debugObjectRay)
        {
            Vector3 start = ray.origin;
            Vector3 end = start + ray.direction * 100f;

            Debug.DrawLine(start, end, _objectRayColor, 0f);

            if (HitObject != null)
                Debug.DrawLine(start, HitObject.transform.position, _objectHitColor, 0f);
        }
    }

    private void DrawRay(Ray ray)
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

    public GameObject RaycastCellCenter(Vector2Int cell)
    {
        Vector3 world = _grid.GetCellCenter(cell) + Vector3.up * 5f;
        Ray ray = new Ray(world, Vector3.down);

        const float distance = 10f;

        if (_debugCellRay)
            Debug.DrawLine(world, world + Vector3.down * distance, _cellRayColor, 0f);

        if (Physics.Raycast(ray, out RaycastHit hit, distance))
        {
            if (_debugCellRay)
            {
                Debug.DrawLine(world, hit.point, _cellHitColor, 0f);
                DebugDrawSphere(hit.point, 0.1f, _cellHitColor);
            }

            return hit.collider.gameObject;
        }

        return null;
    }

    private void DebugDrawSphere(Vector3 pos, float radius, Color color)
    {
        Debug.DrawLine(pos + Vector3.up * radius, pos - Vector3.up * radius, color, 0f);
        Debug.DrawLine(pos + Vector3.right * radius, pos - Vector3.right * radius, color, 0f);
        Debug.DrawLine(pos + Vector3.forward * radius, pos - Vector3.forward * radius, color, 0f);
    }
}
