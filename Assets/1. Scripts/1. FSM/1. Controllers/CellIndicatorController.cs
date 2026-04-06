using UnityEngine;
using System.Collections.Generic;

public class CellIndicatorController : MonoBehaviour
{
    [Header("Colors")]
    private Color validColor = new Color(.25f,1f,.30f,.70f);
    private Color invalidColor = new Color(1f,.22f,.22f,.85f);

    [Header("Indicator Prefab")]
    [SerializeField] private GameObject indicatorPrefab;

    [SerializeField] private PlacementGrid grid;
    [SerializeField] private float yOffset = 0.15f;

    private readonly List<GameObject> _active = new();
    private readonly Stack<GameObject> _pool = new();

    private MaterialPropertyBlock _mpb;

    // For single-cell mode
    private Vector2Int _lastRoot = new Vector2Int(int.MinValue, int.MinValue);

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    // ---------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------

    /// <summary>
    /// Multi-cell indicator for drag placement.
    /// Each cell gets its own indicator tile.
    /// </summary>
    public void ShowCells(Vector2Int root, Vector2Int[] offsets, PlacementGrid grid, bool isValid)
    {
        // This version is used for single-cell placement
        // Drag placement uses ShowCell() below

        _lastRoot = root;

        ClearActive();

        Color color = isValid ? validColor : invalidColor;

        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;
            Vector3 pos = grid.GetCellCenter(cell) + new Vector3(0, yOffset, 0);

            GameObject ind = GetIndicator();
            ind.transform.position = pos;

            ApplyColor(ind, color);

            _active.Add(ind);
        }
    }

    /// <summary>
    /// Show a single indicator tile for a specific cell.
    /// Used for drag rectangle placement.
    /// </summary>
    public void ShowCell(Vector2Int cell, bool isValid)
    {
        GameObject ind = GetIndicator();

        ind.transform.position = grid.GetCellCenter(cell) + new Vector3(0, yOffset, 0);

        ApplyColor(ind, isValid ? validColor : invalidColor);

        _active.Add(ind);
    }

    public void Hide()
    {
        ClearActive();
        _lastRoot = new Vector2Int(int.MinValue, int.MinValue);
    }

    public void ClearAll()
    {
        ClearActive();
        _lastRoot = new Vector2Int(int.MinValue, int.MinValue);
    }

    // ---------------------------------------------------------
    // INTERNAL HELPERS
    // ---------------------------------------------------------

    private GameObject GetIndicator()
    {
        if (_pool.Count > 0)
        {
            var go = _pool.Pop();
            go.SetActive(true);
            return go;
        }

        return Instantiate(indicatorPrefab);
    }

    private void ClearActive()
    {
        foreach (var ind in _active)
        {
            ind.SetActive(false);
            _pool.Push(ind);
        }
        _active.Clear();
    }

    private void ApplyColor(GameObject ind, Color color)
    {
        var renderer = ind.GetComponent<Renderer>();
        renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(_mpb);
    }
}
