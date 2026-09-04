using GameCore.Economy;
using GameCore.Build;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// BuildState handles placing new objects on the grid:
/// - Hovering the grid
/// - Drag placement (multi-place)
/// - Rotation
/// - Cost preview
/// - Validity preview
/// - Final placement
///
/// Inherits from PlacementStateBase for standardized service access and event handling.
/// NO LONGER interacts with the hover popup UI - only IdleState controls those.
/// </summary>
public class BuildState : PlacementStateBase
{
    protected readonly PlacementActions _actions;
    protected readonly PreviewController _preview;
    protected readonly PlacementValidator _validator;
    protected readonly PlacementFinalizer _finalizer;
    protected new readonly PlacementGrid _grid;
    protected readonly PlacementStateMachine _fsm;
    protected readonly RaycastController _raycast;
    protected readonly CellIndicatorController _indicator;
    protected readonly MoneyService _money;
    protected readonly PreviewCostUI _costUI;
    protected readonly BuildMenuUI _buildMenuUI;
    protected TopBarUI _topBarUI;

    protected TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();

    protected ObjDataSO _currentData;

    protected bool _placeRequested;
    protected bool _rotateRequested;
    protected float _currentRotation;

    protected bool _isDragging;
    protected Vector2Int _dragStartCell;
    protected readonly List<Vector2Int> _dragCells = new();

    protected readonly List<Vector2Int> _indicatorBuffer = new();
    protected readonly List<Vector2Int> _footprintBuffer = new();

    // Cells in the current drag that the grid would accept but the wallet won't. Kept so the cell
    // indicator can be told about them — it's driven by a per-cell predicate that only knows about
    // grid validity, and a green indicator under a red ghost reads as a bug.
    protected readonly HashSet<Vector2Int> _unaffordableCells = new();

    // Objects ghosted with the orange-line shader during hover to show they will be replaced
    protected readonly Dictionary<Renderer, Material[]> _replacementOriginalMaterials = new();
    protected Material _ghostReplaceOrangeMat;

public override bool IsPlacementState => true;
    public ObjDataSO CurrentData => _currentData;
    public bool IsDragging => _isDragging;
    public string ObjectName => _currentData != null ? _currentData.objName : "None";

    private Vector2Int _lastHitCell;

    public BuildState(
        PlacementActions actions,
        PreviewController preview,
        PlacementValidator validator,
        PlacementFinalizer finalizer,
        PlacementGrid grid,
        PlacementStateMachine fsm,
        RaycastController raycast,
        CellIndicatorController indicator,
        MoneyService money,
        PreviewCostUI costUI,
        BuildMenuUI buildMenuUI)
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
        _costUI = costUI;
        _buildMenuUI = buildMenuUI;

