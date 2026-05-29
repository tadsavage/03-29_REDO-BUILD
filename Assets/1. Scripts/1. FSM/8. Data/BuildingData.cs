using UnityEngine;
using UnityEngine.AI; // Required for NavMeshObstacle
using Unity.AI.Navigation;
using System.Collections;

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
        bool isWalkableSurface = (Data.pathfindingClear || Data.isFloor || Data.ignorePlacementRules || Data.isStackable || Data.CanUseStairs || Data.category == "Foundation" || Data.category == "Grounds");
        
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
            else if (Data.CanUseStairs)
            {
                if (TryGetComponent<NavMeshObstacle>(out var oldObstacle))
                    DestroyImmediate(oldObstacle);

                // Root stairwell body excluded from baking — children are the walkable surfaces.
                // Don't cascade to children; each child floor tile must be baked independently.
                _modifier.applyToChildren = false;
                _modifier.ignoreFromBuild = true;

                // Mark direct child floor tiles as walkable NavMesh area 4
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
        var links = GetComponents<NavMeshLink>();
        _stairLinkHuman = links.Length > 0 ? links[0] : gameObject.AddComponent<NavMeshLink>();
        _stairLinkRat   = links.Length > 1 ? links[1] : gameObject.AddComponent<NavMeshLink>();

        ConfigureLink(_stairLinkHuman, "Human", Data.navArea);
        ConfigureLink(_stairLinkRat,   "Rat",   0);

        NavMeshManager.OnNavMeshReady -= OnNavMeshReadyForStair;
        NavMeshManager.OnNavMeshReady += OnNavMeshReadyForStair;

        if (NavMeshManager.IsReady)
            StartCoroutine(DelayedUpdateStairLink());
    }

    private void OnNavMeshReadyForStair()
    {
        StartCoroutine(DelayedUpdateStairLink());
    }

    // Waits for NavMeshObstacle carving to settle before placing link endpoints.
    // carveOnlyStationary default settling time is 0.5s — we wait a bit longer.
    private IEnumerator DelayedUpdateStairLink()
    {
        yield return new WaitForSeconds(0.8f);
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
    {        /*
        if (_stairLinkHuman == null && _stairLinkRat == null) return;

        // Ground endpoint: search in all directions for NavMesh at y < 0.5 (truck yard level).
        // The stairwell transform is at the base of the stairs — search outward until we find ground.
        Vector3 startWorld = transform.position;
        bool foundGround = false;
        for (float r = 0.5f; r <= 6f && !foundGround; r += 0.5f)
        {
            for (int a = 0; a < 8 && !foundGround; a++)
            {
                float rad = a * 45f * Mathf.Deg2Rad;
                Vector3 probe = transform.position + new Vector3(Mathf.Cos(rad) * r, 0f, Mathf.Sin(rad) * r);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(probe, out hit, 0.6f, NavMesh.AllAreas) && hit.position.y < 0.5f)
                {
                    startWorld = hit.position;
                    foundGround = true;
                }
            }
        }

        if (!foundGround)
        {
            Debug.LogWarning($"[BuildingData] {gameObject.name}: no ground-level NavMesh found for stair link start.");
            return;
        }

        // Floor endpoint: search all directions at elevated height, starting well away from
        // the entrance to avoid the wall-carving zone right at the door threshold.
        float minUpperY = transform.position.y + 1.0f;
        Vector3 endWorld = Vector3.zero;
        bool foundUpper = false;
        for (float r = 2.5f; r <= 12f && !foundUpper; r += 0.75f)
        {
            for (int a = 0; a < 8 && !foundUpper; a++)
            {
                float rad = a * 45f * Mathf.Deg2Rad;
                Vector3 probe = transform.position + new Vector3(Mathf.Cos(rad) * r, 1.2f, Mathf.Sin(rad) * r);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(probe, out hit, 1.0f, NavMesh.AllAreas) && hit.position.y > minUpperY)
                {
                    endWorld = hit.position;
                    foundUpper = true;
                }
            }
        }

        if (!foundUpper)
        {
            Debug.LogWarning($"[BuildingData] {gameObject.name}: no upper-floor NavMesh found for stair link end.");
            return;
        }

        Debug.Log($"[BuildingData] {gameObject.name}: stair link start={startWorld} end={endWorld}");

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

            */
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
        NavMeshManager.OnNavMeshReady -= OnNavMeshReadyForStair;
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