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

        // 1a. Racking — leave whatever NavMeshObstacle/NavMeshModifier is authored on the prefab
        //     alone (Tad is hand-configuring rack obstacles with Carve=true himself). This used to
        //     unconditionally DestroyImmediate both components here so agents could navigate the
        //     aisle freely and the RTO could drive into rack lanes directly during putaway --
        //     removed at Tad's request 2026-07-26 after reverting the NavMesh-routing approach to
        //     rack avoidance (see navmesh-rack-avoidance-abandoned memory). Still return early:
        //     racks shouldn't fall through to the walkable-surface setup below.
        if (Data.category == "Racking")
        {
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
                // Pallets (Inventory) must NOT be baked as geometry. They are transient — lifted and
                // set down constantly — and baked geometry only changes on a REBAKE, so every pallet
                // that got picked up left its footprint behind in the NavMesh until the next bake
                // (the "pallets leaving their shape in the mesh" artifact). Excluded from the build
                // they block purely by carving, which updates live with no rebake at all.
                // Racks/shelving stay IN the bake: they are static, and their deck is real walkable
                // surface that agents path across.
                _modifier.ignoreFromBuild = Data.category == "Inventory";
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

        bool isInventory = Data != null && Data.category == "Inventory";

        // Inventory used to be excluded from carving ("prevents octagonal holes in the staging lanes
        // and racks") and baked in as walkable geometry instead. That traded one artifact for two
        // worse ones: a NON-carving obstacle does not block at all — agents walked straight through
        // pallets, merely nudged by RVO steering — and a baked footprint only clears on a rebake, so
        // lifted pallets left their shape in the mesh. Carving is the mechanism built for objects
        // that move, so everything carves now; the octagonal artifact was really a SIZING problem,
        // handled just below.
        _obstacle.carving = true;
        _obstacle.carveOnlyStationary = true;

        // Cell pitch is 1.33 (hence the 0.665 half-cell offsets). The stock 1.35 box is very slightly
        // WIDER than its own cell, so neighbouring obstacles overlap and their carves merge into the
        // blobby shapes that made carving look unusable for pallets. Inventory gets a box inset
        // inside its cell so each pallet carves a clean, separate footprint; everything else keeps
        // the original size, which racks and walls are already tuned around.
        // (The old comment here claimed a 0.75f factor while the code used 1.35f — it was stale.)
        float perCell    = isInventory ? 1.15f : 1.35f;
        float gridSpaceX = Data.footprint.x * perCell;
        float gridSpaceZ = Data.footprint.y * perCell;
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
