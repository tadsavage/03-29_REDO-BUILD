using System.Collections.Generic;
using UnityEngine;

public class PreviewController : MonoBehaviour
{
    public static PreviewController Instance { get; private set; }
    private PlacementGrid _grid;

    // ---------------------------------------------------------
    // GHOST POOLING
    // ---------------------------------------------------------
    private readonly Stack<GameObject> _pool = new();
    private readonly Dictionary<Vector2Int, GameObject> _multiGhosts = new();

    private GameObject _singleGhost;
    private ObjDataSO _currentData;
    public float CurrentRotation { get; private set; }

    private Vector3 _targetPos;
    private Vector3 _velocity;
    private bool _hasTarget;

    // ---------------------------------------------------------
    // MOVEMENT / HOVER
    // ---------------------------------------------------------
    [Header("Move Smoothing")]
    [SerializeField] private float moveSmoothTime = 0.08f;
    [SerializeField] private float moveSmoothSpeed = 0.25f;

    [Header("Hover Settings")]
    [SerializeField] private float offsetMovePreview = 0f;

    public float OffsetMovePreview => offsetMovePreview;
    public float MoveSmoothTime => moveSmoothTime;

    private GameObject _currentPreview;

    // ---------------------------------------------------------
    // FACTORIO-STYLE HIGHLIGHTS (SUBTLE TINTS)
    // ---------------------------------------------------------
    private static readonly Color HighlightGreen = new(0.2f, 1.0f, 0.2f, 0.5f);
    private static readonly Color HighlightRed = new(1.0f, 0.2f, 0.2f, 0.5f);

    private MaterialPropertyBlock _highlightMPB;
    private MaterialPropertyBlock _restoreMPB;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Material _ghostMaterial;

    /// <summary>The orange-transparent preview material, so other systems (e.g. the racking
    /// system, which keeps placed racks ghosted until an aisle is initialized) can reuse it.</summary>
    public Material GhostMaterial => _ghostMaterial;

    private bool _multiMode;
    private bool _deleteMode;
    private bool _isMovePreviewMode;

    private void Awake()
    {
        Instance = this;
        _grid = Object.FindAnyObjectByType<PlacementGrid>();

        _highlightMPB = new MaterialPropertyBlock();
        _restoreMPB = new MaterialPropertyBlock();
    }

    private readonly Dictionary<GameObject, Renderer[]> _ghostRendererCache = new();

    // ---------------------------------------------------------
    // FLAT HIGHLIGHT API
    // ---------------------------------------------------------
    public void ApplyFlatHighlight(GameObject obj, Color color)
    {
        if (obj == null)
            return;

        _highlightMPB.Clear();
        _highlightMPB.SetColor(BaseColorID, color);

        if (!_ghostRendererCache.TryGetValue(obj, out var renderers))
        {
            renderers = obj.GetComponentsInChildren<Renderer>(true);
            _ghostRendererCache[obj] = renderers;
        }

        foreach (var r in renderers)
        {
            if (r != null)
                r.SetPropertyBlock(_highlightMPB);
        }
    }

    public void ClearFlatHighlight(GameObject obj)
    {
        if (obj == null)
            return;

        if (!_ghostRendererCache.TryGetValue(obj, out var renderers))
        {
            renderers = obj.GetComponentsInChildren<Renderer>(true);
            _ghostRendererCache[obj] = renderers;
        }

        foreach (var r in renderers)
        {
            if (r != null)
                r.SetPropertyBlock(_restoreMPB); // clears override
        }
    }

    // ---------------------------------------------------------
    // RESET
    // ---------------------------------------------------------
    public void ResetMoveGhostState()
    {
        HideGhost();
        ClearMultiGhosts();
        ClearGhostPool();
        ClearRiderGhosts();

        _currentPreview = null;
        _hasTarget = false;
        _velocity = Vector3.zero;
    }

    public void ResetAllVisuals()
    {
        ResetMoveGhostState();
        Hide();
        _multiMode = false;
        _deleteMode = false;
    }

