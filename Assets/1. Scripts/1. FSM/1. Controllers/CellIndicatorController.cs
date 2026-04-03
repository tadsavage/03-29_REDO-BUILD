using UnityEngine;
using System.Collections.Generic;

public class CellIndicatorController : MonoBehaviour
{   // Colors for valid and invalid placements
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.50f); // Green with 50% opacity
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.50f); // Red with 50% opacity
    // MaterialPropertyBlock for efficient material property changes
    private MaterialPropertyBlock _mpb;

    [SerializeField] private GameObject singleIndicator;   // scene object
    [SerializeField] private GameObject indicatorPrefab;   // prefab for multi-cell
    [SerializeField] private PlacementGrid grid;
    [SerializeField] private float yOffset = 0.1f;
    Vector2 lastPos = Vector2.zero;

    private readonly List<GameObject> _activeIndicators = new();

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    public void ShowAtCell(Vector2Int cell)
    {
        Vector3 cellOffset = new Vector3(0, yOffset, 0);
        singleIndicator.transform.position = grid.GetCellCenter(cell) + cellOffset;
        singleIndicator.SetActive(true);

        lastPos = cell;
    }

    public void Hide()
    {
        singleIndicator.SetActive(false);
    }
    private void PlayCellChangeSoundEffect()
    {
        AudioManager.Play("ValidPlace");
    }
    public void ClearAll()
    {
        foreach (var ind in _activeIndicators)
            Destroy(ind);

        _activeIndicators.Clear();
    }

    public void ShowCells(Vector2Int root, Vector2Int[] offsets, PlacementGrid grid, bool isValid)
    {
        ClearAll();

        if (lastPos != root)
        {
            PlayCellChangeSoundEffect();
        }

        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;
            Vector3 pos = grid.GetCellCenter(cell);

            GameObject ind = Instantiate(indicatorPrefab, pos, Quaternion.identity);
            _activeIndicators.Add(ind);

            // Apply color
            var renderer = ind.GetComponent<Renderer>();
            renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", isValid ? validColor : invalidColor);
            renderer.SetPropertyBlock(_mpb);
        }
        lastPos = root;
    }
}