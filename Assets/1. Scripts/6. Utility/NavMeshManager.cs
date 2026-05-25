using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class NavMeshManager : MonoBehaviour
{
    public static NavMeshManager Instance { get; private set; }

    [SerializeField] private List<NavMeshSurface> _surfaces = new List<NavMeshSurface>();
    [SerializeField] private float _debounceTime = .50f;

    private Coroutine _updateCoroutine;
    private bool _isDirty;
    private bool _isUpdating;

    private void Awake()
    {
        Instance = this;
        
        // Clear and re-find all surfaces to ensure none are missed
        _surfaces = new List<NavMeshSurface>(Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None));
        
        // IMPORTANT: Perform a synchronous bake on Awake so the NavMesh is ready for Start()
        BakeSynchronous();
    }

    public void BakeSynchronous()
    {
        if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
        
        foreach (var surface in _surfaces)
        {
            if (surface != null)
            {
                // Cancel any pending async builds to avoid "g_pVertMem == NULL" assertion
                if (surface.navMeshData != null)
                {
                    NavMeshBuilder.Cancel(surface.navMeshData);
                }

                // Use the high-level BuildNavMesh for synchronous initialization.
                // This is more robust than manual UpdateNavMeshData calls.
                surface.BuildNavMesh();
                
                // Automate the manual toggle fix if required by the project's specific setup
                surface.enabled = false;
                surface.enabled = true;
            }
        }
        _isDirty = false;
        _isUpdating = false;
    }

    private Bounds GetWorldBounds(NavMeshSurface surface)
    {
        if (surface.collectObjects != CollectObjects.All)
        {
            return new Bounds(surface.transform.TransformPoint(surface.center), surface.size);
        }

        // Calculate actual scene bounds for objects on the layer to avoid massive voxel grids
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        Bounds b = new Bounds();
        bool hasBounds = false;
        
        foreach (var r in renderers)
        {
            if (r != null && ((1 << r.gameObject.layer) & surface.layerMask) != 0)
            {
                if (!hasBounds)
                {
                    b = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }
        }
        
        if (!hasBounds) return new Bounds(surface.transform.position, Vector3.one * 10f);
        
        b.Expand(5f); // Add a small margin
        return b;
    }

    private void OnDisable()
    {
        // Cancel all pending async builds to prevent crash when stopping play mode.
        // Doing this in OnDisable ensures it runs before surfaces are potentially destroyed.
        if (_surfaces != null)
        {
            foreach (var surface in _surfaces)
            {
                if (surface != null && surface.navMeshData != null)
                {
                    NavMeshBuilder.Cancel(surface.navMeshData);
                }
            }
        }

        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void MarkDirty(bool immediate = false)
    {
        _isDirty = true;
        
        // If already updating, it will loop in the coroutine if _isDirty remains true
        if (_isUpdating) return;

        if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
        _updateCoroutine = StartCoroutine(UpdateRoutine(immediate));
    }

    public void BakeImmediate()
    {
        // We no longer do actual "Immediate" (blocking) bakes because they freeze the UI.
        // Instead, we trigger the async update without the debounce delay.
        MarkDirty(true);
    }

    private List<NavMeshModifier> _modifierCache = new List<NavMeshModifier>();
    private float _lastModifierUpdate;

    private IEnumerator UpdateRoutine(bool immediate)
    {
        if (!immediate)
        {
            yield return new WaitForSeconds(_debounceTime);
        }

        while (_isDirty)
        {
            _isDirty = false;
            _isUpdating = true;

            // Only refresh modifier cache if it's been more than a few seconds or if it's the first time
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

            // Update each surface but yield between them to keep the game responsive
            foreach (var surface in _surfaces)
            {
                if (surface != null)
                {
                    if (surface.navMeshData == null)
                    {
                        surface.navMeshData = new NavMeshData();
                    }

                    float startTime = Time.realtimeSinceStartup;
                    var settings = surface.GetBuildSettings();
                    var sources = new List<NavMeshBuildSource>();
                    
                    Bounds worldBounds = GetWorldBounds(surface);
                    
                    if (surface.collectObjects == CollectObjects.Children)
                    {
                        NavMeshBuilder.CollectSources(surface.transform, surface.layerMask, surface.useGeometry, surface.defaultArea, markups, sources);
                    }
                    else
                    {
                        NavMeshBuilder.CollectSources(worldBounds, surface.layerMask, surface.useGeometry, surface.defaultArea, markups, sources);
                    }

                    AsyncOperation op = NavMeshBuilder.UpdateNavMeshDataAsync(surface.navMeshData, settings, sources, worldBounds);
                    
                    while (!op.isDone)
                    {
                        yield return null;
                    }

                    surface.UpdateNavMesh(surface.navMeshData);
                    
                    float duration = Time.realtimeSinceStartup - startTime;
                }
            }

            // Only nudge once after all surfaces are updated
            foreach (var surface in _surfaces)
            {
                if (surface != null)
                {
                    surface.enabled = false;
                    surface.enabled = true;
                }
            }

            _isUpdating = false;
        }
    }
}