using UnityEngine;

public class CellIndicatorController : MonoBehaviour
{
    [SerializeField] private GameObject indicatorQuad;
    [SerializeField] private PlacementGrid grid;

    public void ShowAtCell(Vector2Int cell)
    {
        indicatorQuad.SetActive(true);
        indicatorQuad.transform.position = grid.CellToWorld(cell);
        Debug.Log($"Indicator world pos = {indicatorQuad.transform.position}");


    }

    public void Hide()
    {   
        indicatorQuad.SetActive(false);
    }
}