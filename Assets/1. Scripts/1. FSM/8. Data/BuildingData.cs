using UnityEngine;
using UnityEngine.AI; // Required for NavMeshObstacle
using Unity.AI.Navigation;

public class BuildingData : MonoBehaviour
{
    [SerializeField] private ObjDataSO objDataSO;
    public ObjDataSO Data => objDataSO;

    public Vector2Int RootCell { get; private set; }
    public float Rotation { get; private set; }
    public Vector2Int[] Offsets { get; private set; }

    private NavMeshObstacle _obstacle;
    private NavMeshModifier _modifier;

    public void Initialize(Vector2Int root, float rotation, Vector2Int[] offsets)
    {
        RootCell = root;
        Rotation = rotation;
        Offsets = offsets;

        // Auto-configure navigation
        SetupNavigation();
    }

    private void SetupNavigation()
    {
        // 1. Floors and "Ignore Rules" objects shouldn't block AI at all
        if (Data.isFloor || Data.ignorePlacementRules || Data.ClearsGridAfterPlacement)
        {
            if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                Destroy(oldObstacle);

            if (_modifier == null)
                _modifier = GetComponent<NavMeshModifier>();
            if (_modifier == null)
                _modifier = gameObject.AddComponent<NavMeshModifier>();
            
            _modifier.ignoreFromBuild = true;
            return;
        }

        // 2. Clearance objects (Racks, Doors) should be BAKED but NOT have obstacles.
        // This allows different NavMesh surfaces (Humanoid vs MHE) to handle clearance height naturally.
        if (Data.pathfindingClear)
        {
            if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                Destroy(oldObstacle);

            if (_modifier == null)
                _modifier = GetComponent<NavMeshModifier>();
            if (_modifier == null)
                _modifier = gameObject.AddComponent<NavMeshModifier>();

            // Do NOT ignore from build - we want the geometry (legs, headers) to be baked.
            _modifier.ignoreFromBuild = false; 
            
            // Apply area override if specified
            _modifier.overrideArea = true;
            _modifier.area = Data.navArea;

            return;
        }

        // 3. Normal blocking objects get a NavMeshObstacle
        if (_obstacle == null)
            _obstacle = gameObject.GetComponent<NavMeshObstacle>();

        if (_obstacle == null)
            _obstacle = gameObject.AddComponent<NavMeshObstacle>();

        // 3. Configure for "Carving" (The "Option 1" approach)
        _obstacle.shape = NavMeshObstacleShape.Box;
        _obstacle.carving = true;
        _obstacle.carveOnlyStationary = true; // Best for performance in a building game

        // 4. Calculate Size based on the footprint
        // We use the grid dimensions from ObjDataSO to ensure the "hole" matches the grid
        float gridSpaceX = Data.footprint.x * 1.33f; // 1.33 is your grid CellSize
        float gridSpaceZ = Data.footprint.y * 1.33f;

        // Center it (assuming the pivot is at the corner/center based on your PlacementMath)
        // If your pivots are already centered, center is zero. 
        // If your pivots are at the corner, you'd offset the center by half the size.
        _obstacle.size = new Vector3(gridSpaceX * .65f, Data.objHeight, gridSpaceZ * .65f);
        _obstacle.center = new Vector3(0, Data.objHeight*.45f, 0);
    }

    public void Delete()
    {
        Destroy(gameObject);
    }
}