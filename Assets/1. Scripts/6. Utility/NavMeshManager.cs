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

    private Coroutine _updateCoroutine;
    private bool _isDirty;
    private bool _isUpdating;

    private void Awake()
    {
        Instance = this;
        _surfaces = new List<NavMeshSurface>(Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None));
        // NOTE: We do NOT bake here. GameContext.Start() triggers BakeImmediate()
        // after LoadGame() so the bake includes all placed objects.
    }

    // Called by GameContext.Start() after the save is fully loaded.
    public void BakeSynchronous()
    {
        if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);

        IsReady = false;

        foreach (var surface in _surfaces)
        {
            if (surface != null)
            {
                if (surface.navMeshData != null)
                    NavMeshBuilder.Cancel(surface.navMeshData);

                surface.BuildNavMesh();
                surface.enabled = false;
                surface.enabled = true;
            }
        }

        _isDirty = false;
        _isUpdating = false;

        // Signal agents
        IsReady = true;
        OnNavMeshReady?.Invoke();
    }

    private Bounds GetWorldBounds(NavMeshSurface surface)
    {
        if (surface.collectObjects != CollectObjects.All)
            return new Bounds(surface.transform.TransformPoint(surface.center), surface.size);

        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
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
                _modifierCache = new List<NavMeshModifier>(Object.FindObjectsByType<NavMeshModifier>(FindObjectsSortMode.None));
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
