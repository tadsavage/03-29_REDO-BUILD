using GameCore.Build;
using GameCore.Economy;
using GameCore.Events;
using GameCore.Events.Payloads;
using UnityEngine;
using System.Collections.Generic;

public class MoveCommand : PlacementCommandBase
{
    private readonly PlacementGrid _grid;
    private readonly GameObject _instance;
    private readonly ObjDataSO _data;
    private readonly MoneyService _money;
    
    private readonly Vector2Int _oldRoot;
    private readonly Vector2Int _newRoot;
    
    private readonly Vector2Int[] _oldOffsets;
    private readonly Vector2Int[] _newOffsets;
    
    private readonly float _oldRotation;
    private readonly float _newRotation;

    private struct ReplacedData
    {
        public GameObject instance;
        public ObjDataSO data;
        public Vector2Int root;
        public Vector2Int[] offsets;
        public float rotation;
    }
    private readonly List<ReplacedData> _replaced = new();

    /// <summary>
    /// A floor tile that rides along with a foundation move. Its cell is expressed as a local
    /// offset from the foundation root, captured both before (oldLocal) and after (newLocal) any
    /// rotation, so the tile lands on the correct footprint cell when the slab is moved/rotated.
    /// </summary>
    public struct RiderObject
    {
        public GameObject instance;
        public ObjDataSO data;
        public Vector2Int oldLocal;
        public Vector2Int newLocal;
    }
    private readonly List<RiderObject> _riders;

    public MoveCommand(
        PlacementGrid grid,
        GameObject obj,
        ObjDataSO data,
        Vector2Int oldRoot,
        Vector2Int newRoot,
        Vector2Int[] oldOffsets,
        Vector2Int[] newOffsets,
        float oldRotation,
        float newRotation,
        MoneyService money,
        List<RiderObject> riders = null)
        : base($"Move {data?.objName ?? "Object"}")
    {
        _grid = grid;
        _instance = obj;
        _data = data;
        _oldRoot = oldRoot;
        _newRoot = newRoot;
        _oldOffsets = oldOffsets;
        _newOffsets = newOffsets;
        _oldRotation = oldRotation;
        _newRotation = newRotation;
        _money = money;
        _riders = riders ?? new List<RiderObject>();
    }

    public override void Execute()
    {
        if (IsFoundation(_data))
            HandleReplacement(_newRoot, _newOffsets);

        Move(_oldRoot, _newRoot, _oldOffsets, _newOffsets, _newRotation);
        MoveRiders(forward: true);

        if (IsFoundation(_data))
            RegenerateYardFloor();

        PublishBuildEvent(GameEvents.Build.OnObjectMoved, new BuildingMoveData
        {
            FromX = _oldRoot.x,
            FromY = _oldRoot.y,
            ToX = _newRoot.x,
            ToY = _newRoot.y,
            Rotation = (int)_newRotation
        });
    }

    public override void Undo()
    {
        Move(_newRoot, _oldRoot, _newOffsets, _oldOffsets, _oldRotation);
        MoveRiders(forward: false);

        if (_replaced.Count > 0)
            RestoreReplaced();

        if (IsFoundation(_data))
            RegenerateYardFloor();

        PublishBuildEvent(GameEvents.Build.OnObjectMoved, new BuildingMoveData
        {
            FromX = _newRoot.x,
            FromY = _newRoot.y,
            ToX = _oldRoot.x,
            ToY = _oldRoot.y,
            Rotation = (int)_oldRotation
        });
    }

