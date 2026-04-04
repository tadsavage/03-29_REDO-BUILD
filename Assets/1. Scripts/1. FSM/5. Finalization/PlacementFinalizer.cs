using UnityEngine;

public class PlacementFinalizer : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;
    public GameObject dustPoofPrefab;
    [SerializeField] private GameObject shockwaveRingPrefab;

    public void FinalizePlacement(Vector2Int root, Vector2Int[] offsets, ObjDataSO data, float rotation)
    {   
        // Spawn ring poof effect
        Vector3 ringPos = _grid.GetCellCenter(root);
        ringPos.y += 0.05f;
        float scale = Mathf.Max(data.footprint.x, data.footprint.y);
        var ring = Instantiate(shockwaveRingPrefab, ringPos, Quaternion.identity);
        foreach (var ps in ring.GetComponentsInChildren<ParticleSystem>())
            ps.Play(true);
        ring.transform.localScale *= scale * 0.8f;

        // 1. Spawn the object
        GameObject placed = Instantiate(data.prefab);
        placed.transform.position = _grid.GetCellCenter(root);
        placed.transform.rotation = Quaternion.Euler(0f, rotation, 0f);

        // 2. Mark grid cells as occupied
        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;
            _grid.SetOccupied(cell, placed);
        }

        // 3. If this object moves after placement, clear the grid cells immediately
        if (data.ClearsGridAfterPlacement)
        {
            foreach (var offset in offsets)
            {
                Vector2Int cell = root + offset;
                _grid.Clear(cell);
            }
        }

        // 4. Play placement sound effect
        // Play placement sound
        AudioManager.Play("ValidPlace");
    }
}
