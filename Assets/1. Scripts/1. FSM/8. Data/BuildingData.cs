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
    private NavMeshLink _stairLinkHuman;
    private NavMeshLink _stairLinkRat;

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

            // Foundations and Stackable volumes are baked (ignoreFromBuild = false) and get a
            // carving obstacle so agents walk ON TOP of them without sinking to y=0.
            // Floors are also baked (ignoreFromBuild = false) but do NOT get an obstacle —
            // they are the walkable surface itself; a carving obstacle would punch through
            // the NavMesh that was just baked on them.
            // Doors / pathfindingClear objects are excluded (ignoreFromBuild = true) so
            // agents walk through them on the underlying NavMesh.
            if (Data.category == "Foundation" || Data.isStackable)
            {
                ConfigureObstacle();
                _modifier.ignoreFromBuild = false;
            }
            else if (Data.isFloor)
            {
                if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                    DestroyImmediate(oldObstacle);

                _modifier.ignoreFromBuild = false;
            }
            else
            {
                if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                    DestroyImmediate(oldObstacle);

                _modifier.ignoreFromBuild = true;
            }

            if (Data.CanUseStairs) SetupStairLink();
            return;
        }

        // 3. Standard blocking objects (Walls, Barriers, MHE)
        ConfigureObstacle();

        // Standard walls must be ignored from the geometry build (ignoreFromBuild = true)
        // because we are now including the 'Walls' layer in the NavMeshSurface mask.
        // This ensures they block via Carving only and don't create messy vertical geometry.
        if (_modifier == null) _modifier = GetComponent<NavMeshModifier>();
        if (_modifier == null) _modifier = gameObject.AddComponent<NavMeshModifier>();
        _modifier.ignoreFromBuild = true;

        if (Data.CanUseStairs) SetupStairLink();
    }

    private void SetupStairLink()
    {
        // Get all existing NavMeshLink components and assign them by index,
        // adding new ones if needed.
        var links = GetComponents<NavMeshLink>();
        _stairLinkHuman = links.Length > 0 ? links[0] : gameObject.AddComponent<NavMeshLink>();
        _stairLinkRat   = links.Length > 1 ? links[1] : gameObject.AddComponent<NavMeshLink>();

        ConfigureLink(_stairLinkHuman, "Human", Data.navArea);
        ConfigureLink(_stairLinkRat,   "Rat",   0); // Rat uses Walkable area

        NavMeshManager.OnNavMeshReady -= UpdateStairLink;
        NavMeshManager.OnNavMeshReady += UpdateStairLink;

        if (NavMeshManager.IsReady)
            UpdateStairLink();
    }

    private void ConfigureLink(NavMeshLink link, string agentTypeName, int area)
    {
        link.agentTypeID   = GetAgentTypeID(agentTypeName);
        link.area          = area;
        link.width         = 1.2f;
        link.bidirectional = true;
        link.autoUpdate    = false;
    }

    private void UpdateStairLink()
    {
        if (_stairLinkHuman == null && _stairLinkRat == null) return;

        // Ground-floor endpoint: snap to NavMesh at the stair's base
        NavMeshHit groundHit;
        Vector3 startWorld = transform.position;
        if (NavMesh.SamplePosition(transform.position, out groundHit, 2f, NavMesh.AllAreas))
            startWorld = groundHit.position;

        // Upper-floor endpoint: walk forward (stair's local +Z = "inside") to find elevated NavMesh.
        // Require the hit to be meaningfully above the stair base (>1m) to avoid false hits
        // from entrance structures or low geometry near the stair foot.
        float minUpperY = transform.position.y + 1.0f;
        Vector3 endWorld = Vector3.zero;
        bool foundUpper = false;
        for (float dist = 1.0f; dist <= 8f; dist += 0.25f)
        {
            Vector3 probe = transform.position + transform.forward * dist + Vector3.up * 1.2f;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(probe, out hit, 0.25f, NavMesh.AllAreas) && hit.position.y > minUpperY)
            {
                endWorld = hit.position;
                foundUpper = true;
                break;
            }
        }

        if (!foundUpper)
        {
            Debug.LogWarning($"[BuildingData] {gameObject.name}: no upper-floor NavMesh found for stair link.");
            return;
        }

        Vector3 startLocal = transform.InverseTransformPoint(startWorld);
        Vector3 endLocal   = transform.InverseTransformPoint(endWorld);

        if (_stairLinkHuman != null)
        {
            _stairLinkHuman.startPoint = startLocal;
            _stairLinkHuman.endPoint   = endLocal;
            _stairLinkHuman.UpdateLink();
        }

        if (_stairLinkRat != null)
        {
            _stairLinkRat.startPoint = startLocal;
            _stairLinkRat.endPoint   = endLocal;
            _stairLinkRat.UpdateLink();
        }
    }

    private static int GetAgentTypeID(string agentTypeName)
    {
        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            var s = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == agentTypeName)
                return s.agentTypeID;
        }
        Debug.LogWarning($"[BuildingData] Agent type '{agentTypeName}' not found in NavMesh settings.");
        return 0;
    }

    private void OnDestroy()
    {
        NavMeshManager.OnNavMeshReady -= UpdateStairLink;
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