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

            // Hide any floor tile already in these cells — the free ground-plane yard tile, or a
            // real paid floor (pedestrian/MHE/shipping lane) the player had down before deciding
            // to build here. Without this, UpdateStackPositions lifts that floor to the TOP of the
            // new ground (floors stack above grounds), leaving it sitting visibly on top of grass /
            // flowerbed / foundation. This is a swap (golden rule: replace, never orphan) — the old
            // floor is disabled and tracked in disabledObjects so PlaceCommand/DragPlaceCommand can
            // refund its cost and undo/redo it, and DeleteCommand re-reveals it later.
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
        bool useWorldYOffset = false;

        // Determine base height - use the actual visual surface height (foundation/floor)
        // rather than the logical stack height, which can be inaccurate if SO values 
        // don't match mesh bounds.
        float baseHeight = GetFloorTopY(_grid, root);
        pos.y = baseHeight;

        // Apply worldYOffset as a relative additive offset
        if (data.worldYOffset != 0)
        {
            pos.y += data.worldYOffset;
            useWorldYOffset = true;
            Debug.Log($"[PlacementFinalizer] {data.objName}: Applied relative worldYOffset={data.worldYOffset}, pos.y={pos.y}");
        }

        GameObject instance = Instantiate(data.prefab, pos, Quaternion.Euler(0f, rotation, 0f));
        instance.name = data.objName;

        // If worldYOffset was used, re-apply after instantiation to override any prefab pivot offsets
        if (useWorldYOffset)
        {
            instance.transform.position = new Vector3(instance.transform.position.x, pos.y, instance.transform.position.z);
            Debug.Log($"[PlacementFinalizer] {data.objName}: Re-applied worldYOffset (relative) after instantiate. Now at Y={instance.transform.position.y}");
        }

        // Parent Foundations to a shared parent GameObject (performance optimization: one parent collider for all)
        if (data.category == "Foundation")
        {
            GameObject foundationsParent = GameObject.Find("Foundations");
            if (foundationsParent == null)
            {
                foundationsParent = new GameObject("Foundations");
                foundationsParent.SetActive(true);
            }
            instance.transform.SetParent(foundationsParent.transform);
        }

        // Parent all rack objects under RackingSystemManager for testing visibility.
        if (data.category == "Racking")
        {
            var rackingManager = Object.FindAnyObjectByType<RackingSystemManager>();
            if (rackingManager != null)
                instance.transform.SetParent(rackingManager.transform, worldPositionStays: true);
        }

        // For freshly placed NavMesh agents: set transform position directly — don't
        // call Warp because the NavMesh may not be baked yet at this moment.
        // SKIP if worldYOffset was used — don't let the agent override absolute positioning
        var agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && !useWorldYOffset)
        {
            if (agent.isOnNavMesh)
                agent.Warp(pos);
            else
                instance.transform.position = pos;
        }

        if (!silent && FXPool.Instance != null)
            FXPool.Instance.Play("dust", pos);

        // Initialize PlacedObject
        var po = instance.GetComponent<PlacedObject>();
        if (po != null)
            po.Initialize(data, root.x, root.y, (int)(rotation / 90f));

        instance.GetComponent<MHEOperatorSlot>()?.NotifyPlaced();

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
        // Skip this if worldYOffset was set — fixtures like lights use absolute positioning.
        if (instance.GetComponent<NavMeshAgent>() != null && !useWorldYOffset)
        {
            float floorTopY = GetFloorTopY(_grid, root);
            instance.transform.position = new Vector3(pos.x, floorTopY, pos.z);
        }

        Debug.Log($"[PlacementFinalizer] {data.objName}: Returning instance at Y={instance.transform.position.y} (useWorldYOffset={useWorldYOffset})");
        return instance;
    }

    // Static + grid passed in (rather than an instance method on _grid) so PlacementSystem's
    // save-restore path (SpawnFromSave) can reuse the exact same correction — it was missing
    // entirely there, leaving NavMeshAgent-bearing prefabs (vehicles) restored from a save at
    // the grid-cell-center height (~0) instead of the actual floor/foundation surface,
    // visibly clipped into the ground until something else moved them.
    public static float GetFloorTopY(PlacementGrid grid, Vector2Int cell)
    {
        float topY = 0f;
        var cellObjects = grid.GetObjectsInCell(cell);
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

    // Disables any active floor tile in these cells — used both for floor-over-floor
    // replacement (e.g. a pedestrian/MHE tile replacing a yard tile) and for a ground/
    // foundation being placed over an existing floor. Replacement is allowed by the golden
    // rule (floor tiles are swapped, never orphaned); the caller is responsible for refunding
    // the displaced tile's cost via disabledObjects.
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
