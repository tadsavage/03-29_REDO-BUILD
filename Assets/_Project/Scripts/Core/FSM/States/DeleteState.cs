using GameCore.Economy;
using GameCore.Build;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Handles selecting and deleting placed objects from the grid.
/// Inherits from PlacementStateBase for standardized service access and event handling.
/// </summary>
public class DeleteState : PlacementStateBase
{
    private readonly RaycastController _raycast;
    private new readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementStateMachine _fsm;
    private readonly CellIndicatorController _indicator;
    private readonly PlacementActions _actions;
    private readonly MoneyService _money;
    private WorldHoverPopupUI _hoverUI;
    private TopBarUI _topBarUI;

    private readonly float _destructionDuration;
    private readonly float _destructionSinkAmount;
    private readonly float _destructionVibrationAmount;
    private readonly float _destructionVibrationSpeed;

    private TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();
    private WorldHoverPopupUI hoverUI => _hoverUI != null ? _hoverUI : _hoverUI = Object.FindAnyObjectByType<WorldHoverPopupUI>();

    // Primary hover target — the object a click will actually delete (the foundation itself
    // when hovering a foundation or one of its floor tiles, otherwise the hit object).
    private BuildingHighlighter _hover;
    // Full set of highlighters currently tinted yellow. For a plain object this is just _hover.
    // For a Foundation/Grounds object it also includes every floor tile riding on its footprint,
    // so hovering the slab OR any tile on top of it highlights the whole unit together.
    private readonly HashSet<BuildingHighlighter> _hoverGroup = new();
    private readonly List<BuildingHighlighter> _dragTargets = new();

    private bool _isDragging;
    private Vector3 _dragStartWorld;

    public override bool IsPlacementState => true;

    public DeleteState(
        RaycastController raycast,
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        PlacementStateMachine fsm,
        CellIndicatorController indicator,
        PlacementActions actions,
        MoneyService money,
        float destructionDuration,
        float destructionSinkAmount,
        float destructionVibrationAmount,
        float destructionVibrationSpeed)
    {
        _raycast = raycast;
        _grid = grid;
        _finalizer = finalizer;
        _fsm = fsm;
        _indicator = indicator;
        _actions = actions;
        _money = money;
        _destructionDuration = destructionDuration;
        _destructionSinkAmount = destructionSinkAmount;
        _destructionVibrationAmount = destructionVibrationAmount;
        _destructionVibrationSpeed = destructionVibrationSpeed;
    }

    public override void OnEnter()
    {
        base.OnEnter(); // Initialize event manager and services

        BuildModeOverride.Instance?.Activate();

        _raycast.EnableRay();
        _indicator.UseDeleteMode();

        _isDragging = false;
        _dragTargets.Clear();
        ClearHover();

        topBarUI?.SetState(GetType().Name);

        _fsm.OnHistoryChanged += OnHistoryChanged;
    }

    public override void OnExit()
    {
        BuildModeOverride.Instance?.Deactivate();

        _raycast.DisableRay();
        _indicator.UseBuildMode();

        ClearHover();
        ClearDragHighlights();

        _fsm.OnHistoryChanged -= OnHistoryChanged;

        base.OnExit(); // Clean up base state
    }

    private void OnHistoryChanged()
    {
        // When Undo/Redo happens, forget any hover/drag state
        ClearHover();
        ClearDragHighlights();
        _indicator.ClearAll();
        _isDragging = false;
    }

    public override void Update()
    {
        _raycast.Tick();

        if (_raycast.IsPointerOverUI)
        {
            ClearHover();
            _indicator.ClearAll();
            hoverUI?.TickHover(false, null, 0, 0, Vector3.zero, null);
            return;
        }

        if (!_raycast.HasHit)
        {
            ClearHover();
            _indicator.ClearAll();
        }

        Vector3 hitPoint = _raycast.HitPoint;
        Vector2Int cell = _raycast.HitCell;
        topBarUI?.SetCell(cell.x, cell.y);

        if (_raycast.HitObject != null)
        {
            var bd = _raycast.HitObject.GetComponent<BuildingData>();
            if (bd != null)
            {
                hoverUI?.TickHover(
                    true,
                    bd.Data.objName,
                    bd.Data.cost,
                    bd.Data.hourlyCost,
                    _raycast.RawHitPoint,
                    Camera.main
                );
            }
            else
            {
                hoverUI?.TickHover(false, null, 0, 0, Vector3.zero, null);
            }
        }
        else
        {
            hoverUI?.TickHover(false, null, 0, 0, Vector3.zero, null);
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragTargets.Clear();
            _dragStartWorld = hitPoint;
        }

        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if ((hitPoint - _dragStartWorld).sqrMagnitude > 0.05f)
            {
                _isDragging = true;
                ClearHover();
                _indicator.ClearAll();
            }
        }

