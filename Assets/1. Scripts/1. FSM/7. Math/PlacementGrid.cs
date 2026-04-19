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
    public float maxStackHeight = 9f;

    [Header("Visualizer Settings")]
    public bool UseVisualizer = false;
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
    private float[,] _stackHeights;

    private Dictionary<int, GameObject> _activeVisuals;
    private Stack<GameObject> _pool;

    private void Awake()
    {
        InitializeGrid();
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
            _activeVisuals ??= new Dictionary<int, GameObject>();
            _pool ??= new Stack<GameObject>();
            ClearAllVisuals();
        }
    }

    // ---------------------------------------------------------
    // GRID QUERIES
    // ---------------------------------------------------------
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

    public bool IsOccupied(Vector2Int cell)
    {
        if (!IsInsideGrid(cell))
            return true;

        var list = _cells[cell.x, cell.y];
        if (list == null || list.Count == 0)
            return false;

        // Floors and ignorePlacementRules do NOT count as occupied
        foreach (var entry in list)
        {
            if (!entry.data.isFloor && !entry.data.ignorePlacementRules)
                return true;
        }

        return false;
    }

    public GameObject GetTopObject(Vector2Int cell)
    {
        if (!IsInsideGrid(cell))
            return null;

        var list = _cells[cell.x, cell.y];
        if (list == null || list.Count == 0)
            return null;

        // Return the topmost NON-floor object
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i].data.isFloor && !list[i].data.ignorePlacementRules)
                return list[i].instance;
        }

        return null;
    }

    // ---------------------------------------------------------
    // STACK HEIGHT
    // ---------------------------------------------------------
    public float GetStackHeight(Vector2Int cell, GameObject ignore = null)
    {
        if (!IsInsideGrid(cell))
            return 0f;

        float height = 0f;

        var list = _cells[cell.x, cell.y];
        if (list == null)
            return 0f;

        foreach (var entry in list)
        {
            if (entry.instance == ignore)
                continue;

            // Floors and ignorePlacementRules do NOT add height
            if (entry.data.isFloor || entry.data.ignorePlacementRules)
                continue;

            height += entry.data.objHeight;
        }

        return height;
    }

    public bool CanStack(Vector2Int cell, ObjDataSO data)
    {
        if (!IsInsideGrid(cell))
            return false;

        float current = GetStackHeight(cell);
        float newHeight = current + data.objHeight;

        return newHeight <= maxStackHeight;
    }

    // ---------------------------------------------------------
    // ADD / REMOVE OBJECTS
    // ---------------------------------------------------------
    public void AddStackObject(Vector2Int cell, GameObject obj, ObjDataSO data)
    {
        if (!IsInsideGrid(cell))
            return;

        // Floors ALWAYS go at the bottom
        if (data.isFloor)
        {
            _cells[cell.x, cell.y].Insert(0, new PlacedObject
            {
                instance = obj,
                data = data
            });
        }
        else
        {
            // Normal objects go on top
            _cells[cell.x, cell.y].Add(new PlacedObject
            {
                instance = obj,
                data = data
            });
        }

        // Floors do NOT add height
        if (!data.isFloor && !data.ignorePlacementRules)
            _stackHeights[cell.x, cell.y] += data.objHeight;
    }
    public void RemoveStackObject(Vector2Int cell, GameObject obj, ObjDataSO data)
    {
        if (!IsInsideGrid(cell))
            return;

        var list = _cells[cell.x, cell.y];
        if (list == null || list.Count == 0)
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].instance == obj)
            {
                list.RemoveAt(i);

                if (!data.isFloor && !data.ignorePlacementRules)
                {
                    _stackHeights[cell.x, cell.y] -= data.objHeight;
                    if (_stackHeights[cell.x, cell.y] < 0f)
                        _stackHeights[cell.x, cell.y] = 0f;
                }
            }
        }

        if (UseVisualizer)
        {
            if (IsOccupied(cell))
                SetCellVisual(cell, OccupiedColor);
            else
                SetCellVisual(cell, FreeColor);
        }
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

    // ---------------------------------------------------------
    // POSITION HELPERS
    // ---------------------------------------------------------
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

    // ---------------------------------------------------------
    // VISUALIZER
    // ---------------------------------------------------------
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
        if (_pool != null && _pool.Count > 0)
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
        _pool ??= new Stack<GameObject>();
        _pool.Push(go);
    }

    private void SetCellVisual(Vector2Int cell, Color color)
    {
        if (!UseVisualizer || !IsInsideGrid(cell)) return;

        _activeVisuals ??= new Dictionary<int, GameObject>();
        _pool ??= new Stack<GameObject>();

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
            ClearAllVisuals();
        else
            RedrawAllVisuals();
    }

    public void RedrawAllVisuals()
    {
        if (!UseVisualizer) return;

        ClearAllVisuals();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (IsOccupied(cell))
                    SetCellVisual(cell, OccupiedColor);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        for (int x = 0; x <= Width; x++)
        {
            Vector3 start = Origin + new Vector3(x * CellSize, 0f, 0f);
            Vector3 end = Origin + new Vector3(x * CellSize, 0f, Height * CellSize);
            Gizmos.DrawLine(start, end);
        }

        for (int y = 0; y <= Height; y++)
        {
            Vector3 start = Origin + new Vector3(0f, 0f, y * CellSize);
            Vector3 end = Origin + new Vector3(Width * CellSize, 0f, y * CellSize);
            Gizmos.DrawLine(start, end);
        }
    }
}