        _actions.BuildPlacement.BindRotateTo_R();
        _actions.BuildPlacement.BindPlaceToMouseLeft();
    }

    // ---------------------------------------------------------
    // ENTER
    // ---------------------------------------------------------
    public override void OnEnter()
    {
        base.OnEnter(); // Initialize event manager and services

        BuildModeOverride.Instance?.Activate();

        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;
        _actions.BuildPlacement.Place.canceled += OnPlacePerformed;

        topBarUI?.SetState(GetType().Name);

        if (_currentData == null)
            return;

        _indicator.UseBuildMode();
        _raycast.EnableRay();

        _preview.Show(_currentData);

        _placeRequested = false;
        _rotateRequested = false;

        _currentRotation = _preview.CurrentRotation;

        _isDragging = false;
        _dragCells.Clear();

        _costUI.Hide();

        _lastHitCell = new Vector2Int(999, 999); // force first hit to register
    }

    // ---------------------------------------------------------
    // EXIT
    // ---------------------------------------------------------
    public override void OnExit()
    {
        BuildModeOverride.Instance?.Deactivate();

        _raycast.DisableRay();
        _indicator.ClearAll();
        _preview.Hide();
        _costUI.Hide();
        ClearReplacementHighlights();

        _actions.BuildPlacement.Place.canceled -= OnPlacePerformed;
        _actions.BuildPlacement.Rotate.performed -= OnRotatePerformed;

        base.OnExit(); // Clean up base state
    }

    // ---------------------------------------------------------
    // UPDATE
    // ---------------------------------------------------------
    public override void Update()
    {
        _raycast.Tick();

        if (_raycast.IsPointerOverUI)
        {
            _indicator.ClearAll();
            _preview.Hide();
            _costUI.Hide();
            return;
        }

        // Ensure preview is shown if we just left the UI. Gated on !_isDragging: this runs every
        // frame, and during a drag the single hover-ghost was deliberately hidden by
        // BeginSelectionCells() in favor of the per-cell multi-ghosts — without the guard, this
        // call reactivates it every frame with nothing to ever hide it again until the drag ends,
        // leaving it visibly stuck at wherever it was last positioned right before the drag began
        // (frozen, since HandleDragPlacement's `return` skips the normal MoveTo call that would
        // otherwise keep it tracking the cursor). It then sits duplicated on top of the drag-start
        // segment for the whole drag — misread as that one segment floating at the wrong height.
        if (_currentData != null && !_isDragging)
        {
            _preview.Show(_currentData);
        }

        if (!_raycast.HasHit)
        {
            _indicator.ClearAll();
            _preview.Hide();
            _costUI.Hide();
            return;
        }

        Vector2Int root = _raycast.HitCell;

        // If hovering an existing Foundation/Grounds slab (elevated ~1.06 above true ground),
        // HitCell's deliberate ground-plane projection (see RaycastController's "Perspective
        // Jumping" comment) can land a full cell off from what's visually under the cursor. That
        // single-cell error becomes _dragStartCell below, so this correction matters for the very
        // first cell of a drag (or an ordinary single-click placement) — using the object-hit
        // point instead, which IS accurate for whatever's actually under the cursor.
        //
        // Gated on !_isDragging: this whole block re-runs every frame, and once a drag is already
        // underway it becomes a second, DISCONTINUOUS source for `root` — the raycast toggles
        // between hitting bare ground (plain HitCell) and grazing the existing foundation's own
        // edge (this WorldToCell-on-HitPoint branch) as the cursor hovers near that boundary,
        // jumping `root` between two independently-computed cells rather than smoothly crossing
        // one. HandleDragPlacement's own stride-lock (see its kX/kY comment) already keeps the
        // drag stable frame-to-frame as long as `currentCell` comes from ONE consistent source —
        // reintroducing this swap mid-drag defeats that, and reads as the whole strip re-anchoring
        // and overlapping the neighboring foundation the instant the cursor nears its edge.
        if (!_isDragging && _raycast.HitObject != null)
        {
            var hoverBD = _raycast.HitObject.GetComponentInParent<BuildingData>();
            if (hoverBD != null && hoverBD.Data != null &&
                (hoverBD.Data.category == "Foundation" || hoverBD.Data.category == "Grounds"))
            {
                root = _grid.WorldToCell(_raycast.HitPoint);
            }
        }

        // Stacking a rack on top of a LIVE rack row must land EXACTLY on the footprint of the rack
        // underneath — not "the cell nearest the cursor," which is what every other branch here
        // computes and is exactly the source of two related bugs Tad reported:
        //   1. HitCell's ground-plane projection error (see the Foundation/Grounds comment above) is
        //      much larger over a rack top than a Foundation slab, so even the object-hit-point
        //      correction that fixes Foundation/Grounds can land a cell off here.
        //   2. Even with an accurate hit point, a cursor-derived cell is only ONE cell of the hovered
        //      rack's footprint — for a 2-wide rack, hovering its far half still resolves to a
        //      DIFFERENT root than hovering its near half, so the new rack's own footprint (which can
        //      be a completely different size/shape) has no reason to land flush with the one below.
        // The fix for both: skip cell math entirely and read the hovered rack's OWN root cell straight
        // off its BuildingData (set once at placement, in PlaceCommand/DragPlaceCommand). Whichever
        // part of that rack the cursor is over, the new rack's root snaps to the SAME cell — so it's
        // always flush with what's underneath regardless of either rack's footprint, and regardless of
        // where inside the hovered footprint the mouse happens to sit. Not gated on !_isDragging either
        // (see the Foundation comment for why that gate exists): a rack-stacking drag hovers the SAME
        // rack row for its whole length, so there's no discontinuous-source toggle to guard against —
        // and locking to a fixed root cell rather than a live hit point is what makes the anchor "show
        // green and aligned, then never move again" per Tad's explicit ask.
        if (_raycast.HitObject != null)
        {
            var hoverRack = _raycast.HitObject.GetComponentInParent<BuildingData>();
            if (hoverRack != null && hoverRack.Data != null && hoverRack.Data.category == "Racking")
                root = hoverRack.RootCell;
        }

        topBarUI?.SetCell(root.x, root.y);

        // -----------------------------------------------------
        // DRAG / CLICK DETECTION
        // -----------------------------------------------------

        // 1. Mouse pressed → record starting cell
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragCells.Clear();
            _dragStartCell = root;
        }

        // 2. If mouse held AND cell changed → start drag
        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if (root != _dragStartCell)
            {
                _isDragging = true;

                _preview.Hide();
                _indicator.ClearAll();
                _costUI.Hide();

                _dragCells.Clear();
                _preview.BeginSelectionCells();
                return;
            }
        }

        // 3. If dragging, handle drag placement
        if (_isDragging)
        {
            HandleDragPlacement(root);
            return;
        }

        // Chevrons mark a deliberately OPEN cell at the edge of a rack run. Without this guard,
        // hovering/clicking one here is ALSO seen as "place a new rack in this empty cell" — which
        // succeeds, grows the collection, and makes ChevronSpawner destroy/recreate the very
        // chevron GameObject the player just clicked. Its double-click timer lives on that
        // instance, so the second click of an intended double-click lands on a fresh chevron with
        // a reset timer and OpenSetup() never fires — read as "the aisle setup UI won't open."
        // Treat it like hovering UI: suppress the ghost/placement so the click reaches
        // ChevronController untouched.
        if (_raycast.HitObject != null && _raycast.HitObject.GetComponent<ChevronController>() != null)
        {
            _indicator.ClearAll();
            _preview.Hide();
            _costUI.Hide();
            return;
        }

        //4. Play NewCell hover sound if we have moved to a new cell.
        //
        if (root != _lastHitCell)
        {
            AudioManager.Play("NewCell");
            _lastHitCell = root;
        }

        // ---------------------------------------------------------
        // ROTATION
        // ---------------------------------------------------------

        /*
        if (_scrollCooldown > 0)
        {
            _scrollCooldown -= Time.deltaTime;
        }
        
        float scrollDelta = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollDelta) > ScrollThreshold && _scrollCooldown <= 0)
        {
            RotateObject();
            _scrollCooldown = 0.2f; // cooldown in seconds
        }
        */
        if (_rotateRequested)
        {
            _rotateRequested = false;

            _currentRotation += 90f;
            if (_currentRotation >= 360f)
                _currentRotation = 0f;

            _preview.Rotate(_currentRotation);
        }
        
        // ---------------------------------------------------------
        // GHOST + VALIDATION
        // ---------------------------------------------------------
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        // Snap to nearest replaceable target so door previews land on the correct
        // cell even when the raycast hits an adjacent face. Walls (canBeReplacedByDoor)
        // are 1×1 and place precisely — no snap needed, and snap would steal the
        // cursor from the cell adjacent to a placed door (causing the "1-cell gap on X" bug).
        if (_currentData.replacesWalls)
            root = SnapToReplaceTarget(root, offsets, _currentData);
        else
            root = SnapFootprintToHover(root, offsets, _currentData);

        _preview.MoveTo(_grid.GetCellCenter(root), root, _currentData);

        bool isValid = _validator.IsValidPlacement(root, offsets, _currentData);

        // Money is part of validity, not just a label colour. An affordable-looking green ghost
        // over a purchase that will be refused on click is the same lie the drag preview used to
        // tell — see AffordableCount.
        bool canAfford = _money.CanAfford(_currentData.cost);
        if (!canAfford)
            isValid = false;

        // Orange-tint objects that will be replaced so the player sees what disappears.
        ClearReplacementHighlights();
        if (isValid)
            HighlightReplacementTargets(root, offsets, _currentData);

        _indicator.ShowCells(
            BuildFootprintBuffered(root, offsets),
            cell => isValid
        );

        if (isValid)
            _preview.SetGhostValid();
        else
            _preview.SetGhostInvalid();

        // ---------------------------------------------------------
        // COST PREVIEW
        // ---------------------------------------------------------
        int cost = _currentData.cost;

        _costUI.ShowCost(cost, canAfford);
        _costUI.SetScreenPosition(_raycast.RawHitPoint, Camera.main);

        // ---------------------------------------------------------
        // PLACE
        // ---------------------------------------------------------
        if (_placeRequested)
        {
            _placeRequested = false;

            // Prevent placing on "ClearsGridAfterPlacement" objects
            GameObject hitObj = _raycast.HitObject;
            if (hitObj != null)
            {
                var bd = hitObj.GetComponent<BuildingData>();
                if (bd != null && bd.Data.ClearsGridAfterPlacement)
                {
                    AudioManager.Play("InvalidPlace");
                    return;
                }
            }

            bool isValidNow = _validator.IsValidPlacement(root, offsets, _currentData);

            if (!isValidNow || !_money.CanAfford(cost))
            {
                AudioManager.Play("InvalidPlace");
                _preview.SetGhostInvalid();
                _indicator.ShowCells(BuildFootprintBuffered(root, offsets), cell => false);
                return;
            }

            AudioManager.Play("ValidPlace");

            _fsm.History.Push(
                new PlaceCommand(
                    _grid,
                    _finalizer,
                    root,
                    offsets,
                    _currentData,
                    _currentRotation,
                    _money)
            );
        }
    }

    // ---------------------------------------------------------
    // ROTATE INPUT
    // ---------------------------------------------------------
    public void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        RotateObject();
    }

    private void RotateObject()
    {
        AudioManager.Play("Rotate");
        _rotateRequested = true;
    }

    // ---------------------------------------------------------
    // PLACE INPUT
    // ---------------------------------------------------------
    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        if (_isDragging || _raycast.IsPointerOverUI)
            return;

        if (_currentData == null)
            return;

        if (!_raycast.HasHit)
            return;

        // See the matching guard in Update() — a click on a chevron's (deliberately open) cell
        // must not also register as "place a new rack here."
        if (_raycast.HitObject != null && _raycast.HitObject.GetComponent<ChevronController>() != null)
            return;

        Vector2Int root = _raycast.HitCell;
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        if (_currentData.replacesWalls)
            root = SnapToReplaceTarget(root, offsets, _currentData);
        else
            root = SnapFootprintToHover(root, offsets, _currentData);

        bool isValid = _validator.IsValidPlacement(root, offsets, _currentData);

        GameObject hitObj = _raycast.HitObject;
        if (hitObj != null)
        {
            var bd = hitObj.GetComponent<BuildingData>();
            if (bd != null && bd.Data.ClearsGridAfterPlacement)
                isValid = false;
        }

        if (!isValid)
        {
            AudioManager.Play("InvalidPlace");
            return;
        }

        _placeRequested = true;
    }

    // ---------------------------------------------------------
    // DRAG PLACEMENT
    // ---------------------------------------------------------
    protected virtual void HandleDragPlacement(Vector2Int currentCell)
    {
        _dragCells.Clear();
        // Clear the VISUAL ghosts too, not just the logical drag set — otherwise ghosts
        // for cells that drop out of a shrinking drag stay active (the preview only ever
        // grows and gets "stuck" at the drag's furthest extent). Pooled, so no churn.
        _preview.ClearMultiGhosts();

        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);
        Vector2Int stride = GetStride(offsets);

        // How many whole stride-widths currentCell has moved from the drag's start, along each
        // axis. Integer division truncates toward zero, so any currentCell still inside the FIRST
        // segment's own footprint span (its stride width/height, in either direction from the
        // start cell) maps to k=0 — the segment the drag began on never re-anchors just because
        // the raw grid cell under the cursor changed while still hovering that same segment. For a
        // multi-cell-wide object (e.g. a 2-wide foundation or a rack run), the old code used the
        // raw currentCell directly as both the loop bound AND the direction test — so a cursor
        // that dipped from one grid cell to another INSIDE the same first segment (e.g. local
        // offset x=1 to x=0, never actually leaving the segment's own footprint) could flip
        // stepX's sign and re-walk the whole strip from a different anchor, landing it overlapping
        // itself. Quantizing by stride first keeps the strip's segment boundaries stable across
        // that wobble; only once the cursor genuinely crosses into the NEXT stride-width does a
        // new segment appear.
        int kX = (currentCell.x - _dragStartCell.x) / stride.x;
        int kY = (currentCell.y - _dragStartCell.y) / stride.y;

        int stepX = kX >= 0 ? stride.x : -stride.x;
        int stepY = kY >= 0 ? stride.y : -stride.y;

        int startX = _dragStartCell.x;
        int endX = _dragStartCell.x + kX * stride.x;

        int startY = _dragStartCell.y;
        int endY = _dragStartCell.y + kY * stride.y;

        _indicatorBuffer.Clear();
        _unaffordableCells.Clear();

        // Ghosts past this many are drawn invalid even where the grid would accept them, so the
        // drag stops "buying" the moment the money runs out. The loop walks outward from
        // _dragStartCell, so what gets clipped is the far end nearest the cursor.
        int budget = AffordableCount();
        bool clipped = false;

        for (int x = startX; stepX > 0 ? x <= endX : x >= endX; x += stepX)
        {
            for (int y = startY; stepY > 0 ? y <= endY : y >= endY; y += stepY)
            {
                Vector2Int cell = new Vector2Int(x, y);

                // Full-footprint check (not just the root cell) so multi-cell objects like
                // foundations can't drag-place with part of their footprint overlapping
                // something — e.g. an adjacent foundation.
                bool valid = _validator.IsValidPlacement(cell, offsets, _currentData);

                GameObject objAtCell = _raycast.RaycastCellCenter(cell);
                if (objAtCell != null)
                {
                    var bd = objAtCell.GetComponent<BuildingData>();
                    if (bd != null && bd.Data.ClearsGridAfterPlacement)
                        valid = false;
                }

                if (!_currentData.ignorePlacementRules)
                {
                    if (_currentData.isStackable)
                    {
                        if (!_grid.CanStack(cell, _currentData))
                            valid = false;
                    }
                    else
                    {
                        if (_grid.IsOccupied(cell))
                            valid = false;
                    }
                }

                _indicatorBuffer.Add(cell);
                foreach (var o in offsets)
                    _indicatorBuffer.Add(cell + o);

                if (valid && _dragCells.Count >= budget)
                {
                    // Placeable, just not payable. Mark the footprint so the cell indicator agrees
                    // with the ghost instead of showing a green square under a red object.
                    valid = false;
                    clipped = true;
                    _unaffordableCells.Add(cell);
                    foreach (var o in offsets)
                        _unaffordableCells.Add(cell + o);
                }

                if (valid)
                {
                    _dragCells.Add(cell);
                    _preview.ShowMultiGhost(cell, true, _currentRotation);
                }
                else
                {
                    _preview.ShowMultiGhost(cell, false, _currentRotation);
                }
            }
        }

        _indicator.ShowCells(_indicatorBuffer, cell => IsFootprintValid(cell) && !_unaffordableCells.Contains(cell));

        int totalCost = _dragCells.Count * _currentData.cost;
        _costUI.ShowCost(totalCost, !clipped, clipped ? "max affordable" : null);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndDragPlacement();
            return;
        }
    }

    protected virtual void EndDragPlacement()
{
        if (_dragCells.Count == 0)
        {
            AudioManager.Play("InvalidPlace");
            _preview.EndSelectionCells();
            _indicator.ClearAll();
            _isDragging = false;
            _costUI.Hide();
            return;
        }

        int totalCost = _dragCells.Count * _currentData.cost;
        if (!_money.CanAfford(totalCost))
        {
            AudioManager.Play("InvalidPlace");

            foreach (var cell in _dragCells)
                _preview.ShowMultiGhost(cell, false, _currentRotation);

            _preview.EndSelectionCells();
            _indicator.ClearAll();
            _isDragging = false;
            _dragCells.Clear();
            _costUI.Hide();
            return;
        }

        AudioManager.Play("ValidPlace");

        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        _fsm.History.Push(
            new DragPlaceCommand(
                _grid,
                _finalizer,
                new List<Vector2Int>(_dragCells),
                offsets,
                _currentData,
                _currentRotation,
                _money)
        );

        _preview.EndSelectionCells();
        _indicator.ClearAll();
        _isDragging = false;
        _placeRequested = false;
        _dragCells.Clear();
        _unaffordableCells.Clear();
        _costUI.Hide();
    }

    // ---------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------
    private List<Vector2Int> BuildFootprintBuffered(Vector2Int root, Vector2Int[] offsets)
    {
        _footprintBuffer.Clear();

        foreach (var o in offsets)
            _footprintBuffer.Add(root + o);

        return _footprintBuffer;
    }

    private Vector2Int GetStride(Vector2Int[] offsets)
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var o in offsets)
        {
            if (o.x < minX) minX = o.x;
            if (o.x > maxX) maxX = o.x;
            if (o.y < minY) minY = o.y;
            if (o.y > maxY) maxY = o.y;
        }

        int width = (maxX - minX) + 1;
        int height = (maxY - minY) + 1;

        return new Vector2Int(width, height);
    }

    /// <summary>
    /// How many copies of the current item the player can still pay for outright.
    ///
    /// This is the drag preview's budget: a drag never spends past it, so the release can't be
    /// refused wholesale for being one segment too long — you get the segments you could afford
    /// and the rest were never green in the first place. Free items (cost 0) are unlimited;
    /// dividing by zero would otherwise throw here rather than anywhere useful.
    /// </summary>
    protected int AffordableCount()
    {
        int unit = _currentData != null ? _currentData.cost : 0;
        if (unit <= 0) return int.MaxValue;
        return Mathf.Max(0, _money.CurrentCapital / unit);
    }

    protected bool IsFootprintValid(Vector2Int root)
    {
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            if (!_validator.IsCellValid(cell, _currentData))
                return false;
        }
        return true;
    }

    // ---------------------------------------------------------
    // DOOR / WALL REPLACEMENT HELPERS
    // ---------------------------------------------------------

    /// <summary>
    /// Returns true if any cell in the given footprint contains an object
    /// that this data item would replace (wall↔door mutual replacement).
    /// </summary>
    private bool HasReplaceableTarget(Vector2Int root, Vector2Int[] offsets, ObjDataSO data)
    {
        foreach (var o in offsets)
        {
            var objs = _grid.GetObjectsInCell(root + o);
            if (objs == null) continue;
            foreach (var entry in objs)
            {
                if (entry.instance == null || !entry.instance.activeSelf || entry.data == null) continue;
                if (data.replacesWalls       && entry.data.canBeReplacedByDoor) return true;
                if (data.canBeReplacedByDoor && entry.data.replacesWalls)       return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Quality-of-life snap for MULTI-cell objects (foundations etc.): if the footprint doesn't
    /// fit with the hovered cell as its (0,0) origin, try shifting it so the hovered cell maps to
    /// each footprint cell, and use the first position that's valid. This lets the player drop the
    /// object by pointing at ANY of its cells — e.g. a foundation into the empty hole left by a
    /// deleted one — instead of hunting for the exact corner.
    ///
    /// It only ever activates when the default origin placement is INVALID, so normal open-area
    /// placement behaviour is unchanged.
    /// </summary>
    private Vector2Int SnapFootprintToHover(Vector2Int hoverCell, Vector2Int[] offsets, ObjDataSO data)
    {
        if (offsets == null || offsets.Length <= 1) return hoverCell;          // single-cell: nothing to snap
        if (_validator.IsValidPlacement(hoverCell, offsets, data)) return hoverCell; // already fits

        foreach (var o in offsets)
        {
            Vector2Int candidate = hoverCell - o;
            if (candidate == hoverCell) continue;                             // (0,0) already tested
            if (_validator.IsValidPlacement(candidate, offsets, data))
                return candidate;
        }
        return hoverCell;                                                     // nothing fits — preview invalid
    }

    /// <summary>
    /// If the current hover cell has no replaceable target, try the four
    /// cardinal neighbours and return the first one that does. This fixes
    /// the common case where the raycast lands on the face-adjacent cell
    /// instead of the wall/door cell itself.
    /// </summary>
    private Vector2Int SnapToReplaceTarget(Vector2Int root, Vector2Int[] offsets, ObjDataSO data)
    {
        // Only snap when the hovered cell genuinely CAN'T take the door as-is — mirrors
        // SnapFootprintToHover's guard ("only activates when the default origin is INVALID").
        // Without this, a door hovered over any open, perfectly valid cell would get silently
        // kidnapped onto an unrelated wall the moment ANY of its 4 neighbours had one — the
        // "door won't place where I click" / "shoved left/right/front/back" bug.
        if (_validator.IsValidPlacement(root, offsets, data)) return root;

        if (HasReplaceableTarget(root, offsets, data)) return root;

        Vector2Int[] dirs = { new(0,1), new(0,-1), new(1,0), new(-1,0) };
        foreach (var d in dirs)
        {
            Vector2Int candidate = root + d;
            if (HasReplaceableTarget(candidate, offsets, data))
                return candidate;
        }
        return root;
    }

    /// <summary>
    /// Swaps every renderer on objects being replaced to the GhostReplacerOrange
    /// material, storing originals so ClearReplacementHighlights can restore them.
    /// </summary>
    private void HighlightReplacementTargets(Vector2Int root, Vector2Int[] offsets, ObjDataSO data)
    {
        if (_ghostReplaceOrangeMat == null)
            _ghostReplaceOrangeMat = Resources.Load<Material>("Materials/GhostReplacerOrange");
        if (_ghostReplaceOrangeMat == null) return;

        var seen = new HashSet<GameObject>();
        foreach (var o in offsets)
        {
            var objs = _grid.GetObjectsInCell(root + o);
            if (objs == null) continue;
            foreach (var entry in objs)
            {
                if (entry.instance == null || !entry.instance.activeSelf || entry.data == null) continue;
                bool isTarget = (data.replacesWalls       && entry.data.canBeReplacedByDoor)
                             || (data.canBeReplacedByDoor && entry.data.replacesWalls);
                if (!isTarget || !seen.Add(entry.instance)) continue;

                foreach (var r in entry.instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || _replacementOriginalMaterials.ContainsKey(r)) continue;
                    _replacementOriginalMaterials[r] = r.sharedMaterials;

                    var ghost = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < ghost.Length; i++) ghost[i] = _ghostReplaceOrangeMat;
                    r.sharedMaterials = ghost;
                }
            }
        }
    }

    private void ClearReplacementHighlights()
    {
        foreach (var kvp in _replacementOriginalMaterials)
            if (kvp.Key != null) kvp.Key.sharedMaterials = kvp.Value;
        _replacementOriginalMaterials.Clear();
    }

    public void SetBuildData(ObjDataSO data)
    {
        _currentData = data;
    }
}
