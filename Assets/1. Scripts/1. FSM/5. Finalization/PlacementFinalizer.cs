using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlacementFinalizer : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;

    // Original no-arg entry point
    public GameObject FinalizePlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation)
    {
        return FinalizePlacement(root, offsets, data, rotation, null, false);
    }

    // Entry point with disabled-object tracking (for undo)
    public GameObject FinalizePlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation,
        List<GameObject> disabledObjects)
    {
        return FinalizePlacement(root, offsets, data, rotation, disabledObjects, false);
    }

    // Full implementation — silent=true suppresses FX (used for bulk yard-tile spawn)
    public GameObject FinalizePlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation,
        List<GameObject> disabledObjects,
        bool silent)
    {
        if (data == null || data.prefab == null)
            return null;

        // --- GROUND REPLACEMENT LOGIC ---
        if (IsGround(data))
        {
            DisableExistingGrounds(root, offsets, disabledObjects);
            // Foundations also displace yard floor tiles sitting in the same cells
            DisableExistingFloors(root, offsets, disabledObjects);
        }

        // --- FLOOR REPLACEMENT LOGIC ---
        if (data.isFloor)
        {
            if (IsSameFloorAlreadyThere(root, offsets, data))
                return null;

            // Replace different floors
            DisableExistingFloors(root, offsets, disabledObjects);
        }

        // --- BULLDOZER LOGIC ---
        if (data.ClearsGridAfterPlacement)
        {
            foreach (var o in offsets)
            {
                _grid.ClearCell(root + o, true);
            }
        }

        Vector3 pos = _grid.GetCellCenter(root);

        // For prefabs that carry a NavMeshAgent (humanoid workers, forklifts, etc.):
        // spawn at the ACTUAL stack height of this cell rather than y=0.
        // GetCellCenter always returns y=0, but the agent needs to start at the
        // walkable surface height (e.g. ~1.06 on a Foundation floor) so the
        // NavMeshAgent finds the correct NavMesh surface on its first frame.
        // If the floor NavMesh hasn't been baked yet AiNavigation.OnNavMeshBaked()
        // will re-snap once the bake completes.
        if (data.prefab != null && data.prefab.GetComponent<NavMeshAgent>() != null)
            pos.y = _grid.GetStackHeight(root);

        GameObject instance = Instantiate(data.prefab, pos, Quaternion.Euler(0f, rotation, 0f));
        instance.name = data.objName;

        // For freshly placed NavMesh agents: set transform position directly — don't
        // call Warp because the NavMesh may not be baked yet at this moment.
        var agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled)
        {
            if (agent.isOnNavMesh)
                agent.Warp(pos);
            else
                instance.transform.position = pos;
        }

        if (!silent)
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

        // For mobile agents, set Y from the actual renderer bounds of floor tiles in the cell.
        // objHeight is the tile's physical thickness (0.05f), not its elevation (1.06f),
        // so the stacking math would leave the agent at Y≈0 — fix that here.
        if (instance.GetComponent<NavMeshAgent>() != null)
        {
            float floorTopY = GetFloorTopY(root);
            instance.transform.position = new Vector3(pos.x, floorTopY, pos.z);
        }

        return instance;
    }

    private float GetFloorTopY(Vector2Int cell)
    {
        float topY = 0f;
        var cellObjects = _grid.GetObjectsInCell(cell);
        if (cellObjects == null) return topY;

        foreach (var entry in cellObjects)
        {
            if (entry.instance == null || entry.data == null) continue;
            // Include floor tiles AND Foundation/Grounds — previously only isFloor was
            // checked, so agents placed on a bare foundation got floorTopY=0 → y=0.
            bool isSurface = entry.data.isFloor
                || entry.data.category == "Foundation"
                || entry.data.category == "Grounds";
            if (!isSurface) continue;
            foreach (var r in entry.instance.GetComponentsInChildren<Renderer>())
                topY = Mathf.Max(topY, r.bounds.max.y);
        }
        return topY;
    }

    private bool IsGround(ObjDataSO data)
    {
        if (data == null) return false;
        return data.category == "Foundation" || data.category == "Grounds";
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
                    disabledObjects?.Add(entry.instance);
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
                if (entry.data != null && entry.data.isFloor
                    && entry.instance != null && entry.instance.activeSelf
                    && entry.data.id == data.id)
                    return true;
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
                    disabledObjects?.Add(entry.instance);
                }
            }
        }
    }
}
