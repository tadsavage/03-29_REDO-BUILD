using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Add this to any raised-dock foundation prefab.
///
/// On Awake and after every NavMesh bake it creates one NavMeshLink + LedgeLinkMarker
/// child per outer-edge cell along each face of the foundation. Sides shared with an
/// adjacent foundation are skipped — agents never try to climb an interior wall.
///
/// Pivot note: PlacementFinalizer places objects at GetCellCenter(rootCell), which is the
/// CENTER of the root cell (0,0) of the footprint — NOT the center of the whole footprint.
/// For a 2×2 foundation the footprint extends in the +X and +Z local directions from that
/// pivot, so the outer edges are ASYMMETRIC in local space:
///   East  = fp.x * cellSize - halfCellSize   (e.g.  1.995 for fp.x=2)
///   West  = -halfCellSize                    (always -0.665, regardless of fp.x)
///   North = fp.y * cellSize - halfCellSize
///   South = -halfCellSize
///
/// Footprint is read automatically from the sibling PlacedObject/ObjDataSO. The
/// serialised `footprint` field is a fallback for prefab-editing mode.
/// </summary>
public class DockLedgeSetup : MonoBehaviour
{
    [Header("Dock Geometry")]
    [Tooltip("Height of the dock surface above the surrounding floor.")]
    [SerializeField] private float dockHeight   = 1.06f;
    [Tooltip("Half the grid cell width (CellSize / 2 = 1.33 / 2 = 0.665).")]
    [SerializeField] private float halfCellSize = 0.665f;
    [Tooltip("How far the floor endpoint is pushed outside the edge so it lands on adjacent floor.")]
    [SerializeField] private float floorNudge   = 0.4f;
    [Tooltip("Width of each NavMeshLink — should cover one grid cell.")]
    [SerializeField] private float linkWidth    = 1.2f;
    [Tooltip("Footprint in grid cells (X = columns, Z = rows). Auto-read from PlacedObject if present.")]
    [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);

    [Header("Animation")]
    [SerializeField] private float climbDuration = 1.2f;
    [SerializeField] private float jumpDuration  = 0.8f;

    // ── Static registry ──────────────────────────────────────────────────────
    private static readonly List<DockLedgeSetup> s_all = new List<DockLedgeSetup>();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildLinks();
    }

    private void OnEnable()
    {
        s_all.Add(this);
        NavMeshManager.OnNavMeshReady += BuildLinks;
    }

    private void OnDisable()
    {
        s_all.Remove(this);
        NavMeshManager.OnNavMeshReady -= BuildLinks;
    }

    // ── Link construction ────────────────────────────────────────────────────

    private void BuildLinks()
    {
        int        humanTypeID = GetHumanAgentTypeID();
        float      cellSize    = halfCellSize * 2f;
        Vector2Int fp          = GetFP();

        // Collect existing LedgeLink_ children to detect stale ones after the rebuild.
        var existing = new HashSet<Transform>();
        foreach (Transform child in transform)
            if (child.name.StartsWith("LedgeLink_"))
                existing.Add(child);

        var kept = new HashSet<Transform>();

        // ── Correct edge positions in local space ─────────────────────────────
        // The pivot sits at the root-cell CENTER, so the footprint spans:
        //   local X: -halfCellSize  …  fp.x * cellSize - halfCellSize
        //   local Z: -halfCellSize  …  fp.y * cellSize - halfCellSize
        float northZ =  fp.y * cellSize - halfCellSize;
        float southZ = -halfCellSize;
        float eastX  =  fp.x * cellSize - halfCellSize;
        float westX  = -halfCellSize;

        // ── North / South faces — one link per cell along X ───────────────────
        for (int i = 0; i < fp.x; i++)
        {
            float x = i * cellSize; // center of cell i in local X
            TryBuildLink($"LedgeLink_N_{i}", new Vector3(x, dockHeight, northZ), Vector3.forward, humanTypeID, existing, kept);
            TryBuildLink($"LedgeLink_S_{i}", new Vector3(x, dockHeight, southZ), Vector3.back,    humanTypeID, existing, kept);
        }

        // ── East / West faces — one link per cell along Z ─────────────────────
        for (int j = 0; j < fp.y; j++)
        {
            float z = j * cellSize; // center of cell j in local Z
            TryBuildLink($"LedgeLink_E_{j}", new Vector3(eastX, dockHeight, z), Vector3.right, humanTypeID, existing, kept);
            TryBuildLink($"LedgeLink_W_{j}", new Vector3(westX, dockHeight, z), Vector3.left,  humanTypeID, existing, kept);
        }

        // Destroy stale children (old single-name scheme or now-shared faces).
        foreach (var stale in existing)
            if (!kept.Contains(stale) && stale != null)
                Destroy(stale.gameObject);
    }

    private void TryBuildLink(string name, Vector3 localPos, Vector3 outDir,
                               int agentTypeID,
                               HashSet<Transform> existing, HashSet<Transform> kept)
    {
        if (IsFacingAdjacentDock(transform.TransformPoint(localPos), outDir))
            return;

        Transform child = transform.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(transform, false);
        }
        kept.Add(child);

        child.localPosition = localPos;
        child.localRotation = Quaternion.LookRotation(outDir);

        var link = child.GetComponent<NavMeshLink>() ?? child.gameObject.AddComponent<NavMeshLink>();
        link.startPoint    = Vector3.zero;
        link.endPoint      = new Vector3(0f, -dockHeight, floorNudge);
        link.width         = linkWidth;
        link.bidirectional = true;
        link.agentTypeID   = agentTypeID;
        link.costModifier  = -1f;

        var marker = child.GetComponent<LedgeLinkMarker>() ?? child.gameObject.AddComponent<LedgeLinkMarker>();
        marker.climbDuration = climbDuration;
        marker.jumpDuration  = jumpDuration;
    }

    private PlacementGrid _grid;

    private bool IsFacingAdjacentDock(Vector3 worldLinkPos, Vector3 outDir)
    {
        if (_grid == null) _grid = Object.FindAnyObjectByType<PlacementGrid>();
        if (_grid == null) return false;

        // Calculate the neighbor cell position based on outDir
        Vector3 probeWorld = worldLinkPos + outDir * halfCellSize;
        Vector2Int cell = _grid.WorldToCell(probeWorld);

        var list = _grid.GetObjectsInCell(cell);
        if (list == null) return false;

        foreach (var entry in list)
        {
            if (entry.instance == null || entry.instance == gameObject) continue;
            // If the neighbor cell has a foundation or grounds object, we don't need a link.
            if (entry.data != null && (entry.data.category == "Foundation" || entry.data.category == "Grounds"))
                return true;
        }

        return false;
    }

    // True when worldPoint falls within this foundation's XZ footprint in local space.
    // Uses InverseTransformPoint so it handles rotated foundations correctly.
    public bool ContainsPointXZ(Vector3 worldPoint)
    {
        Vector2Int fp     = GetFP();
        float      cSize  = halfCellSize * 2f;
        Vector3    local  = transform.InverseTransformPoint(worldPoint);
        return local.x >= -halfCellSize && local.x <= fp.x * cSize - halfCellSize
            && local.z >= -halfCellSize && local.z <= fp.y * cSize - halfCellSize;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector2Int GetFP()
    {
        var placed = GetComponent<PlacedObject>();
        return (placed != null && placed.data != null) ? placed.data.footprint : footprint;
    }

    private static int GetHumanAgentTypeID()
    {
        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            var s = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == "Human")
                return s.agentTypeID;
        }
        return 0;
    }
}
