using GameCore.Economy;
using GameCore.Build;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles selecting an existing placed object, picking it up,
/// moving it around the grid, rotating it, validating placement,
/// and confirming the new position.
///
/// Inherits from PlacementStateBase for standardized service access and event handling.
///
/// Key behavior:
/// - Clicking ANY footprint cell selects the object.
/// - If the user clicked an offset cell, movement preserves that offset.
/// - Rotation only applies to the currently selected object.
/// - No rotation leaks between objects.
/// </summary>
public class MoveState : PlacementStateBase
{
    // ---------------------------------------------------------
    // DEPENDENCIES
    // ---------------------------------------------------------
    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private new readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;
    private readonly MoneyService _money;
    private TopBarUI _topBarUI;

    private TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();

    private AisleInitializer _aisleInitializer;
    private AisleInitializer aisleInitializer => _aisleInitializer != null ? _aisleInitializer : _aisleInitializer = Object.FindAnyObjectByType<AisleInitializer>();

    private GameContext _gameContext;
    private GameContext gameContext => _gameContext != null ? _gameContext : _gameContext = Object.FindAnyObjectByType<GameContext>();

    // ---------------------------------------------------------
    // SELECTED OBJECT DATA
    // ---------------------------------------------------------
    private GameObject _obj;          // The actual object being moved
    private ObjDataSO _data;          // Its data (footprint, cost, etc.)
    private Vector2Int[] _offsets;    // Footprint offsets (rotated)
    private float _rotation;          // Current rotation (0/90/180/270)
    private Vector2Int _originalRoot; // Where the object started
    private Vector2Int[] _originalOffsets;
    private float _originalRotation;

    private bool _hasSelection;
    private bool _justSelectedThisFrame;

    // ---------------------------------------------------------
    // OFFSET‑AWARE SELECTION
    // ---------------------------------------------------------
    private Vector2Int _clickedCell;      // Cell user clicked on
    private Vector2Int _originAtSelect;   // Root at selection time
    private Vector2Int _selectionDelta;   // clickedCell - originAtSelect

    // ---------------------------------------------------------
    // MOVEMENT + VISUALS
    // ---------------------------------------------------------
    private Vector2Int _lastHoverCell = new Vector2Int(int.MinValue, int.MinValue);
    private readonly List<Vector2Int> _footprint = new();

    private static readonly Color MoveHighlightBlue =
        new Color(0.20f, 0.60f, 1.00f, 0.15f);

    private float _scrollCooldown = 0f;
    private const float ScrollThreshold = 0.01f;

    // Every hovered highlighter — a bare object is just itself, but a Foundation/Grounds slab
    // brings its floor-tile riders along so the whole footprint highlights (and reddens) as one
    // group instead of just the slab mesh.
    private readonly List<BuildingHighlighter> _hoveredHighlighters = new();

    // Floor tiles that travel with the foundation
    private struct RiderTile
    {
        public GameObject go;
        public ObjDataSO data;
        public Vector2Int cell;
        public Vector2Int localOffset; // cell - root
    }
    private readonly List<RiderTile> _riders = new();

    public override bool IsPlacementState => true;
    public string ObjectName => _obj != null ? _obj.name : "None";

    // ---------------------------------------------------------
    // CONSTRUCTOR
    // ---------------------------------------------------------
    public MoveState(
        PlacementActions actions,
        PreviewController preview,
        PlacementValidator validator,
        PlacementFinalizer finalizer,
        PlacementGrid grid,
        PlacementStateMachine fsm,
        RaycastController raycast,
        CellIndicatorController indicator,
        MoneyService money)
    {
        _actions = actions;
        _preview = preview;
        _validator = validator;
        _finalizer = finalizer;
        _grid = grid;
        _fsm = fsm;
        _raycast = raycast;
        _indicator = indicator;
        _money = money;

        // Bind controls
        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.BindRotateTo_R();
    }

