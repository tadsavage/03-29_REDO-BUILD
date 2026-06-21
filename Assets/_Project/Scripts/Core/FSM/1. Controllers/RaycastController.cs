using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class RaycastController : MonoBehaviour
{
    public bool AllowPlacementEvents { get; set; } = false;

    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private PlacementGrid _grid;

    [Header("Walls Bypass Configuration")]
    [Tooltip("Select the exact same Walls layer you created here so the raycaster can safely ignore it.")]
    [SerializeField] private LayerMask _wallLayer;

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
    public Vector3 RawHitPoint { get; private set; }

    private bool _isPointerOverUI;
    public bool IsPointerOverUI => _isPointerOverUI;

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

    // Cached for the IsPointerOverBuildMenu check — the most reliable way to know
    // if the cursor is anywhere over the bottom bar (including empty space between buttons).
    private BuildMenuUI _buildMenuUI;

    private void Start()
    {
        _buildMenuUI = FindAnyObjectByType<BuildMenuUI>();

        // Unity wraps UXML in a TemplateContainer child of rootVisualElement.
        // Both root and TemplateContainer must be Ignore — otherwise PanelRaycaster
        // hits the full-screen TemplateContainer and blocks all game-world raycasts.
        foreach (var doc in FindObjectsByType<UIDocument>())
        {
            if (doc.rootVisualElement == null) continue;
            doc.rootVisualElement.pickingMode = PickingMode.Ignore;
            foreach (var child in doc.rootVisualElement.Children())
                child.pickingMode = PickingMode.Ignore;
        }
    }

    public void Tick()
    {
        _isPointerOverUI = CheckIfPointerOverUI();

        if (_isPointerOverUI && _enabled && Mouse.current.leftButton.wasPressedThisFrame)
        {
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            var eventData = new UnityEngine.EventSystems.PointerEventData(EventSystem.current);
            eventData.position = Mouse.current.position.ReadValue();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var res in results)
            {
                //Debug.Log($"[RaycastController] Blocked by UI: {res.gameObject.name} (Module: {res.module.GetType().Name})", res.gameObject);
            }
        }

        if (!_enabled || _isPointerOverUI)
        {
            HasHit = false;
            HitObject = null;
            if (_line != null) _line.enabled = false;
            return;
        }

        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // DYNAMIC FILTER: Get dynamic layer mask filters based on current wall visibility state
        LayerMask dynamicGroundMask = _groundMask;
        LayerMask dynamicObjectMask = Physics.DefaultRaycastLayers; // Standard default layout mask matches everything

        if (WallVisibilityManager.Instance != null)
        {
            dynamicGroundMask = WallVisibilityManager.Instance.GetDynamicPlacementMask(_groundMask);
            dynamicObjectMask = WallVisibilityManager.Instance.GetDynamicPlacementMask(Physics.DefaultRaycastLayers);
        }
        else
        {
            // Manual fallback if your scene manager instance hasn't loaded yet
            dynamicGroundMask = _groundMask & ~_wallLayer;
            dynamicObjectMask = Physics.DefaultRaycastLayers & ~_wallLayer;
        }

        // ---------------------------------------------------------
        // 1. Ground raycast (grid placement) using dynamic filter mask
        // ---------------------------------------------------------
        if (Physics.Raycast(ray, out RaycastHit hit, 999f, dynamicGroundMask))
        {
            RawHitPoint = hit.point;   // ⭐ continuous world position
            HitPoint = _grid.GetCellCenter(_grid.WorldToCell(hit.point)); // snapped
            HasHit = true;
            HitPoint = hit.point;
            HitCell = _grid.WorldToCell(hit.point);
            HitObject = hit.collider.gameObject;

            if (AllowPlacementEvents && HitCell != _lastHitCell)
                AudioManager.Play("NewCell");

            _lastHitCell = HitCell;
        }
        else
        {
            HasHit = false;
        }

        // ---------------------------------------------------------
        // 2. Object raycast (now uses dynamic filter mask to ignore lowered walls!)
        // ---------------------------------------------------------
        if (Physics.Raycast(ray, out RaycastHit objHit, 500f, dynamicObjectMask))
        {
            var hitGO = objHit.collider.gameObject;
            // If we hit a parent collider (like "Foundations"), query the grid to get the actual object
            if (hitGO.GetComponent<BuildingData>() == null && hitGO.name == "Foundations")
            {
                var cellObjs = _grid.GetObjectsInCell(HitCell);
                if (cellObjs != null && cellObjs.Count > 0)
                {
                    // Return topmost object (skip floor tiles)
                    for (int i = cellObjs.Count - 1; i >= 0; i--)
                    {
                        if (cellObjs[i].data != null && !cellObjs[i].data.isFloor && cellObjs[i].instance != null)
                        {
                            HitObject = cellObjs[i].instance;
                            return;
                        }
                    }
                }
                HitObject = null;
            }
            else
            {
                HitObject = hitGO;
            }
        }
        else
            HitObject = null;

        if (AllowPlacementEvents)
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

    private bool CheckIfPointerOverUI()
    {
        // Most reliable: the build menu tracks PointerEnter/Leave on the bar element itself.
        if (_buildMenuUI != null && _buildMenuUI.IsPointerOverBuildMenu) return true;

        // Direct bounds check for the Tools Window — more reliable than panel.Pick()
        // for dynamically-built scroll content (dev settings toggles etc.)
        if (ToolsWindowController.Instance != null &&
            ToolsWindowController.Instance.IsPointerOverWindow()) return true;

        // UI Toolkit panel.Pick() for other floating panels
        if (UIInputGuard.IsPointerOverUIToolkit()) return true;

        // Legacy UGUI fallback
        if (EventSystem.current == null) return false;
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        var eventData = new UnityEngine.EventSystems.PointerEventData(EventSystem.current);
        eventData.position = Mouse.current.position.ReadValue();
        EventSystem.current.RaycastAll(eventData, results);
        if (results.Count == 0) return false;
        return results[0].module is UnityEngine.UI.GraphicRaycaster;
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

        // Optional layer filter matching for down-facing tile sweeps
        LayerMask dynamicCellMask = Physics.DefaultRaycastLayers;
        if (WallVisibilityManager.Instance != null)
        {
            dynamicCellMask = WallVisibilityManager.Instance.GetDynamicPlacementMask(Physics.DefaultRaycastLayers);
        }

        if (Physics.Raycast(ray, out RaycastHit hit, distance, dynamicCellMask))
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

    public void ResetHitData()
    {
        HasHit = false;
        HitObject = null;
        HitCell = Vector2Int.zero;
        _lastHitCell = new Vector2Int(999, 999);
    }
}
