using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class NavMeshManager : MonoBehaviour
{
    public static NavMeshManager Instance { get; private set; }

    // ── Startup Signal ──────────────────────────────────────────────────────────
    // Fired once after every bake (sync or async) completes.
    // AiNavigation subscribes to this instead of polling agent.isOnNavMesh.
    public static event System.Action OnNavMeshReady;
    public static bool IsReady { get; private set; }

    [SerializeField] private List<NavMeshSurface> _surfaces = new List<NavMeshSurface>();
    [SerializeField] private float _debounceTime = .50f;

    [Tooltip("Objects always excluded from NavMesh baking (e.g. the ground plane). Assign once in the Inspector — survives crashes and play mode.")]
    [SerializeField] private List<GameObject> _alwaysExclude = new List<GameObject>();

    private Coroutine _updateCoroutine;
    private bool _isDirty;
    private bool _isUpdating;

    // ── Incremental per-cell top-floor cache ────────────────────────────────────
    // AddFloorNavMeshSources() used to rebuild this by scanning ALL of
    // PlacedObjectRegistry (9,400+ yard tiles alone) on EVERY rebake — including the
    // ~every-25s stuck-agent rebake, which never actually changes floor topology. That
    // full rescan + fresh Dictionary build was the dominant cost behind multi-MB GC
    // spikes recurring throughout play. Maintained incrementally via PlacedObjectRegistry's
    // (Un)Registered events instead: O(1) per placement/removal, O(0) — direct reuse —
    // on every rebake that doesn't touch floor tiles at all (the common case).
    private readonly Dictionary<Vector2Int, PlacedObject> _floorTopCache = new();
    private PlacementGrid _grid;

    private void Awake()
    {
        Instance = this;
        _surfaces = new List<NavMeshSurface>(Object.FindObjectsByType<NavMeshSurface>());

        // Enforce exclusions at runtime regardless of saved NavMeshModifier state.
        foreach (var go in _alwaysExclude)
        {
            if (go == null) continue;
            var mod = go.GetComponent<NavMeshModifier>();
            if (mod == null) mod = go.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
            mod.applyToChildren = true;
        }

        // Seed the cache once from whatever's already placed (scene-authored tiles, or a
        // save already restored before this Awake ran) — every change after this point is
        // captured incrementally via the registry events instead of a re-scan.
        _grid = Object.FindAnyObjectByType<PlacementGrid>();
        foreach (var placed in PlacedObjectRegistry.All)
            TryAddFloorToCache(placed);

        PlacedObjectRegistry.OnRegistered   += OnPlacedObjectRegistered;
        PlacedObjectRegistry.OnUnregistered += OnPlacedObjectUnregistered;
        PlacedObjectRegistry.OnCleared      += OnPlacedObjectsCleared;
    }

    private void OnPlacedObjectsCleared() => _floorTopCache.Clear();

    private void OnPlacedObjectRegistered(PlacedObject placed) => TryAddFloorToCache(placed);

    private void TryAddFloorToCache(PlacedObject placed)
    {
        if (placed == null || placed.data == null || !placed.data.isFloor) return;
        if (placed.gameObject == null || !placed.gameObject.activeSelf) return;

        var key = new Vector2Int(placed.gridX, placed.gridY);
        if (!_floorTopCache.TryGetValue(key, out var cur)
            || placed.transform.position.y > cur.transform.position.y)
            _floorTopCache[key] = placed;
    }

    private void OnPlacedObjectUnregistered(PlacedObject placed)
    {
        if (placed == null || placed.data == null || !placed.data.isFloor) return;

        var key = new Vector2Int(placed.gridX, placed.gridY);
        if (!_floorTopCache.TryGetValue(key, out var cur) || cur != placed) return;

        // The removed tile WAS this cell's cached top — re-derive from the grid's own stack
        // for just this one cell (rare event; placement/removal, not the routine stuck-rebake).
        _floorTopCache.Remove(key);
        if (_grid == null) _grid = Object.FindAnyObjectByType<PlacementGrid>();
        if (_grid == null) return;

        var stack = _grid.GetObjectsInCell(key);
        if (stack == null) return;

        foreach (var entry in stack)
        {
            if (entry.instance == null || !entry.instance.activeSelf) continue;
            var po = entry.instance.GetComponent<PlacedObject>();
            if (po == null || po == placed || po.data == null || !po.data.isFloor) continue;

            if (!_floorTopCache.TryGetValue(key, out var best) || po.transform.position.y > best.transform.position.y)
                _floorTopCache[key] = po;
        }
    }

    // Called by GameContext.Start() after the save is fully loaded.
    public void BakeSynchronous()
    {
        if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
        IsReady = false;

        var modifiers = new List<NavMeshModifier>(Object.FindObjectsByType<NavMeshModifier>());
        var markups = BuildMarkups(modifiers);

        foreach (var surface in _surfaces)
        {
            if (surface == null) continue;
            if (surface.navMeshData != null) NavMeshBuilder.Cancel(surface.navMeshData);

            var sources = new List<NavMeshBuildSource>();
            Bounds worldBounds = GetWorldBounds(surface);

            if (surface.collectObjects == CollectObjects.Children)
                NavMeshBuilder.CollectSources(surface.transform, surface.layerMask, surface.useGeometry, surface.defaultArea, markups, sources);
            else
                NavMeshBuilder.CollectSources(worldBounds, surface.layerMask, surface.useGeometry, surface.defaultArea, markups, sources);

            RemoveInvalidSources(sources);

            AddFloorNavMeshSources(sources, surface.defaultArea);
            AddStairRampSources(sources);
            AddDockTopNavMeshSources(sources, surface.defaultArea);

            var settings = surface.GetBuildSettings();
            var newData = NavMeshBuilder.BuildNavMeshData(settings, sources, worldBounds, surface.transform.position, surface.transform.rotation);

            // Mirrors NavMeshSurface.BuildNavMesh(): remove old instance, swap data, register new instance.
            // UpdateNavMesh() is async-only and never calls AddData(), so we must do this manually.
            surface.RemoveData();
            surface.navMeshData = newData;
            surface.AddData();
        }

        _isDirty = false;
        _isUpdating = false;

        // Carving obstacles (walls, barriers) settle ~0.15–0.5s after creation.
        // Firing OnNavMeshReady immediately lets agents start navigating before
        // carving has cut the mesh, so their paths get invalidated as each obstacle
        // settles. Wait 1.5s to let all carving stabilise first.
        StartCoroutine(ReadyAfterCarvingSettles());
    }

    private IEnumerator ReadyAfterCarvingSettles()
    {
        yield return new WaitForSeconds(1.5f);

        // BakeSynchronous() replaces the NavMeshData object entirely (RemoveData/AddData),
        // so any NavMeshLink that was registered against the old data loses its connection.
        // Toggle each link to force re-registration with the freshly installed data.
        // (The async UpdateRoutine path uses UpdateNavMeshDataAsync which updates in-place
        //  and keeps link registrations intact — this toggle is only needed here.)
        RefreshNavMeshLinks();

        IsReady = true;
        OnNavMeshReady?.Invoke();
    }

    private void RefreshNavMeshLinks()
    {
        int count = 0;
        foreach (var link in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshLink>())
        {
            if (link == null || !link.isActiveAndEnabled) continue;
            // UpdateLink() = RemoveLink + AddLink, re-sampling the endpoints against the
            // freshly-baked NavMesh tiles. This is what actually reconnects the dock links.
            link.UpdateLink();
            count++;
        }
    }

    private void RemoveInvalidSources(List<NavMeshBuildSource> sources)
    {
        if (sources == null) return;
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            var src = sources[i];
            
            // Filter out null source objects for meshes/terrains
            if (src.sourceObject == null && (src.shape == NavMeshBuildSourceShape.Mesh || src.shape == NavMeshBuildSourceShape.Terrain))
            {
                sources.RemoveAt(i);
                continue;
            }

            if (src.shape == NavMeshBuildSourceShape.Mesh && src.sourceObject != null)
            {
                string name = src.sourceObject.name;
                if (name.Contains("TextMeshPro") || name.Contains("TextMesh Pro") || name.Contains("TextMesh") || name == "TextMeshPro Mesh")
                {
                    sources.RemoveAt(i);
                    continue;
                }
            }

            // Exclude ChepAnchorFront/Rear and their children from the bake.
            // These are used for positioning/anchoring and should never carve the NavMesh,
            // as they block MHE (Reach Trucks) from completing putaways.
            if (src.component != null && IsInsideChepAnchor(src.component.transform))
            {
                sources.RemoveAt(i);
                continue;
            }
        }
    }

    private bool IsInsideChepAnchor(Transform t)
    {
        while (t != null)
        {
            string name = t.name;
            if (name.Contains("ChepAnchorFront") || name.Contains("ChepAnchorRear"))
                return true;
            t = t.parent;
        }
        return false;
    }

    private const string RackingCategory = "Racking";

    private List<NavMeshBuildMarkup> BuildMarkups(List<NavMeshModifier> modifiers)
    {
        var markups = new List<NavMeshBuildMarkup>();

        // Hard-coded exclusions — survives crashes and play mode exits.
        foreach (var go in _alwaysExclude)
        {
            if (go != null)
                markups.Add(new NavMeshBuildMarkup { root = go.transform, ignoreFromBuild = true });
        }

        foreach (var mod in modifiers)
        {
            if (mod != null && mod.isActiveAndEnabled)
                markups.Add(new NavMeshBuildMarkup
                {
                    root = mod.transform,
                    overrideArea = mod.overrideArea,
                    area = mod.area,
                    ignoreFromBuild = mod.ignoreFromBuild
                });
        }

        // Auto-exclude all rack objects. Racks are instantiated at runtime from prefabs
        // so they can never appear in _alwaysExclude (which requires scene-instance references).
        // Marking the root excludes the entire rack hierarchy during CollectSources.
        foreach (var placed in PlacedObjectRegistry.All)
        {
            if (placed == null || placed.gameObject == null) continue;
            if (placed.data == null || placed.data.category != RackingCategory) continue;
            markups.Add(new NavMeshBuildMarkup { root = placed.transform, ignoreFromBuild = true });
        }

        return markups;
    }

    // Injects the nav_Plane_Transparent ramp mesh from each stairwell as a walkable source.
    // This makes the NavMesh visibly bake on the stair surface rather than using invisible links alone.
    private void AddStairRampSources(List<NavMeshBuildSource> sources)
    {
        var stairObjects = Object.FindObjectsByType<BuildingData>();
        foreach (var bd in stairObjects)
        {
            if (bd.Data == null || !bd.Data.CanUseStairs) continue;

            // Search all descendants, not just direct children
            foreach (Transform child in bd.GetComponentsInChildren<Transform>(includeInactive: false))
            {
                if (!child.name.Contains("nav_Plane")) continue;

                var mf = child.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                sources.Add(new NavMeshBuildSource
                {
                    transform    = child.localToWorldMatrix,
                    shape        = NavMeshBuildSourceShape.Mesh,
                    sourceObject = mf.sharedMesh,
                    area         = bd.Data.navArea
                });
                break;
            }
        }
    }

    // Injects a thin walkable Box source at the top surface of every placed foundation
    // (detected via DockLedgeSetup). This guarantees the dock surface is NavMesh-walkable
    // even when the foundation geometry is not picked up by CollectSources (e.g. wrong layer).
    private void AddDockTopNavMeshSources(List<NavMeshBuildSource> sources, int defaultArea)
    {
        int scanned = 0;
        int count   = 0;
        foreach (var dock in Object.FindObjectsByType<DockLedgeSetup>())
        {
            if (dock == null) continue;
            scanned++;

            // LedgeLinkMarker children are placed at dock-surface height in world space.
            var marker = dock.GetComponentInChildren<LedgeLinkMarker>();
            if (marker == null)
            {
                Debug.LogWarning($"[NavDock] {dock.name}: no LedgeLinkMarker child — skipping.");
                continue;
            }

            float topY = marker.transform.position.y;  // dock surface world Y (typically 1.06)

            // Box must be wider than the cell (1.33m) to account for agent-radius erosion.
            // NavMesh erodes inward by the agent radius (~0.35–0.5m per side); the link
            // endpoints sit at ±0.665m from center, so we need navigable area to reach them.
            // 3.0m gives ~1.0m navigable margin on each side at typical agent radii.
            const float sourceThickness = 0.10f;
            const float navBoxSize      = 3.0f;

            sources.Add(new NavMeshBuildSource
            {
                transform = Matrix4x4.TRS(
                    new Vector3(dock.transform.position.x, topY - sourceThickness * 0.5f, dock.transform.position.z),
                    Quaternion.identity,
                    Vector3.one),
                shape = NavMeshBuildSourceShape.Box,
                area  = defaultArea,
                size  = new Vector3(navBoxSize, sourceThickness, navBoxSize)
            });
            count++;
        }
        // Always log — a missing "[NavDock]" line means this method was never called.
    }

    // Builds the walkable NavMesh sources from floor tiles. Model (per Tug): each grid CELL's
    // surface is its HIGHEST floor tile. Lower tiles in the same cell — e.g. a ground yard tile
    // sitting under an elevated building floor — are IGNORED; height changes are bridged by
    // stair/dock LINKS, not by overlapping nav layers. This is what stops agents pathing on a
    // phantom ground layer UNDER the building and clipping through the foundations.
    //
    // Thin floor geometry doesn't voxelize, so we emit explicit Box sources:
    //   • Elevated cells  → one per-tile box (thick enough to rasterize at the ~0.167m voxel).
    //   • Ground cells    → merged into contiguous gridX RUNS per row. A single bounding box
    //                       would fill the hole under a building; one box per tile froze the
    //                       bake (~2,500 yard tiles). Runs do neither.
    private void AddFloorNavMeshSources(List<NavMeshBuildSource> sources, int defaultArea)
    {
        var floorStartTime = Time.realtimeSinceStartup;
        const float cellSize = 1.33f;   // project grid cell size

        // 1) Highest floor tile per cell — read directly from the incrementally-maintained
        // cache (see _floorTopCache / OnPlacedObjectRegistered / OnPlacedObjectUnregistered)
        // instead of rescanning all of PlacedObjectRegistry (9,400+ entries) on every rebake.
        // NOTE: deliberately no early-return when this is empty — step 4 below still needs to
        // run on a fresh game (pure bare yard, zero real floors placed yet).
        var top = _floorTopCache;
        //Debug.Log($"[NavMesh]    FloorTopCache has {top.Count} cells");

        // 2) Elevated cells → exact per-tile box. The box must be THICKER than the voxel size or
        //    thin floor geometry never rasterizes (a 0.05m box left the whole building floor
        //    un-walkable). Extend DOWNWARD so the walkable top stays at the floor surface.
        const float elevThickness = 0.30f;
        var groundTiles = new List<PlacedObject>();
        foreach (var kv in top)
        {
            var p = kv.Value;
            if (p.transform.position.y < 0.5f) { groundTiles.Add(p); continue; }

            var renderers = p.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            sources.Add(new NavMeshBuildSource
            {
                transform = Matrix4x4.TRS(
                    new Vector3(b.center.x, b.max.y - elevThickness * 0.5f, b.center.z),
                    p.transform.rotation, Vector3.one),
                shape = NavMeshBuildSourceShape.Box,
                area  = p.data.navArea != 0 ? p.data.navArea : defaultArea,
                size  = new Vector3(b.size.x, elevThickness, b.size.z)
            });
        }

        // 3) Ground cells → contiguous gridX runs per row. Splitting a run when the nav area
        //    changes keeps the ped / MHE lane areas distinct.
        const float gThickness = 0.12f;
        var byRow = new Dictionary<int, List<PlacedObject>>();
        foreach (var p in groundTiles)
        {
            if (!byRow.TryGetValue(p.gridY, out var lst)) { lst = new List<PlacedObject>(); byRow[p.gridY] = lst; }
            lst.Add(p);
        }
        foreach (var kv in byRow)
        {
            var row = kv.Value;
            row.Sort((a, c) => a.gridX.CompareTo(c.gridX));

            int i = 0;
            while (i < row.Count)
            {
                int area = row[i].data.navArea != 0 ? row[i].data.navArea : defaultArea;
                int j = i;
                while (j + 1 < row.Count
                       && row[j + 1].gridX == row[j].gridX + 1
                       && (row[j + 1].data.navArea != 0 ? row[j + 1].data.navArea : defaultArea) == area)
                    j++;

                PlacedObject first = row[i], last = row[j];
                float topY = first.transform.position.y + (first.data.objHeight > 0f ? first.data.objHeight : 0.1f);
                float minX = first.transform.position.x - cellSize * 0.5f;
                float maxX = last.transform.position.x  + cellSize * 0.5f;
                float cz   = first.transform.position.z;

                sources.Add(new NavMeshBuildSource
                {
                    transform = Matrix4x4.TRS(
                        new Vector3((minX + maxX) * 0.5f, topY - gThickness * 0.5f, cz), Quaternion.identity, Vector3.one),
                    shape = NavMeshBuildSourceShape.Box,
                    area  = area,
                    size  = new Vector3(maxX - minX, gThickness, cellSize)
                });

                i = j + 1;
            }
        }

        // 4. Default yard ground — any grid cell with no real floor/foundation (covered
        // visually by YardFloorMeshBuilder's single merged mesh, which carries no per-cell
        // PlacedObject for this method to read from `top`) still needs a walkable box. Same
        // contiguous-row-run merging as step 3, just driven directly by grid occupancy instead
        // of the floor cache, since there's no PlacedObject per bare cell anymore.
        if (_grid != null)
        {
            var defaultRuns = new Dictionary<int, List<int>>();
            for (int gy = 0; gy < _grid.Height; gy++)
            {
                List<int> xs = null;
                for (int gx = 0; gx < _grid.Width; gx++)
                {
                    if (top.ContainsKey(new Vector2Int(gx, gy))) continue; // real floor covers it
                    if (xs == null) { xs = new List<int>(); defaultRuns[gy] = xs; }
                    xs.Add(gx);
                }
            }

            const float defaultThickness = 0.12f;
            foreach (var kv in defaultRuns)
            {
                int gy = kv.Key;
                var xs = kv.Value; // ascending by construction
                int i2 = 0;
                while (i2 < xs.Count)
                {
                    int j2 = i2;
                    while (j2 + 1 < xs.Count && xs[j2 + 1] == xs[j2] + 1) j2++;

                    Vector3 firstWorld = _grid.GetCellCenter(new Vector2Int(xs[i2], gy));
                    Vector3 lastWorld  = _grid.GetCellCenter(new Vector2Int(xs[j2], gy));
                    float minX = firstWorld.x - cellSize * 0.5f;
                    float maxX = lastWorld.x  + cellSize * 0.5f;

                    sources.Add(new NavMeshBuildSource
                    {
                        transform = Matrix4x4.TRS(
                            new Vector3((minX + maxX) * 0.5f, firstWorld.y - defaultThickness * 0.5f, firstWorld.z),
                            Quaternion.identity, Vector3.one),
                        shape = NavMeshBuildSourceShape.Box,
                        area  = defaultArea,
                        size  = new Vector3(maxX - minX, defaultThickness, cellSize)
                    });

                    i2 = j2 + 1;
                }
            }
        }

        //Debug.Log($"[NavMesh]    AddFloorNavMeshSources COMPLETE: {(Time.realtimeSinceStartup - floorStartTime) * 1000:F2}ms");
    }

    private Bounds GetWorldBounds(NavMeshSurface surface)
    {
        if (surface.collectObjects != CollectObjects.All)
            return new Bounds(surface.transform.TransformPoint(surface.center), surface.size);

        var renderers = Object.FindObjectsByType<Renderer>();
        Bounds b = new Bounds();
        bool hasBounds = false;

        foreach (var r in renderers)
        {
            if (r != null && ((1 << r.gameObject.layer) & surface.layerMask) != 0)
            {
                if (!hasBounds) { b = r.bounds; hasBounds = true; }
                else b.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds) return new Bounds(surface.transform.position, Vector3.one * 10f);
        b.Expand(5f);
        return b;
    }

    private void OnDisable()
    {
        if (_surfaces != null)
            foreach (var surface in _surfaces)
                if (surface != null && surface.navMeshData != null)
                    NavMeshBuilder.Cancel(surface.navMeshData);

        if (_updateCoroutine != null) { StopCoroutine(_updateCoroutine); _updateCoroutine = null; }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PlacedObjectRegistry.OnRegistered   -= OnPlacedObjectRegistered;
        PlacedObjectRegistry.OnUnregistered -= OnPlacedObjectUnregistered;
        PlacedObjectRegistry.OnCleared      -= OnPlacedObjectsCleared;
    }

    public void MarkDirty(bool immediate = false)
    {
        _isDirty = true;
        IsReady = false; // agents should pause during a re-bake

        if (_isUpdating) return;
        if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
        _updateCoroutine = StartCoroutine(UpdateRoutine(immediate));
    }

    public void BakeImmediate()
    {
        MarkDirty(true);
    }

    private List<NavMeshModifier> _modifierCache = new List<NavMeshModifier>();
    private float _lastModifierUpdate;

    private IEnumerator UpdateRoutine(bool immediate)
    {
        if (!immediate)
            yield return new WaitForSeconds(_debounceTime);

        while (_isDirty)
        {
            _isDirty = false;
            _isUpdating = true;

            if (Time.realtimeSinceStartup - _lastModifierUpdate > 5f || _modifierCache.Count == 0)
            {
                _modifierCache = new List<NavMeshModifier>(Object.FindObjectsByType<NavMeshModifier>());
                _lastModifierUpdate = Time.realtimeSinceStartup;
            }

            var markups = BuildMarkups(_modifierCache);

            foreach (var surface in _surfaces)
            {
                if (surface == null) continue;

                if (surface.navMeshData == null)
                    surface.navMeshData = new NavMeshData();

                var settings = surface.GetBuildSettings();
                var sources = new List<NavMeshBuildSource>();
                Bounds worldBounds = GetWorldBounds(surface);

                if (surface.collectObjects == CollectObjects.Children)
                    NavMeshBuilder.CollectSources(surface.transform, surface.layerMask, surface.useGeometry, surface.defaultArea, markups, sources);
                else
                    NavMeshBuilder.CollectSources(worldBounds, surface.layerMask, surface.useGeometry, surface.defaultArea, markups, sources);

                RemoveInvalidSources(sources);

                AddFloorNavMeshSources(sources, surface.defaultArea);
                AddStairRampSources(sources);
                AddDockTopNavMeshSources(sources, surface.defaultArea);

                AsyncOperation op = NavMeshBuilder.UpdateNavMeshDataAsync(surface.navMeshData, settings, sources, worldBounds);
                while (!op.isDone) yield return null;

                // NOTE: do NOT call surface.UpdateNavMesh(surface.navMeshData) here — it
                // re-collects sources via the surface's own CollectSources() (missing our
                // floor/dock/stair box injections) and kicks an un-awaited async rebuild
                // that overwrites this navMeshData a moment later, silently stripping the
                // ground/floor/dock walkable areas we just baked in.
            }

            // Nudge all surfaces to register updated data
            foreach (var surface in _surfaces)
            {
                if (surface != null) { surface.enabled = false; surface.enabled = true; }
            }

            // CRITICAL: every rebake rebuilds the NavMesh tiles, which invalidates the
            // connection of every NavMeshLink added against the OLD tiles. Without this,
            // the first runtime rebake permanently disconnects all dock/stair links (the
            // dock becomes an unreachable island and agents can never climb). BakeSynchronous
            // already refreshes after its bake — this covers every async rebake too.
            RefreshNavMeshLinks();

            _isUpdating = false;
        }

        // Bake is fully complete — signal all waiting agents
        IsReady = true;
        OnNavMeshReady?.Invoke();
    }
}