    public void SetDeleteMode(bool on)
    {
        _deleteMode = on;
    }

    public void SetMovePreviewMode(bool on)
    {
        _isMovePreviewMode = on;
    }

    // ---------------------------------------------------------
    // SINGLE GHOST
    // ---------------------------------------------------------
    private bool IsGround(ObjDataSO data)
    {
        if (data == null) return false;
        return data.category == "Foundation" || data.category == "Grounds";
    }

    public void Show(ObjDataSO data)
{
        if (_currentData != data)
        {
            if (_singleGhost != null)
            {
                _ghostRendererCache.Remove(_singleGhost);
                Destroy(_singleGhost);
            }

            ClearGhostPool();
            _singleGhost = CreateGhostFromPrefab(data.prefab);
        }

        _currentData = data;

        _singleGhost.SetActive(true);
        _currentPreview = _singleGhost;

        _singleGhost.transform.rotation = Quaternion.Euler(0, CurrentRotation, 0);
        SetGhostValid();
    }

    public void Hide()
    {
        if (_singleGhost != null)
            _singleGhost.SetActive(false);

        ClearMultiGhosts();
    }

    // ---------------------------------------------------------
    // MOVE SINGLE GHOST
    // ---------------------------------------------------------
    public void MoveTo(Vector3 pos, Vector2Int cell, ObjDataSO data)
    {
        _targetPos = CalculateTargetPos(pos, cell, data);

        if (_currentPreview == null)
        {
            _hasTarget = true;
            return;
        }

        // BUILD MODE: the ghost must sit EXACTLY on the cell indicator with zero lag.
        // Snap it directly every frame and clear _hasTarget so Update()'s SmoothDamp
        // doesn't drag it off the cell. (SmoothDamp lag — driven by serialized
        // moveSmoothTime/Speed — was making the ghost appear to "stick" or jump.)
        if (!_isMovePreviewMode)
        {
            _currentPreview.transform.position = _targetPos;
            _velocity = Vector3.zero;
            _hasTarget = false;
            return;
        }

        // MOVE-PREVIEW MODE: keep the smooth lift animation. Snap only when the ghost
        // is far away (first-show at prefab origin, or re-entry onto the grid).
        _hasTarget = true;
        if ((_currentPreview.transform.position - _targetPos).sqrMagnitude > 9f)
        {
            _currentPreview.transform.position = _targetPos;
            _velocity = Vector3.zero;
        }
    }

    public void SnapTo(Vector3 baselinePos, Vector2Int cell, ObjDataSO data)
    {
        _targetPos = CalculateTargetPos(_grid.GetCellCenter(cell), cell, data);
        _hasTarget = true; // We want it to start moving towards the target goal (lifting)
        _velocity = Vector3.zero;

        if (_currentPreview != null)
        {
            // Snap the visual exactly where the object was (the baseline).
            // The lift offset will be applied in the Update loop starting this frame.
            _currentPreview.transform.position = baselinePos;
        }
    }

    private Vector3 CalculateTargetPos(Vector3 pos, Vector2Int cell, ObjDataSO data)
    {
        // Start with the raw input Y (could be floor 0 or a raycast hit point)
        float targetY = pos.y;

        if (data != null && !IsGround(data) && !_deleteMode)
        {
            // Determine logical heights
            float stackHeight = (data.replacesWalls || data.canBeReplacedByDoor)
                ? _grid.GetStackHeightIgnoringWalls(cell)
                : _grid.GetStackHeight(cell);
            
            float visualHeight = PlacementFinalizer.GetFloorTopY(_grid, cell);

            // The ghost should snap to the HIGHEST surface available in that cell.
            // If there's a foundation (visualHeight) and a rack (stackHeight),
            // it must sit on the rack.
            targetY = Mathf.Max(targetY, stackHeight, visualHeight);

            // Special Case: Agents (Workers/Vehicles) should usually sit on the floor surface,
            // not float on top of racks.
            bool isAgent = data.prefab != null && data.prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;
            if (isAgent)
            {
                targetY = visualHeight;
            }
        }

        // Apply any manual world offset from the data
        if (data != null && data.worldYOffset != 0)
            targetY += data.worldYOffset;

        pos.y = targetY;
        return pos;
    }

