using UnityEngine;
using UnityEngine.AI;
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

        if (objDataSO != null)
            SetupNavigation();
        else
            Debug.LogWarning($"[BuildingData] Initialized on {gameObject.name} without ObjDataSO!");
    }

    private void SetupNavigation()
    {
        // Objects with a NavMeshAgent are dynamic agents — never add an obstacle to them.
        if (GetComponent<NavMeshAgent>() != null) return;

        // 1. ClearsGridAfterPlacement objects (Racks, Doors) — agents pass through freely.
        if (Data.ClearsGridAfterPlacement)
        {
            if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                DestroyImmediate(oldObstacle);
            if (TryGetComponent<NavMeshModifier>(out var oldModifier))
                DestroyImmediate(oldModifier);
            return;
        }

        // 2. Walkable surfaces (Floors, Foundations, Stairs, etc.)
        bool isWalkableSurface = Data.pathfindingClear || Data.isFloor || Data.ignorePlacementRules
                              || Data.isStackable || Data.CanUseStairs
                              || Data.category == "Foundation" || Data.category == "Grounds";

        if (Data.category == "Walls" && !Data.pathfindingClear)
            isWalkableSurface = false;

        if (isWalkableSurface)
        {
            if (_modifier == null) _modifier = GetComponent<NavMeshModifier>();
            if (_modifier == null) _modifier = gameObject.AddComponent<NavMeshModifier>();

            _modifier.applyToChildren = true;
            _modifier.overrideArea = true;
            _modifier.area = Data.navArea;

            if (Data.category == "Foundation" || Data.category == "Grounds")
            {
                // Ground surfaces must never have a carving obstacle — it punches a
                // hole through the NavMesh across the whole XZ footprint, destroying
                // the walkable surface that floor tiles bake on top of the slab.
                if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                    DestroyImmediate(oldObstacle);
                _modifier.ignoreFromBuild = false;
            }
            else if (Data.isStackable)
            {
                // Stackable objects (racks, shelving): carving obstacle blocks agent
                // access under/through them while keeping the floor surface walkable.
                ConfigureObstacle();
                _modifier.ignoreFromBuild = false;
            }
            else if (Data.isFloor)
            {
                // Walkable surface itself — no obstacle (would punch through the baked mesh).
                if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                    DestroyImmediate(oldObstacle);
                _modifier.ignoreFromBuild = false;
            }
            else if (Data.CanUseStairs)
            {
                // Root stairwell body excluded from baking — children are the walkable surfaces.
                if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                    DestroyImmediate(oldObstacle);

                _modifier.applyToChildren = false;
                _modifier.ignoreFromBuild = true;

                foreach (Transform child in transform)
                {
                    var childMod = child.GetComponent<NavMeshModifier>();
                    if (childMod == null) childMod = child.gameObject.AddComponent<NavMeshModifier>();
                    childMod.applyToChildren = false;
                    childMod.overrideArea    = true;
                    childMod.area            = Data.navArea;
                    childMod.ignoreFromBuild = false;
                }
                // NavMeshLinks are placed manually in the prefab — no code generation needed.
            }
            else
            {
                if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                    DestroyImmediate(oldObstacle);
                _modifier.ignoreFromBuild = true;
            }

            return;
        }

        // 3. Standard blocking objects (Walls, Barriers, MHE) — carving obstacle only.
        ConfigureObstacle();

        // Included in the Walls layer mask but must be ignored from geometry build;
        // blocking is done via carving only to avoid messy vertical NavMesh geometry.
        if (_modifier == null) _modifier = GetComponent<NavMeshModifier>();
        if (_modifier == null) _modifier = gameObject.AddComponent<NavMeshModifier>();
        _modifier.ignoreFromBuild = true;
    }

    private void ConfigureObstacle()
    {
        if (_obstacle == null) _obstacle = gameObject.GetComponent<NavMeshObstacle>();
        if (_obstacle == null) _obstacle = gameObject.AddComponent<NavMeshObstacle>();

        _obstacle.shape = NavMeshObstacleShape.Box;
        _obstacle.carving = true;
        _obstacle.carveOnlyStationary = true;

        // 0.75f factor leaves a gap at cell boundaries wide enough for agents (>= 2× AgentRadius).
        float gridSpaceX = Data.footprint.x * 0.95f;
        float gridSpaceZ = Data.footprint.y * 0.95f;
        float centerX    = (Data.footprint.x - 1) * 0.665f;
        float centerZ    = (Data.footprint.y - 1) * 0.665f;

        _obstacle.size   = new Vector3(gridSpaceX, Data.objHeight, gridSpaceZ);
        _obstacle.center = new Vector3(centerX, Data.objHeight * 0.45f, centerZ);
    }

    public void Delete()
    {
        Destroy(gameObject);
    }
}
