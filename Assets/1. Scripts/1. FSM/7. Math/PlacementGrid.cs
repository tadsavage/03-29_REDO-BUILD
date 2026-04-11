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

    [Header("Stacking Settings")]
    [Tooltip("Maximum allowed vertical height (in meters) for stacked objects in a cell.")]
    public float maxStackHeight = 9f;

    [Header("Visualizer Settings")]
    public bool UseVisualizer = true;
    public Material CellMaterial;
    public Transform VisualParent;

    [Header("Colors")]
    public Color FreeColor = new Color(0f, 0f, 0f, 0f);
    public Color OccupiedColor = new Color(1f, 0.4f, 0.4f, 0.6f);
    public Color SelectedColor = new Color(0.4f, 1f, 0.4f, 0.6f);
    public Color DeleteColor = new Color(1f, 1f, 0.2f, 0.5f);

    public struct PlacedObject
    {
        public GameObject instance;
        public ObjDataSO data;
    }

    private List<PlacedObject>[,] _cells;

    // Cached stack heights per cell
    private float[,] _stackHeights;

    private Dictionary<int, GameObject> _activeVisuals;
    private Stack<GameObject> _pool;

    private void Awake()
    {
        InitializeGrid();
    }

    private void Update()
    {
        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            ToggleVisualizer();
    }

    public List<Vector2Int> GetRectangleCells(Vector2Int a, Vector2Int b)
    {
        List<Vector2Int> cells = new();

        int minX = Mathf.Min(a.x, b.x);
        int maxX = Mathf.Max(a.x, b.x);
        int minY = Mathf.Min(a.y, b.y);
        int maxY = Mathf.Max(a.y, b.y);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                cells.Add(new Vector2Int(x, y));

        return cells;
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
        _cells = new List<PlacedObject>[Width, Height];
        _stackHeights = new float[Width, Height];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                _cells[x, y] = new List<PlacedObject>();
                _stackHeights[x, y] = 0f;
            }
        }

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
    public void RemoveCellVisual(Vector2Int cell)
    {
        if (!IsInsideGrid(cell))
            return;

        int idx = CellIndex(cell);

        if (_activeVisuals.TryGetValue(idx, out var go))
        {
            ReturnToPool(go);
            _activeVisuals.Remove(idx);
        }
    }

    public void HighlightCellForDelete(Vector2Int cell)
    {
        if (!UseVisualizer || !IsInsideGrid(cell))
            return;

        int idx = CellIndex(cell);

        if (_activeVisuals.TryGetValue(idx, out var go))
        {
            var rend = go.GetComponent<MeshRenderer>();
            rend.material.color = DeleteColor;
        }
    }

    public void RestoreCellVisual(Vector2Int cell)
    {
        if (!UseVisualizer || !IsInsideGrid(cell))
            return;

        int idx = CellIndex(cell);

        if (_activeVisuals.TryGetValue(idx, out var go))
        {
            var rend = go.GetComponent<MeshRenderer>();

            if (_cells[cell.x, cell.y].Count > 0)
                rend.material.color = OccupiedColor;
            else
                rend.material.color = FreeColor;
        }
    }

    public bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
    }

    public List<PlacedObject> GetObjectsInCell(Vector2Int cell)
    {
        if (!IsInsideGrid(cell))
            return null;

        return _cells[cell.x, cell.y];
    }

    public GameObject GetTopObject(Vector2Int cell)
    {
        if (!IsInsideGrid(cell))
            return null;

        var list = _cells[cell.x, cell.y];
        if (list == null || list.Count == 0)
            return null;

        return list[list.Count - 1].instance;
    }

    public bool IsOccupied(Vector2Int cell)
    {
        if (!IsInsideGrid(cell)) return true;
        return _cells[cell.x, cell.y].Count > 0;
    }

    // ================================
    // STACKING LOGIC (CACHED HEIGHT)
    // ================================
    public float GetStackHeight(Vector2Int cell)
    {
        if (!IsInsideGrid(cell))
            return 0f;

        return _stackHeights[cell.x, cell.y];
    }

    public bool CanStack(Vector2Int cell, ObjDataSO data)
    {
        float current = GetStackHeight(cell);
        float newHeight = current + data.objHeight;

        return newHeight <= maxStackHeight;
    }

    public void AddStackObject(Vector2Int cell, GameObject obj, ObjDataSO data)
    {
        if (!IsInsideGrid(cell))
            return;

        _cells[cell.x, cell.y].Add(new PlacedObject
        {
            instance = obj,
            data = data
        });

        _stackHeights[cell.x, cell.y] += data.objHeight;

        if (UseVisualizer)
            SetCellVisual(cell, OccupiedColor);
    }

    public void AdjustStackHeight(Vector2Int cell, float delta)
    {
        if (!IsInsideGrid(cell))
            return;

        _stackHeights[cell.x, cell.y] += delta;
        if (_stackHeights[cell.x, cell.y] < 0f)
            _stackHeights[cell.x, cell.y] = 0f;
    }

    // -------------------------
    // LEGACY API
    // -------------------------
    public void SetOccupied(Vector2Int cell, GameObject obj, ObjDataSO data)
    {
        AddStackObject(cell, obj, data);
    }

    public void ClearCell(Vector2Int cell, bool destroyObject)
    {
        if (!IsInsideGrid(cell))
            return;

        foreach (var entry in _cells[cell.x, cell.y])
        {
            if (destroyObject && entry.instance != null)
                Destroy(entry.instance);
        }

        _cells[cell.x, cell.y].Clear();
        _stackHeights[cell.x, cell.y] = 0f;

        if (UseVisualizer)
            SetCellVisual(cell, FreeColor);
    }

    // -------------------------
    // POSITION HELPERS
    // -------------------------
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
                if (_cells[x, y].Count > 0)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    SetCellVisual(cell, OccupiedColor);
                }
            }
        }
    }
}
