using UnityEngine;

public class PlacementFinalizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlacementGrid _grid;

    [Header("FX")]
    [SerializeField] private GameObject dustPoofPrefab;
    [SerializeField] private GameObject shockwaveRingPrefab;

    /// <summary>
    /// Finalizes placement of a single object at a root cell.
    /// This is used by both single placement and drag placement.
    /// </summary>
    public GameObject FinalizePlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation)
    {
        // Spawn FX
        PlayPlacementFX(root, data);

        // Spawn object
        GameObject placed = SpawnObject(root, data, rotation);

        // Mark grid cells
        MarkGridCells(root, offsets, placed, data);

        return placed;
    }

    // ---------------------------------------------------------
    // FX
    // ---------------------------------------------------------
    private void PlayPlacementFX(Vector2Int root, ObjDataSO data)
    {
        if (shockwaveRingPrefab == null)
            return;

        Vector3 pos = _grid.GetCellCenter(root);
        pos.y += 0.05f;

        float scale = Mathf.Max(data.footprint.x, data.footprint.y);

        GameObject ring = Instantiate(shockwaveRingPrefab, pos, Quaternion.identity);
        ring.transform.localScale *= scale * 0.8f;

        foreach (var ps in ring.GetComponentsInChildren<ParticleSystem>())
            ps.Play(true);
    }

    // ---------------------------------------------------------
    // Object Spawn
    // ---------------------------------------------------------
    private GameObject SpawnObject(Vector2Int root, ObjDataSO data, float rotation)
    {
        GameObject placed = Instantiate(data.prefab);
        placed.transform.position = _grid.GetCellCenter(root);
        placed.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
        return placed;
    }

    // ---------------------------------------------------------
    // Grid Occupancy
    // ---------------------------------------------------------
    private void MarkGridCells(Vector2Int root, Vector2Int[] offsets, GameObject placed, ObjDataSO data)
    {
        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;
            _grid.SetOccupied(cell, placed, data);
        }

        // Optional: if the object clears the grid after placement
        if (data.ClearsGridAfterPlacement)
        {
            foreach (var offset in offsets)
            {
                Vector2Int cell = root + offset;
                _grid.ClearCell(cell);
            }
        }
    }

    // ---------------------------------------------------------
    // Public helper for drag placement FX
    // ---------------------------------------------------------
    public void SpawnDust(Vector3 pos)
    {
        if (dustPoofPrefab != null)
            Instantiate(dustPoofPrefab, pos, Quaternion.identity);
    }
}