    // ---------------------------------------------------------
    // ENTER / EXIT
    // ---------------------------------------------------------
    public override void OnEnter()
    {
        base.OnEnter(); // Initialize event manager and services

        BuildModeOverride.Instance?.Activate();

        _actions.BuildPlacement.Place.performed += OnConfirmMove;
        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;

        _raycast.EnableRay();
        _preview.ResetMoveGhostState();
        _preview.SetMovePreviewMode(true);
        _indicator.UseMoveMode();

        _hasSelection = false;
        _lastHoverCell = new Vector2Int(int.MinValue, int.MinValue);

        topBarUI?.SetState(GetType().Name);
    }

    public override void OnExit()
    {
        BuildModeOverride.Instance?.Deactivate();

        _raycast.DisableRay();
        _preview.ResetMoveGhostState();
        _preview.SetMovePreviewMode(false);
        _indicator.ClearAll();
        ClearHoverHighlight();

        if (_obj != null)
{
            _preview.ClearFlatHighlight(_obj);

            // If we still have a selection, it means the move wasn't confirmed.
            // We should put it back.
            if (_hasSelection)
            {
                _obj.SetActive(true);
                foreach (var o in _offsets)
                {
                    _grid.AddStackObject(_originalRoot + o, _obj, _data);
                }

                // Put rider tiles back where they came from
                foreach (var rider in _riders)
                {
                    if (rider.go == null) continue;
                    rider.go.SetActive(true);
                    _grid.AddStackObject(rider.cell, rider.go, rider.data);
                }
            }
        }

        // Clear selection state
        _obj = null;
        _data = null;
        _offsets = null;
        _rotation = 0f;
        _originalRoot = default;
        _hasSelection = false;
        _riders.Clear();

        _actions.BuildPlacement.Rotate.performed -= OnRotatePerformed;
        _actions.BuildPlacement.Place.performed -= OnConfirmMove;

        base.OnExit(); // Clean up base state
    }

