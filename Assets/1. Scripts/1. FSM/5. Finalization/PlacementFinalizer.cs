using UnityEngine;

public class PlacementFinalizer : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;

    public void FinalizePlacement(Vector2Int root, Vector2Int[] offsets, ObjDataSO data, float rotation)
    {
        GameObject placed = Instantiate(data.prefab);
        placed.transform.position = _grid.GetCellCenter(root);
        placed.transform.rotation = Quaternion.Euler(0f, rotation, 0f);

        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;
            _grid.SetOccupied(cell, placed);
        }
    }
}
