using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PlacementGrid: MonoBehaviour
{
    [Header("Grid Settings")]
    public float CellSize = 1.33f;   // 5ft
    public int Width = 50;
    public int Height = 50;
    public Vector3 Origin = Vector3.zero;

    [Header("Visualizer Settings")]
    public bool UseVisualizer = true;
    [Tooltip("Optional material for cell quads. If null, a default material will be created.")]
    public Material CellMaterial;
    [Tooltip("Parent transform for pooled visuals (optional).")]
    public Transform VisualParent;

    [Header("Colors")]
    public Color FreeColor = new Color(0f, 0f, 0f, 0f); // transparent by default
    public Color OccupiedColor = new Color(1f, 0.4f, 0.4f, 0.6f);
    public Color SelectedColor = new Color(0.4f, 1f, 0.4f, 0.6f);

    // occupancy map: 0 = free, >0 = occupied (owner id)
    private int[,] _occupancy;

    // pooled visuals keyed by cell index (x + y * Width)
    private Dictionary<int, GameObject> _activeVisuals;
    private Stack<GameObject> _pool;

    public event Action OnGridInitialized;

    private void Awake()
    {
        InitializeGrid();
    }

    private void OnValidate()
    {
        if (Width < 1) Width = 1;
        if (Height < 1) Height = 1;
        if (CellSize <= 0f) CellSize = 1.0f;
        InitializeGrid();
    }

    public void InitializeGrid()
    {
        _occupancy = new int[Width, Height];

        if (UseVisualizer)
        {
            if (_activeVisuals == null) _activeVisuals = new Dictionary<int, GameObject>();
            if (_pool == null) _pool = new Stack<GameObject>();
            // Optionally clear visuals when grid reinitializes
            ClearAllVisuals();
        }

        OnGridInitialized?.Invoke();
    }

    #region Grid API

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = worldPos - Origin;
        int x = Mathf.FloorToInt(local.x / CellSize);
        int z = Mathf.FloorToInt(local.z / CellSize);
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return Origin + new Vector3(cell.x * CellSize, 0f, cell.y * CellSize);
    }

    public Vector3 GetCellCenter(Vector2Int cell)
    {
        Vector3 corner = CellToWorld(cell);
        return corner + new Vector3(CellSize * 0.5f, 0f, CellSize * 0.5f);
    }

    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
    }

    public bool IsOccupied(Vector2Int originCell, Vector2Int size)
    {
        for (int x = originCell.x; x < originCell.x + size.x; x++)
        {
            for (int y = originCell.y; y < originCell.y + size.y; y++)
            {
                if (!IsInside(new Vector2Int(x, y))) return true;
                if (_occupancy[x, y] != 0) return true;
            }
        }
        return false;
    }

    public bool TryOccupy(Vector2Int originCell, Vector2Int size, int ownerId)
    {
        if (IsOccupied(originCell, size)) return false;
        for (int x = originCell.x; x < originCell.x + size.x; x++)
            for (int y = originCell.y; y < originCell.y + size.y; y++)
                _occupancy[x, y] = ownerId;

        if (UseVisualizer)
            MarkRegionVisual(originCell, size, OccupiedColor);

        return true;
    }

    public int ReleaseByOwner(int ownerId)
    {
        int freed = 0;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_occupancy[x, y] == ownerId)
                {
                    _occupancy[x, y] = 0;
                    freed++;
                    if (UseVisualizer) SetCellVisual(new Vector2Int(x, y), FreeColor);
                }
        return freed;
    }

    public int[,] GetOccupancyCopy()
    {
        var copy = new int[Width, Height];
        Array.Copy(_occupancy, copy, _occupancy.Length);
        return copy;
    }

    #endregion

    #region Visualizer Pooling

    private int CellIndex(Vector2Int cell) => cell.x + cell.y * Width;

    private void EnsureMaterial()
    {
        if (CellMaterial != null) return;
        // create a simple transparent material if none assigned
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        CellMaterial = new Material(shader);
    }

    private GameObject GetPooledVisual()
    {
        if (_pool == null) _pool = new Stack<GameObject>();
        if (_pool.Count > 0)
        {
            var go = _pool.Pop();
            go.SetActive(true);
            return go;
        }

        // create new quad
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "CellVisual";
        // rotate to lie flat on XZ plane
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        if (VisualParent != null) quad.transform.SetParent(VisualParent, true);
        // remove collider to avoid physics overhead
        var col = quad.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        EnsureMaterial();
        var rend = quad.GetComponent<MeshRenderer>();
        if (rend != null) rend.sharedMaterial = CellMaterial;
        return quad;
    }

    private void ReturnToPool(GameObject go)
    {
        go.SetActive(false);
        _pool.Push(go);
    }

    private void SetCellVisual(Vector2Int cell, Color color)
    {
        if (!UseVisualizer) return;
        if (!IsInside(cell)) return;

        int idx = CellIndex(cell);
        if (_activeVisuals == null) _activeVisuals = new Dictionary<int, GameObject>();

        if (!_activeVisuals.TryGetValue(idx, out var go))
        {
            go = GetPooledVisual();
            _activeVisuals[idx] = go;
        }

        go.transform.position = GetCellCenter(cell) + new Vector3(0f, 0.01f, 0f); // slight offset to avoid z-fighting
        go.transform.localScale = new Vector3(CellSize, CellSize, 1f);

        var rend = go.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            if (rend.sharedMaterial == null) EnsureMaterial();
            // set color on material instance to avoid tinting other quads if using sharedMaterial
            rend.material.color = color;
        }
    }

    private void ClearAllVisuals()
    {
        if (_activeVisuals == null) return;
        foreach (var kv in _activeVisuals)
        {
            if (kv.Value != null) DestroyImmediate(kv.Value);
        }
        _activeVisuals.Clear();
        _pool?.Clear();
    }

    private void MarkRegionVisual(Vector2Int originCell, Vector2Int size, Color color)
    {
        for (int x = originCell.x; x < originCell.x + size.x; x++)
            for (int y = originCell.y; y < originCell.y + size.y; y++)
                SetCellVisual(new Vector2Int(x, y), color);
    }

    /// <summary>
    /// Call to highlight a single cell as selected (e.g., during drag selection).
    /// </summary>
    public void HighlightCell(Vector2Int cell)
    {
        SetCellVisual(cell, SelectedColor);
    }

    /// <summary>
    /// Clear visual for a single cell (returns it to pool).
    /// </summary>
    public void ClearCellVisual(Vector2Int cell)
    {
        if (_activeVisuals == null) return;
        int idx = CellIndex(cell);
        if (_activeVisuals.TryGetValue(idx, out var go))
        {
            _activeVisuals.Remove(idx);
            ReturnToPool(go);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (_occupancy == null) InitializeGrid();

        Gizmos.color = Color.gray;
        for (int x = 0; x <= Width; x++)
        {
            Vector3 a = Origin + new Vector3(x * CellSize, 0f, 0f);
            Vector3 b = Origin + new Vector3(x * CellSize, 0f, Height * CellSize);
            Gizmos.DrawLine(a, b);
        }
        for (int y = 0; y <= Height; y++)
        {
            Vector3 a = Origin + new Vector3(0f, 0f, y * CellSize);
            Vector3 b = Origin + new Vector3(Width * CellSize, 0f, y * CellSize);
            Gizmos.DrawLine(a, b);
        }
    }// Selection state
    private HashSet<int> _selectedCells = new HashSet<int>();
    private Vector2Int _selectionStart;
    private bool _isSelecting = false;
    public Color SelectionPreviewColor = new Color(0.2f, 0.6f, 1f, 0.45f); // blue-ish

    /// <summary>
    /// Start a drag selection at startCell.
    /// </summary>
    public void BeginSelection(Vector2Int startCell)
    {
        if (!UseVisualizer) return;
        _selectionStart = startCell;
        _isSelecting = true;
        ClearSelection(); // ensure clean start
        UpdateSelection(startCell);
    }

    /// <summary>
    /// Update the current selection rectangle to include start->current.
    /// </summary>
    public void UpdateSelection(Vector2Int currentCell)
    {
        if (!UseVisualizer || !_isSelecting) return;

        // compute rectangle bounds
        int minX = Mathf.Min(_selectionStart.x, currentCell.x);
        int maxX = Mathf.Max(_selectionStart.x, currentCell.x);
        int minY = Mathf.Min(_selectionStart.y, currentCell.y);
        int maxY = Mathf.Max(_selectionStart.y, currentCell.y);

        // determine which cells should be selected now
        var newSelected = new HashSet<int>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!IsInside(cell)) continue;
                newSelected.Add(CellIndex(cell));
            }
        }

        // remove visuals for cells no longer selected
        var toRemove = new List<int>();
        foreach (var idx in _selectedCells)
            if (!newSelected.Contains(idx)) toRemove.Add(idx);
        foreach (var idx in toRemove)
        {
            _selectedCells.Remove(idx);
            if (_activeVisuals.TryGetValue(idx, out var go))
            {
                _activeVisuals.Remove(idx);
                ReturnToPool(go);
            }
        }

        // add visuals for newly selected cells
        foreach (var idx in newSelected)
        {
            if (_selectedCells.Contains(idx)) continue;
            _selectedCells.Add(idx);

            // compute cell coords from index
            int cx = idx % Width;
            int cy = idx / Width;
            var cell = new Vector2Int(cx, cy);

            // reuse SetCellVisual but with selection color
            if (!_activeVisuals.TryGetValue(idx, out var go))
            {
                go = GetPooledVisual();
                _activeVisuals[idx] = go;
            }
            go.transform.position = GetCellCenter(cell) + new Vector3(0f, 0.01f, 0f);
            go.transform.localScale = new Vector3(CellSize, CellSize, 1f);
            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                if (rend.sharedMaterial == null) EnsureMaterial();
                rend.material.color = SelectionPreviewColor;
            }
        }
    }

    /// <summary>
    /// End the selection. Keeps visuals active until ClearSelection or TryOccupy is called.
    /// </summary>
    public void EndSelection()
    {
        _isSelecting = false;
        // leave visuals in place; caller decides whether to occupy or clear
    }

    /// <summary>
    /// Clear selection visuals without changing occupancy.
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedCells == null || _selectedCells.Count == 0) return;
        foreach (var idx in _selectedCells)
        {
            if (_activeVisuals.TryGetValue(idx, out var go))
            {
                _activeVisuals.Remove(idx);
                ReturnToPool(go);
            }
        }
        _selectedCells.Clear();
    }


    #endregion
}

