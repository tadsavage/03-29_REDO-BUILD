---
description: Working on the warehouse racking / aisle-initialization system — rack collections, chevrons, aisle setup, location naming, or ghost/preview racks. Use whenever the task mentions racks, racking, chevrons, aisles, bays, pick paths, rack collections, or files under Assets/_Project/Scripts/Racking/.
---

# Racking / Aisle Initialization System

The player places rack prefabs in Build mode → the system auto-groups adjacent racks into
**collections** → spawns **chevrons** the player uses to set aisle direction & open setup →
on submit, racks commit (un-ghost + get location labels) and chevrons are deleted.

**Branch this was built on:** `PreRack_2ndAttempt` (a rewrite of an earlier corridor-based
attempt — do NOT resurrect `CorridorDetector`/`AislePad`/`GhostRack` from the old system).

## The workflow (player's POV)
1. Place racks (build mode, click or drag) → they stay **orange-transparent ghost** placeholders.
2. Adjacent same-direction racks auto-group into ONE `RackCollection`.
3. 4 chevrons spawn: both sides × both ends of the run, one cell beyond the first/last bay.
4. Right-click a chevron → flips that whole **side team** (both chevrons on that side) to set travel direction.
5. Click a chevron → its **side team turns green** = "armed" for this collection (one green side per collection).
6. Double-click a chevron → opens `RackSetupUI` (aisle number + level Pick/Reserve designations).
7. Submit → racks un-ghost (real materials back) + get **AA-BB-LP** location labels; chevrons deleted; collection marked Initialized.
8. **Add more racks ON TOP of a finalized (live) rack** → NO chevrons/UI/ghost; it commits instantly as the next LEVEL of the same bay (see "Vertical stacking" below).

## Location name format — `AA-BB-LP` (Aisle-Bay-Level-**Position**)
- `AA` aisle (01-99), `BB` bay, `L` level char, `P` position (column) 0/1.
- Level char: index `0,1` → numeric `"0"/"1"` (pickable); index `2+` → `"A","B","C"…` (reserve). `ConvertLevelToChar`'s reserve branch is `'A' + (levelIndex-2)` — so passing "Reserve" for level 1 yields `@` (ASCII 64). Levels 0-1 must use the "Pick" designation. `PICK_LEVELS = 2` is the boundary.
- **Position is a VERTICAL COLUMN** — a whole stack shares one position number; only the level climbs. Ground positions ascend in the chevron/travel direction; stacked levels inherit each column's position from the rack directly below.

