using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// Ensures NavMeshSurface components are enabled and refreshed on startup.
/// This automates the manual "disable/enable" toggle required to activate them in some cases.
/// </summary>
[DefaultExecutionOrder(-50)]
public class NavMeshSurfaceInitializer : MonoBehaviour
{
    private NavMeshSurface _surface;

    private void Awake()
    {
        _surface = GetComponent<NavMeshSurface>();
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_surface == null) _surface = GetComponent<NavMeshSurface>();
        
        if (_surface != null)
        {
            // The "nudge": toggling enabled forces the surface to register its NavMeshData
            // with the navigation system.
            _surface.enabled = false;
            _surface.enabled = true;
        }
    }
}
