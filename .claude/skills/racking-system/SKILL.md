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
7. Submit → racks un-ghost (real materials back) + get AA-BB-LC location labels; chevrons deleted; collection marked Initialized.

## File map (all in `Assets/_Project/Scripts/Racking/` unless noted)
- `RackPlacedEvent.cs` — static event fired by `PlaceCommand`/`DragPlaceCommand` for category=="Racking".
- `RackGridUtil.cs` — grid-cell helpers: `RotationStep`, `SameAxis`, `GetCells` (world→cell root + footprint offsets).
- `RackCollectionDetector.cs` — grid-cell adjacency grouping + merge; applies ghost on place; handles delete cleanup.
- `RackCollection.cs` — MonoBehaviour holding a collection's racks; `AddRack`/`RemoveRack`/`Initialize`.
- `ChevronSpawner.cs` — computes 4 chevron positions from cell extent; facing; side-teams; green material.
- `ChevronController.cs` — per-chevron click handling: side-flip, green selection, double-click→setup.
- `ChevronGroup.cs` — one selected (green) side-team per collection.
- `RackGhost.cs` — caches real materials, swaps to ghost, `RestoreReal()` on commit. Skips TMP labels.
- `LocationNameGenerator.cs` — AA-BB-LC name generation.
- `AisleInitializer.cs` — on setup submit: `CommitRealRacks` (un-ghost in place + labels), delete chevrons, mark initialized.
- `RackingSystemManager.cs` — scene orchestrator; Awake adds the components, feeds them the `PlacementGrid`, sprite, materials.
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

## Known gaps / deferred (don't assume these work)
- **Undo-of-delete won't re-detect** — `DeleteCommand.Undo` fires `Build.OnObjectPlaced`, not `RackPlacedEvent`, so an undone rack won't rejoin/recreate its collection+chevrons.
- **Save/load while ghosted** — ghosted racks save as plain racks and reload without ghost/collection/chevron state.
- **Bay naming counts stacked racks** — `LocationNameGenerator` uses `collection.Racks.Count`, which includes vertically stacked bays → over-counts bays on multi-level racks.
- **No warehouse-DB write on submit** — only names + labels are generated; wire a real location/inventory DB when one exists.
- **Parallel/back-to-back aisles** — now handled: parallel rows stay SEPARATE collections, each chevroned only on its open outer side. NOT yet done: linking the two facing rows of ONE walkway into a shared aisle number, and the aisle-used **on-screen toast** (uniqueness check works but only Debug.LogWarnings).
- **Adjacency ambiguity for single racks** — if the player places one rack of row A, then one rack of row B *before* extending A, A is still a "point" so B merges into it. The normal drag-a-row-then-drag-the-next workflow avoids this.

## How to verify (bridge is flaky — reconnect and probe)
- Compile check + live state: `Unity_RunCommand` a small `IRunCommand` (class MUST be `internal class CommandScript`). Probe types/state, e.g. count `Object.FindObjectsByType<RackCollection>()`, check `RackGhost.IsGhosted`.
- Console: `Unity_ReadConsole` Types=["Error"] (the `mcp__unity-mcp__*` one works; the other relays' console readers throw).
- The MCP bridge **drops on every recompile** — expect to reconnect; relaunch the Advanced Unity MCP relay if needed (`...\CodeMaestro\UnityMcpRelay\launch.bat`).
- Game-camera capture fails (URP); use Scene View capture instead.

## Cross-references
- Deep session notes: memory files `racking-collection-chevron-rewrite-2026-06-30`, `racking-aisle-system-2026-06-29`.
- Main project doc: the "Racking/Aisle Initialization System" section in `CLAUDE.md`.