## File map (all in `Assets/_Project/Scripts/Racking/` unless noted)
- `RackPlacedEvent.cs` — static event fired by `PlaceCommand`/`DragPlaceCommand` for category=="Racking".
- `RackGridUtil.cs` — grid-cell helpers: `RotationStep`, `SameAxis`, `GetCells` (world→cell root + footprint offsets).
- `RackCollectionDetector.cs` — grid-cell adjacency grouping + merge; applies ghost on place; handles delete cleanup.
- `RackCollection.cs` — MonoBehaviour holding a collection's racks; `AddRack`/`RemoveRack`/`Initialize`.
- `ChevronSpawner.cs` — computes 4 chevron positions from cell extent; facing; side-teams; green material.
- `ChevronController.cs` — per-chevron click handling: side-flip, green selection, double-click→setup.
- `ChevronGroup.cs` — one selected (green) side-team per collection.
- `RackGhost.cs` — caches real materials, swaps to ghost, `RestoreReal()` on commit. Skips TMP labels.
- `LocationNameGenerator.cs` — `AA-BB-LP` names; `GenerateBayNumbers(count, isEvenSide)` is `public` (even side 02,04,06…; odd 01,03,05…); `GetLocationName(...)`.
- `AisleInitializer.cs` — on submit `CommitAndLabelAisle`: split each collection into GROUND racks (bays) vs UPPER racks (levels), label via **world-geometry** (never group names), delete chevrons, mark initialized. Also `TryCommitStackedRack` (public) for on-top placement.
- `RackingSystemManager.cs` — scene orchestrator; Awake adds the components, feeds them the `PlacementGrid`, sprite, materials; auto-creates the `RackSetupUI` UIDocument.
- `UI_UX/RackSetup/RackSetupUI.{cs,uxml,uss}` — the aisle-config modal (aisle # + level Pick/Reserve). Blueprint bg auto-loads from `Assets/_Project/Resources/RackBlueprint.png`.
- `PlacedObject.cs` (Core/FSM) — carries `isRackLive` + `rackAisle`/`rackBay`/`rackLevelIndex` (−1 = not in an aisle) so stacked racks can inherit identity.
- `PreviewController.cs` (Core/FSM/Preview) — owns the ghost material; exposes `GhostMaterial`.

## Rack footprints (from the assets)
- **Full Bay** racks = footprint **2×1** (customShape) — a single one already has a direction.
- **Half Bay** racks = footprint **1×1** (square) — a single one has NO direction; run axis must come from the collection's overall shape, not one rack.

## Load-bearing conventions (get these wrong and it breaks subtly)
- **Adjacency is GRID-CELL, not world-distance.** The OLD world-space `transform.right` dot-product made every rack its own collection — never reintroduce it.
- **Merge ONLY racks that extend a row along its run axis; side-by-side parallel rows stay SEPARATE collections** (they become back-to-back aisles). `IsAdjacentToCollection`: gather collection cells → bbox → if point/square, merge on touch (direction not set yet); else run axis = longer bbox span, and a new rack merges only if it TOUCHES the collection AND its perpendicular range OVERLAPS the row's line. A rack beside the row (perp doesn't overlap) does NOT merge.
- **Chevrons only on OPEN sides.** A side (perpMin-1 / perpMax+1) is blocked if any cell along that side line holds a "Racking" object (`IsSideBlocked` via `grid.GetObjectsInCell`). Blocked side → 0 chevrons; open side → 2 (start+end). So back-to-back rows get 2 chevrons on each OUTER face, not 8 between them.
- **RefreshAll, not per-collection.** Placing/removing a rack can block/open a NEIGHBOUR's side, so `ChevronSpawner` refreshes EVERY uninitialized collection on any change (`OnCollectionCreated/Added/Merged/Removed` → `RefreshAll`). Chevrons are keyed per-collection by slot (`Neg_Start/Neg_End/Pos_Start/Pos_End`) and reused across refreshes so flip/green state survives.
- **Aisle numbers are unique** via static `AisleRegistry` — `RackSetupUI.HandleSubmit` rejects a used number (blocks submit, keeps modal open); `AisleInitializer` calls `AisleRegistry.Register` on successful commit. (`ShowValidationError` is still just a Debug.LogWarning — no on-screen toast yet.)
- **Chevron placement:** union all collection cells → bounding box → run axis = longer span → chevrons at `{alongMin,alongMax} × {perpMin-1,perpMax+1}`. Recomputed on every add/merge/partial-delete so they track the true ends.
- **Chevron facing:** the flat "Free Flat Arrow" sprite lies along world **+Z** after `Euler(90,0,0)`. To point down the run, `yaw = Vector3.SignedAngle(Vector3.forward, runWorldDir, up)` — reference is `Vector3.forward`, NOT `Vector3.right`. (Reference-axis mistakes point chevrons ACROSS the aisle.)
- **All 4 chevrons start pointing the SAME way;** right-click flips a side-team (Left = Start_Left+End_Left, Right = Start_Right+End_Right), sides independent.
- **Green = the clicked chevron's whole side-team;** clears the other side. Keyed by side ("neg"/"pos") in `ChevronGroup.SelectSideKey` so it survives chevron refreshes.
- **Chevrons are children of the collection GameObject** → destroyed with it (prevents orphans). Racks are NOT children of the collection.
- **Ghost lifecycle:** placed racks stay ghosted (RackGhost + PreviewController.GhostMaterial) until `AisleInitializer` submit un-ghosts them. Commit un-ghosts IN PLACE — do not destroy+re-instantiate (that lost grid registration).
- **`BuildingHighlighter` skips ghosted racks.** It caches a rack's REAL materials in `Awake` (before RackGhost swaps in the ghost), so on Move/Delete hover its restore would reveal the real material and undo the ghost. `ApplyHighlight` early-returns when the object has a ghosted `RackGhost`. Don't remove that guard.
- **Chevron collider:** Center (0,0,0), Size (2,2,0.5), not trigger. Chevron transform scale 0.7 (30% smaller).
- **Delete cleanup:** `RackCollectionDetector` subscribes to `GameEvents.Build.OnObjectDeleted` (via `EventManager.Instance.Subscribe<PlacedObject>`, namespace `GameCore.Events`). On a "Racking" delete: remove from collection; empty → destroy collection (chevrons die with it); else reposition chevrons.

