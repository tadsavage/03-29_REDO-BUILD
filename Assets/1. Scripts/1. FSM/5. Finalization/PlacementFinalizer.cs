using System.Collections.Generic;
using UnityEngine;

public class PlacementFinalizer : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;

    // Original entry point
    public GameObject FinalizePlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation)
    {
        return FinalizePlacement(root, offsets, data, rotation, null);
    }

    // Extended: can track disabled floors for undo
    public GameObject FinalizePlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation,
        List<GameObject> disabledFloors)
    {
        if (data == null || data.prefab == null)
            return null;

        // ---------------------------------------------------------
        // 1. Disable floors under footprint (if placing a floor)
        // ---------------------------------------------------------
        if (data.isFloor)
            DisableExistingFloors(root, offsets, disabledFloors);

        // ---------------------------------------------------------
        // 2. Instantiate object
        // ---------------------------------------------------------
        GameObject instance = Instantiate(data.prefab);
        instance.name = data.objName;

        // ---------------------------------------------------------
        // 3. Compute correct stack height
        // ---------------------------------------------------------
        float stackY = 0f;
        if (!data.isFloor)
            stackY = _grid.GetStackHeight(root);

        Vector3 pos = _grid.GetCellCenter(root);
        pos.y += stackY;

        instance.transform.position = pos;
        instance.transform.rotation = Quaternion.Euler(0f, rotation, 0f);

        FXPool.Instance.Play("dust", pos);

        // ---------------------------------------------------------
        // 4. Initialize PlacedObject (save/load consistency)
        // ---------------------------------------------------------
        var po = instance.GetComponent<PlacedObject>();
        if (po != null)
            po.Initialize(data, root.x, root.y, (int)(rotation / 90f));

        // ---------------------------------------------------------
        // 5. Add to grid for ALL footprint cells
        // ---------------------------------------------------------
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            _grid.AddStackObject(cell, instance, data);
        }

        // ---------------------------------------------------------
        // 6. Initialize BuildingData (MoveState consistency)
        // ---------------------------------------------------------
        var bd = instance.GetComponent<BuildingData>();
        if (bd != null)
            bd.Initialize(root, rotation, offsets);

        return instance;
    }

    // ---------------------------------------------------------
    // Disable floors under footprint (for floor placement)
    // ---------------------------------------------------------
    private void DisableExistingFloors(
        Vector2Int root,
        Vector2Int[] offsets,
        List<GameObject> disabledFloors)
    {
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null)
                continue;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var entry = list[i];

                if (entry.data == null || !entry.data.isFloor)
                    continue;

                if (entry.instance == null)
                    continue;

                if (entry.instance.activeSelf)
                {
                    entry.instance.SetActive(false);

                    if (disabledFloors != null)
                        disabledFloors.Add(entry.instance);
                }
            }
        }
    }
}