        if (_isDragging)
        {
            UpdateDragDelete(hitPoint);
            return;
        }

        UpdateHoverDelete(cell);

        if (Mouse.current.leftButton.wasReleasedThisFrame && _hover != null)
        {
            var bd = _hover.GetComponent<BuildingData>();
            // Employees can't be deleted (terminate them instead) — guard even though the hover
            // filter already excludes them.
            if (bd == null || bd.GetComponent<EmployeeIdentity>() != null)
            {
                ClearHover();
                return;
            }

            if (IsFoundationData(bd.Data))
            {
                // Deleting on a CUSTOM tile (pedestrian/MHE/ship lane, etc.) reverts just that cell
                // back to the foundation's default tile — the foundation stays. Only a click on the
                // DEFAULT tile falls through to actually delete the foundation (and all its tiles).
                if (TryRevertCustomTile(bd, cell))
                {
                    ClearHover();
                    return;
                }

                // Don't bulldoze a foundation that still has a wall / pallet / prop on it — make the
                // player clear it off first. Deleting a clear foundation takes all its floor tiles too.
                if (!FoundationIsClearToDelete(bd))
                {
                    ClearHover();
                    AudioManager.Play("InvalidPlace");
                    UIToast.Show("Clear everything off the foundation before deleting it");
                    return;
                }
            }

            // IMPORTANT: Clear highlight before deleting/disabling
            ClearHover();

            // Single delete = single command
            _fsm.History.Push(new DeleteCommand(bd.gameObject, _grid, _money, _destructionDuration, _destructionSinkAmount, _destructionVibrationAmount, _destructionVibrationSpeed));

            AudioManager.Play("Delete");
        }
}

    private void UpdateHoverDelete(Vector2Int cell)
    {
        // Only show indicator if we have a valid hit to avoid a permanent square trail
        if (_raycast.HasHit)
            _indicator.ShowCell(cell);
        else
            _indicator.ClearAll();

        BuildingData targetBd = null;

        if (_raycast.HitObject != null)
        {
            var bd = _raycast.HitObject.GetComponentInParent<BuildingData>();
            if (bd != null && bd.Data != null && bd.GetComponent<EmployeeIdentity>() == null)
            {
                if (bd.Data.isFloor)
                {
                    // Redirect to foundation. Yard pads (no group) are ignored.
                    targetBd = FindFoundationInCell(cell);
                }
                else
                {
                    // Non-floor objects (racks, walls) are directly deletable
                    targetBd = bd;
                }
            }
        }

        BuildingHighlighter newHover = targetBd != null ? targetBd.GetComponent<BuildingHighlighter>() : null;

        // Build the highlight group (foundation + all tiles on it)
        HashSet<BuildingHighlighter> newGroup = new HashSet<BuildingHighlighter>();
        if (newHover != null)
        {
            newGroup.Add(newHover);

            // If it's a foundation, add all floor tiles on its footprint to the highlight group
            if (targetBd != null && targetBd.GetComponent<FoundationFloorGroup>() != null && targetBd.Offsets != null)
            {
                var root = targetBd.RootCell;
                foreach (var o in targetBd.Offsets)
                {
                    var footprintObjs = _grid.GetObjectsInCell(root + o);
                    if (footprintObjs == null) continue;

                    foreach (var entry in footprintObjs)
                    {
                        if (entry.instance == null || entry.data == null || !entry.data.isFloor) continue;
                        var tileHighlighter = entry.instance.GetComponent<BuildingHighlighter>();
                        if (tileHighlighter != null) newGroup.Add(tileHighlighter);
                    }
                }
            }
        }

        if (!newGroup.SetEquals(_hoverGroup))
        {
            foreach (var h in _hoverGroup)
                if (h != null) h.HighlightDelete(false);

            foreach (var h in newGroup)
                if (h != null) h.HighlightDelete(true);

            _hoverGroup.Clear();
            foreach (var h in newGroup) _hoverGroup.Add(h);
        }

        _hover = newHover;
    }

    // Returns the BuildingData for the ground (Foundation or Grounds) in the given cell, or null if none.
    private BuildingData FindFoundationInCell(Vector2Int cell)
    {
        var objs = _grid.GetObjectsInCell(cell);
        if (objs == null) return null;
        foreach (var entry in objs)
        {
            if (entry.instance == null) continue;
            string cat = entry.data?.category;
            if (cat == "Foundation" || cat == "Grounds")
                return entry.instance.GetComponent<BuildingData>();
        }
        return null;
    }

    private void ClearHover()
    {
        foreach (var h in _hoverGroup)
            if (h != null) h.HighlightDelete(false);

        _hoverGroup.Clear();
        _hover = null;
    }

    // Returns the highlighter for the top visible floor tile riding on `foundation` in `cell`
    // (the tile a click would revert), or null if there isn't one.
    private BuildingHighlighter FindTopFloorHighlighter(BuildingData foundation, Vector2Int cell)
    {
        var objs = _grid.GetObjectsInCell(cell);
        if (objs == null) return null;
        for (int i = objs.Count - 1; i >= 0; i--)
        {
            var e = objs[i];
            if (e.instance == foundation.gameObject) continue;
            if (e.instance != null && e.instance.activeSelf && e.data != null && e.data.isFloor)
                return e.instance.GetComponent<BuildingHighlighter>();
        }
        return null;
    }

    private static bool IsFoundationData(ObjDataSO d)
        => d != null && (d.category == "Foundation" || d.category == "Grounds");

    private static bool FoundationCoversCell(BuildingData foundation, Vector2Int cell)
    {
        if (foundation?.Offsets == null) return false;
        var root = foundation.RootCell;
        foreach (var o in foundation.Offsets)
            if (root + o == cell) return true;
        return false;
    }

    private enum CellFloorKind
    {
        None,
        Default,
        CustomRevertable,
        CustomNoDefault
    }

    private CellFloorKind ClassifyFoundationCell(BuildingData foundation, Vector2Int cell)
    {
        if (foundation == null || !FoundationCoversCell(foundation, cell))
            return CellFloorKind.None;

        var objs = _grid.GetObjectsInCell(cell);
        if (objs == null) return CellFloorKind.None;

        var floorGroup = foundation.GetComponent<FoundationFloorGroup>();

        GameObject activeCustomFloor = null;
        GameObject defaultFloor = null;

        for (int i = objs.Count - 1; i >= 0; i--)
        {
            var entry = objs[i];
            if (entry.instance == null || entry.data == null || !entry.data.isFloor || IsFoundationData(entry.data))
                continue;
            if (entry.data.id == 200) continue; // yard tile

            bool isOwnedDefault = floorGroup != null && floorGroup.Owns(entry.instance.GetComponent<PlacedObject>());
            if (!isOwnedDefault && foundation.Data != null && foundation.Data.defaultFloorTile != null && entry.data.id == foundation.Data.defaultFloorTile.id)
                isOwnedDefault = true;

            if (isOwnedDefault)
            {
                if (entry.instance.activeSelf)
                    defaultFloor = entry.instance;
                else if (defaultFloor == null)
                    defaultFloor = entry.instance;
            }
            else if (entry.instance.activeSelf)
            {
                activeCustomFloor = entry.instance;
            }
        }

        if (activeCustomFloor != null)
        {
            if (defaultFloor != null)
                return CellFloorKind.CustomRevertable;

            if (floorGroup != null && floorGroup.DefaultTiles != null && foundation.Offsets != null)
            {
                for (int i = 0; i < floorGroup.DefaultTiles.Length && i < foundation.Offsets.Length; i++)
                {
                    if (foundation.RootCell + foundation.Offsets[i] == cell && floorGroup.DefaultTiles[i] != null)
                        return CellFloorKind.CustomRevertable;
                }
            }

            if (foundation.Data != null && foundation.Data.defaultFloorTile != null)
                return CellFloorKind.CustomRevertable;

            return CellFloorKind.CustomNoDefault;
        }

        if (defaultFloor != null && defaultFloor.activeSelf)
            return CellFloorKind.Default;

        if (floorGroup != null && floorGroup.AllDefaultTilesIntact())
            return CellFloorKind.Default;

        return CellFloorKind.Default;
    }

    private bool TryBuildRevertCommand(BuildingData foundation, Vector2Int cell, out ICommand revert)
    {
        revert = null;
        if (foundation == null || !FoundationCoversCell(foundation, cell))
            return false;

        var objs = _grid.GetObjectsInCell(cell);
        if (objs == null) return false;

        var floorGroup = foundation.GetComponent<FoundationFloorGroup>();

        GameObject customFloor = null;
        GameObject defaultFloor = null;

        for (int i = objs.Count - 1; i >= 0; i--)
        {
            var entry = objs[i];
            if (entry.instance == null || entry.data == null || !entry.data.isFloor || IsFoundationData(entry.data))
                continue;
            if (entry.data.id == 200) continue; // yard tile

            bool isOwnedDefault = floorGroup != null && floorGroup.Owns(entry.instance.GetComponent<PlacedObject>());
            if (!isOwnedDefault && foundation.Data != null && foundation.Data.defaultFloorTile != null && entry.data.id == foundation.Data.defaultFloorTile.id)
                isOwnedDefault = true;

            if (isOwnedDefault)
            {
                defaultFloor = entry.instance;
            }
            else if (entry.instance.activeSelf)
            {
                customFloor = entry.instance;
            }
        }

        if (customFloor == null)
            return false;

        if (defaultFloor == null && floorGroup != null && floorGroup.DefaultTiles != null && foundation.Offsets != null)
        {
            for (int i = 0; i < floorGroup.DefaultTiles.Length && i < foundation.Offsets.Length; i++)
            {
                if (foundation.RootCell + foundation.Offsets[i] == cell)
                {
                    var po = floorGroup.DefaultTiles[i];
                    if (po != null) defaultFloor = po.gameObject;
                    break;
                }
            }
        }

        revert = new RevertFloorTileCommand(customFloor, defaultFloor, foundation, cell, _grid, _money);
        return true;
    }

    private bool TryRevertCustomTile(BuildingData bd, Vector2Int cell)
    {
        if (TryBuildRevertCommand(bd, cell, out var revert))
        {
            _fsm.History.Push(revert);
            AudioManager.Play("Delete");
            return true;
        }
        return false;
    }

    // True if any rack (category "Racking") sits anywhere in the drag rectangle.Drives the QoL
    // exception that spares foundations during a rack swipe (see UpdateDragDelete).
    private bool DragCapturesRack(List<Vector2Int> footprint)
    {
        foreach (var cell in footprint)
        {
            var objs = _grid.GetObjectsInCell(cell);
            if (objs == null) continue;
            foreach (var entry in objs)
            {
                if (entry.instance == null || entry.data == null) continue;
                if (entry.data.category == "Racking") return true;
            }
        }
        return false;
    }

    // True if the drag covers any non-default (custom) floor tile sitting on a foundation. When it
    // does, every foundation in the drag is spared (see spareFoundations) — the player is stripping
    // tiles back to default, not demolishing slabs.
    private bool DragCapturesCustomFloor(List<Vector2Int> footprint)
    {
        foreach (var cell in footprint)
        {
            var f = FindFoundationInCell(cell);
            if (f == null) continue;
            var kind = ClassifyFoundationCell(f, cell);
            if (kind == CellFloorKind.CustomRevertable || kind == CellFloorKind.CustomNoDefault)
                return true;
        }
        return false;
    }

    // A foundation may only be deleted when nothing but its own floor tiles sits anywhere in its
    // footprint. A wall / pallet / door / prop blocks the delete so the player has to clear it
    // first — this stops the foundation (and everything on it) from being bulldozed by accident
    // when the player was really aiming at a wall. Checks the WHOLE footprint, since a foundation
    // spans several cells and the blocker may be on a different cell than the one clicked.
    private bool FoundationIsClearToDelete(BuildingData foundation)
    {
        if (foundation == null || foundation.Data == null) return true;
        var offsets = foundation.Offsets;
        if (offsets == null) return true;

        var root = foundation.RootCell;
        foreach (var o in offsets)
        {
            var objs = _grid.GetObjectsInCell(root + o);
            if (objs == null) continue;
            foreach (var entry in objs)
            {
                if (entry.instance == null || !entry.instance.activeSelf || entry.data == null) continue;
                if (entry.instance == foundation.gameObject) continue; // the foundation itself
                if (entry.data.isFloor) continue;                      // its floor tiles ride along
                if (IsFoundationData(entry.data)) continue;            // stacked grounds — ignore
                return false;                                          // wall / pallet / door / prop / etc.
            }
        }
        return true;
    }

    private readonly HashSet<BuildingHighlighter> _lastDragTargets = new();
    // Cells the current drag will revert to their foundation's default tile (custom tile removed but
    // the foundation kept) — parallel to _dragTargets, which are the objects that get fully deleted.
    private readonly List<Vector2Int> _revertCells = new();

    private void UpdateDragDelete(Vector3 dragEndWorld)
    {
        Vector2Int a = _grid.WorldToCell(_dragStartWorld);
        Vector2Int b = _grid.WorldToCell(dragEndWorld);

        List<Vector2Int> footprint = GetRectangleCells(a, b);

        // Rack QoL exception: when a drag-delete captures ANY rack, spare the foundations/grounds
        // under the swipe so you can bulldoze a run of racking without having to rebuild the
        // foundation beneath it. If the drag caught NO racks, foundations delete per the normal
        // rules (they become ordinary drag targets like anything else).
        bool dragHasRack = DragCapturesRack(footprint);

        // If the drag captures ANY non-default (custom) floor tile, protect EVERY foundation under
        // the swipe — exactly like the rack exception (dragHasRack): the player is stripping tiles,
        // not demolishing slabs. Custom tiles still revert to their default; no foundation is deleted.
        bool dragHasCustomTile = DragCapturesCustomFloor(footprint);
        bool spareFoundations = dragHasRack || dragHasCustomTile;

        // 1. Collect new targets using Grid data instead of Physics Raycasts.
        // newDeleteTargets = the actual objects a DeleteCommand will be built for on release.
        // newHighlightTargets = everything that should tint yellow, which additionally includes
        // every floor tile riding on a targeted foundation's footprint (visual only — the
        // foundation's own DeleteCommand already removes its floor tiles, so they're never
        // added to newDeleteTargets to avoid double-deleting them).
        HashSet<BuildingHighlighter> newDeleteTargets = new HashSet<BuildingHighlighter>();
        HashSet<BuildingHighlighter> newHighlightTargets = new HashSet<BuildingHighlighter>();
        _revertCells.Clear();

        foreach (var cell in footprint)
        {
            var objs = _grid.GetObjectsInCell(cell);
            if (objs != null && objs.Count > 0)
            {
                // Walk from top down; floor tiles cannot be deleted so skip them — EXCEPT
                // Foundations/Grounds, which are also marked isFloor (they're the supporting
                // surface) but must still be selectable as delete targets in their own right.
                for (int i = objs.Count - 1; i >= 0; i--)
                {
                    var entry = objs[i];
                    if (entry.data != null && entry.data.isFloor && !IsFoundationData(entry.data)) continue;
                    if (entry.instance != null)
                    {
                        if (entry.instance.GetComponent<EmployeeIdentity>() != null) continue; // employees: terminate, not delete
                        var ebd = entry.instance.GetComponent<BuildingData>();

                        if (ebd != null && IsFoundationData(ebd.Data))
                        {
                            // Always revert a custom tile back to default (the whole point of the drag).
                            if (ClassifyFoundationCell(ebd, cell) == CellFloorKind.CustomRevertable)
                                _revertCells.Add(cell);
                            // Only a bare/default foundation cell in a drag with NO custom tiles (and no
                            // racks) is an actual delete target; otherwise the foundation is spared.
                            else if (!spareFoundations)
                            {
                                var hf = entry.instance.GetComponent<BuildingHighlighter>();
                                if (hf != null)
                                {
                                    newDeleteTargets.Add(hf);
                                    newHighlightTargets.Add(hf);
                                }

                                // Highlight every floor tile riding on this foundation's footprint too,
                                // so the whole slab (foundation + tiles) tints yellow together.
                                if (ebd.Offsets != null)
                                {
                                    var root = ebd.RootCell;
                                    foreach (var o in ebd.Offsets)
                                    {
                                        var footprintObjs = _grid.GetObjectsInCell(root + o);
                                        if (footprintObjs == null) continue;

                                        foreach (var fe in footprintObjs)
                                        {
                                            if (fe.instance == null || fe.data == null || !fe.data.isFloor || IsFoundationData(fe.data)) continue;
                                            var th = fe.instance.GetComponent<BuildingHighlighter>();
                                            if (th != null) newHighlightTargets.Add(th);
                                        }
                                    }
                                }
                            }
                            break;
                        }

                        var h = entry.instance.GetComponent<BuildingHighlighter>();
                        if (h != null)
                        {
                            newDeleteTargets.Add(h);
                            newHighlightTargets.Add(h);
                            break;
                        }
                    }
                }
            }
        }

        // 2. Only update highlights if the selection changed
        foreach (var h in _lastDragTargets)
        {
            if (!newHighlightTargets.Contains(h))
            {
                if (h != null) h.HighlightDelete(false);
            }
        }

        foreach (var h in newHighlightTargets)
        {
            if (!_lastDragTargets.Contains(h))
            {
                if (h != null) h.HighlightDelete(true);
            }
        }

        _lastDragTargets.Clear();
        foreach (var h in newHighlightTargets) _lastDragTargets.Add(h);

        _dragTargets.Clear();
        _dragTargets.AddRange(newDeleteTargets);

        _indicator.ShowCells(footprint, cell => true);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            _fsm.History.BeginBatch();

            // Reverts first: swap each flagged cell's custom tile back to the foundation's default
            // (the foundation stays). Rebuilt here so the command reflects final grid state.
            foreach (var cell in _revertCells)
            {
                var foundation = FindFoundationInCell(cell);
                if (foundation != null && TryBuildRevertCommand(foundation, cell, out var revert))
                    _fsm.History.AddToBatch(revert);
            }

            // Clear every highlighted object first (includes floor-tile extras that ride along
            // visually but aren't deleted directly — the foundation's DeleteCommand handles them).
            foreach (var h in _lastDragTargets)
                if (h != null) h.HighlightDelete(false);

            foreach (var h in _dragTargets)
            {
                if (h != null)
                {
                    var bd = h.GetComponent<BuildingData>();
                    if (bd != null)
                    {
                        _fsm.History.AddToBatch(new DeleteCommand(bd.gameObject, _grid, _money, _destructionDuration, _destructionSinkAmount, _destructionVibrationAmount, _destructionVibrationSpeed));
                    }
                }
            }
            _fsm.History.EndBatch();
            AudioManager.Play("Delete");

            _dragTargets.Clear();
            _lastDragTargets.Clear();
            _revertCells.Clear();
            _isDragging = false;
            _indicator.ClearAll();
        }
    }

    private void ClearDragHighlights()
    {
        foreach (var h in _lastDragTargets)
        {
            if (h)
                h.HighlightDelete(false);
        }

        _dragTargets.Clear();
        _lastDragTargets.Clear();
    }

    private List<Vector2Int> GetRectangleCells(Vector2Int a, Vector2Int b)
    {
        List<Vector2Int> cells = new();

        int minX = Mathf.Min(a.x, b.x);
        int maxX = Mathf.Max(a.x, b.x);
        int minY = Mathf.Min(a.y, b.y);
        int maxY = Mathf.Max(a.y, b.y);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                cells.Add(new Vector2Int(x, y));

        return cells;
    }
}