## Labeling, setup UI & stacking (2026-07-01 — the load-bearing stuff for names)
- **Rack prefab labels = 4 TMP groups.** Orange prefabs name them `LabelFront.L/.R`, `LabelRear.L/.R` (each with one `TMP_Label` child); **NO parent `LabelFront`/`LabelRear` object exists** — old `transform.Find("LabelFront")` was a silent no-op. `Rack-FullYellow48` names them `LabelFront.L.002` and MIRRORS the layout. Front labels at local +Z (~0.65), Rear at −Z; `L` high local X (~0.04), `R` low (~−1.34). Front-face normal = `transform.forward`.
- **Labeling is 100% WORLD-GEOMETRY — never key off group names.** `SetRackLabels` iterates `GetComponentsInChildren<TextMeshPro>(true)` and sets each via a resolver; `ConfigureFaces(rackGO, aisleDir)` enables only labels whose world offset·aisleDir > 0 (toggles the TMP GameObject). This is what makes yellow/half/any prefab work. **Do NOT reintroduce name-based label lookups.**
  - Ground position resolver = `TravelPositionResolver` (split labels' projection onto travelDir at the midpoint → earlier column = pos 0). Travel dir = `ground[1].pos − ground[0].pos` (bay N→N+1), so positions ascend with the chevron. Aisle dir = lateral (⊥ run) part of direction to chevron (`GroundAisleDir`).
  - Stacked position resolver = `InheritPositionFromBelow` (nearest below-label in world X/Z → same column number, rotation-proof). Aisle dir = below rack's active-label face normal (`BelowAisleDir`).
- **Vertical stacking = append level, not new aisle.** `RackCollectionDetector.HandleRackPlaced` calls `AisleInitializer.TryCommitStackedRack` FIRST; if the rack sits on a live rack (`FindLiveRackBelow`, height-aware via grid stack) it commits instantly — no ghost/chevron/UI — inheriting aisle+bay, level = below+1. `RackPlacedEvent` fires BEFORE the rack is added to the grid stack, so `GetObjectsInCell` returns the rack below. Grid cell entries are the struct `PlacementGrid.PlacedObject` (`.instance`/`.data`), distinct from the MonoBehaviour.
- **Second-side aisle join (the ONE unique-aisle exception).** Placing a fresh ground rack FACING an already-finalized aisle (within 5 cells, that aisle's labeled side pointing back at it, nothing racking between) commits it LIVE as that aisle's OTHER side — same aisle #, opposite bay parity (`facingBay ∓ 1`, paired across), no ghost/chevron/UI. `RackCollectionDetector.HandleRackPlaced` → `AisleInitializer.TryCommitSecondSide` (runs after the stack check, before ghosting). Positions come from the aisle's TRAVEL direction (`TravelDirFromRack`→`TravelPositionResolver`), NOT cross-aisle nearest-XZ inheritance (the facing row faces the opposite way, so its columns don't overlap — that produced all-pos-0). `AisleGroundBayExists` blocks a duplicate/third row.
- **Pick/Reserve is honored via `LocationNameGenerator.LevelChar(levelIndex, designations)`:** pick = numeric index; reserve = letter lettered bottom-up among reserves (first reserve = "A", so reserve-at-level-0 = "A", never "@"/"?"). `AisleRegistry.Register(aisle, designations)` stores the length-6 scheme per aisle; every commit path (ground, stacked, second-side, post-init stack) looks it up so a post-init stack uses the same scheme.
- **One-shot init of a pre-built multi-level structure is height-aware.** `CommitAndLabelAisle` splits each collection into GROUND racks (lowest per column, via `WorldToCell`+Y — stacked racks share a cell) vs UPPER; only ground racks are numbered as bays, upper racks committed bottom-up through `TryCommitStackedRack`. Without this, all levels count as bays and the numbering "wraps" bottom→top.
- **RackSetupUI reliability (all three bit us):** (1) UXML root MUST declare `xmlns:xsi` if it uses `xsi:` attributes, else the whole file parses to an EMPTY VisualTreeAsset (panel renders nothing — Unity only warns). (2) The auto-created UIDocument must have `visualTreeAsset` set BEFORE its first enable (create GO inactive → set asset → SetActive), or the tree clones empty. (3) Toggle modal visibility via the overlay's **`display` style**, NOT `GameObject.SetActive` — an inactive UIDocument sharing a PanelSettings keeps rendering.

## Known gaps / deferred (don't assume these work)
- **Undo-of-delete won't re-detect** — `DeleteCommand.Undo` fires `Build.OnObjectPlaced`, not `RackPlacedEvent`, so an undone rack won't rejoin/recreate its collection+chevrons.
- **Save/load while ghosted** — ghosted racks save as plain racks and reload without ghost/collection/chevron state.
- **Bay naming counts stacked racks** — FIXED: `CommitAndLabelAisle` numbers only ground racks as bays and treats stacked racks as levels (see "One-shot init" above).
- **No warehouse-DB write on submit** — only names + labels are generated; wire a real location/inventory DB when one exists.
- **Parallel/back-to-back aisles** — now handled: parallel rows stay SEPARATE collections, each chevroned only on its open outer side. NOT yet done: linking the two facing rows of ONE walkway into a shared aisle number, and the aisle-used **on-screen toast** (uniqueness check works but only Debug.LogWarnings).
- **Adjacency ambiguity for single racks** — if the player places one rack of row A, then one rack of row B *before* extending A, A is still a "point" so B merges into it. The normal drag-a-row-then-drag-the-next workflow avoids this.

## How to verify (bridge is flaky — reconnect and probe)
- Compile check + live state: `Unity_RunCommand` a small `IRunCommand` (class MUST be `internal class CommandScript`). Probe types/state, e.g. count `Object.FindObjectsByType<RackCollection>()`, check `RackGhost.IsGhosted`.
- Console: `Unity_ReadConsole` Types=["Error"] (the `mcp__unity-mcp__*` one works; the other relays' console readers throw).
- The MCP bridge **drops on every recompile** — expect to reconnect; relaunch the Advanced Unity MCP relay if needed (`...\CodeMaestro\UnityMcpRelay\launch.bat`).
- Game-camera capture fails (URP); use Scene View capture instead.

## Cross-references
- Deep session notes: memory files `racking-setup-ui-stacking-naming-2026-07-01` (setup UI + stacking + geometry labeling), `racking-collection-chevron-rewrite-2026-06-30`, `racking-aisle-system-2026-06-29`.
- Main project doc: the "Racking/Aisle Initialization System" section in `CLAUDE.md`.
