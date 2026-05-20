using UnityEngine;
using System.Collections.Generic;

public class CellIndicatorController : MonoBehaviour
{
    // =========================================================
    //  COLORS
    // =========================================================
    [Header("Build Colors")]
    [SerializeField] private Color buildValidColor = new Color(.1f, .25f, .65f, .50f);
    [SerializeField] private Color buildInvalidColor = new Color(1f, .2f, .2f, .75f);
    [SerializeField] private Color moveValidColor = new Color(0f, .9f, .1f, .85f);
    [SerializeField] private Color moveInvalidColor = new Color(1f, .2f, .2f, .75f);

    [Header("Delete Color")]
    [SerializeField] private Color deleteColor = new Color(1f, 1f, .20f, .45f);

    // =========================================================
    //  PREFAB + GRID
    // =========================================================
    [Header("Indicator Prefab")]
    [SerializeField] private GameObject indicatorPrefab;

    [SerializeField] private PlacementGrid grid;

    private float yOffset = 0.0f;

    // =========================================================
    //  INTERNAL STATE
    // =========================================================
    private readonly List<GameObject> _active = new();
    private readonly Stack<GameObject> _pool = new();

    private MaterialPropertyBlock _mpb;

    private enum IndicatorMode
    {
        Build,
        Delete,
        Move
    }

    private IndicatorMode _mode = IndicatorMode.Build;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    // =========================================================
    //  PUBLIC API — MODES
    // =========================================================
    public void UseBuildMode()
    {
        _mode = IndicatorMode.Build;
        ClearAll();
    }

    public void UseDeleteMode()
    {
        _mode = IndicatorMode.Delete;
        ClearAll();
    }
    public void UseMoveMode()
    {
        _mode = IndicatorMode.Move;
        ClearAll();
    }

    // =========================================================
    //  PUBLIC API — SINGLE CELL
    // =========================================================
    public void ShowCell(Vector2Int cell, bool isValid = true)
    {
        ClearActive();

        float stackY = grid.GetStackHeight(cell);

        GameObject ind = GetIndicator();
        Vector3 pos = grid.GetCellCenter(cell);
        pos.y += stackY + yOffset;
        ind.transform.position = pos;

        ApplyBuildOrDeleteColor(ind, isValid);

        _active.Add(ind);
    }

    // =========================================================
    //  PUBLIC API — MULTI-CELL FOOTPRINT
    // =========================================================
    public void ShowCells(List<Vector2Int> cells, System.Func<Vector2Int, bool> isCellValid)
    {
        ClearActive();

        foreach (var cell in cells)
        {
            float stackY = grid.GetStackHeight(cell);

            GameObject ind = GetIndicator();
            Vector3 pos = grid.GetCellCenter(cell);
            pos.y += stackY + yOffset;
            ind.transform.position = pos;

            bool valid = isCellValid(cell);
            ApplyBuildOrDeleteColor(ind, valid);

            _active.Add(ind);
        }
    }

    // =========================================================
    //  CLEAR
    // =========================================================
    public void ClearAll()
    {
        ClearActive();
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

    // =========================================================
    //  INTERNAL HELPERS
    // =========================================================
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

    private void ApplyBuildOrDeleteColor(GameObject ind, bool isValid)
    {
        var renderer = ind.GetComponent<Renderer>();
        if (!renderer)
            return;

        renderer.GetPropertyBlock(_mpb);

        if (_mode == IndicatorMode.Delete)
        {
            _mpb.SetColor("_BaseColor", deleteColor);
        }
        else if (_mode == IndicatorMode.Move)
        {
            _mpb.SetColor("_BaseColor", isValid ? moveValidColor : moveInvalidColor);
        }   
        else
        {
            _mpb.SetColor("_BaseColor", isValid ? buildValidColor : buildInvalidColor);
        }

        renderer.SetPropertyBlock(_mpb);
    }
}
