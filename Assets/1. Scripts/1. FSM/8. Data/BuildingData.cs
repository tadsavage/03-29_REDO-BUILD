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

    public void Initialize(Vector2Int root, float rotation, Vector2Int[] offsets, ObjDataSO data = null)
    {
        if (data != null) objDataSO = data;

        RootCell = root;
        Rotation = rotation;
        Offsets = offsets;
        _obstacle = GetComponent<NavMeshObstacle>();
        _modifier = GetComponent<NavMeshModifier>();

        // Auto-configure navigation
        if (objDataSO != null)
        {
            SetupNavigation();
        }
        else
        {
            Debug.LogWarning($"[BuildingData] Initialized on {gameObject.name} without ObjDataSO!");
        }
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

        // 2. Identify objects that contribute to walkable surfaces (Floors, Foundations, etc.)
        bool isWalkableSurface = (Data.pathfindingClear || Data.isFloor || Data.ignorePlacementRules || Data.isStackable || Data.category == "Foundation" || Data.category == "Grounds");
        
        // Walls should only be walkable surfaces if they are explicitly marked as pathfindingClear (like Doors)
        if (Data.category == "Walls" && !Data.pathfindingClear)
        {
            isWalkableSurface = false;
        }

        if (isWalkableSurface)
        {
            if (_modifier == null)
                _modifier = GetComponent<NavMeshModifier>();
            if (_modifier == null)
                _modifier = gameObject.AddComponent<NavMeshModifier>();

            _modifier.applyToChildren = true;
            _modifier.overrideArea = true;
            _modifier.area = Data.navArea;

            // 🌟 DOOR/FLOOR FIX: 
            // - Foundations and Stackable volumes (Crates) MUST be baked (ignoreFromBuild = false) 
            //   so agents can walk ON top of them. They also carve the ground to prevent sinking.
            // - Doors and Clearance objects MUST NOT be baked (ignoreFromBuild = true)
            //   so agents can walk THROUGH them on the underlying ground NavMesh.
            if (Data.category == "Foundation" || Data.isStackable)
            {
                ConfigureObstacle();
                _modifier.ignoreFromBuild = false; 
            }
            else
            {
                if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                    DestroyImmediate(oldObstacle);
                
                _modifier.ignoreFromBuild = true;
            }

            return;
        }

        // 3. Standard blocking objects (Walls, Barriers, MHE)
        ConfigureObstacle();
        
        // 🌟 WALL FIX: Standard walls must be ignored from the geometry build (ignoreFromBuild = true)
        // because we are now including the 'Walls' layer in the NavMeshSurface mask.
        // This ensures they block via Carving only and don't create messy vertical geometry.
        if (_modifier == null) _modifier = GetComponent<NavMeshModifier>();
        if (_modifier == null) _modifier = gameObject.AddComponent<NavMeshModifier>();
        _modifier.ignoreFromBuild = true;
    }

    private void ConfigureObstacle()
    {
        if (_obstacle == null)
            _obstacle = gameObject.GetComponent<NavMeshObstacle>();

        if (_obstacle == null)
        {
            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
        }

        // Always configure the obstacle settings even if it already existed
        _obstacle.shape = NavMeshObstacleShape.Box;
        _obstacle.carving = true;
        _obstacle.carveOnlyStationary = true;

        // Calculate Size based on the footprint.
        // We use a factor of 0.75f to ensure there is a gap between obstacles 
        // at cell boundaries that is wide enough for agents to pass (at least 2x AgentRadius).
        float gridSpaceX = Data.footprint.x * 0.75f; 
        float gridSpaceZ = Data.footprint.y * 0.75f;
        
        float centerX = (Data.footprint.x - 1) * 0.665f; 
        float centerZ = (Data.footprint.y - 1) * 0.665f;

        _obstacle.size = new Vector3(gridSpaceX, Data.objHeight, gridSpaceZ);
        _obstacle.center = new Vector3(centerX, Data.objHeight * 0.45f, centerZ);
    }
    public void Delete()
    {
        Destroy(gameObject);
    }
}