    public void Rotate(float angle)
    {
        CurrentRotation = angle;

        if (_singleGhost != null)
            _singleGhost.transform.rotation = Quaternion.Euler(0, angle, 0);
    }

    public void SetGhostValid()
    {
        if (_singleGhost != null)
            ApplyFlatHighlight(_singleGhost, HighlightGreen);
    }

    public void SetGhostInvalid()
    {
        if (_singleGhost != null)
            ApplyFlatHighlight(_singleGhost, HighlightRed);
    }

    // ---------------------------------------------------------
    // MULTI-GHOST MODE
    // ---------------------------------------------------------
    public void BeginSelectionCells()
    {
        _multiMode = true;

        if (_singleGhost != null)
            _singleGhost.SetActive(false);

        ClearMultiGhosts();
    }

    public void EndSelectionCells()
    {
        _multiMode = false;

        ClearMultiGhosts();

        if (_singleGhost != null)
            _singleGhost.SetActive(true);
    }

    public void ShowMultiGhost(Vector2Int cell, bool valid, float rotation)
    {
        if (!_multiMode)
            return;

        if (!_multiGhosts.TryGetValue(cell, out GameObject ghost))
        {
            ghost = _pool.Count > 0
                ? _pool.Pop()
                : CreateGhostFromPrefab(_currentData.prefab);

            _multiGhosts[cell] = ghost;
        }

        ghost.SetActive(true);

        // Foundations are always at y=0. Everything else (Floors, Objects) sits on the stack.
        float stackY = IsGround(_currentData) ? 0f : _grid.GetStackHeight(cell);

        Vector3 pos = _grid.GetCellCenter(cell);
        pos.y += stackY;

        ghost.transform.position = pos;
        ghost.transform.rotation = Quaternion.Euler(0, rotation, 0);

        ApplyFlatHighlight(ghost, valid ? HighlightGreen : HighlightRed);
    }

    public void ClearMultiGhosts()
    {
        foreach (var kvp in _multiGhosts)
        {
            kvp.Value.SetActive(false);
            _pool.Push(kvp.Value);
        }

        _multiGhosts.Clear();
    }

    // ---------------------------------------------------------
    // RIDER GHOSTS (floor tiles that travel with a foundation/grounds slab)
    // ---------------------------------------------------------
    // A foundation's floor-tile "riders" (see MoveState.GatherRiderTiles / MoveCommand.MoveRiders)
    // used to just vanish for the whole drag and pop back in at the end, while the foundation
    // itself got a proper ghost that followed the cursor — one piece of the group visibly lifted,
    // the other four disappeared. These give each rider its own ghost so the whole slab (footprint
    // + its floor pattern) lifts and follows the cursor together, matching the single-object case.
    private struct RiderGhost
    {
        public GameObject ghost;
        public Vector2Int localOffset; // offset from the foundation's root cell, never rotates (1x1 tiles)
    }
    private readonly List<RiderGhost> _riderGhosts = new();

    /// <summary>Creates one ghost per rider, replacing any already showing. Positions are set on
    /// the next MoveRiderGhosts call — this only spawns them.</summary>
    public void ShowRiderGhosts(List<(ObjDataSO data, Vector2Int localOffset)> riders)
    {
        ClearRiderGhosts();
        if (riders == null) return;

        foreach (var (data, localOffset) in riders)
        {
            if (data == null || data.prefab == null) continue;
            var ghost = CreateGhostFromPrefab(data.prefab);
            ghost.SetActive(true);
            _riderGhosts.Add(new RiderGhost { ghost = ghost, localOffset = localOffset });
        }
    }

