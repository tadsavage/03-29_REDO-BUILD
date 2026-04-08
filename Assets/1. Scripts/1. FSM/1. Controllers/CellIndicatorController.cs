using UnityEngine;
using System.Collections.Generic;

public class CellIndicatorController : MonoBehaviour
{
    [Header("Colors")]
    private Color validColor = new Color(.25f, 1f, .30f, .50f);
    private Color invalidColor = new Color(1f, .22f, .22f, .75f);

    [Header("Indicator Prefab")]
    [SerializeField] private GameObject indicatorPrefab;

    [SerializeField] private PlacementGrid grid;

    // REM: This is the tiny lift above the stack base so the tile doesn't Z-fight
    [SerializeField] private float yOffset = 0.15f;

    // ---------------------------------------------------------
    // REM: Vertical Line Settings (NEW)
    // ---------------------------------------------------------
    [Header("Vertical Line Settings")]
    [SerializeField] private LineRenderer linePrefab;   // REM: Prefab for dashed vertical line
    [SerializeField, Range(0f, 1f)]
    private float criticalHeightRatio = 0.9f;           // REM: Turns yellow when stack ratio >= 0.9

    // REM: Pools for indicators and lines
    private readonly List<GameObject> _active = new();
    private readonly Stack<GameObject> _pool = new();

    private readonly List<LineRenderer> _activeLines = new();
    private readonly Stack<LineRenderer> _linePool = new();

    private MaterialPropertyBlock _mpb;

    // For single-cell mode
    private Vector2Int _lastRoot = new Vector2Int(int.MinValue, int.MinValue);

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    // ---------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------

    /// <summary>
    /// Multi-cell indicator for drag placement.
    /// Each cell gets its own indicator tile.
    /// </summary>
    public void ShowCells(Vector2Int root, Vector2Int[] offsets, PlacementGrid grid, bool isValid)
    {
        _lastRoot = root;

        ClearActive();

        Color color = isValid ? validColor : invalidColor;

        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;

            // REM: Compute stack height for this cell
            float stackY = grid.GetStackHeight(cell);
            float maxY = grid.maxStackHeight;

            // REM: Height ratio for critical warning
            float ratio = (maxY > 0f) ? (stackY / maxY) : 0f;
            bool isCritical = ratio >= criticalHeightRatio;

            // REM: Position indicator at stack base
            Vector3 pos = grid.GetCellCenter(cell);
            pos.y += stackY + yOffset;

            GameObject ind = GetIndicator();
            ind.transform.position = pos;
            ApplyColor(ind, color);
            _active.Add(ind);

            // REM: Draw vertical dashed line from floor → stack base
            Vector3 floorPos = grid.GetCellCenter(cell);
            floorPos.y = 0f;

            Color lineColor = isCritical ? Color.yellow : Color.white;
            ShowVerticalLine(floorPos, stackY, lineColor);
        }
    }

    /// <summary>
    /// Show a single indicator tile for a specific cell.
    /// Used for drag rectangle placement.
    /// </summary>
    public void ShowCell(Vector2Int cell, bool isValid)
    {
        // REM: Compute stack height
        float stackY = grid.GetStackHeight(cell);
        float maxY = grid.maxStackHeight;

        // REM: Height ratio for critical warning
        float ratio = (maxY > 0f) ? (stackY / maxY) : 0f;
        bool isCritical = ratio >= criticalHeightRatio;

        // REM: Position indicator at stack base
        GameObject ind = GetIndicator();
        Vector3 pos = grid.GetCellCenter(cell);
        pos.y += stackY + yOffset;
        ind.transform.position = pos;

        ApplyColor(ind, isValid ? validColor : invalidColor);
        _active.Add(ind);

        // REM: Draw vertical dashed line
        Vector3 floorPos = grid.GetCellCenter(cell);
        floorPos.y = 0f;

        Color lineColor = isCritical ? Color.yellow : Color.white;
        ShowVerticalLine(floorPos, stackY, lineColor);
    }

    public void Hide()
    {
        ClearActive();
        _lastRoot = new Vector2Int(int.MinValue, int.MinValue);
    }

    public void ClearAll()
    {
        ClearActive();
        _lastRoot = new Vector2Int(int.MinValue, int.MinValue);
    }

    // ---------------------------------------------------------
    // INTERNAL HELPERS
    // ---------------------------------------------------------

    private GameObject GetIndicator()
    {
        if (_pool.Count > 0)
        {
            var go = _pool.Pop();
            go.SetActive(true);
            return go;
        }

        return Instantiate(indicatorPrefab);
    }

    private LineRenderer GetLine()
    {
        LineRenderer lr;

        if (_linePool.Count > 0)
        {
            lr = _linePool.Pop();
            lr.gameObject.SetActive(true);
        }
        else
        {
            lr = Instantiate(linePrefab);
        }

        // ⭐ CRITICAL FIX: initialize positions so Unity doesn't draw at (0,0,0)
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.positiveInfinity);
        lr.SetPosition(1, Vector3.positiveInfinity);

        return lr;
    }

    private void ShowVerticalLine(Vector3 floorPos, float height, Color color)
    {
        LineRenderer lr = GetLine();
        _activeLines.Add(lr);

        // REM: Set dashed line start/end
        Vector3 start = floorPos;
        Vector3 end = floorPos + new Vector3(0, height + 2.5f, 0);

        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        // REM: Apply color
        lr.startColor = lr.endColor = color;
    }

    private void ClearActive()
    {
        // REM: Clear indicator tiles
        foreach (var ind in _active)
        {
            ind.SetActive(false);
            _pool.Push(ind);
        }
        _active.Clear();

        // REM: Clear vertical lines
        foreach (var lr in _activeLines)
        {
            lr.gameObject.SetActive(false);
            _linePool.Push(lr);
        }
        _activeLines.Clear();
    }

    private void ApplyColor(GameObject ind, Color color)
    {
        var renderer = ind.GetComponent<Renderer>();
        renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(_mpb);
    }
}