    // ---------------------------------------------------------
    // SELECT OBJECT - Stack Aware
    // ---------------------------------------------------------
    private void TrySelectObject()
    {
        // Must hit an object and not be over UI
        if (_raycast.HitObject == null || _raycast.IsPointerOverUI)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // Apply "Trace to Top" for selection - priority to grid data for clicking.
        // If we hit the floor or a foundation, check if there's a stackable object on top of it.
        GameObject target = _raycast.HitObject;
        var hitBD = target.GetComponentInParent<BuildingData>();
        if (hitBD == null || hitBD.Data.isFloor || hitBD.Data.category == "Foundation" || hitBD.Data.category == "Grounds")
        {
            // When we DID hit a Foundation/Grounds slab directly (its own collider), scan its own
            // real footprint rather than trusting _raycast.HitCell — see FindTallestOnFootprint for
            // why that cell can be a full grid square off from what's actually under the cursor for
            // anything elevated above true ground level. Gated on ground/foundation specifically
            // (not "!isFloor") since Foundation/Grounds are ALSO flagged isFloor.
            GameObject top = (hitBD != null && IsGroundOrFoundation(hitBD.Data))
                ? FindTallestOnFootprint(hitBD)
                : _grid.GetTopObject(_raycast.HitCell);
            if (top != null) target = top;
        }

        var bd = target.GetComponentInParent<BuildingData>();

        // Validate we found a movable object. Decorative floor-pattern tiles can only be replaced,
        // not moved — but Foundation/Grounds slabs are ALSO flagged isFloor (they're walkable) while
        // still being legitimately movable structural objects, so isFloor alone can't be the gate.
        if (bd == null || bd.Data == null || (bd.Data.isFloor && !IsGroundOrFoundation(bd.Data)))
            return;

        // -----------------------------------------------------
        // MOVE PROTECTION
        // -----------------------------------------------------
        // Block manual movement of Racking and Inventory items (Pallets).
        // Racks should only be deleted or added to.
        // Inventory must be moved via MHE.
        if (bd.Data.category == "Inventory" || bd.Data.category == "Racking")
        {
            AudioManager.Play("InvalidPlace");
            return;
        }

        ClearHoverHighlight();

        // Capture object data
        _obj = bd.gameObject;
        _data = bd.Data;
        _offsets = bd.Offsets;
        _rotation = bd.Rotation;
        _originalRoot = bd.RootCell;

        _originalOffsets = _offsets;
        _originalRotation = _rotation;

        // -----------------------------------------------------
        // OFFSET-AWARE SELECTION
        // -----------------------------------------------------
        _clickedCell = _raycast.HitCell;
        _originAtSelect = _originalRoot;
        _selectionDelta = _clickedCell - _originAtSelect;

        Vector3 lastWorldPos = _obj.transform.position;
        float foundationY = _obj.transform.position.y;

        // REMOVE from grid FIRST so SnapTo calculates the correct baseline height
        // (now that the slot it was occupying is 'empty')
        foreach (var o in _offsets)
        {
            Vector2Int cell = _originalRoot + o;
            _grid.RemoveStackObject(cell, _obj, _data);
        }

        // -----------------------------------------------------
        // RIDER TILES (Foundation Travel)
        // -----------------------------------------------------
        // Only foundations/grounds carry "rider" tiles (like floor patterns)
        // with them when moved. Flavor items (lights), racks, and vehicles
        // should never pick up the floor beneath them.
        //
        // A combined Foundation whose 4 default tiles are still intact (never swapped to a custom
        // walkway/lane tile) doesn't need any of this — they're real Transform children of _obj now,
        // so Move()/rotation on the parent already carries them for free (MoveCommand's
        // SyncDefaultChildrenGrid handles the one thing Transform parenting doesn't: grid cell
        // membership). Only fall back to gathering independent riders when there's no group, or when
        // at least one cell has been swapped out (no longer intact) and is riding as a loose object.
        var floorGroup = bd.GetComponent<FoundationFloorGroup>();
        bool hasIntactDefaultChildren = floorGroup != null && floorGroup.AllDefaultTilesIntact();
        if (hasIntactDefaultChildren)
        {
            // Vacate the children's own grid cells too, right now — otherwise the grid still thinks
            // these cells are occupied for the whole drag (they only get cleared at drop time by
            // MoveCommand), even though the whole group just visually lifted off. Index correlation
            // between _offsets[i] and DefaultTiles[i] holds regardless of the foundation's current
            // rotation (RotateOffsets preserves each corner's index slot as it rotates) — must use
            // _offsets (the object's CURRENT, possibly-already-rotated footprint) here, not a
            // freshly-recomputed unrotated one, since that's what the children were actually
            // registered against. SyncDefaultChildrenGrid's own RemoveStackObject at drop is a safe
            // no-op against this early removal.
            for (int i = 0; i < floorGroup.DefaultTiles.Length && i < _offsets.Length; i++)
            {
                var tile = floorGroup.DefaultTiles[i];
                if (tile == null) continue;
                _grid.RemoveStackObject(_originalRoot + _offsets[i], tile.gameObject, tile.data);
            }
        }
        else if (_data != null && (_data.category == "Foundation" || _data.category == "Grounds"))
        {
            GatherRiderTiles(foundationY);
        }

        // Reveal underlying yard tiles (or other hidden floor tiles) under the picked up foundation
        if (_data != null && (_data.category == "Foundation" || _data.category == "Grounds"))
        {
            foreach (var o in _offsets)
            {
                Vector2Int cell = _originalRoot + o;
                var cellObjs = _grid.GetObjectsInCell(cell);
                if (cellObjs != null)
                {
                    foreach (var entry in cellObjs)
                    {
                        if (entry.data != null && entry.data.isFloor)
                        {
                            if (entry.instance != null && !entry.instance.activeSelf)
                            {
                                // Check if this is one of our rider tiles (we shouldn't enable it because it's disabled for movement)
                                bool isRider = false;
                                foreach (var r in _riders)
                                {
                                    if (r.go == entry.instance)
                                    {
                                        isRider = true;
                                        break;
                                    }
                                }
                                if (!isRider)
                                {
                                    entry.instance.SetActive(true);
                                }
                            }
                        }
                    }
                }
                _grid.UpdateStackPositions(cell);
            }
        }

        // Highlight + show ghost
        _preview.ApplyFlatHighlight(_obj, MoveHighlightBlue);
        _preview.Show(_data);
        _preview.Rotate(_rotation); // IMPORTANT: match selected object's rotation

        // Snap ghost to the object's last position.
        // It will then smooth-lift to the offset in the next frame.
        _preview.SnapTo(lastWorldPos, _originalRoot, _data);
        _lastHoverCell = _originalRoot;

        // Give every rider its own ghost too, so the whole slab (foundation + floor pattern) lifts
        // and follows the cursor as one group instead of the tiles just vanishing mid-drag.
        // Positioned once immediately at the baseline so they don't flash at the origin for a frame
        // before the first Update() call places them — MoveRiderGhosts reads the main ghost's
        // just-snapped (un-lifted) height here, then tracks its lift every frame after.
        if (_riders.Count > 0)
        {
            var riderSpecs = new List<(ObjDataSO, Vector2Int)>();
            foreach (var r in _riders)
                riderSpecs.Add((r.data, r.localOffset));
            _preview.ShowRiderGhosts(riderSpecs);
            _preview.MoveRiderGhosts(_originalRoot);
        }

        _obj.SetActive(false);
        _hasSelection = true;
        _justSelectedThisFrame = true;
    }

