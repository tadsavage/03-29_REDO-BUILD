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
        _obstacle = GetComponent<NavMeshObstacle>();
        _modifier = GetComponent<NavMeshModifier>();

        // Auto-configure navigation
        SetupNavigation();
    }

    private void SetupNavigation()
    {
        // 1. Anything that moves through the building (Forklifts, Humans) should be able to pathfind through clearance objects (Racks, Doors) but NOT be blocked by them.
        if (Data.ClearsGridAfterPlacement)
        {
            if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                DestroyImmediate(oldObstacle);
            if (TryGetComponent<NavMeshModifier>(out var oldModifier))
                DestroyImmediate(oldModifier);
            return;
        }
        // 2. Clearance objects (Racks, Doors) should be BAKED but NOT have obstacles.
        // This allows different NavMesh surfaces (Humanoid vs MHE) to handle clearance height naturally.
        if (Data.pathfindingClear || Data.isFloor || Data.ignorePlacementRules)
        {
            // Set layer to Ground (3) to ensure collection by NavMeshSurface
            gameObject.layer = LayerMask.NameToLayer("Ground");
            
            if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                DestroyImmediate(oldObstacle);

            if (_modifier == null)
                _modifier = GetComponent<NavMeshModifier>();
            if (_modifier == null)
                _modifier = gameObject.AddComponent<NavMeshModifier>();

            _modifier.applyToChildren = true;
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
        _obstacle.size = new Vector3(gridSpaceX * 1f, Data.objHeight, gridSpaceZ * 1f);

        //if the footprint.x is larger than 1 then we need to make the center of the obstacle half of the amount the footprint is over 1 times -1.33f to pull the center back towards the middle of the object. If the footprint is 1 then we want the center to be at 0. If the footprint is less than 1 then we want the center to be at 0 as well.
        float centerX = (Data.footprint.x - 1) * -0.665f; // 0.665 is half of 1.33
        float centerY = (Data.footprint.y - 1) * -0.665f; // 0.665 is half of 1.33
        // Y is slightly below center of the object for better carving results
        _obstacle.center = new Vector3(centerX, Data.objHeight * 0.45f, centerY); 
    }
    public void Delete()
    {
        Destroy(gameObject);
    }
}