    /// <summary>Repositions every rider ghost relative to the foundation's own ghost this frame —
    /// call alongside MoveTo/SnapTo. Reads the main ghost's ACTUAL current transform.position.y
    /// (not _targetPos, which is where it's headed) — Update()'s SmoothDamp is what makes the main
    /// ghost lift smoothly off the ground on pickup, and riders need to inherit that same in-flight
    /// height every frame rather than snapping straight to the fully-lifted target, or they'd pop up
    /// instantly while the foundation is still visibly rising to meet them.</summary>
    public void MoveRiderGhosts(Vector2Int foundationRoot)
    {
        if (_riderGhosts.Count == 0 || _currentPreview == null) return;

        float y = _currentPreview.transform.position.y;
        Quaternion rot = _currentPreview.transform.rotation;

        foreach (var r in _riderGhosts)
        {
            if (r.ghost == null) continue;
            Vector3 pos = _grid.GetCellCenter(foundationRoot + r.localOffset);
            pos.y = y;
            r.ghost.transform.position = pos;
            r.ghost.transform.rotation = rot;
        }
    }

    public void SetRiderGhostsValid()
    {
        foreach (var r in _riderGhosts)
            if (r.ghost != null) ApplyFlatHighlight(r.ghost, HighlightGreen);
    }

    public void SetRiderGhostsInvalid()
    {
        foreach (var r in _riderGhosts)
            if (r.ghost != null) ApplyFlatHighlight(r.ghost, HighlightRed);
    }

    public void ClearRiderGhosts()
    {
        foreach (var r in _riderGhosts)
        {
            if (r.ghost == null) continue;
            _ghostRendererCache.Remove(r.ghost);
            Destroy(r.ghost);
        }
        _riderGhosts.Clear();
    }

