using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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

    private bool IsGround(ObjDataSO data)
    {
        if (data == null) return false;
        return data.category == "Foundation" || data.category == "Grounds";
    }

    // Extended: can track disabled objects for undo
    public GameObject FinalizePlacement(
     Vector2Int root,
     Vector2Int[] offsets,
     ObjDataSO data,
     float rotation,
     List<GameObject> disabledObjects)
    {
        if (data == null || data.prefab == null)
            return null;

        // --- GROUND REPLACEMENT LOGIC ---
        if (IsGround(data))
        {
            if (IsSameGroundAlreadyThere(root, offsets, data))
                return null;

            DisableExistingGrounds(root, offsets, disabledObjects);
        }

        // --- FLOOR REPLACEMENT LOGIC ---
        if (data.isFloor)
        {
            if (IsSameFloorAlreadyThere(root, offsets, data))
            {
                // "if we're just the same type of floor though then do nothing"
                return null;
            }
            // Replace different floors
            DisableExistingFloors(root, offsets, disabledObjects);
        }
        // --- BULLDOZER LOGIC ---
        if (data.ClearsGridAfterPlacement)
        {
            foreach (var o in offsets)
            {
                _grid.ClearCell(root + o, true); // true to destroy objects
            }
        }
        GameObject instance = Instantiate(data.prefab);
        instance.name = data.objName;

        // Position will be set by UpdateStackPositions called via AddStackObject
        // but we still need a reasonable starting point for FX etc.
        Vector3 pos = _grid.GetCellCenter(root);
        instance.transform.position = pos;
        instance.transform.rotation = Quaternion.Euler(0f, rotation, 0f);

        // Handle NavMeshAgent warping safely
        var agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Warp(pos);
        }
        else
        {
            instance.transform.position = pos;
        }

        FXPool.Instance.Play("dust", pos);

        // Initialize PlacedObject
var po = instance.GetComponent<PlacedObject>();
        if (po != null)
            po.Initialize(data, root.x, root.y, (int)(rotation / 90f));

        // Initialize BuildingData BEFORE adding to grid so UpdateStackPositions works
        var bd = instance.GetComponent<BuildingData>();
        if (bd != null)
            bd.Initialize(root, rotation, offsets, data);

        // Add to grid
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            _grid.AddStackObject(cell, instance, data);
        }

        return instance;
    }
    private bool IsSameGroundAlreadyThere(Vector2Int root, Vector2Int[] offsets, ObjDataSO data)
    {
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

            foreach (var entry in list)
            {
                if (IsGround(entry.data) && entry.data.id == data.id)
                    return true;
            }
        }
        return false;
    }
    private void DisableExistingGrounds(Vector2Int root, Vector2Int[] offsets, List<GameObject> disabledObjects)
    {
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var entry = list[i];
                if (IsGround(entry.data) && entry.instance != null && entry.instance.activeSelf)
                {
                    entry.instance.SetActive(false);
                    if (disabledObjects != null)
                        disabledObjects.Add(entry.instance);
                }
            }
        }
    }
    private bool IsSameFloorAlreadyThere(Vector2Int root, Vector2Int[] offsets, ObjDataSO data)
    {
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

            foreach (var entry in list)
            {
                if (entry.data != null && entry.data.isFloor)
                {
                    if (entry.data.id == data.id) 
                        return true;
                }
            }
        }
        return false;
    }
    private void DisableExistingFloors(Vector2Int root, Vector2Int[] offsets, List<GameObject> disabledObjects)
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

                    if (disabledObjects != null)
                        disabledObjects.Add(entry.instance);
                }
            }
        }
    }
}
