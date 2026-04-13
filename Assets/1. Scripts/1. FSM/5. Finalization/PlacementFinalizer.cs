using UnityEngine;

public class PlacementFinalizer : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;
    [SerializeField] private Transform _parent;      // Optional parent for placed objects
    [SerializeField] private GameObject dustPrefab;  // Optional dust FX

    // ---------------------------------------------------------
    // FINALIZE SINGLE OR DRAG PLACEMENT
    // ---------------------------------------------------------
    public GameObject FinalizePlacement(Vector2Int root, Vector2Int[] offsets, ObjDataSO data, float rotation)
    {
        // ================================
        // STACKING: compute vertical offset
        // If object is stackable, place it on top of existing stack height
        // ================================
        float stackY = 0f;
        if (data.isStackable)
            stackY = _grid.GetStackHeight(root);
        // ================================

        Vector3 pos = _grid.GetCellCenter(root);
        pos.y += stackY;
        Quaternion rot = Quaternion.Euler(0f, rotation, 0f);

        GameObject placed = Instantiate(data.prefab, pos, rot, _parent);

        var bd = placed.GetComponent<BuildingData>();
        bd.Initialize(root, rotation, offsets);
       
        foreach (var o in offsets)
        {
            pos = _grid.GetCellCenter(root + o);
            pos.y += stackY; // Align dust effect with stack height
            FXPool.Instance.Play("dust", pos);
        }

        // ================================
        // STACKING: register object in grid
        // Each footprint cell gets the same placed instance
        // ================================
        if (!data.ClearsGridAfterPlacement)
        {
            foreach (var o in offsets)
            {
                Vector2Int cell = root + o;
                _grid.AddStackObject(cell, placed, data);
            }
        }
        // ================================

        return placed;
    }
}