    // ---------------------------------------------------------
    // GHOST CREATION
    // ---------------------------------------------------------
    private GameObject CreateGhostFromPrefab(GameObject source)
    {
        GameObject ghost = Instantiate(source);
        ghost.name = source.name + "_Ghost";

        // Set EVERY object in the hierarchy to Ignore Raycast (layer 2).
        // Setting only the root leaves child colliders on the default layer — the
        // raycast then hits the ghost instead of the floor, causing the ghost to
        // track its own position and appear stuck where it is.
        foreach (var t in ghost.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = 2;

        // STEP 1 — NEUTRALIZE before destroying. A NavMeshAgent is the thing that
        // pins the ghost to the NavMesh surface (it overrides transform.position every
        // frame and clamps it to walkable mesh, so the ghost sticks at foundation edges).
        // Disabling it + updatePosition=false guarantees the ghost is free to follow the
        // cursor EVEN IF the DestroyImmediate cleanup below fails for any reason
        // (e.g. a future RequireComponent dependency we don't know about). Disable the
        // AI behaviours too so their Start()/Update() coroutines never drive the agent.
        foreach (var a in ghost.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
        {
            if (a == null) continue;
            a.updatePosition = false;
            a.updateRotation = false;
            a.enabled = false;
        }
        foreach (var b in ghost.GetComponentsInChildren<Behaviour>(true))
        {
            // Keep Animator (visual pose) and Light. Kill every other behaviour —
            // AiNavigation, NoWaypointIndicator, NavAgentGuidance, PlacedObject, etc.
            if (b == null || b is Animator || b is Light) continue;
            b.enabled = false;
        }

        // STEP 2 — Destroy RequireComponent dependents in order so the shared
        // dependencies can be removed cleanly. NoWaypointIndicator requires both
        // AiNavigation AND NavMeshAgent; NavAgentGuidance requires NavMeshAgent.
        // Removing them out of order makes DestroyImmediate fail silently and leaves
        // the agent alive — hence the explicit order here.
        var removalOrder = new System.Type[]
        {
            typeof(NoWaypointIndicator),
            typeof(NavAgentGuidance),
            typeof(MHEOperatorSlot),
            typeof(AiNavigation),
            typeof(UnityEngine.AI.NavMeshAgent),
            typeof(Rigidbody),
        };
        foreach (var type in removalOrder)
        {
            foreach (var comp in ghost.GetComponentsInChildren(type, true))
            {
                if (comp != null)
                    try { DestroyImmediate(comp); } catch { }
            }
        }

        // STEP 3 — Generic pass: remove everything else that is not a visual component.
        // NavMeshAgent and friends are already gone, so this runs without dependency errors.
        bool removed = true;
        while (removed)
        {
            removed = false;
            foreach (var comp in ghost.GetComponentsInChildren<Component>(true))
            {
                if (comp is Transform || comp is Renderer || comp is MeshFilter || comp is Light || comp is Animator)
                    continue;
                try { DestroyImmediate(comp); removed = true; }
                catch { }
            }
        }

        // STEP 4 — Destroy every surviving Collider.
        // The ground raycast uses Physics.Raycast; if ANY collider remains on the ghost
        // (even on layer 2, which Unity normally ignores) it can still be returned as
        // HitObject in the second object-raycast pass, and some paths re-use HitObject
        // to derive HitCell. When that happens the ghost tracks its own cell and appears
        // "stuck" — the classic self-tracking bug. Removing all Colliders is the only
        // guarantee this can never occur, regardless of layer assignment.
        bool colRemoved = true;
        while (colRemoved)
        {
            colRemoved = false;
            foreach (var col in ghost.GetComponentsInChildren<Collider>(true))
            {
                if (col == null) continue;
                try { DestroyImmediate(col); colRemoved = true; }
                catch { }
            }
        }

        // Keep the Animator enabled so the ghost shows a live pose while hovering.
        // applyRootMotion=false stops root-motion channels. Any remaining direct
        // Transform.position curves are overridden each frame by LateUpdate().
        foreach (var anim in ghost.GetComponentsInChildren<Animator>(true))
            if (anim != null)
                anim.applyRootMotion = false;

        if (_ghostMaterial != null)
        {
            foreach (var r in ghost.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = _ghostMaterial;
        }

        ApplyFlatHighlight(ghost, HighlightGreen);
        return ghost;
    }

    // ---------------------------------------------------------
    // GHOST POOL
    // ---------------------------------------------------------
    public void ClearGhostPool()
    {
        foreach (var g in _pool)
        {
            if (g != null)
            {
                _ghostRendererCache.Remove(g);
                Destroy(g);
            }
        }

        _pool.Clear();
    }

    // ---------------------------------------------------------
    // SMOOTH FOLLOW + LIFT
    // ---------------------------------------------------------
    private void Update()
    {
        if (_currentPreview == null)
            return;

        // Smooth follow with vertical offset (Lift) - only in Move Mode
        if (_hasTarget && !_deleteMode)
        {
            float adjustedSmooth = moveSmoothTime / Mathf.Max(0.01f, moveSmoothSpeed);

            Vector3 finalTarget = _targetPos;
            if (_isMovePreviewMode)
            {
                finalTarget.y += offsetMovePreview;
            }

            _currentPreview.transform.position =
                Vector3.SmoothDamp(
                    _currentPreview.transform.position,
                    finalTarget,
                    ref _velocity,
                    adjustedSmooth
                );

            if ((_currentPreview.transform.position - finalTarget).sqrMagnitude < 0.01f)
            {
                _hasTarget = false;
                _velocity = Vector3.zero;
            }
        }
    }

    // ---------------------------------------------------------
    // LATE ENFORCEMENT (build mode only)
    // ---------------------------------------------------------
    // Animators, Rigidbodies, and other late-running systems can write to
    // transform.position after Update(). In build mode (not move-preview)
    // we re-apply _targetPos here so the ghost always lands exactly on the
    // cursor cell — nothing can override it after this point.
    private void LateUpdate()
    {
        if (_currentPreview == null || _isMovePreviewMode || _deleteMode)
            return;

        if (_targetPos != Vector3.zero)
            _currentPreview.transform.position = _targetPos;
    }

    // ---------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------
    public void HideGhost()
    {
        if (_singleGhost != null)
            _singleGhost.SetActive(false);
    }
}