    // ---------------------------------------------------------
    // UPDATE — MOVEMENT + VALIDATION
    // ---------------------------------------------------------
    public override void Update()
    {
        _justSelectedThisFrame = false;
        _raycast.Tick();

        if (_raycast.IsPointerOverUI)
        {
            ClearHoverHighlight();
            if (!_hasSelection)
            {
                _indicator.ClearAll();
                return;
            }
            // If moving, we still want to show the ghost maybe? 
            // Or hide it? Usually, if you move the mouse over UI while holding an object, 
            // the object should stay at its last valid position or hide.
            _preview.HideGhost();
            _indicator.ClearAll();
            return;
        }

        // Restore ghost if we have a selection and just left the UI
        if (_hasSelection)
        {
            _preview.Show(_data);
            _preview.Rotate(_rotation);
        }

        Vector2Int hitCell = _raycast.HitCell;
        topBarUI?.SetCell(hitCell.x, hitCell.y);

        if (!_hasSelection)
        {
            // Skip the raw single-cell indicator when hovering a Foundation/Grounds slab — its
            // group highlight (below, via UpdateHoverHighlight) already gives accurate feedback
            // using the object-hit raycast pass and this state's own footprint-aware lookup. This
            // indicator instead uses the ground-projected HitCell, which RaycastController
            // deliberately keeps anchored to the ground plane for movement-drag stability (see its
            // "Perspective Jumping" comment) — for anything elevated, that can land a full cell off
            // from what's visually under the cursor, showing up as a stray quad beside the correct
            // highlight instead of on top of it.
            var hoverBD = _raycast.HitObject != null ? _raycast.HitObject.GetComponentInParent<BuildingData>() : null;
            bool hoveringFoundation = hoverBD != null && hoverBD.Data != null && IsGroundOrFoundation(hoverBD.Data);
            if (hoveringFoundation)
                _indicator.ClearAll();
            else
                _indicator.ShowCell(hitCell);

            UpdateHoverHighlight();
            TrySelectObject();
            return;
        }

        _indicator.ShowCell(hitCell);

        if (!_raycast.HasHit)
        {
            _preview.HideGhost();
            return;
        }

        // -----------------------------------------------------
        // SCROLL WHEEL ROTATION
        // -----------------------------------------------------
        if (_scrollCooldown > 0)
        {
            _scrollCooldown -= Time.deltaTime;
        }

        float scrollDelta = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollDelta) > ScrollThreshold && _scrollCooldown <= 0)
        {
            RotateObject();
            _scrollCooldown = 0.2f;
        }

        // -----------------------------------------------------
        // OFFSET‑AWARE MOVEMENT
        // newRoot = hitCell - (clickedCell - originAtSelect)
        // -----------------------------------------------------
        Vector2Int newRoot = hitCell - _selectionDelta;

        if (newRoot != _lastHoverCell)
        {
            AudioManager.Play("NewCell");
            _lastHoverCell = newRoot;
        }

        bool valid = _validator.IsValidPlacement(newRoot, _offsets, _data, _obj);

        // Build footprint for indicator
        _footprint.Clear();
        foreach (var o in _offsets)
            _footprint.Add(newRoot + o);

        _indicator.ShowCells(_footprint, cell => valid);

        // Move ghost
        Vector3 pos = _grid.GetCellCenter(newRoot);
        _preview.MoveTo(pos, newRoot, _data);
        _preview.MoveRiderGhosts(newRoot);

        if (valid)
        {
            _preview.SetGhostValid();
            _preview.SetRiderGhostsValid();
        }
        else
        {
            _preview.SetGhostInvalid();
            _preview.SetRiderGhostsInvalid();
        }
    }

    // ---------------------------------------------------------
    // CONFIRM MOVE
    // ---------------------------------------------------------
    private void OnConfirmMove(InputAction.CallbackContext ctx)
    {
        if (!_hasSelection || _justSelectedThisFrame || _raycast.IsPointerOverUI)
            return;

        Vector2Int hitCell = _raycast.HitCell;
Vector2Int newRoot = hitCell - _selectionDelta;

        if (!_validator.IsValidPlacement(newRoot, _offsets, _data, _obj))
        {
            AudioManager.Play("InvalidPlace");
            return;
        }

        AudioManager.Play("ValidPlace");

        _preview.ClearFlatHighlight(_obj);

        // Build rider list for the command with proper offsets
        var riderList = new List<MoveCommand.RiderObject>();
        foreach (var rider in _riders)
        {
            if (rider.go != null)
            {
                // Find the new local offset by looking up the old offset in the old footprint
                Vector2Int newLocal = rider.localOffset;
                int idx = System.Array.IndexOf(_originalOffsets, rider.localOffset);
                if (idx >= 0 && idx < _offsets.Length)
                    newLocal = _offsets[idx];

                riderList.Add(new MoveCommand.RiderObject
                {
                    instance = rider.go,
                    data = rider.data,
                    oldLocal = rider.localOffset,
                    newLocal = newLocal
                });
            }
        }

        // Push undo/redo command with riders
        _fsm.History.Push(
            new MoveCommand(
                _grid,
                _obj,
                _data,
                _originalRoot,
                newRoot,
                _originalOffsets,
                _offsets,
                _originalRotation,
                _rotation,
                _money,
                riderList
            )
        );

        _preview.ResetMoveGhostState();

        // A committed aisle rack that was moved/rotated must re-hide its away face — the labels
        // travelled with it, so a rotate would otherwise leave the hidden face pointing at the aisle.
        // Uses the rack's STORED aisle-facing direction, so it's correct regardless of new rotation.
        if (_data != null && _data.category == "Racking")
            aisleInitializer?.ReapplyRackFaces(_obj);

        // Clear selection
        _obj = null;
        _data = null;
        _offsets = null;
        _rotation = 0f;
        _originalRoot = default;
        _hasSelection = false;
        _riders.Clear();

        _indicator.ClearAll();
    }

    // ---------------------------------------------------------
    // ROTATE SELECTED OBJECT
    // ---------------------------------------------------------
    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        RotateObject();
    }

    private void RotateObject()
    {
        if (!_hasSelection)
            return;

        AudioManager.Play("Rotate");

        _rotation += 90f;
        if (_rotation >= 360f)
            _rotation = 0f;

        _preview.Rotate(_rotation);

        // Update footprint for new rotation
        if (_data != null)
            _offsets = _data.GetFootprintOffsets(-_rotation);
    }

    private void UpdateHoverHighlight()
    {
        if (_hasSelection) return;

        bool isValid = true;

        // Apply "Trace to Top" - if hitting a stack but not the top, select the top.
        GameObject target = _raycast.HitObject;
        var hitBD = target != null ? target.GetComponentInParent<BuildingData>() : null;
        if (hitBD == null || hitBD.Data.isFloor || hitBD.Data.category == "Foundation" || hitBD.Data.category == "Grounds")
        {
            // When we DID hit a Foundation/Grounds slab directly, scan its own real footprint rather
            // than _raycast.HitCell — see FindTallestOnFootprint. Same ground/foundation gate as
            // TrySelectObject, not "!isFloor" (Foundation/Grounds are ALSO flagged isFloor).
            GameObject top = (hitBD != null && IsGroundOrFoundation(hitBD.Data))
                ? FindTallestOnFootprint(hitBD)
                : _grid.GetTopObject(_raycast.HitCell);
            if (top != null) target = top;
        }

        BuildingData bd = target != null ? target.GetComponentInParent<BuildingData>() : null;

        var newTargets = new List<BuildingHighlighter>();

        // Same isFloor caveat as TrySelectObject: Foundation/Grounds are flagged isFloor too, but
        // must still pass this gate to be highlighted (and to pull in their rider tiles) as a group.
        if (bd != null && bd.Data != null && (!bd.Data.isFloor || IsGroundOrFoundation(bd.Data)))
        {
            var mainHighlighter = bd.GetComponentInParent<BuildingHighlighter>();
            if (mainHighlighter != null) newTargets.Add(mainHighlighter);

            string cat = bd.Data.category;

            // Racks and Inventory (Pallets) are not movable.
            // Highlight them red immediately on hover.
            if (cat == "Racking" || cat == "Inventory")
            {
                isValid = false;
            }
            else
            {
                // Normal objects check validity (usually true for already placed objects)
                isValid = _validator.IsValidPlacement(bd.RootCell, bd.Offsets, bd.Data, bd.gameObject);

                // A Foundation/Grounds slab brings its floor-tile riders along visually too — the
                // player sees one object (the whole 2x2 footprint + its floor pattern), so it should
                // highlight, and turn red when invalid, as one group rather than just the bare slab
                // mesh with its tiles looking untouched.
                if (cat == "Foundation" || cat == "Grounds")
                {
                    foreach (var rider in FindRiderTilesReadOnly(bd))
                    {
                        var rh = rider.GetComponent<BuildingHighlighter>();
                        if (rh != null) newTargets.Add(rh);
                    }
                }
            }
        }

        if (!SameHighlighterSet(newTargets))
        {
            ClearHoverHighlight();
            _hoveredHighlighters.AddRange(newTargets);
            foreach (var h in _hoveredHighlighters)
            {
                if (isValid)
                    h.HighlightValid(true);
                else
                    h.HighlightInvalid(true);
            }
        }
    }

    /// <summary>Foundation/Grounds slabs are flagged isFloor (they're walkable surface) even though
    /// they're structural, movable objects — unlike a decorative floor-pattern tile, which is
    /// isFloor and category "Floor" and can only be replaced, never picked up.</summary>
    private bool IsGroundOrFoundation(ObjDataSO data)
    {
        return data != null && (data.category == "Foundation" || data.category == "Grounds");
    }

    private bool SameHighlighterSet(List<BuildingHighlighter> newTargets)
    {
        if (newTargets.Count != _hoveredHighlighters.Count) return false;
        foreach (var h in newTargets)
            if (!_hoveredHighlighters.Contains(h)) return false;
        return true;
    }

    /// <summary>
    /// Scans every cell of bd's own real footprint — not the single _raycast.HitCell — for
    /// something non-floor stacked above it, and returns the first one found.
    ///
    /// _raycast.HitCell is derived from RaycastController's ground-level raycast pass, which uses
    /// QueryTriggerInteraction.Ignore and so deliberately ignores a foundation's own (trigger)
    /// collider — for anything elevated above true ground level, projecting that ground hit back to
    /// the camera ray can land a full grid cell off from where the cursor visually is. Confirmed
    /// empirically: hovering a 2x2 foundation's own floor tiles (sitting ~1m above true ground)
    /// mapped HitCell to a cell consistently one off from the tile actually under the cursor —
    /// which is what let _grid.GetTopObject(HitCell) occasionally resolve to an unrelated or
    /// nonexistent object and silently fail to select/highlight the foundation at all. Scanning the
    /// object's own already-known footprint sidesteps the mismatch instead of trusting that cell.
    /// </summary>
    private GameObject FindTallestOnFootprint(BuildingData bd)
    {
        if (bd == null || bd.Offsets == null) return null;

        foreach (var o in bd.Offsets)
        {
            GameObject top = _grid.GetTopObject(bd.RootCell + o);
            if (top == null || top == bd.gameObject) continue;

            var topBd = top.GetComponentInParent<BuildingData>();
            if (topBd != null && topBd.Data != null && !topBd.Data.isFloor)
                return top;
        }
        return null;
    }

    /// <summary>Read-only twin of GatherRiderTiles — used for hover highlighting, where we must NOT
    /// touch the grid or (de)activate anything, just find out which floor tiles are currently riding
    /// this foundation so their highlighters can be included in the hover group.</summary>
    private List<GameObject> FindRiderTilesReadOnly(BuildingData foundationBd)
    {
        var found = new List<GameObject>();
        if (foundationBd == null || foundationBd.Offsets == null) return found;

        float foundationY = foundationBd.transform.position.y;
        foreach (var o in foundationBd.Offsets)
        {
            Vector2Int cell = foundationBd.RootCell + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

            foreach (var entry in list)
            {
                if (entry.data == null || !entry.data.isFloor) continue;
                if (entry.instance == null || !entry.instance.activeSelf) continue;
                if (entry.data.id == 200) continue; // never the yard tile
                if (entry.instance.transform.position.y > foundationY)
                    found.Add(entry.instance);
            }
        }
        return found;
    }

    private void GatherRiderTiles(float foundationY)
    {
        _riders.Clear();

        foreach (var o in _originalOffsets)
        {
            Vector2Int cell = _originalRoot + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

            foreach (var entry in list.ToArray())
            {
                if (entry.data == null || !entry.data.isFloor) continue;
                if (entry.instance == null || !entry.instance.activeSelf) continue;

                // Never pick up the yard tile (ID 200)
                if (entry.data.id == 200) continue;

                // Only pick up floors that are ABOVE the foundation
                if (entry.instance.transform.position.y > foundationY)
                {
                    _riders.Add(new RiderTile
                    {
                        go = entry.instance,
                        data = entry.data,
                        cell = cell,
                        localOffset = cell - _originalRoot
                    });
                    _grid.RemoveStackObject(cell, entry.instance, entry.data);
                    entry.instance.SetActive(false);
                }
            }
        }
    }

    private void ClearHoverHighlight()
    {
        foreach (var h in _hoveredHighlighters)
        {
            if (h == null) continue;
            h.HighlightValid(false);
            h.HighlightInvalid(false);
        }
        _hoveredHighlighters.Clear();
    }

    private bool IsObjectOccupied(BuildingData bd)
    {
        if (bd == null || bd.Offsets == null) return false;

        // 1. Flavor items (Lights, Props) are never blocked by occupation.
        if (bd.Data != null && bd.Data.category == "Flavor") return false;

        // 2. Procedural Content Check (Pallets)
        // If a rack has inventory (cases) on it, it cannot be moved.
        var pb = bd.GetComponent<PalletBuilder>();
        if (pb != null && pb.TotalCases > 0) return true;

        // 3. Grid Stack Check (Racks/Stacks)
        float myY = bd.gameObject.transform.position.y;
        foreach (var o in bd.Offsets)
        {
            Vector2Int cell = bd.RootCell + o;
            var cellObjs = _grid.GetObjectsInCell(cell);
            if (cellObjs == null) continue;

            foreach (var entry in cellObjs)
            {
                // Skip the object itself
                if (entry.instance != null && entry.instance != bd.gameObject)
                {
                    if (entry.data != null && !entry.data.isFloor)
                    {
                        string cat = entry.data.category;
                        
                        // Ignore environment/utility categories that don't represent "contents"
                        if (cat == "Foundation" || cat == "Grounds" || cat == "Floor" || 
                            cat == "Ground" || cat == "Flavor" || cat == "Walls")
                        {
                            continue;
                        }

                        // Ignore dynamic agents (Workers, Vehicles) - they'll just move out of the way.
                        // We check for components instead of categories for these.
                        if (entry.instance.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
                        {
                            continue;
                        }

                        // For everything else (Racking, Inventory/Pallets):
                        // Only block if the other object is HIGHER than us (sitting on top).
                        if (entry.instance.transform.position.y > myY + 0.05f)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

}
