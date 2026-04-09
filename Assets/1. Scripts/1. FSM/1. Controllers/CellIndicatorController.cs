using UnityEngine;
using System.Collections.Generic;

public class CellIndicatorController : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color validColor = new(.25f, 1f, .30f, .50f);
    [SerializeField] private Color invalidColor = new(1f, .22f, .22f, .75f);
    [SerializeField] private Color deleteColor = new(1f, 1f, .20f, .60f);   // REM: yellow for delete mode

    [Header("Indicator Prefab")]
    [SerializeField] private GameObject indicatorPrefab;

    [SerializeField] private PlacementGrid grid;

    // REM: Tiny lift above stack so tile doesn't Z‑fight
    [SerializeField] private float yOffset = 0.15f;

    [Header("Vertical Line Settings")]
    [SerializeField] private LineRenderer linePrefab;
    [SerializeField, Range(0f, 1f)]
    private float criticalHeightRatio = 0.9f;   // REM: turns line yellow when near max stack

    // REM: Pools for indicators and lines
    private readonly List<GameObject> _active = new();
    private readonly Stack<GameObject> _pool = new();
    private readonly List<LineRenderer> _activeLines = new();
    private readonly Stack<LineRenderer> _linePool = new();

    private MaterialPropertyBlock _mpb;

    // REM: Mode flag so we can override color in delete mode
    private bool _deleteMode = false;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    // =========================================================
    //  MODE
    // =========================================================
    public void SetDeleteMode(bool on)
    {
        _deleteMode = on;
        ClearAll();   // REM: avoid mixing build + delete visuals
    }

    // =========================================================
    //  PUBLIC API
    // =========================================================

    /// <summary>
    /// Multi‑cell indicator for placement footprints.
    /// </summary>
    public void ShowCells(Vector2Int root, Vector2Int[] offsets, PlacementGrid grid, bool isValid)
    {
        ClearActive();

        Color baseColor = GetBaseColor(isValid);

        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;

            float stackY = grid.GetStackHeight(cell);
            float maxY = grid.maxStackHeight;
            float ratio = (maxY > 0f) ? (stackY / maxY) : 0f;
            bool isCritical = ratio >= criticalHeightRatio;

            Vector3 pos = grid.GetCellCenter(cell);
            pos.y += stackY + yOffset;

            GameObject ind = GetIndicator();
            ind.transform.position = pos;
            ApplyColor(ind, baseColor);
            _active.Add(ind);

            Vector3 floorPos = grid.GetCellCenter(cell);
            floorPos.y = 0f;

            Color lineColor = isCritical ? Color.yellow : Color.white;
            ShowVerticalLine(floorPos, stackY, lineColor);
        }
    }

    /// <summary>
    /// Single‑cell indicator (used for hover / delete).
    /// </summary>
    public void ShowCell(Vector2Int cell, bool isValid)
    {
        float stackY = grid.GetStackHeight(cell);
        float maxY = grid.maxStackHeight;
        float ratio = (maxY > 0f) ? (stackY / maxY) : 0f;
        bool isCritical = ratio >= criticalHeightRatio;

        GameObject ind = GetIndicator();
        Vector3 pos = grid.GetCellCenter(cell);
        pos.y += stackY + yOffset;
        ind.transform.position = pos;

        ApplyColor(ind, GetBaseColor(isValid));
        _active.Add(ind);

        Vector3 floorPos = grid.GetCellCenter(cell);
        floorPos.y = 0f;

        Color lineColor = isCritical ? Color.yellow : Color.white;
        ShowVerticalLine(floorPos, stackY, lineColor);
    }

    public void Hide()
    {
        ClearActive();
    }

    public void ClearAll()
    {
        ClearActive();
    }

    // =========================================================
    //  INTERNAL HELPERS
    // =========================================================

    private Color GetBaseColor(bool isValid)
    {
        if (_deleteMode)
            return deleteColor;

        return isValid ? validColor : invalidColor;
    }

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

        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.positiveInfinity);
        lr.SetPosition(1, Vector3.positiveInfinity);

        return lr;
    }

    private void ShowVerticalLine(Vector3 floorPos, float height, Color color)
    {
        LineRenderer lr = GetLine();
        _activeLines.Add(lr);

        Vector3 start = floorPos;
        Vector3 end = floorPos + new Vector3(0, height + 2.5f, 0);

        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        lr.startColor = lr.endColor = color;
    }

    private void ClearActive()
    {
        foreach (var ind in _active)
        {
            ind.SetActive(false);
            _pool.Push(ind);
        }
        _active.Clear();

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
        if (!renderer) return;

        renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(_mpb);
    }
}
