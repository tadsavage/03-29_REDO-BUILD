using UnityEngine;
using System.Collections.Generic;

public class CellIndicatorController : MonoBehaviour
{
    [SerializeField] private GameObject indicatorQuad;
    [SerializeField] private PlacementGrid grid;
    [SerializeField] private float yOffset = 0.1f;
    Vector2 lastPos = Vector2.zero;

    private readonly List<GameObject> _activeIndicators = new();

    public void ShowAtCell(Vector2Int cell)
    {
        if (lastPos != cell)
        {
            PlayCellChangeSoundEffect();
        }
        Vector3 cellOffset = new Vector3(0, yOffset, 0); // Set the Y position to the desired offset
        indicatorQuad.transform.position = grid.GetCellCenter(cell) + cellOffset;
        indicatorQuad.SetActive(true);
        
        lastPos = cell;
    }

    public void Hide()
    {   
        indicatorQuad.SetActive(false);
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

    public void ShowCells(Vector2Int root, Vector2Int[] offsets, PlacementGrid grid)
    {
        ClearAll();

        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;
            Vector3 pos = grid.GetCellCenter(cell);

            GameObject ind = Instantiate(indicatorQuad, pos, Quaternion.identity); // ??? ins indicator prefab right?
            _activeIndicators.Add(ind);
        }
    }
}