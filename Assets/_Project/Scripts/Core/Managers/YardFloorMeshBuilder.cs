using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds ONE combined mesh + ONE collider standing in for what used to be a real
/// PlacedObject GameObject (with its own collider, PlacedObject, BuildingData, etc.)
/// instantiated per grid cell — up to 10,000 of them on a 100x100 grid. That per-cell
/// Instantiate loop (GameContext.PopulateYardFloors) was the dominant cost behind the
/// multi-second load-time freeze and a steady stream of idle colliders for physics to track.
///
/// The combined mesh repeats the yard tile's ACTUAL source mesh (same vertices, same UVs,
/// same material) at each cell center via CombineInstance, so it is visually identical to
/// the old per-tile carpet — just one draw call and one collider instead of thousands of
/// GameObjects. Cells that already have a real floor/ground/foundation (scene-authored grass,
/// flowerbeds, yard pads, or anything restored from a save) are excluded from the combine so
/// the merged mesh never z-fights with what's already there.
///
/// This mesh carries NO PlacedObject/BuildingData — it isn't a placeable, sellable, or
/// individually-deletable object, just a backdrop. NavMeshManager.AddFloorNavMeshSources
/// independently injects the walkable NavMesh boxes for whatever cells this mesh covers
/// (see its "default yard ground" pass) — completely decoupled from this mesh's geometry,
/// consistent with how the rest of this game's floor NavMesh sourcing already works.
/// </summary>
public static class YardFloorMeshBuilder
{
    public static GameObject Build(PlacementGrid grid, ObjDataSO yardTileData, Transform parent = null)
    {
        if (grid == null || yardTileData == null || yardTileData.prefab == null)
        {
            Debug.LogWarning("[YardFloorMeshBuilder] Missing grid or yard tile data — nothing built.");
            return null;
        }

        var sourceMF = yardTileData.prefab.GetComponentInChildren<MeshFilter>();
        var sourceMR = yardTileData.prefab.GetComponentInChildren<MeshRenderer>();
        if (sourceMF == null || sourceMF.sharedMesh == null || sourceMR == null || sourceMR.sharedMaterial == null)
        {
            Debug.LogWarning("[YardFloorMeshBuilder] Yard tile prefab is missing a MeshFilter/MeshRenderer — nothing built.");
            return null;
        }

        Mesh sourceMesh = sourceMF.sharedMesh;
        Material sourceMat = sourceMR.sharedMaterial;

        var combine = new List<CombineInstance>(grid.Width * grid.Height);
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                var cell = new Vector2Int(x, y);

                // Skip any cell that already has a real surface (scene-authored grass/
                // flowerbeds/yard pads, or anything restored from a save) — the same check
                // PopulateYardFloors used to do per-tile before instantiating one.
                var list = grid.GetObjectsInCell(cell);
                bool hasSurface = false;
                if (list != null)
                {
                    foreach (var e in list)
                    {
                        if (e.data == null) continue;
                        if (e.data.isFloor || e.data.category == "Grounds" || e.data.category == "Foundation")
                        {
                            hasSurface = true;
                            break;
                        }
                    }
                }
                if (hasSurface) continue;

                combine.Add(new CombineInstance
                {
                    mesh      = sourceMesh,
                    transform = Matrix4x4.TRS(grid.GetCellCenter(cell), Quaternion.identity, Vector3.one),
                });
            }
        }

        var go = new GameObject("YardFloor");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;

        if (combine.Count == 0)
        {
            // Every cell already has a real surface (e.g. a fully-built-over save) — nothing
            // to carpet. Leave an empty, harmless placeholder rather than a meshless object.
            return go;
        }

        var combinedMesh = new Mesh
        {
            // 10,000 cells * 36 verts can exceed UInt16's 65,535-index ceiling.
            indexFormat = IndexFormat.UInt32,
        };
        combinedMesh.CombineMeshes(combine.ToArray(), mergeSubMeshes: true, useMatrices: true);
        combinedMesh.name = "YardFloor_Combined";
        combinedMesh.RecalculateBounds();

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = combinedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = sourceMat;

        // One collider for the whole carpet — ground/object raycasts hit this exactly like
        // they used to hit whichever individual tile was under the cursor; RaycastController's
        // _grid.WorldToCell(hit.point) conversion doesn't care what it hit.
        Bounds b = combinedMesh.bounds;
        var col = go.AddComponent<BoxCollider>();
        col.center = b.center;
        col.size   = new Vector3(b.size.x, Mathf.Max(b.size.y, 0.05f), b.size.z);

        // Match the source tile's layer so the existing ground-raycast LayerMask still hits it.
        go.layer = yardTileData.prefab.layer;

        return go;
    }
}
