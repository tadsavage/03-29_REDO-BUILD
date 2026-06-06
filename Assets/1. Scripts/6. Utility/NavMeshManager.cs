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

            AddFloorNavMeshSources(sources, surface.defaultArea);
            AddStairRampSources(sources);

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
        foreach (var link in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshLink>(FindObjectsInactive.Exclude))
        {
            if (link == null || !link.isActiveAndEnabled) continue;
            link.enabled = false;
            link.enabled = true;
        }
    }

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

    // Injects explicit Box NavMesh sources at each floor tile's top surface.
    // This bypasses the NavMesh voxel resolution limit for thin floor geometry.
    private void AddFloorNavMeshSources(List<NavMeshBuildSource> sources, int defaultArea)
    {
        foreach (var placed in PlacedObjectRegistry.All)
        {
            if (placed == null || placed.data == null || !placed.data.isFloor) continue;
            if (placed.gameObject == null || !placed.gameObject.activeSelf) continue;

            var renderers = placed.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            const float sourceThickness = 0.05f;
            float topY = b.max.y;

            sources.Add(new NavMeshBuildSource
            {
                transform = Matrix4x4.TRS(
                    new Vector3(b.center.x, topY - sourceThickness * 0.5f, b.center.z),
                    placed.transform.rotation,
                    Vector3.one),
                shape = NavMeshBuildSourceShape.Box,
                area = placed.data.navArea != 0 ? placed.data.navArea : defaultArea,
                size = new Vector3(b.size.x, sourceThickness, b.size.z)
            });
        }
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

            var markups = new List<NavMeshBuildMarkup>();
            foreach (var mod in _modifierCache)
            {
                if (mod != null && mod.isActiveAndEnabled)
                {
                    markups.Add(new NavMeshBuildMarkup
                    {
                        root = mod.transform,
                        overrideArea = mod.overrideArea,
                        area = mod.area,
                        ignoreFromBuild = mod.ignoreFromBuild
                    });
                }
            }

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

                AddFloorNavMeshSources(sources, surface.defaultArea);
                AddStairRampSources(sources);

                AsyncOperation op = NavMeshBuilder.UpdateNavMeshDataAsync(surface.navMeshData, settings, sources, worldBounds);
                while (!op.isDone) yield return null;

                surface.UpdateNavMesh(surface.navMeshData);
            }

            // Nudge all surfaces to register updated data
            foreach (var surface in _surfaces)
            {
                if (surface != null) { surface.enabled = false; surface.enabled = true; }
            }

            _isUpdating = false;
        }

        // Bake is fully complete — signal all waiting agents
        IsReady = true;
        OnNavMeshReady?.Invoke();
    }
}
