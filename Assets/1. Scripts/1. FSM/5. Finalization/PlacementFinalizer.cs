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

        // If this is a floor, disable any existing floors in the footprint.
        if (data.isFloor)
            DisableExistingFloors(root, offsets, disabledFloors);

        GameObject instance = Instantiate(data.prefab);
        instance.name = data.objName;

        // Apply stack height for non-floor objects
        float stackY = 0f;
        if (!data.isFloor)
            stackY = _grid.GetStackHeight(root);

        Vector3 pos = _grid.GetCellCenter(root);
        pos.y += stackY;

        instance.transform.position = pos;
        instance.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
        FXPool.Instance.Play("dust", pos);

        // ⭐ ADD THIS ⭐
        var po = instance.GetComponent<PlacedObject>();
        if (po != null)
            po.Initialize(data, root.x, root.y, (int)(rotation / 90f));

        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            _grid.AddStackObject(cell, instance, data);
        }
        // FIX: If the object moves away immediately, remove it from the grid
        if (data.ClearsGridAfterPlacement)
        {
            foreach (var o in offsets)
            {
                Vector2Int cell = root + o;
                _grid.RemoveStackObject(cell, instance, data);
            }
        }

        var bd = instance.GetComponent<BuildingData>();
        if (bd != null)
            bd.Initialize(root, rotation, offsets);
<<<<<<< HEAD
        //Debug.Log($"Placed w/Finalizer {data.objName} at {root}");
=======

>>>>>>> parent of 3c2bc81 (Finally working Save load - good save window - everything working)
        return instance;
    }

    private void DisableExistingFloors(
        Vector2Int root,
        Vector2Int[] offsets,
        List<GameObject> disabledFloors)
    {
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

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
