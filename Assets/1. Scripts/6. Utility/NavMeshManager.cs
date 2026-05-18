using UnityEngine;
using Unity.AI.Navigation;
using System.Collections;
using System.Collections.Generic; // Added for List

public class NavMeshManager : MonoBehaviour
{
    public static NavMeshManager Instance { get; private set; }

    // Changed from singular "Surface" to a List of "Surfaces"
    [SerializeField] private List<NavMeshSurface> _surfaces = new List<NavMeshSurface>();
    [SerializeField] private float _debounceTime = 0.5f;

    private Coroutine _updateCoroutine;
    private bool _isDirty;

    private void Awake()
    {
        Instance = this;
        
        // Clear and re-find all surfaces to ensure none are missed (especially if scene structure changed)
        _surfaces = new List<NavMeshSurface>(Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None));
    }

    private string GetAgentIDs()
    {
        if (_surfaces == null) return "None";
        string ids = "";
        foreach (var s in _surfaces) if (s != null) ids += s.agentTypeID + " ";
        return ids;
    }

    public void MarkDirty()
    {
        _isDirty = true;
        if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
        _updateCoroutine = StartCoroutine(DebounceUpdate());
    }

    public void BakeImmediate()
    {
        if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
        
        foreach (var surface in _surfaces)
        {
            if (surface != null)
            {
                // Full rebuild is more reliable after a major scene load
                surface.BuildNavMesh();
            }
        }
        _isDirty = false;
        //Debug.Log("NavMesh Rebuilt Immediately (Load Phase).");
    }

    private IEnumerator DebounceUpdate()
    {
        yield return new WaitForSeconds(_debounceTime);

        if (_isDirty)
        {
            // Loop through all surfaces and update each one
            foreach (var surface in _surfaces)
            {
                if (surface != null && surface.navMeshData != null)
                {
                    surface.UpdateNavMesh(surface.navMeshData);
                }
            }
            _isDirty = false;
            Debug.Log("All NavMeshes Updated!");
        }
    }
}