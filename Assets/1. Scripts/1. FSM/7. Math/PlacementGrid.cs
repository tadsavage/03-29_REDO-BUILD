using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteAlways]
public class PlacementGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public float CellSize = 1.33f;
    public int Width = 50;
    public int Height = 50;
    public Vector3 Origin = Vector3.zero;

    [Header("Visualizer Settings")]
    public bool UseVisualizer = true;
    public Material CellMaterial;
    public Transform VisualParent;

    [Header("Colors")]
    public Color FreeColor = new Color(0f, 0f, 0f, 0f);
    public Color OccupiedColor = new Color(1f, 0.4f, 0.4f, 0.6f);
    public Color SelectedColor = new Color(0.4f, 1f, 0.4f, 0.6f);


    // Unified occupancy system
    private GameObject[,] _cells;

    // Visualizer pooling
    private Dictionary<int, GameObject> _activeVisuals;
    private Stack<GameObject> _pool;

    private void Awake()
    {
        InitializeGrid();
    }

    private void Update()
    {
        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            ToggleVisualizer();
        }
    }

    private void OnValidate()
    {
        Width = Mathf.Max(1, Width);
        Height = Mathf.Max(1, Height);
        CellSize = Mathf.Max(0.01f, CellSize);

        InitializeGrid();
    }

    public void InitializeGrid()
    {
        _cells = new GameObject[Width, Height];

        if (UseVisualizer)
        {
            if (_activeVisuals == null) _activeVisuals = new Dictionary<int, GameObject>();
            if (_pool == null) _pool = new Stack<GameObject>();
            ClearAllVisuals();
        }
    }

    // -------------------------
    // GRID API
    // -------------------------

    public bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
    }

    public bool IsOccupied(Vector2Int cell)
    {
        if (!IsInsideGrid(cell)) return true;
        return _cells[cell.x, cell.y] != null;
    }

    public void SetOccupied(Vector2Int cell, GameObject obj, ObjDataSO data)
    {
        if (!IsInsideGrid(cell)) return;
        _cells[cell.x, cell.y] = obj;

        if (UseVisualizer)
            SetCellVisual(cell, OccupiedColor);
    }

    public void ClearCell(Vector2Int cell)
    {
        // Bounds check
        if (cell.x < 0 || cell.x >= _cells.GetLength(0)) return;
        if (cell.y < 0 || cell.y >= _cells.GetLength(1)) return;

        GameObject placed = _cells[cell.x, cell.y];

        if (placed != null)
            GameObject.Destroy(placed);

        _cells[cell.x, cell.y] = null;

        if (UseVisualizer)
            SetCellVisual(cell, FreeColor);
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = worldPos - Origin;
        int x = Mathf.FloorToInt(local.x / CellSize);
        int y = Mathf.FloorToInt(local.z / CellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return Origin + new Vector3(cell.x * CellSize, 0f, cell.y * CellSize);
    }

    public Vector3 GetCellCenter(Vector2Int cell)
    {
        return CellToWorld(cell) + new Vector3(CellSize * 0.5f, 0f, CellSize * 0.5f);
    }

    // -------------------------
    // VISUALIZER
    // -------------------------

    private int CellIndex(Vector2Int cell) => cell.x + cell.y * Width;

    private void EnsureMaterial()
    {
        if (CellMaterial != null) return;

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        CellMaterial = new Material(shader);
    }

    private GameObject GetPooledVisual()
    {
        if (_pool.Count > 0)
        {
            var go = _pool.Pop();
            go.SetActive(true);
            return go;
        }

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "CellVisual";
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        if (VisualParent != null) quad.transform.SetParent(VisualParent, true);

        DestroyImmediate(quad.GetComponent<Collider>());

        EnsureMaterial();
        quad.GetComponent<MeshRenderer>().sharedMaterial = CellMaterial;

        return quad;
    }

    private void ReturnToPool(GameObject go)
    {
        go.SetActive(false);
        _pool.Push(go);
    }

    private void SetCellVisual(Vector2Int cell, Color color)
    {
        if (!UseVisualizer || !IsInsideGrid(cell)) return;

        int idx = CellIndex(cell);

        if (!_activeVisuals.TryGetValue(idx, out var go))
        {
            go = GetPooledVisual();
            _activeVisuals[idx] = go;
        }

        go.transform.position = GetCellCenter(cell) + new Vector3(0f, 0.01f, 0f);
        go.transform.localScale = new Vector3(CellSize, CellSize, 1f);

        var rend = go.GetComponent<MeshRenderer>();
        rend.material.color = color;
    }

    private void ClearAllVisuals()
    {
        if (_activeVisuals == null) return;

        foreach (var kv in _activeVisuals)
            if (kv.Value != null)
                DestroyImmediate(kv.Value);

        _activeVisuals.Clear();
        _pool?.Clear();
    }
    public void ToggleVisualizer()
    {
        UseVisualizer = !UseVisualizer;

        if (!UseVisualizer)
        {
            ClearAllVisuals();
        }
        else
        {
            RedrawAllVisuals();
        }
    }
    public void RedrawAllVisuals()
    {
        if (!UseVisualizer) return;

        ClearAllVisuals();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                // Only draw visuals for OCCUPIED cells
                if (_cells[x, y] != null)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    SetCellVisual(cell, OccupiedColor);
                }
            }
        }
    }
}
