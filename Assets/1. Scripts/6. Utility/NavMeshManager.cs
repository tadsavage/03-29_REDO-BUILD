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
        
        _modifierCache = new List<NavMeshModifier>(Object.FindObjectsByType<NavMeshModifier>(FindObjectsSortMode.None));
        
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
            if (surface != null)
            {
                if (surface.navMeshData == null)
                {
                    surface.navMeshData = new NavMeshData();
                }

                var settings = surface.GetBuildSettings();
                var sources = new List<NavMeshBuildSource>();
                
                Bounds worldBounds;
                if (surface.collectObjects == CollectObjects.All)
                {
                    worldBounds = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 1000f));
                }
                else
                {
                    worldBounds = new Bounds(surface.transform.TransformPoint(surface.center), surface.size);
                }
                
                if (surface.collectObjects == CollectObjects.Children)
                {
                    NavMeshBuilder.CollectSources(surface.transform, surface.layerMask, surface.useGeometry, surface.defaultArea, markups, sources);
                }
                else
                {
                    NavMeshBuilder.CollectSources(worldBounds, surface.layerMask, surface.useGeometry, surface.defaultArea, markups, sources);
                }

                // Synchronous update
                NavMeshBuilder.UpdateNavMeshData(surface.navMeshData, settings, sources, worldBounds);
                surface.UpdateNavMesh(surface.navMeshData);
                
                // Automate the manual toggle fix
                surface.enabled = false;
                surface.enabled = true;
            }
        }
        _isDirty = false;
        _isUpdating = false;
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
                    
                    Bounds worldBounds;
                    if (surface.collectObjects == CollectObjects.All)
                    {
                        worldBounds = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 1000f));
                    }
                    else
                    {
                        worldBounds = new Bounds(surface.transform.TransformPoint(surface.center), surface.size);
                    }
                    
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
                    
                    // Automate the manual toggle fix to ensure the system registers the update
                    surface.enabled = false;
                    surface.enabled = true;
                    
                    float duration = Time.realtimeSinceStartup - startTime;
                }
            }
            _isUpdating = false;
        }
    }
}