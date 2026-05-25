using System;
using System.Collections.Generic;
using UnityEngine;

public class WallVisibilityManager : MonoBehaviour
{
    public static WallVisibilityManager Instance { get; private set; }

    [Header("Layer Configuration")]
    [Tooltip("The layer assigned to your main wall objects.")]
    [SerializeField] private LayerMask wallLayer;

    [Tooltip("Include any other layers (like Default, Props, or Decor) that your trim/roof pieces might be on.")]
    [SerializeField] private LayerMask overlappingDecorLayers;

    private List<WallData> _trackedWalls = new();
    private List<Renderer> _dynamicallyFoundDecor = new();
    private WallVisibilityMode _currentMode = WallVisibilityMode.Full;

    public enum WallVisibilityMode { Full, Cut, Hidden }

    private struct WallData
    {
        public GameObject gameObject;
        public List<Renderer> renderers;
        public List<Vector3> originalScales;
        public Collider mainCollider; // Used to detect things floating above it
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        RefreshWallList();
    }

    public void RefreshWallList()
    {
        _trackedWalls.Clear();

        string targetLayerName = LayerMaskToString(wallLayer);
        int actualLayerIndex = LayerMask.NameToLayer(targetLayerName);

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == actualLayerIndex)
            {
                Renderer[] rends = obj.GetComponentsInChildren<Renderer>(true);
                Collider col = obj.GetComponent<Collider>() ?? obj.GetComponentInChildren<Collider>();

                if (rends.Length > 0)
                {
                    List<Renderer> wallRends = new();
                    List<Vector3> scales = new();

                    foreach (var r in rends)
                    {
                        wallRends.Add(r);
                        scales.Add(r.transform.localScale);
                    }

                    _trackedWalls.Add(new WallData
                    {
                        gameObject = obj,
                        renderers = wallRends,
                        originalScales = scales,
                        mainCollider = col
                    });
                }
            }
        }
        Debug.Log($"[WallVisibilityManager] Tracking {_trackedWalls.Count} wall bases.");
    }

    public void SetVisibilityMode(WallVisibilityMode mode)
    {
        _currentMode = mode;

        if (_trackedWalls.Count == 0) RefreshWallList();

        // If restoring full view, make sure previously hidden dynamic items turn back on first
        if (_currentMode == WallVisibilityMode.Full)
        {
            foreach (var decorRend in _dynamicallyFoundDecor)
            {
                if (decorRend != null) decorRend.enabled = true;
            }
            _dynamicallyFoundDecor.Clear();
        }

        foreach (var wall in _trackedWalls)
        {
            if (wall.gameObject == null) continue;

            // 1. Toggle the main wall base renderers
            for (int i = 0; i < wall.renderers.Count; i++)
            {
                Renderer rend = wall.renderers[i];
                if (rend == null) continue;

                switch (_currentMode)
                {
                    case WallVisibilityMode.Full:
                        rend.enabled = true;
                        break;
                    case WallVisibilityMode.Cut:
                        rend.enabled = false; // Vanish main structure
                        break;
                }
            }

            // 2. COUNTERMEASURE: Scan the spatial air zone directly above the wall collider
            // This sweeps for unparented roof trims, headers, or floating decor components
            if (_currentMode == WallVisibilityMode.Cut && wall.mainCollider != null)
            {
                HideFloatingObjectsAbove(wall.mainCollider);
            }
        }
    }

    /// <summary>
    /// Projects a 3D box sweep upward into the air from the wall's location to catch and hide unparented trim pieces.
    /// </summary>
    private void HideFloatingObjectsAbove(Collider wallCollider)
    {
        Bounds b = wallCollider.bounds;

        // Position the scanning zone starting from the top center of the wall, extending 10 meters upward
        Vector3 scanCenter = new Vector3(b.center.x, b.max.y + 5f, b.center.z);
        Vector3 scanHalfExtents = new Vector3(b.extents.x * 1.1f, 5f, b.extents.z * 1.1f);

        // Combine your decor layer with your wall layer just in case
        LayerMask combinedScanMask = overlappingDecorLayers | wallLayer;

        Collider[] hits = Physics.OverlapBox(scanCenter, scanHalfExtents, wallCollider.transform.rotation, combinedScanMask);

        foreach (var hit in hits)
        {
            // Skip the wall itself
            if (hit.gameObject == wallCollider.gameObject || hit.transform.IsChildOf(wallCollider.transform))
                continue;

            Renderer r = hit.GetComponent<Renderer>() ?? hit.GetComponentInChildren<Renderer>();
            if (r != null && r.enabled)
            {
                r.enabled = false;
                _dynamicallyFoundDecor.Add(r); // Cache it so RAISE WALL can restore it later
            }
        }
    }

    public LayerMask GetDynamicPlacementMask(LayerMask defaultBaseMask)
    {
        if (_currentMode == WallVisibilityMode.Cut)
        {
            return defaultBaseMask & ~wallLayer;
        }
        return defaultBaseMask;
    }

    private string LayerMaskToString(LayerMask mask)
    {
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) != 0) return LayerMask.LayerToName(i);
        }
        return "Default";
    }
}