    /// <summary>
    /// Cells a Foundation/Grounds slab vacates or arrives on aren't necessarily covered by
    /// individually-tracked floor tiles — most of the map is the single combined yard-floor mesh
    /// (see YardFloorMeshBuilder), baked once with whatever had a real object on it at the time
    /// excluded. Moving a foundation off/onto that mesh's territory doesn't touch the mesh itself,
    /// so without this the vacated footprint stays a permanent hole (nothing else regenerates it
    /// until the next full load) and the arrival cells can z-fight with the carpet still rendering
    /// underneath. Mesh-only rebuild — see GameContext.RegenerateYardFloorMesh for why this doesn't
    /// go through the much heavier PopulateYardFloors (grid rebuild + synchronous NavMesh bake).
    /// </summary>
    private void RegenerateYardFloor()
    {
        var ctx = Object.FindAnyObjectByType<GameContext>();
        ctx?.RegenerateYardFloorMesh(_grid);
    }

    public override void Redo() => Execute();

    private bool IsFoundation(ObjDataSO data)
    {
        if (data == null) return false;
        return data.category == "Foundation" || data.category == "Grounds";
    }

    private void HandleReplacement(Vector2Int root, Vector2Int[] offsets)
    {
        _replaced.Clear();
        HashSet<GameObject> found = new HashSet<GameObject>();

        foreach (var o in offsets)
        {
            var cellObjs = _grid.GetObjectsInCell(root + o);
            if (cellObjs == null) continue;

            foreach (var entry in cellObjs)
            {
                if (entry.instance != null && entry.instance != _instance && IsFoundation(entry.data))
                {
                    found.Add(entry.instance);
                }
            }
        }

        foreach (var obj in found)
        {
            var bd = obj.GetComponent<BuildingData>();
            if (bd == null) continue;

            // Store data for Undo
            _replaced.Add(new ReplacedData
            {
                instance = obj,
                data = bd.Data,
                root = bd.RootCell,
                offsets = bd.Offsets,
                rotation = bd.Rotation
            });

            // Remove from grid
            foreach (var o in bd.Offsets)
            {
                _grid.RemoveStackObject(bd.RootCell + o, obj, bd.Data);
            }

            // Refund
            if (_money != null)
            {
                _money.Refund(bd.Data.cost, bd.Data.category);
                _money.RemoveHourlyCost(bd.Data.hourlyCost, FinanceCategory.ForHourlyCost(bd.Data.category), bd.Data.category);
            }

            // Disable
            var highlighter = obj.GetComponent<BuildingHighlighter>();
            if (highlighter != null)
                highlighter.HighlightValid(false);

            obj.SetActive(false);
        }
    }

    /// <summary>
    /// Relocates the foundation's floor tiles in lockstep with the slab.
    /// forward = the slab moved old→new; !forward = an undo moving new→old.
    /// Floor tiles are 1×1, never rotate, and always sit directly on the foundation.
    ///
    /// Each rider gets the same lift-then-drop-with-dust-poof SmoothLanding treatment the main
    /// instance gets below in Move() — they used to just pop into place instantly the moment the
    /// slab landed, which read as one object landing softly and four others teleporting in.
    /// </summary>
    private void MoveRiders(bool forward)
    {
        if (_riders == null) return;

        float offset = PreviewController.Instance != null ? PreviewController.Instance.OffsetMovePreview : 1.0f;
        float smooth = PreviewController.Instance != null ? PreviewController.Instance.MoveSmoothTime : 0.1f;

        foreach (var r in _riders)
        {
            if (r.instance == null || r.data == null) continue;

            Vector2Int fromCell = forward ? _oldRoot + r.oldLocal : _newRoot + r.newLocal;
            Vector2Int toCell   = forward ? _newRoot + r.newLocal : _oldRoot + r.oldLocal;

            // Remove from wherever it currently lives (no-op on the first Execute, where the
            // tile was already pulled out of the grid by MoveState at selection time).
            _grid.RemoveStackObject(fromCell, r.instance, r.data);

            r.instance.SetActive(true);

            Vector2Int[] tileOffsets = r.data.GetFootprintOffsets(0f); // 1×1 → {(0,0)}

            var bd = r.instance.GetComponent<BuildingData>();
            if (bd != null)
                bd.Initialize(toCell, 0f, tileOffsets, r.data);

            // AddStackObject calls UpdateStackPositions internally, so transform.position already
            // reflects the tile's correct final resting spot by the time we read it below.
            _grid.AddStackObject(toCell, r.instance, r.data);

            var po = r.instance.GetComponent<PlacedObject>();
            if (po != null)
            {
                po.gridX = toCell.x;
                po.gridY = toCell.y;
                po.rotation = 0;
            }

            Vector3 finalPos = r.instance.transform.position;
            var oldLanding = r.instance.GetComponent<SmoothLanding>();
            if (oldLanding != null) Object.DestroyImmediate(oldLanding);
            var landing = r.instance.AddComponent<SmoothLanding>();
            landing.Initialize(finalPos + Vector3.up * offset, finalPos, smooth);
        }
    }

