using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
    
    [System.Serializable]
    public struct PlacedObject
    {
        public GameObject instance;
        public ObjDataSO data;
    }
    [System.Serializable]
    public class DebugCell
    {
        public Vector2Int cell;
        public List<PlacedObject> items = new List<PlacedObject>();
    }
    // keep internal storage private
    private List<PlacedObject>[,] _cells;
    // inspector-friendly debug view
    [Header("Debug")]
    public List<DebugCell> DebugCells = new List<DebugCell>();
    [ContextMenu("Populate DebugCells")]
    public void PopulateDebugCells()
    {
        DebugCells.Clear();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var list = _cells[x, y];
                if (list != null && list.Count > 0)
                {
                    var dc = new DebugCell { cell = new Vector2Int(x, y), items = new List<PlacedObject>(list) };
                    DebugCells.Add(dc);
                }
            }
    }

    private float[,] _stackHeights;

    private Dictionary<int, GameObject> _activeVisuals;
    private Stack<GameObject> _pool;

    private void Awake()
    {
        if (Application.isPlaying)
            InitializeGrid();
    }


    private void OnValidate()
    {
        Width = Mathf.Max(1, Width);
        Height = Mathf.Max(1, Height);
        CellSize = Mathf.Max(0.01f, CellSize);
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

        // Floors, ignorePlacementRules, and ClearsGridAfterPlacement do NOT count as occupied
        foreach (var entry in list)
        {
            if (!entry.data.isFloor && !entry.data.ignorePlacementRules && !entry.data.ClearsGridAfterPlacement)
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

        // Return the topmost NON-floor object that isn't a clearer
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i].data.isFloor && !list[i].data.ignorePlacementRules && !list[i].data.ClearsGridAfterPlacement)
                return list[i].instance;
        }

        return null;
    }

    // ---------------------------------------------------------
    // STACK HEIGHT & POSITIONING
    // ---------------------------------------------------------
    public void UpdateStackPositions(Vector2Int cell)
    {
        if (!IsInsideGrid(cell)) return;

        var list = _cells[cell.x, cell.y];
        if (list == null) return;

        float currentY = 0f;
        foreach (var entry in list)
        {
            if (entry.instance == null || !entry.instance.activeSelf) continue;

            // Objects that ignore rules or clear grid stay at y=0, unless they are floors
            if ((entry.data.ignorePlacementRules || entry.data.ClearsGridAfterPlacement) && !entry.data.isFloor)
            {
                Vector3 p = GetCellCenter(cell);
                p.y = 0f;
                entry.instance.transform.position = p;
                continue;
            }

            // Set position - Only the Root cell of a building should drive its transform position
            var bd = entry.instance.GetComponent<BuildingData>();
            if (bd != null)
            {
                if (bd.RootCell == cell)
                {
                    Vector3 pos = GetCellCenter(cell);
                    pos.y = currentY;

                    var agent = entry.instance.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    {
                        agent.Warp(pos);
                    }
                    else
                    {
                        entry.instance.transform.position = pos;
                    }
                }
            }
            else
            {
                // Fallback for items without building data
                Vector3 pos = GetCellCenter(cell);
                pos.y = currentY;

                var agent = entry.instance.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                {
                    agent.Warp(pos);
                }
                else
                {
                    entry.instance.transform.position = pos;
                }
            }

            // Increment height for next object
            currentY += entry.data.objHeight;
        }

        // Cache the total height for queries
        _stackHeights[cell.x, cell.y] = currentY;
    }

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
            if (entry.instance == ignore || (entry.instance != null && !entry.instance.activeSelf))
                continue;

            // ignorePlacementRules and ClearsGridAfterPlacement do NOT add height, but floors always DO if they have a height.
            if ((entry.data.ignorePlacementRules || entry.data.ClearsGridAfterPlacement) && !entry.data.isFloor)
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

        var list = _cells[cell.x, cell.y];

        // Floors go after other floors but before everything else
        if (data.isFloor)
        {
            int lastFloorIndex = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].data.isFloor)
                    lastFloorIndex = i;
                else
                    break;
            }
            list.Insert(lastFloorIndex + 1, new PlacedObject { instance = obj, data = data });
        }
        else
        {
            // Normal objects go on top
            list.Add(new PlacedObject { instance = obj, data = data });
        }

        UpdateStackPositions(cell);

        if (UseVisualizer)
        {
            if (IsOccupied(cell))
                SetCellVisual(cell, OccupiedColor);
            else
                SetCellVisual(cell, FreeColor);
        }
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
            }
        }

        UpdateStackPositions(cell);

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

        var list = _cells[cell.x, cell.y];
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var entry = list[i];
            
            // Do NOT clear floors here. Floor replacement is handled separately.
            if (entry.data != null && entry.data.isFloor)
                continue;

            if (destroyObject && entry.instance != null)
                Destroy(entry.instance);

            list.RemoveAt(i);
        }

        UpdateStackPositions(cell);

        if (UseVisualizer)
        {
            if (IsOccupied(cell))
                SetCellVisual(cell, OccupiedColor);
            else
                SetCellVisual(cell, FreeColor);
        }
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
    /// <summary>
    /// Rebuild internal grid storage from the global PlacedObjectRegistry at runtime.
    /// Call this after loading/spawning objects so _cells/_stackHeights match the scene.
    /// </summary>
    public void RebuildFromRegistry()
    {
        // Recreate storage
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

        // Populate from registry
        foreach (var placed in PlacedObjectRegistry.All)
        {
            if (placed == null || placed.data == null)
                continue;

            Vector2Int root = new Vector2Int(placed.gridX, placed.gridY);

            if (!IsInsideGrid(root))
            {
                Debug.LogWarning($"RebuildFromRegistry: {placed.name} root {root} is outside grid bounds. Skipping.");
                continue;
            }

            // Compute rotation + footprint using SAME convention as placement / move
            float rotDeg = placed.rotation * 90f;
            Vector2Int[] offsets = placed.data.GetFootprintOffsets(-rotDeg);

            if (offsets == null || offsets.Length == 0)
            {
                Debug.LogWarning($"RebuildFromRegistry: {placed.name} has no footprint offsets. Skipping.");
                continue;
            }

            // Initialize BuildingData once so MoveState has correct root/rotation/offsets
            var bd = placed.GetComponent<BuildingData>();
            if (bd != null)
            {
                bd.Initialize(root, rotDeg, offsets);
            }

            // Register this object in EVERY footprint cell
            foreach (var o in offsets)
            {
                Vector2Int cell = root + o;

                if (!IsInsideGrid(cell))
                {
                    Debug.LogWarning($"RebuildFromRegistry: {placed.name} footprint cell {cell} outside grid. Skipping that cell.");
                    continue;
                }

                var list = _cells[cell.x, cell.y];

                // Avoid duplicates per cell
                bool exists = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].instance == placed.gameObject)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                    continue;

                // Floors at bottom, others on top
                if (placed.data.isFloor)
                    list.Insert(0, new PlacedObject { instance = placed.gameObject, data = placed.data });
                else
                    list.Add(new PlacedObject { instance = placed.gameObject, data = placed.data });
                }
                }

                // Second pass: Update all cell heights and positions
                for (int x = 0; x < Width; x++)
                {
                for (int y = 0; y < Height; y++)
                {
                UpdateStackPositions(new Vector2Int(x, y));
                }
                }

        if (UseVisualizer)
            RedrawAllVisuals();
    }

    public void LogGridVsRegistryDiagnostics()
    {
        int totalRegistry = PlacedObjectRegistry.Count;
        int missingInGrid = 0;
        int misplaced = 0;

        foreach (var placed in PlacedObjectRegistry.All)
{
            if (placed == null || placed.data == null) continue;

            Vector2Int expected = new Vector2Int(placed.gridX, placed.gridY);

            if (!IsInsideGrid(expected))
            {
                Debug.LogWarning($"Diagnostics: {placed.name} expected {expected} OUTSIDE grid bounds.");
                continue;
            }

            bool found = false;
            var list = _cells[expected.x, expected.y];
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].instance == placed.gameObject)
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                // try to find it anywhere
                bool foundElsewhere = false;
                for (int x = 0; x < Width && !foundElsewhere; x++)
                {
                    for (int y = 0; y < Height && !foundElsewhere; y++)
                    {
                        var other = _cells[x, y];
                        if (other == null) continue;
                        for (int i = 0; i < other.Count; i++)
                        {
                            if (other[i].instance == placed.gameObject)
                            {
                                foundElsewhere = true;
                                Debug.LogWarning($"Diagnostics: {placed.name} stored at {x},{y} but registry says {expected}.");
                                misplaced++;
                                break;
                            }
                        }
                    }
                }

                if (!foundElsewhere)
                {
                    Debug.LogWarning($"Diagnostics: {placed.name} missing from grid at {expected}.");
                    missingInGrid++;
                }
            }
        }

    }
}
