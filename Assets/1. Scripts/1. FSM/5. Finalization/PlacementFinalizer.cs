using UnityEngine;

public class PlacementFinalizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlacementGrid _grid;

    [Header("FX")]
    [SerializeField] private GameObject dustPoofPrefab;
    [SerializeField] private GameObject shockwaveRingPrefab;

    public GameObject FinalizePlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation)
    {
        PlayPlacementFX(root, data);

        GameObject placed = SpawnObject(root, data, rotation);

        MarkGridCells(root, offsets, placed, data);

        return placed;
    }

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

    private GameObject SpawnObject(Vector2Int root, ObjDataSO data, float rotation)
    {
        GameObject placed = Instantiate(data.prefab);
        placed.transform.position = _grid.GetCellCenter(root);
        placed.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
        return placed;
    }

    // ⭐ Only occupy grid for non-clearing objects
    private void MarkGridCells(Vector2Int root, Vector2Int[] offsets, GameObject placed, ObjDataSO data)
    {
        if (!data.ClearsGridAfterPlacement)
        {
            foreach (var offset in offsets)
            {
                Vector2Int cell = root + offset;
                _grid.SetOccupied(cell, placed, data);
            }
        }
        else
        {
            // Object is visual-only in terms of grid occupancy
            foreach (var offset in offsets)
            {
                Vector2Int cell = root + offset;
                _grid.ClearCell(cell, destroyObject: false);
            }
        }
    }

    public void SpawnDust(Vector3 pos)
    {
        if (dustPoofPrefab != null)
            Instantiate(dustPoofPrefab, pos, Quaternion.identity);
    }
}