    private bool IsRider(GameObject go)
    {
        if (_riders == null || go == null) return false;
        foreach (var r in _riders)
            if (r.instance == go) return true;
        return false;
    }

    /// <summary>
    /// Re-enables whatever floor tile (yard tile or a real paid floor) was hidden beneath a
    /// foundation at the cells it is leaving, so the ground is restored (no holes). Skips the
    /// foundation's own rider tiles. Only re-enables when the cell is otherwise clear, matching
    /// DeleteCommand's reveal logic.
    /// </summary>
    private void RevealHiddenFloors(Vector2Int root, Vector2Int[] offsets)
    {
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            if (_grid.IsOccupied(cell)) continue;

            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

            foreach (var entry in list)
            {
                // isFloor alone isn't enough to identify "a floor covering to reveal" — Foundation/
                // Grounds slabs are ALSO flagged isFloor (they're walkable) but are never one of this
                // move's own riders, so excluding only IsFoundation(...) here would wrongly reveal (and
                // reposition) some OTHER foundation slab that happens to be hidden at this cell.
                if (entry.data == null || !entry.data.isFloor || IsFoundation(entry.data)) continue;
                if (entry.instance == null || entry.instance.activeSelf) continue;
                if (IsRider(entry.instance)) continue;
                entry.instance.SetActive(true);
            }

            _grid.UpdateStackPositions(cell);
        }
    }

    /// <summary>
    /// Hides whatever floor tile (yard tile or a real paid floor) already occupies the cells a
    /// foundation is arriving on, so the foundation's own floor tile sits flush on the slab. Skips
    /// the foundation's own rider tiles (they are added by MoveRiders after this runs and must
    /// stay visible).
    /// </summary>
    private void HideUnderlyingFloors(Vector2Int root, Vector2Int[] offsets)
    {
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

            foreach (var entry in list)
            {
                // isFloor alone isn't enough to identify "a floor covering to hide" — Foundation/
                // Grounds slabs are ALSO flagged isFloor (they're walkable), and the slab THIS move
                // just placed is obviously never in _riders (only its floor-pattern tiles are), so
                // without the IsFoundation(...) exclusion this disabled the arriving foundation itself
                // right after Move() activated it — stranding it inactive (its own SmoothLanding never
                // gets an Update() tick on a disabled GameObject) and making UpdateStackPositions treat
                // the cell as groundless when it positioned this foundation's own floor-tile riders,
                // landing them at ~Y=0 instead of on top of the slab.
                if (entry.data == null || !entry.data.isFloor || IsFoundation(entry.data)) continue;
                if (entry.instance == null || !entry.instance.activeSelf) continue;
                if (IsRider(entry.instance)) continue;
                entry.instance.SetActive(false);
            }

            _grid.UpdateStackPositions(cell);
        }
    }

    private void RestoreReplaced()
    {
        foreach (var rd in _replaced)
        {
            if (rd.instance == null) continue;

            rd.instance.SetActive(true);

            // Re-add to grid
            foreach (var o in rd.offsets)
            {
                _grid.AddStackObject(rd.root + o, rd.instance, rd.data);
            }

            // Deduct refund
            if (_money != null)
            {
                _money.Deduct(rd.data.cost, rd.data.category);
                _money.AddHourlyCost(rd.data.hourlyCost, FinanceCategory.ForHourlyCost(rd.data.category), rd.data.category);
            }
        }
        _replaced.Clear();
    }

    private void Move(Vector2Int from, Vector2Int to, Vector2Int[] fromOffsets, Vector2Int[] toOffsets, float toRotation)
{
        if (_instance == null) return;

        foreach (var o in fromOffsets)
            _grid.RemoveStackObject(from + o, _instance, _data);

        // Foundation LEAVING these cells: re-reveal whatever floor tile (yard tile or a real paid
        // floor) was hidden beneath it. Placing/moving a foundation disables the underlying floor
        // (so the foundation's own floor tile can sit flush) — this re-enables it so the vacated
        // cells don't read as holes. Mirrors DeleteCommand's reveal logic.
        if (IsFoundation(_data))
            RevealHiddenFloors(from, fromOffsets);

        _instance.transform.rotation = Quaternion.Euler(0f, toRotation, 0f);
        _instance.SetActive(true);

        // Update BuildingData BEFORE adding to grid so UpdateStackPositions knows the new root
        var bd = _instance.GetComponent<BuildingData>();
        if (bd != null)
        {
            bd.Initialize(to, toRotation, toOffsets, _data);
        }

        foreach (var o in toOffsets)
            _grid.AddStackObject(to + o, _instance, _data);

        // Foundation ARRIVING on these cells: hide whatever floor tile is already there (yard tile
        // or a real paid floor) so the slab's own floor tile (re-added next by MoveRiders) sits
        // flush on the foundation instead of stacking on top of it, which would push it ~0.05 too
        // high. Mirrors what PlacementFinalizer.DisableExistingFloors does at first placement.
        if (IsFoundation(_data))
            HideUnderlyingFloors(to, toOffsets);

        // Immediate position at cell center as a baseline
        Vector3 targetPos = _grid.GetCellCenter(to);

        // If it has an agent, disable it temporarily so UpdateStackPositions sets transform.position directly
        // rather than using agent.Warp which might fail if not near a navmesh
        var agent = _instance.GetComponent<UnityEngine.AI.NavMeshAgent>();

        // Mobile agents need visual surface correction (e.g. sit on foundation meshes) to prevent sinking
        if (agent != null)
        {
            targetPos.y = PlacementFinalizer.GetFloorTopY(_grid, to);
            if (_data != null && _data.worldYOffset != 0)
                targetPos.y += _data.worldYOffset;
        }

        _instance.transform.position = targetPos;

        bool wasAgentEnabled = agent != null && agent.enabled;
        if (agent != null) agent.enabled = false;

        // Explicitly force height recalculation for all cells in old and new footprint
        foreach (var o in fromOffsets)
            _grid.UpdateStackPositions(from + o);
        foreach (var o in toOffsets)
            _grid.UpdateStackPositions(to + o);

        // Landing Logic: Capture the CORRECT target position calculated by the grid
        Vector3 finalPos = _instance.transform.position;
        float offset = PreviewController.Instance != null ? PreviewController.Instance.OffsetMovePreview : 1.0f;
        float smooth = PreviewController.Instance != null ? PreviewController.Instance.MoveSmoothTime : 0.1f;

        // Clean up any existing landing component if the user is moving fast
        var oldLanding = _instance.GetComponent<SmoothLanding>();
        if (oldLanding != null) Object.DestroyImmediate(oldLanding);

        var landing = _instance.AddComponent<SmoothLanding>();
        landing.Initialize(finalPos + Vector3.up * offset, finalPos, smooth);

        var po = _instance.GetComponent<PlacedObject>();
        if (po != null)
        {
            po.gridX = to.x;
            po.gridY = to.y;
            po.rotation = (int)(toRotation / 90f);
        }

        // Tell NavMesh to update if it's a modifier-based object or if we replaced foundations
        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || IsFoundation(_data) || _replaced.Count > 0)
        {
            NavMeshManager.Instance?.MarkDirty();
        }
}
}