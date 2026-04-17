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
        float stackY = 0f;

        // Floor tiles always sit at ground level
        if (!data.ignorePlacementRules && data.isStackable)
            stackY = _grid.GetStackHeight(root);

        Vector3 pos = _grid.GetCellCenter(root);
        pos.y = data.ignorePlacementRules ? 0f : pos.y + stackY;

        Quaternion rot = Quaternion.Euler(0f, rotation, 0f);

        GameObject placed = Instantiate(data.prefab, pos, rot, _parent);

        var bd = placed.GetComponent<BuildingData>();
        bd.Initialize(root, rotation, offsets);

        foreach (var o in offsets)
        {
            Vector3 dustPos = _grid.GetCellCenter(root + o);
            dustPos.y = pos.y;
            FXPool.Instance.Play("dust", dustPos);
        }

        if (!data.ClearsGridAfterPlacement)
        {
            foreach (var o in offsets)
                _grid.AddStackObject(root + o, placed, data);
        }

        return placed;
    }
}
