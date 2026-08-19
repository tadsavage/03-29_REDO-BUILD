# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 6 (URP) warehouse/facility builder simulation game. Players place objects on a 2D grid in a 3D world, with a simulated economy (money + time) and full save/load support.

**Unity Version:** 6 (project name: `03-29-26 REDO`)
**Render Pipeline:** Universal Render Pipeline (URP) 17.3.0
**Input System:** Unity Input System 1.18.0

## Development Commands

All development is done through the **Unity Editor** — open the project in Unity 6, then:

- **Play/Test:** Press Play in the Editor
- **Build:** File → Build Settings → Build
- **Run Tests:** Window → General → Test Runner (uses `com.unity.test-framework`)
- **Editor Tools (custom menus):**
  - `Tools/ObjData/Auto‑Assign Unique IDs` — assigns unique IDs to all `ObjDataSO` assets after creating new ones
  - `Tools/Checklist/Open Checklist JSON` — opens the dev checklist file
  - Window → `Build Phase Checklist` — shows the in-Editor checklist window

**Quicksave/Quickload (in Play Mode):**
- `F5` — quicksave to `autosave`
- `F9` — quickload from `autosave`
- `F6` — open the slot-based save/load window

**UI hotkeys (in Play Mode, number row — rebound from F1–F4 on 2026-06-26 to make room for 5):**
- `1` — Dev Console (`ToolsWindowController` — currently non-functional, see BUGS & ISSUES)
- `2` — Hiring Board
- `3` — Employee Roster
- `4` — Employee List
- `5` — Shift Manager (first draft, UI-only — see Economy & Financial Reporting System below)

## Architecture

### Core Systems

**Placement FSM** (`Assets/1. Scripts/1. FSM/`)

The heart of the game. `PlacementStateMachine` drives a stack-based FSM with five states:
- `IdleState` — hover inspection, popup display
- `RaycastPlacementState` — grid hover without placement
- `BuildState` — place new objects (click or click-drag for multi-place)
- `MoveState` — pick up and re-place existing objects
- `DeleteState` — remove objects with destruction animation

`PlacementController` translates UI button events (from `BuildMenuUI`) into FSM transitions. The FSM owns a `CommandHistory` for undo/redo (Ctrl+Z / Ctrl+Y, also available via UI buttons). ESC or RMB pops the state stack back to the previous state.

**Key rule:** Only `IdleState` drives the hover popup (`WorldHoverPopupUI`). All other states must not show it.

**PlacementGrid** (`Assets/1. Scripts/1. FSM/7. Math/PlacementGrid.cs`)

2D array of cell stacks (`List<PlacedObject>[,]`). Manages object stacking, height tracking, and world↔cell coordinate conversion. Default cell size is `1.33f` units. Object ordering within a cell is: Foundations/Grounds → Floors → normal objects.

After a save is loaded or objects are manually placed in the Editor, call `grid.RebuildFromRegistry()` to sync the internal grid state with `PlacedObjectRegistry`.

**Command Pattern** (`Assets/1. Scripts/1. FSM/6. Commands/`)

Every placement/delete/move action is wrapped in an `ICommand` and pushed to `CommandHistory`. Multi-cell drag-place uses `DragPlaceCommand`; batching uses `CommandBatch`. Commands execute immediately on `Push()` and support `Undo()`/`Redo()`.

**GameContext** (`Assets/1. Scripts/1. FSM/7. Math/TimeAndMoney/GameContext.cs`)

Scene singleton (`[DefaultExecutionOrder(-100)]`) that owns and initializes `MoneyService` and `SimulationTimeService`. Wires hourly cost deduction and daily spending resets. Reads `PlayerPrefs` for difficulty and save-slot routing in `Start()`, then bakes the NavMesh synchronously.

Difficulty levels (set via Main Menu, stored in `PlayerPrefs("Difficulty")`):
- `0` — Clerk (easy): $120k starting capital, 100% sell-back
- `1` — Supervisor (normal): $100k starting capital, 75% sell-back
- `2` — Manager (hard): $80k starting capital, 50% sell-back

**⚠️ Difficulty balance needs review** — numbers are placeholder, not playtested.

**MoneyService / SimulationTimeService**

Plain C# classes (not MonoBehaviours) held by `GameContext`, registered with `ServiceLocator`. `SimulationTimeService` runs at 1 real-second = 1 in-game-minute. `MoneyService` fires `OnMoneyChanged` on any balance change; hourly costs are applied via event subscription. `MoneyService` now takes a `sellBackRate` (0–1 float) that scales all deletion refunds — `DeleteCommand` uses `_money.SellBackRate` to compute the adjusted refund.

`MoneyService`, `EconomyService`, and `PayrollService` together drive the full economy — see [Economy & Financial Reporting System](#economy--financial-reporting-system) below for the complete picture (GL_Line cost tracking, wage tiers, overtime, the 4 TopBar reporting panels).

### Data Model

**ObjDataSO** (`Assets/1. Scripts/3. ScriptableObjects/SO Scripts/ObjDataSO.cs`)

ScriptableObject describing a placeable item: `id` (unique int), `prefab`, `cost`, `hourlyCost`, `category`, `GL_Line`, `footprint` (Vector2Int), optional `customShapeOffsets`, stacking rules, pathfinding flags, and special behavior flags (`isFloor`, `ClearsGridAfterPlacement`, `ignorePlacementRules`).

`GL_Line` is the General Ledger line for hourly-cost reporting — see [Economy & Financial Reporting System](#economy--financial-reporting-system) below. It's a plain hand-editable string per asset (no runtime default-computation); the `Tools/ObjData/Auto-Assign GL Lines` one-time batch (run via `Unity_RunCommand`, not an Editor menu item yet) populated it across all 105 assets from `category` with named overrides.

`GetFootprintOffsets(rotation)` returns the rotated cell offsets for a placement. The convention is: pass `-currentRotation` to get footprint offsets (negated because world rotation and grid rotation are inversed).

**ObjDataRegistry** (`Assets/1. Scripts/3. ScriptableObjects/SO Scripts/ObjDataRegistry.cs`)

ScriptableObject list of all `ObjDataSO` assets. Lookup by `id` via `GetByID(int)`. After creating new `ObjDataSO` assets, run `Tools/ObjData/Auto‑Assign Unique IDs` to ensure no ID collisions.

**PlacedObject** (`Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/PlacedObject.cs`)

MonoBehaviour on every placed prefab. Stores `gridX`, `gridY`, `rotation` (in 90° increments), and a reference to its `ObjDataSO`. Automatically registers/unregisters with the static `PlacedObjectRegistry` via `OnEnable`/`OnDisable`/`OnDestroy`. Nested `PlacedObject` children (e.g. items on a pallet) skip registration to avoid being saved at (0,0).

**BuildingData** (`Assets/1. Scripts/1. FSM/8. Data/BuildingData.cs`)

Stores the root cell, rotation (degrees), footprint offsets, and data reference for a placed object. Used by `MoveState` and `PlacementGrid` to know where an object's root and footprint cells are.

### Save System

**PlacedObjectRegistry** (static) — global `HashSet<PlacedObject>` of all active placed objects.

**PlacementSystem** (`Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/PlacementSystem.cs`) — serializes/deserializes the world state to/from JSON via `ObjDataRegistry` IDs. The quicksave (F5/F9) path writes to `Assets/_Saves/autosave.json` using the legacy `SaveSystem` static class.

**SaveManager** (`Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/SaveManager.cs`) — manages up to 8 named save slots. Each slot has a game data JSON file and a thumbnail PNG. Metadata (names, timestamps, file references) is persisted to `Assets/_Saves/metadata.json`.

Save files are stored at `Application.dataPath + "/_Saves/"` (inside the project's `Assets` folder — intentional for this prototype).

#### Dock Product (Pallet) Persistence — 2026-07-09

**Status: COMPLETE** — Pallets on dock now saved/loaded with correct Y positioning for stacking.

**System:** Pallets are saved with absolute world Y coordinate (`SavedObject.worldY`) to restore at correct elevations accounting for stacking.

**Key Classes:**
- **PalletHeightCalculator** — Calculates correct Y positions: ground level = foundation(1.06m) + floor(0.06m) + gap(0.015m) + pallet(0.16m) + cases. Stacked = pallet_below_top + pallet(0.16m) + gap(0.015m) + cases.
- **PalletPlacementHelper** — Utility to calculate Y for a pallet at a grid cell, checking for existing pallets in that cell.
- **InventoryPersistenceService** — Instantiates ChepEmpty prefab for inventory-data pallets, uses PalletHeightCalculator for correct positioning.

**SaveData.SavedObject** now has `float worldY` field to store absolute height.

**Load Path:** PlacementSystem.SpawnFromSave() uses saved worldY if > 0, otherwise calculates from grid.

**Testing:** Place/stack pallets → F5 (save) → Restart editor → F9 (load) → pallets should appear at correct stacking heights. Console logs show instantiation count and positions.

### Audio

**AudioManager** — singleton MonoBehaviour that persists across scenes. Sound effects are defined in a `SoundDefinition` ScriptableObject and played by name: `AudioManager.Play("soundName")`. Music plays probabilistically on an interval with fade-in/fade-out.

### Racking/Aisle Initialization System (`Assets/_Project/Scripts/Racking/`)

> **📎 Skill:** When working on this system, use the **`racking-system`** skill (`.claude/skills/racking-system/`) — it captures the load-bearing conventions (grid-cell adjacency, chevron facing = `Vector3.forward` reference, side-teams, ghost lifecycle, delete cleanup) and the known deferred gaps. Auto-triggers on racking/chevron/aisle tasks, or invoke `/racking-system`.

#### NavMesh rack-avoidance for MHE — hybrid manual + NavMeshAgent (current, 2026-07-26)

Tad asked for reach trucks/dock stockers to be blocked from driving through rack footprints. First attempt (three rounds: NavMesh floor-source exclusion under racks, a custom `DriveAlongPath` NavMesh-*query*-based corner-walker for `ReachTruckOperator`, then the same for `TrailerOffloadController`) was reverted at Tad's request — "not working in practice." He then hand-authored a `NavMeshObstacle` (Carve=true) directly on rack prefabs himself, which exposed a second bug: `BuildingData.SetupNavigation()`'s `category == "Racking"` branch was unconditionally `DestroyImmediate`-ing any `NavMeshObstacle`/`NavMeshModifier` on every placed rack — fixed by removing those destroy calls (kept the early return).

Tad then specified the exact architecture he wanted: **manual/scripted transform driving for the precision work** (grab pallet, insert/extend forks, pivot) **and the vehicle's OWN real `NavMeshAgent` for the open-floor legs in between** (staging lane ↔ rack approach anchor) — not a hand-rolled path-walker. Implemented via `AiNavigation.SeekPosition(Vector3, Action)`, an *already-existing* public API (used elsewhere for workers walking to tasks/equipment) that samples+sets a real NavMeshAgent destination and invokes a callback on arrival — no new pathfinding code needed, just wiring it into the two MHE controllers:

- **`ReachTruckOperator.cs`**: removed the upfront `Commandeer()` at the top of `PutawayRoutine`/`ReplenishRoutine` (vehicle now stays on whatever NavMeshAgent state it had — patrol — until the first long leg). New `SeekViaNavMesh(Vector3 target)`: re-enables `_vehicleNav`/`_vehicleAgent`, calls `SeekPosition`, waits for arrival (or a 30s timeout / `IsSeekingTask` going false), then always re-`Commandeer()`s before returning — falls back to the old straight-line `DriveToPoint` if unreachable. Applied at the same 3 long-distance call sites as the reverted attempt (lane exit, rack approach anchor in `DeliverPalletToRack`, reserve approach anchor in `PickupFromReserve`); short/local moves (in-lane grab, reversing back the way it came) untouched.
- **`TrailerOffloadController.cs`**: same pattern with local (not per-instance) `nav`/`agent` variables; new `SeekViaNavMesh(Transform ds, Vector3 target)` applied to the initial drive-to-trailer-pivot and the drive-to-lane-entry legs in `OffloadOnePallet`.

**Verification status (first pass):** compiled clean. Live-proved the core mechanism by invoking `SeekViaNavMesh` via reflection on a real in-scene `ReachTruckOperator` — it covered ~13m via genuine NavMeshAgent movement (not a teleport) in 3 real seconds, arrived within the MHE `stoppingDistance`, and cleanly handed back to manual control. A follow-up obstacle-specific test got contaminated (the vehicle's own `Update()`/task-claiming loop ran concurrently with the manual reflection test and took over the same vehicle mid-test), so obstacle avoidance was inferred, not watched.

#### Explicit drive-mode state machine (2026-07-26, later same day)

Tad's follow-up: the handoff points should be **explicit**, either via a switch component on the target transform or driven by task phase. **Recommended and implemented: task-phase-driven, owned by the controller.** A component on the target object can't work cleanly — the handoff is a property of the *task phase*, not the target: the same rack anchor is both an arrival point (AI→Manual, for the putdown) and a departure point (Manual→AI, heading to the next task), and those anchors are runtime-generated (`LocationData` children from `LocationRegistry`, lane slots from `LaneNamingService`) so it'd mean plumbing components onto hundreds of generated objects and reading a misbehaving run across N scattered objects instead of one file.

**What changed (`ReachTruckOperator.cs`):**
- New `private enum DriveMode { Ai, Manual }` + `_mode` field + **`SetDriveMode(DriveMode, string phase)` as the ONLY method permitted to touch `_vehicleNav.enabled`/`_vehicleAgent.enabled`.** Nothing else in the file may flip them. This matters because `AiNavigation.LateUpdate()` writes `transform.position = agent.nextPosition` every frame while its agent is enabled, and `AiNavigation.Update()` can re-issue a patrol destination on its own (rebake handler, waypoint-progression fallback, stuck-detector) — so an agent left live during a precision step means the two systems fight over the transform frame-by-frame, and both-off means the vehicle just sits there. One chokepoint makes both states unreachable by accident, and logs every transition with its phase name.
- `SetDriveMode` deliberately does **not** early-return when the mode is unchanged — external code (`MHEOperatorSlot` boarding/vacating via `AiNavigation.GoActive/GoIdle`) also toggles these components, so `_mode` can drift out of sync with reality; it always reasserts the component state and only gates the log line.
- **Removed the silent straight-line fallback.** This was likely a real contributor to "it's still driving through racks": when the NavMesh leg failed or timed out, the old code fell back to `DriveToPoint` — a straight line — which plows through anything in the way and looks *exactly* like the obstacle being ignored. `SeekViaNavMesh` now reports success/failure via an `Action<bool>` callback and logs a `LogError` naming the phase, the target, and the from-position. Every caller handles failure by aborting/re-queueing (putaway: cancel the slot reservation + backoff; delivery: put the pallet back and abort; replenish: report false so both slot reservations revert). A stuck truck with a clear log line beats a truck ghosting through the racking.
- `Commandeer()` removed entirely (replaced by the chokepoint); `Restore()` now just calls `SetDriveMode(Ai, …)` + resumes patrol.

**`TrailerOffloadController.cs`** got the same treatment for consistency (it had the identical silent fallback): `SeekViaNavMesh(Transform, Vector3, string phase, Action<bool>)`, no straight-line fallback, loud `LogError` on failure. Its two failure paths differ because the second leg is **mid-carry** — a bare `yield break` would strand the pallet on the forks, so it reuses the existing "no free lane slot" handling (set the pallet down where the DS stands + `RegisterAndQueue` so the Receiver can still find it) and releases the `_pendingDrops` reservation it took.

#### ⭐ ROOT CAUSE (found 2026-07-26 via Editor.log): approach anchors are at SHELF height, not floor height

Tad: "it still seems like the AI thing is timing out and then it drives through the rack." Read `%LOCALAPPDATA%/Unity/Editor/Editor.log` from disk (bridge was down — the documented fallback). No `error CS`. 21 real `SeekViaNavMesh: could not reach …` lines, and **the target coordinates are the whole story:**

```
could not reach (-3.36, 5.11, 22.54)  timedOut=True
could not reach (-2.04, 7.11, 22.54)  timedOut=True
could not reach (20.49, 5.11, 29.16)  timedOut=True
```

**Y = 5.11 and Y = 7.11 are UPPER RACK LEVEL shelf heights; the floor is Y≈1.1.** `FindLocApproachAnchor(address)` returns the `LocApproachAnchor` child of that *specific slot* — so an upper-level slot's anchor floats metres in the air. A forklift drives on the floor, so it's unpathable: `SetDestinationSnapped` samples with a 2m radius and can never reach floor navmesh 4–6m below → agent never arrives → full 30s timeout → the (then still present) straight-line fallback drove at a mid-air point, straight through racking. That's the "drives through the rack and does weird shit," and it's been the real cause the whole time.

**Why it hid so long:** manual `DriveToPoint` does `new Vector3(target.x, t.position.y, target.z)` — it silently rewrites the target's Y to the truck's own height, so the manual system always discarded the anchor's altitude and hit the right XZ by accident. Only the NavMesh leg was faithful to the anchor's real 3D position. **Any future code using `FindLocApproachAnchor` for *driving* must project to floor height first.**

**Fixes (2026-07-26):**
1. `SeekViaNavMesh` flattens the anchor to drive height (`target.x`, `transform.position.y`, `target.z`) and snaps that to navmesh (`NavMesh.SamplePosition`, 4m, agent's `areaMask`) before calling `SeekPosition`.
2. **Second landmine:** `AiNavigation.SeekPosition` opens with `if (_seekingTask) return;` — it SILENTLY does nothing when a prior seek is still flagged (no destination, no callback), so the wait loop burns its full 30s on a target it never attempted. Likely explains the *floor-height* targets that also timed out. `SeekViaNavMesh` now calls `CancelSeekPosition()` first. Deliberately did NOT change shared `AiNavigation` — `ReceivingTaskDriver`/`SeekEquipment` depend on it.
3. Failure log now prints `anchor=` and `driveTarget=` plus `agentOnMesh`/`pathStatus`.

**⚠️ NOT compile- or play-verified** — Unity was closed / MCP bridge down. Self-reviewed only (call sites grep-confirmed against new signatures; `using UnityEngine.AI` present for `NavMesh.SamplePosition`; variable scope in each failure branch read through). **Check console for `error CS` first, then watch a putaway** — the new log names the anchor, the projected drive target, and the agent's path status.

#### ✅ Hybrid drive VERIFIED WORKING (2026-07-26, later) — plus two more root causes

Reconnected to a live session and confirmed the above compiles clean. Then found and fixed two further defects that were keeping legs from ever completing. **Result: 8 successful AI-leg arrivals, 0 failures in a 50s window.**

1. **Freshly-enabled NavMeshAgent isn't on the NavMesh in the same frame.** `SetDriveMode(Ai)` enabled the agent and `SeekViaNavMesh` immediately queried `isOnNavMesh`/called `SeekPosition`. `AiNavigation.SeekPosition` sets `_seekingTask = true` and THEN calls `SetDestinationSnapped`, which bails on `!agent.isOnNavMesh` — so the destination was silently never set while `_seekingTask` stayed true, and the wait loop burned its full 30s on a path never issued. (Editor.log: `agentOnMesh=False` on every failure, though the same vehicles sampled valid MHE navmesh moments later.) Fixed with a registration wait loop (≤1s, warping each frame) plus a hard fail if it never registers.
2. **`GoToRandomWaypoint()` hijacked legs at the finish line.** `AiNavigation.Update()`'s waypoint-progression fallback (active for Forklift-role agents — they have no `AgentAnimation`) fires at `remainingDistance <= stoppingDistance + 0.1` = **1.1**, while seek-arrival needs `<= 1.0`. Any leg landing in that 1.0–1.1 window got re-destinationed to a random patrol waypoint *before* arrival registered: `_seekingTask` stayed true, callback never fired, 30s timeout, truck ended up somewhere random. Fixed with `SetTaskBusy(true/false)` around the leg — the public guard `AiNavigation` already provides for exactly this.
3. Wait loop also made **rebake-resilient** (re-warps / re-issues the destination if a NavMesh rebake knocks the agent off mid-leg — `NavMeshManager` swaps the mesh wholesale on every placement/deletion).

**Precise final alignment (2026-07-26).** Pallets were entering bays crooked and snapping square on release. Cause: the NavMesh leg only guarantees arrival within the MHE `stoppingDistance` (**1.0m**), and the code went straight from there into `FaceForks` → extend. Now a manual `DriveToPoint(locApproach.position, PrecisePlaceThreshold=0.10f)` closes the last metre onto the anchor's exact X/Z *before* any fork work (`DriveToPoint` snaps to the target XZ on completion, so residual error ≈ 0). Applied to both the delivery leg and `PickupFromReserve`. Also `pallet.rotation = locationTr.rotation` on release — position was being set without rotation, so the pallet kept the truck's heading.

**⚠️ `read_console` only returns errors/warnings, NOT `Debug.Log`.** Confirmed by planting a probe log that never appeared through the tool but was present in `Editor.log`. Phase/trace logging must be read from `%LOCALAPPDATA%/Unity/Editor/Editor.log` — this cost real time (it looked like `SetDriveMode` was never firing when it was firing all along).

#### Slot ↔ pallet cross-reference + Editor jump buttons (2026-07-26)

**Location contents were never recorded.** Measured live: **91 occupied slots, 90 of them blank**. `LocationStatusRegistry` (status) IS persisted across save/load but `LocationData` *contents* are NOT, so restored slots came back marked `Occupied` with empty PalletId/SkuId/Quantity. The repair pass `LocationRegistry.ReconcilePhysicalOccupancy()` was gated on `if (nearest.IsAvailable)` — **false for precisely the slots it existed to fix**. Re-keyed to "has no pallet recorded" → now 91/91 filled. Putaway also now fails LOUDLY (LogError) rather than silently writing blanks.

**Human-readable cross-reference.** GUIDs vs grid vectors were unusable for eyeballing, so: `LocationData` gained **`LoadId`** (the 10-digit plate) and `PalletData` gained **`LocationName`** (rack slot address like `01-01-A0`, or a lane slot). Both written on live putaway AND backfilled by the reconciliation pass. Verified 25/25 resolving in both directions. Also `PalletData.SetLocation(cell, name)` overload; putaway now syncs it (it previously kept pointing at the stale staging-lane cell).

**Editor jump buttons** (`.../Core/Inventory/Editor/`, `.../Racking/Editor/`): `PalletDataEditor` adds **Find Location** under the location field; `LocationDataEditor` adds **Find Pallet** under the contents block. Both select + ping + frame the Scene camera on the target. Shared logic in `InventoryCrossRefEditorUtil` — registry lookup first, then a scene scan, so **they work in Edit mode too** (the case that matters when chasing a mis-placed pallet in a save-loaded scene). "Not found" raises a dialog + console warning rather than silently doing nothing. `LocationDataEditor` also warns inline when a slot is `Occupied` with no contents.

#### Agent unstick system (2026-07-26)

Two layers, both verified live:
1. **Off-mesh recovery widened.** The old code sampled a fixed 2m with `NavMesh.AllAreas` and then refused to warp unless the hit was within **1m vertically** — anything beyond was permanently unrecoverable with no fallback (caught a reach truck at y=0.02 with its mesh 1.15m above). Now progressive radii (2→6→12→20m) filtered by the agent's **own type + areaMask**. Verified: stranded a receiver 8m in the air, back on-mesh and moving within seconds.
2. **Walk-out escape hatch** (`AiNavigation.UnstickWalkOut`). After `StuckEscalateSeconds` (3s) of genuine stuckness — off-mesh with nothing to snap to, OR on-mesh but unable to advance — the agent disables its NavMeshAgent, physically **walks** at 1.6 m/s to an escape point (animation plays, overshooting 1m so it clears the pocket), then warps back on and resumes its task/patrol. **Key subtlety:** a walled-in agent is *standing on* navmesh — the pocket IS navmesh, just an isolated island — so a plain nearest-point search returns the agent's own position and moves nobody (measured: "walking out to <same spot> (0.00m)"). `TryFindEscapePoint` therefore probes 12 directions at expanding rings and prefers a point that is **currently unreachable by path**, since unreachable proves it's on the far side of the blockage. Verified: 12.43m real walk-out. Escalation sits *after* the existing rebake/re-route logic so ordinary congestion still self-resolves.

#### Receiver deadlock — "stuck in the middle of a rack" (2026-07-26)

Reported as a receiver stuck inside a rack. **Not geometry — a stuck flag.** `ReceivingTaskDriver.Update()` opens `if (_taskInProgress) return;`, and that flag is only cleared by `HandleWorkflowComplete` via the workflow's `OnWorkflowComplete` event. Found her with `_taskInProgress=true` while her workflow read `Idle` with no current task and a completed fill bar — so `Update()` bailed on line 1 every frame: never claimed another task, never patrolled, and `AiNavigation.taskBusy` stayed set so nav wouldn't re-dispatch her either. She just stood where she finished, which happened to be inside a rack footprint.

Likely trigger, and the second fix: her path was **`PathPartial`** (0.14m remaining). `AiNavigation`'s seek-arrival handled only `PathComplete` (arrive) and `PathInvalid` (cancel) — **`PathPartial` matched neither**, so a seek to a partially-reachable target hung forever, callback never firing.

- **`AiNavigation`**: `PathPartial` + stopped at end of reachable path for `PartialSeekGraceSeconds` (1.5s) → treat as arrived and fire the callback.
- **`ReceivingTaskDriver`**: watchdog verifying the flag against reality — `_taskInProgress` true while `_workflow.IsBusy == false` for `StuckFlagTimeout` (12s) → clear flag, release `taskBusy`, resume patrol, log a warning. New public `ReceiverReceivingWorkflow.IsBusy` for this. Same self-healing shape as `WorkQueueSystem.ReleaseStaleAssignments()`; recovers from *any* path that drops the event, not just the traced one.

**Verified:** the previously-stuck receiver now cycles normally (moving, `PathComplete`, all flags clean).

> **Gotcha for future debugging:** there are TWO objects per employee — `Employee_<Name>` (the real one) and `LiveFeed_<Name>` parented under `StaffManager/PhotoBooth` at ~(101, -2, -75), the off-map portrait rig. A `name.Contains(...)` lookup will match the PhotoBooth clone first and send you chasing a disabled agent that is *supposed* to be inert.

**Status: BUILT 2026-06-29** — Core system complete; chevrons spawning, positioning/rotation fixed, adjacency detection working.

**Overview:** Player places rack prefabs → system auto-detects adjacent racks (via local X-axis) → groups into collections → spawns 4 chevrons per aisle (one at each corner: first/last bay, left/right side) → player right-clicks chevron to rotate (set aisle travel direction) → double-clicks to open setup UI → enters aisle #/level designations → system instantiates real racks with location labels (AA-BB-LC format) → chevrons deleted.

**Key Components:**

- **RackPlacedEvent** — Static event fired when rack placed in build mode. Listened to by RackCollectionDetector.
- **RackCollectionDetector** — Detects adjacent racks by projecting distance vector onto existing rack's local X-axis (transform.right). Groups into `RackCollection` objects. Fires `OnCollectionCreated`/`OnCollectionAdded` events. ADJACENCY_THRESHOLD = 1.5f.
- **RackCollection** — MonoBehaviour grouping adjacent racks. Tracks `Racks` list, `Initialized` state, collection center/bounds for chevron placement.
- **ChevronSpawner** — Listens to `OnCollectionCreated`, spawns exactly 4 chevrons per aisle (Left_Front, Left_Rear, Right_Front, Right_Rear) at first and last rack Z positions. Position: Y=1.15f (on ground), Rotation: 90° on X-axis (Quaternion.Euler(90,0,0) to make horizontal). Uses _chevronSprite and _chevronMaterial from RackingSystemManager.
- **ChevronController** — MonoBehaviour on each chevron. Right-click rotates 180° (toggles _currentRotation between 0/180). Double-click fires `OnChevronSelected`, calls `RackSetupUI.SetSelectedChevron(this)`.
- **RackSetupUI** — Modal UI receiving selected chevron. Collects aisle # (01-99) and level designations (Pick/Reserve by height ≥80"). On submit, calls `AisleInitializer.HandleSetupSubmit()`.
- **LocationNameGenerator** — Static utility. `GenerateAisleLocations()` creates AA-BB-LC names (Aisle-Bay-Level-Column) for all racks in collections. Determines even/odd sides from chevron rotation: rotation < 90° = first collection even bays (02,04,06...), other side odd (01,03,05...). Level indices 0-1 → "0","1"; 2-5 → "A"-"D" (pick/reserve tiers).
- **AisleInitializer** — Orchestrates initialization. On RackSetupUI submit: generates location names, instantiates real racks from `PlacedObject.data.prefab` (NOT a serialized field), assigns locations to TMP labels on each rack, disables non-aisle-facing label groups (determined by chevron rotation), marks collections initialized, deletes all chevrons.
- **RackingSystemManager** — Scene orchestrator. In Awake(), adds RackCollectionDetector/ChevronSpawner/AisleInitializer components and configures them via `SetSprite()`/`SetMaterial()`/`SetHeight()` setters with values from Inspector fields (Real Rack Material, Chevron Sprite, Chevron Material).

**Modifications to Existing Code:**
- **PlaceCommand.Execute()** — Added `if (_data != null && _data.category == "Racking") RackPlacedEvent.Fire(_instance);`
- **DragPlaceCommand.Execute()** — Added same RackPlacedEvent.Fire() inside `if (placed != null)` loop for multi-place.

**Setup Checklist:**
- [ ] RackingSystemManager GameObject in scene
- [ ] Configure Inspector fields: Real Rack Material, Chevron Sprite, Chevron Material
- [ ] Rack ObjDataSO must have category == "Racking" and prefab reference
- [ ] Test workflow: place racks → collections detected → chevrons spawn at first/last bays → right-click rotate → double-click open UI → submit → racks instantiate with labels

**Known Issues:**
- Adjacency detection uses local X-axis via dot product projection — works for any global orientation but threshold may need tuning if rack spacing varies.
- Two-sided aisle detection (SpawnChevronPair) logic may need refinement based on gameplay testing.
- Location naming + label assignment not yet end-to-end tested in full workflow.

#### 2026-07-01 Session — Aisle Setup UI + Vertical Stacking + Naming Fixes

Big pass that got the double-click→setup→commit→label flow actually working, plus vertical stacking. All items below compile clean and were verified via `Unity_RunCommand` in Play mode unless noted.

**Rack prefab label structure (load-bearing — memorize before touching labels):** Each rack prefab has **4 TMP label groups** named exactly `LabelFront.L`, `LabelFront.R`, `LabelRear.L`, `LabelRear.R` (each with one `TMP_Label` child). **There is NO parent `LabelFront`/`LabelRear` object** — code that did `transform.Find("LabelFront")` was a silent no-op for months. Front labels sit at local **+Z** (~0.65), Rear at **−Z** (~−0.66); `L` at high local X (~0.04), `R` at low local X (~−1.34). A rack's front-face normal **equals `transform.forward` (+Z)** — verified. ⚠️ `Rack-FullYellow48` has a DIFFERENT/mirrored label layout with `.002`-suffixed group names — which is exactly why the final labeling code keys off **world geometry, never group names** (see "Labeling is now fully geometry-based" below). Don't reintroduce `transform.Find("LabelFront.L")`-style lookups.

**Location format is `AA-BB-LP`** (Aisle-Bay-Level-**Position**, not Column). Level char: index `0,1` → numeric `"0"/"1"` (pickable); index `2+` → `"A","B","C"…` (reserve), via `LocationNameGenerator.ConvertLevelToChar` whose reserve branch is `'A' + (levelIndex − 2)`.

**RackSetupUI (the aisle setup modal) — was 100% non-functional, now works** (`Assets/_Project/Scripts/UI_UX/RackSetup/`):
- **The panel never rendered** because `RackSetupUI.uxml`'s root used `xsi:schemaLocation` without declaring `xmlns:xsi` → the whole UXML failed to parse into an empty `VisualTreeAsset` (Unity swallows it as a warning). The committed original had the same broken header. Fixed the header. **If a UI Toolkit panel is mysteriously empty, suspect a UXML parse error first** — check via `AssetDatabase.ImportAsset(...ForceSynchronousImport)` which surfaces the exact XML exception.
- **Dropdowns/handlers were unwired** because `RackingSystemManager.EnsureRackSetupUI` added the `UIDocument` while the GO was active and set `visualTreeAsset` *after* — UIDocument clones its tree on first enable, so it cloned nothing. Fixed: create GO **inactive**, set `panelSettings`+`visualTreeAsset`, THEN `SetActive(true)`.
- **Panel showed at Play start** because visibility was toggled with `GameObject.SetActive(false)` — but an inactive `UIDocument` that shares a `PanelSettings` stays attached to the panel and keeps rendering (`rootVisualElement.panel != null` while inactive). Fixed: keep the GO **active always**; drive show/hide via the overlay's `display` style (`Flex`/`None`). This is the reliable pattern for UI-Toolkit modals here.
- Rebuilt `.uxml`/`.uss` to the blueprint look. Background image **auto-loads from `Assets/_Project/Resources/RackBlueprint.png`** (Resources.Load in build, AssetDatabase fallback in editor) — **user must drop that PNG in**; without it the panel just has a plain navy background.

**Vertical stacking — place rack on top of a LIVE rack = append to aisle, NOT a new aisle** (`AisleInitializer.TryCommitStackedRack`, called first thing in `RackCollectionDetector.HandleRackPlaced`, before ghost/detection):
- Short-circuits: no new collection, no chevrons, no setup UI, **no orange ghost — live immediately** (gets the live material). Inherits `aisle`+`bay` from the rack below; `level = below.level + 1`.
- New fields on `PlacedObject`: `rackAisle`, `rackBay`, `rackLevelIndex` (−1 = not in an aisle). Ground racks record these at commit (`level 0`).
- **`RackPlacedEvent` fires BEFORE the rack is added to the grid stack** (`PlaceCommand.Execute` ~L72 vs `AddStackObject` ~L334), so `grid.GetObjectsInCell(cell)` returns the rack *below*. `FindLiveRackBelow` takes the topmost live "Racking" object in the root cell (skips self defensively for the drag path).
- **Grid cell entries are the nested struct `PlacementGrid.PlacedObject` (`.instance`,`.data`) — distinct from the MonoBehaviour `PlacedObject`.** Read `isRackLive` via `entry.instance.GetComponent<PlacedObject>()`.

**Naming bug fixes:**
- **Stacked level `@` bug:** forcing `"Reserve"` for every stacked level made level 1 compute `'A' + (1−2)` = `@` (ASCII 64). Fixed with `PICK_LEVELS = 2`: levels `<2` use `"Pick"` (numeric), `≥2` use `"Reserve"`.
- **Stacked position order reversed (1,0 instead of 0,1):** a position is a **vertical column** property, but labels were assigned by local group name, which swaps physical L/R when the stacked rack lands at a different rotation than the one below. Fixed: `AssignStackedRackLabels` inherits each label's position digit from whichever below-rack label is physically nearest in **world X/Z** (`InheritPositionFromBelow`) — rotation-proof.
- **Ground: labels showed on both sides + reported wrong level.** `ConfigureRackLabels` was the dead no-op above (wrong object names), so the non-aisle face was never hidden. And `AssignLocationsToLabels` crammed level 0 onto the front face and level 1 onto the rear, so an aisle-facing rear read level 1. Rewrote: each ground rack is **level 0**, both position columns labeled `AA-BB-00`/`AA-BB-01`; the non-aisle face is disabled. Removed the tangled `AssignLocationsToLabels`/`AssignLocationToLabelGroup`.

**Labeling is now fully geometry-based / prefab-agnostic (final form).** `Rack-FullYellow48` names its label groups `LabelFront.L.002` (and mirrors them), so ALL `transform.Find("LabelFront.L")` lookups silently failed on it — a stacked yellow rack kept its prefab default `A-01-01`. Fix: nothing keys off group NAMES anymore. `SetRackLabels` iterates `GetComponentsInChildren<TextMeshPro>(true)` and sets each label's position via a resolver; `ConfigureFaces(rackGO, aisleDir)` enables only labels whose world offset·aisleDir > 0 (disables the far face by toggling the TMP GameObject). Resolvers: ground = `TravelPositionResolver` (split the labels' projection onto travelDir at the midpoint → earlier column = pos 0); stacked = `InheritPositionFromBelow` (nearest below-label in world X/Z). Aisle direction: ground = lateral (⊥ run) part of the direction to the chevron (`GroundAisleDir`); stacked = the below rack's aisle-face normal from its still-active labels (`BelowAisleDir`). Deleted the name-based `ConfigureRackLabels`/`SetFaceEnabled`/`AssignGroundRackLabelsByTravel`. Verified: a yellow rack stacked on a committed orange level-1 rack now reads `01-02-A0`/`01-02-A1` on the aisle face, far face hidden.

**Height-aware one-shot init + travel-order positions (added same session, after the above):**
- **Problem seen in-game:** a fully pre-built 2-level structure, initialized in one shot, numbered the TOP level as *new bays at level 0* (…09,11,13,15) instead of *level 1 of the same bays* — the naming "wrapped" from the bottom row up. Also ground positions read `1,0` (reversed) because they were assigned by local L/R name, not travel direction.
- **Fix:** `CommitRealRacks` replaced by `CommitAndLabelAisle`. Per collection it splits racks into **ground** (lowest rack in each vertical column — `IsGroundRack`, keyed on `WorldToCell` + Y; stacked racks verified to share a cell) and **upper**. Only ground racks are numbered as bays (`LocationNameGenerator.GenerateBayNumbers`, now `public`); upper racks are committed **bottom-up via `TryCommitStackedRack`** so each inherits bay + level+1 from the committed rack directly below. So one-shot init of a multi-level structure now names correctly.
- **Ground position order now follows travel direction:** `AssignGroundRackLabelsByTravel` projects each label onto the travel vector (`ground[1].pos − ground[0].pos`, i.e. bay N→N+1) — the column earlier along travel = position 0. Matches the chevron/arrow direction, same as the stacked fix. `FindLiveRackBelow` is now height-aware (highest live rack strictly below this one's Y; falls back to topmost when Y isn't finalized, as on the post-init placement path). Face selection folded into `CommitGroundRack` (old `ConfigureLabels` removed).

**Second-side aisle join + designations + QoL (2026-07-01, later still):**
- **Two-sided aisles by placing the other side after finalizing** — the ONE allowed exception to unique aisle numbers. `RackCollectionDetector.HandleRackPlaced` now calls `AisleInitializer.TryCommitSecondSide` (after the vertical-stack check, before ghosting). If a fresh ground rack is placed FACING an already-finalized aisle (within 5 cells, that aisle's labeled side pointing back at it, nothing racking in between — `FindFacingFinalizedRack`/`IsRackingBetween`), it commits LIVE immediately as that aisle's OTHER side: same aisle #, opposite bay parity (`facingBay ∓ 1`, paired directly across), level 0, positions by the aisle's travel direction (`TravelDirFromRack` → `TravelPositionResolver`; do NOT inherit cross-aisle by nearest-XZ — the facing row faces the opposite way so columns don't overlap). No ghost/chevron/UI. Upper levels then stack normally. `AisleGroundBayExists` guards against adding a duplicate/third row.
- **Pick/Reserve designations are now honored.** `LocationNameGenerator.LevelChar(levelIndex, designations)`: a Pick level uses its numeric index ("0","1"…); a Reserve level uses a letter lettered bottom-up among reserves (first reserve = "A"). So Reserve at level 0 = "A" (not "@"/"?"), and a Pick above a Reserve keeps its number. `AisleRegistry.Register(aisle, designations)` stores the length-6 scheme per aisle so ground/stacked/second-side commits (incl. post-init stacks) all look it up. Ground/stacked label code calls `LevelChar` instead of the old `PICK_LEVELS` shortcut.
- **Drag-delete never eats foundations/grounds** (`DeleteState.UpdateDragDelete`) — too easy to swipe one up. Single-click delete of a clear foundation still works.
- **Toast sits above all panels** (`Toast.cs` `sortingOrder = 1000`) so warnings (e.g. duplicate aisle number) aren't hidden behind the RackSetupUI modal.

**Still open / not yet done:**
- The full one-shot ground init still **needs one real in-game confirmation** (couldn't drive `HandleSetupSubmit` headlessly — it's behind the UI Submit button). All its building blocks + the new ground/upper split, travel-order, and stacked-cell/Y assumptions were verified by script.
- `RackBlueprint.png` not yet added by user.
- Bay sequencing still assumes `collection.Racks` insertion order matches physical travel order (worked in testing; revisit if bays ever come out of order).
- The "workers pull from which side" arrows from the old lost UI were intentionally left out of the rebuilt panel (chevron right-click already sets direction).

### Dock Doors & Shipping Lanes (`Assets/_Project/Scripts/Gameplay/`)

> **📎 Skill:** Use the **`lane_setup`** skill (`.claude/skills/lane_setup/`) for this system.

**Status: BUILT 2026-07-03, confirmed working in-game.** Two self-bootstrapping, always-on services — no scene wiring, no guard shack, no manager to configure (each spawns a hidden `DontDestroyOnLoad` object via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`).

**Door numbering — `DockSlot.AssignDoorNumbers()`:** every `ShippingDoor` (carries a `DockSlot`, registered in static `DockSlot.All`) gets a **permanent** number shown via `DoorNumberDisplay`. Numbers **never renumber existing doors** — each is persisted in `PlacedObject.customData` (saved/restored), a newly placed door takes the **lowest free** number (5th door → "5"), and deleting a door frees its number for reuse (gap-fill, like aisle numbers). This stability is load-bearing: trucks, lane names, and employee/inventory destinations all reference door numbers, so we only add/remove, never rename (a manual double-click rename UI can come later). `DockNumberingService` drives it on placement/deletion events + a 1s heartbeat (catches save-loads, which bypass `PlaceCommand`); `TruckYardManager` also delegates to it. (Previously numbering lived only on the guard-shack's `TruckYardManager` and broke with no guard shack — decoupled 2026-07-02.)

**Shipping-lane naming — `LaneNamingService`:** names every shipping lane `<doorNumber><letter>` (e.g. `1A`, `2C`) — one name per lane, globally unique = one exact place in the warehouse. A **lane = one row of `Flr-ShipLane` tiles (id 35, `isFloor`)** running out from a door in the door's facing/depth direction (~5-8 tiles); lanes sit side-by-side along the wall. The `Flr-ShipLane` prefab has a child **`LaneNo`** (TMP) that both displays the name and self-identifies a lane tile (never hard-code id 35 — look for the `LaneNo` child). Algorithm: assign each tile to its **owner door** → group **per door** → wall axis from **that door's `transform.forward`** (`wallIsZ = |fwd.x| >= |fwd.z|`) → group the door's tiles into lanes by wall-coord cell → letter A.. with **A = lowest wall coordinate**. Per-door grouping + facing-derived axis is essential because docks can sit on opposite walls (verified: one at grid x=67 facing −X, another at x=36 facing +X). Recomputes on placement/deletion + 1s heartbeat. **Ownership is STICKY (2026-07-03 fix):** each lane tile's owning door NUMBER is persisted in its own `PlacedObject.customData` (same field doors use for their number) and, once set, is **never re-stolen** by a door placed closer later — only unowned/orphaned tiles get a fresh owner (`ChooseDoorForNewTile` = the door the tile sits most directly *in front of*, then persisted). This fixed the bleed-over bug where placing a new door near an existing dock's lanes relabeled them. Do **not** revert to re-assigning every tile by nearest-door each Recompute. To re-home mis-owned lanes: delete the owner door (lanes orphan → reassign → re-lock) then re-place it, or delete & re-place the lane tiles.

**Gotcha:** the Unity MCP bridge drops on every recompile and can't compile while the Editor is in Play mode — stop Play first; verify compiles via `%LOCALAPPDATA%/Unity/Editor/Editor.log` (`error CS`). Lane tiles are runtime/save-driven (absent from the edit-mode scene).

### Editor Tools (`Assets/10. Editor/`)

- **ObjDataRegistryEditor** — custom Inspector for `ObjDataRegistry`
- **ObjDataIDAssigner** — menu item to auto-assign unique IDs to all `ObjDataSO` assets
- **Build PhaseChecklistWindow** — in-Editor task tracker reading from a JSON file

### Input

Input is handled via the Unity Input System. `PlacementActions` is the generated C# wrapper for `InputSystem_Actions.inputactions`. States subscribe/unsubscribe their action bindings in `OnEnter`/`OnExit`. Global shortcuts (Ctrl+Z, Ctrl+Y, ESC, RMB) are handled directly in `PlacementStateMachine.Update()`.

### Key Conventions

- **Rotation convention:** rotations are stored in 90° increments as integers on `PlacedObject.rotation`. In `BuildingData` and state logic, rotation is stored as float degrees. Pass `-currentRotation` to `GetFootprintOffsets()`.
- **Cell stacking order matters:** Foundations and Grounds go at index 0; Floors go after grounds; normal objects append to the top. `PlacementGrid` enforces this in `AddStackObject`.
- **Undo disables objects, not destroys:** Undo for placement calls `SetActive(false)` on the GameObject so it can be re-enabled on redo, keeping the same instance alive.
- **`FindFirstObjectByType` usage:** `PlacementStateMachine.Start()` uses `FindFirstObjectByType` to locate shared systems — these must all be present in the scene at startup.

---

## Game Vision

This is a warehouse simulation game with a serious logistics core and a whimsical/absurdist chaos layer on top — think Sims 4 build mode meets real operations management meets escalating insanity.

**Current phase:** Build mode only. The placement system, grid, economy, and save/load are the foundation. The actual gameplay loop comes next.

**Player character:** The player is **Tug Dudley**, the warehouse manager / "The Boss." This name appears in the welcome overlay and is stored via `PlayerPrefs("PlayerName")`.

**Design principle:** The chaos should *emerge from* the simulation, not be bolted on. Serious systems create the conditions for absurd outcomes.

---

## IDEAS

Ideas and planned systems. These range from fully thought-out to early sparks.

### Lighting System (NEXT — starting 2026-07-03)
> **Status: on deck.** Tad's next feature, and "probably the last thing I need to add before we get into core game." Not started — no code yet.

**Real-time lighting (NOT baked)** delivered as purchasable, placeable **light fixtures**:
- Fixtures are normal placeable objects (`ObjDataSO` + prefab with a real-time `Light`, a `cost`, category, etc.) placed through the usual Build/FSM flow.
- Placed lights **illuminate the warehouse**; lit areas improve worker **safety** and **accuracy**, unlit areas degrade them.
- Enables the **night shift** to work safely — a dark warehouse at night without lights is unsafe/inaccurate.
- Should feed the employee stat/economy systems (safety & accuracy already exist as concepts). When building: confirm fixture types/costs, how "lit vs unlit" is measured per cell/area (light range/intensity → coverage), and how coverage maps to the safety/accuracy numbers.

### The Rat System
> **Status: Deferred.** Rats are intentionally being held until the core gameplay loop is solid. Do not implement until employee AI, inventory, and the basic simulation loop are all functional. The foundation has to come first.

Rats are a pest control challenge that layers chaos on top of the simulation. Left unaddressed, an infestation spirals out of control.

**Core behavior (implement first):**
- Rats hide inside pallets and boxes at rest
- When a worker passes nearby, a rat bolts from its hiding spot and scurries to a new location in the warehouse
- A green contamination cloud hangs around areas where rats have been present
- Rats can damage inventory — spoilage, contamination, loss

**Escalation (implement after core behavior is solid):**
- Rats procreate and multiply over time
- Individual rats grow larger and more aggressive as the infestation matures
- Cascading consequences: employee morale drops, employees quit, health inspectors arrive, fines accumulate, potential shutdown
- Endgame escalation: the player may have to physically fight a boss rat

**Testing checklist (for when this is built):**
- [ ] Rats spawn hidden inside pallets/boxes at scene load
- [ ] Rats flee to a new hiding spot when a worker comes within trigger range
- [ ] Green contamination cloud appears at rat locations
- [ ] Inventory items near rats take damage / show contamination state
- [ ] Rat population grows over time if unchecked
- [ ] Infestation consequences (morale, inspectors, fines) fire at correct thresholds

### Employee Simulation
Deep human element for warehouse workers:
- **Stats:** Speed, Safety awareness, Intelligence, Promotion potential
- **State:** Fatigue, Morale
- Behavior driven by stats + current conditions (a tired, low-morale employee is accident-prone)
- Career progression — employees can be promoted based on performance and potential

### Inventory & Ordering System
Core logistics loop:
- Place orders for goods
- Receive shipments, store inventory
- Fill outgoing orders from stock
- Stockouts, overstocking, and supplier delays create pressure

### Performance Optimizations (2026-06-11)

**Collider Architecture — Parent Colliders Instead of Per-Object**

Analyzed raycast system and discovered that individual Box Colliders on every placed object (floor tiles, foundations) causes massive performance overhead. Solution: one **parent collider per category**.

**Why This Works:**
- `RaycastController` performs two passes: ground raycast (gets grid cell) + object raycast (gets clicked object)
- The object raycast only needs to HIT something — it doesn't care if it's 1 collider or 3600
- Move/Delete logic identifies the exact object via BuildingData/PlacedObject components, not the collider itself

**Implementation:**
- **Floors:** Removed Box Colliders from FloorTile prefab. Added one large Box Collider to parent Ground GameObject. ✅ Complete, tested working.
- **Foundations:** Removing individual Box Colliders and adding one large Box Collider to parent Foundations GameObject. ⏳ In progress.
- **Walls:** Same pattern (TBD).

**Performance Gain:** 3600+ individual colliders → 1 parent collider per category. Ground optimization alone yielded +200 FPS. Foundations should see similar improvement.

**Code Impact:** Placement system needs to automatically parent placed objects to their category parent (Ground, Foundations, Walls) so the raycast hits the correct parent collider.

---

## Wave 4: FSM & Build State Refactoring (COMPLETED 2026-06-25)

### Architecture Refactoring Overview

Wave 4 introduced a standardized, event-driven architecture for FSM states and build operations. The work involved 3 major phases:

**Phase 1: Infrastructure Creation**
- `IBuildService` — Service contract for placement/move/delete operations
- `IPlacementCommand` — Unified command interface (Execute/Undo/Redo)
- `PlacementCommandBase` — Abstract base for command implementations
- `PlacementStateBase` — Abstract base implementing `IPlacementState` with shared services
- `BuildService` — Facade coordinating validation, command execution, and undo/redo
- `GameEvents.Build` — New event registry entries (OnValidationFailed, OnCommandUndone, OnCommandRedone)

**Phase 2: Service Integration**
- `GameContext` now instantiates and registers `BuildService` with `ServiceLocator`
- `CommandHistory` created fresh for each game session
- `EventManager` has runtime fallback instantiation if not in scene
- All services initialized in dependency order in `Awake()`

**Phase 3: State Refactoring (All 5 States Updated)**
All FSM states now inherit from `PlacementStateBase`:
- **IdleState** — Hover inspection and popup display
- **RaycastPlacementState** — Grid hover with cell indicators
- **BuildState** — Object placement with drag-place and validation
- **MoveState** — Object relocation with offset preservation
- **DeleteState** — Object removal with refunds and animations

### Service Access Pattern

All states now use `PlacementStateBase` to access services:
```csharp
public override void OnEnter()
{
    base.OnEnter();  // Initializes _eventManager, caches services
    // State-specific logic here
    // Access _moneyService, _timeService, _grid via protected members
}
```

### Build Operation Flow

```
User Input (Click/Drag)
    ↓
FSM State (BuildState, MoveState, DeleteState)
    ↓
BuildService.TryPlaceObject() / TryMoveObject() / TryDeleteObject()
    ↓
Validation: CanAffordCost? GridRulesOK? Etc.
    ↓
Create ICommand (PlaceCommand, MoveCommand, DeleteCommand)
    ↓
CommandHistory.Push(command) → Execute immediately
    ↓
Publish GameEvents.Build.OnObjectPlaced
    ↓
UI/Economy react to event
```

### Key Design Decisions

1. **Facade Pattern for BuildService** — Minimal implementation that delegates to existing commands and validators. Designed for future unification of command creation logic.

2. **PlacementStateBase as IPlacementState** — Base class implements the interface; concrete states override and call `base.OnEnter()` / `base.OnExit()` to ensure service initialization.

3. **Service Caching in Initialize()** — Services looked up once from ServiceLocator and cached in state instance, avoiding repeated registry queries.

4. **Protected Helpers** — `PublishBuildEvent()`, `CanAfford()`, `CurrentCapital`, `CurrentTime` provide quick access to common operations.

5. **Backward Compatibility** — Existing command classes (PlaceCommand, MoveCommand, DeleteCommand) remain unchanged. CommandHistory still owns undo/redo logic.

### Files Created

- `Assets/_Project/Scripts/1. FSM/1. Services/IBuildService.cs`
- `Assets/_Project/Scripts/1. FSM/1. Services/BuildService.cs`
- `Assets/_Project/Scripts/1. FSM/6. Commands/IPlacementCommand.cs`
- `Assets/_Project/Scripts/1. FSM/6. Commands/PlacementCommandBase.cs`
- `Assets/_Project/Scripts/1. FSM/2. States/PlacementStateBase.cs`

### Files Modified

- `GameContext.cs` — Registers BuildService and creates CommandHistory
- `IdleState.cs`, `RaycastPlacementState.cs`, `BuildState.cs`, `MoveState.cs`, `DeleteState.cs` — All now inherit PlacementStateBase
- `GameEvents.cs` — Added Build.OnValidationFailed, Build.OnCommandUndone, Build.OnCommandRedone

### Testing Checklist

- ✅ All 5 states refactored and compile clean
- ✅ Services accessible via ServiceLocator from all states
- ✅ EventManager initializes at runtime if not in scene
- ✅ BuildService facade works for validation queries
- ✅ Undo/redo via CommandHistory still functions
- ✅ No breaking changes to existing gameplay

### Next Steps (Future Waves)

- Refactor command classes to inherit from PlacementCommandBase and implement IPlacementCommand
- Unify command creation logic in BuildService (currently deferred to states/UI)
- Add more granular build events for UI/feedback systems
- Consider state-specific event publishers for cleaner decoupling

---

## Wave 5: Command Refactoring — Phase A COMPLETED (2026-06-25)

**Phase A done:** `PlaceCommand`, `MoveCommand`, `DeleteCommand`, `DragPlaceCommand` now inherit `PlacementCommandBase` and implement `IPlacementCommand` (which now extends `ICommand`, so `CommandHistory` needed no changes). Commands get real `Description` text and publish `GameEvents.Build.OnObjectPlaced/OnObjectMoved/OnObjectDeleted` through `EventManager`. Also fixed in the same pass: `PlacementStateMachine` previously constructed its **own** `CommandHistory` separate from the one `GameContext` handed to `BuildService` — `BuildService.Undo()/Redo()` were operating on a permanently-empty stack. `PlacementStateMachine.Initialize()` now adopts `BuildService.CommandHistory` via `ServiceLocator` instead of creating a fresh one.

**Side effect worth knowing:** this event-publishing is what made `EconomyService` start actually receiving `OnObjectPlaced`/`OnObjectDeleted` for the first time — hourly cost deduction for placed objects was dormant before this (see Economy section below).

**Phase B — deliberately deferred, not started:** centralizing command *construction* into `BuildService.TryPlaceObject/TryMoveObject/TryDeleteObject` instead of states constructing commands directly. User's call after weighing it: real benefit is only "a non-FSM caller could place objects programmatically" (nothing currently needs that), real cost is touching the placement hot path plus `DeleteState`'s `AddToBatch` has no equivalent in `IBuildService` today. Revisit only if a concrete feature needs headless placement.

**Secondary items, still open:**
- Fix ToolsWindow (non-functional UI currently)
- Migrate saves to `Application.persistentDataPath` (shipping blocker)
- Configure SimulationTimeService time scale (tuning needed)

---

## Economy & Financial Reporting System

Built out 2026-06-25, actively being extended. This is the active focus area for the foreseeable future — keep this section current as it evolves rather than letting it drift like the Wave 4/5 docs did.

**⚠️ Testing gotcha, confirmed 2026-06-25: restart Play Mode after any code change before re-testing this system.** Recompiling scripts mid-Play-session triggers a Unity domain reload, which silently nulls `EventManager.Instance`, `ServiceLocator`'s entire registry, and `GameContext.MoneyService`/`TimeService` (plain C# objects don't survive domain reload the way the `GameContext` GameObject itself does). The result: `SimulationTimeService` stops ticking, `EconomyService`'s hourly accrual stops, `PayrollService` stops paying wages — but the TopBar UI keeps showing whatever numbers were last rendered, so it LOOKS like the game is still running when it's actually frozen. This reads exactly like a UI/data-wiring bug (and cost real debugging time before the actual cause was found via `Unity_RunCommand`: `EventManager.Instance == null` + `ServiceLocator.TryGet<MoneyService>()` failing while `GameContext` itself is still findable in-scene). There's no automatic recovery — stop and re-enter Play Mode to get a clean `GameContext.Awake()`.

### Core data flow

Two **separate, parallel** cost pipelines feed `MoneyService`, both ultimately routed through `FinanceCategory`:

1. **Object hourly costs** (`EconomyService`) — driven by `ObjDataSO.hourlyCost` for placed buildings/vehicles/decor. Tracked per `GL_Line`, NOT per individual object.
2. **Employee wages** (`PayrollService`) — driven by `EmployeeRecord.hourlyWage`, paid once per in-game hour. Completely separate from (1) — see the "Worker/Staff exclusion" gotcha below.

**`ObjDataSO.GL_Line`** — a plain string field, hand-set per asset (Inspector-editable, no runtime default-computation). Defaults were populated by a one-time batch (`Unity_RunCommand`, not a menu item) mapping `category` → `GL_Line`:

| category | default GL_Line | top-level bucket |
|---|---|---|
| Barrier/Barriers | Barriers | Maintenance |
| Flavor | Flavor | Maintenance |
| Floor | Floor | Maintenance |
| Grounds | Groundskeeping | Groundskeeping |
| Inventory | Pallet Lease and Repair | Pallet Lease and Repair |
| Loss Prevention | Loss Prevention | Loss Prevention |
| Racking | Racking | Maintenance |
| Staff | Wages | Wages |
| Vehicles | (per-vehicle, see below) | MHE Costs |
| Walls | Walls | Maintenance |
| Waypoints | Waypoints | Maintenance |
| Worker | Worker | Maintenance |

**Per-asset overrides on top of the table above** (set directly on the asset, not derivable from category):
- `ManDoor`, `RollupDoor`, `ShippingDoor`, `Wall-Win-Entrance` → GL_Line `"Doors"` (still Maintenance)
- `Truck`, `TruckSavageDev`, `TruckDriver` → GL_Line `"Transportation"` (own top-level bucket, pairs the vehicle + its driver's wage)
- `DS`/`PJ`/`RT` (Dockstocker/PalletJack/ReachTruck) → GL_Line `"Dock Stocker"`/`"Pallet Jack"`/`"Reach Truck"` respectively — each vehicle type gets its **own** GL_Line string so the MHE Costs tooltip can break them out individually, all three routed to the same `MHECosts` top bucket via `FinanceCategory.ForGLLine`
- `Dumpster`, `Garbage Can`, `Trash Can` → GL_Line `"Sanitation"` (own top-level bucket, pairs with the Sanitation employee tier)
- `Exterminator` (the Staff body model) → GL_Line `"Contract Labor"` (pairs with the Exterminator employee's wage)

**`FinanceCategory.cs`** (`Assets/_Project/Scripts/Core/Managers/TimeAndMoney/FinanceCategory.cs`) is the single source of truth for category strings and routing:
- `IncomeOrder`/`ExpenseOrder` — fixed arrays that drive row order in every breakdown panel.
- `ForGLLine(string glLine)` — maps a GL_Line value to its top-level bucket. **Whenever you give an object/category a new distinct GL_Line value, you must add a case here too**, or it silently falls through to the `Maintenance` default (this has bitten us twice already — Sanitation and Contract Labor GL_Line values were missing cases and got miscategorized).
- `ForWageGLLine(EmployeeRole role)` — maps an employee role to `(topCategory, detailKey)`. Exterminator→Contract Labor, Security→Loss Prevention, Sanitation→Sanitation (own line), TruckDriver→Transportation — these four are pulled OUT of Wages entirely. Everything else stays under Wages, tiered into `FloorWages`/`HourlyWages`/`SalaryWages` (OrderSelector/Loader/DockStockerOperator/ReachTruckOperator/Receiver = Floor; InventoryControl/Admin = Hourly; HR/Boss/Supervisor = Salary).

### EconomyService (`Assets/_Project/Scripts/1. FSM/1. Services/EconomyService.cs`)

- Subscribes to `GameEvents.Build.OnObjectPlaced/OnObjectDeleted` and pools hourly cost **per GL_Line** in `_hourlyByGLLine` (NOT a single flat total — that was the old, dormant-until-2026-06-25 design).
- **Fractional-carry accumulator (`_fractionalByGLLine`) is load-bearing, do not remove.** Without it, `Mathf.CeilToInt(hourlyAmount/60f)` rounds every GL_Line up to a **$1/minute floor** regardless of its real rate — several cheap categories (a few dollars/hour each) all hit the same floor and accumulate *identical* lifetime totals, which looks exactly like a data bug but is a rounding bug.
- **`IsWageTracked()` excludes `category == "Worker" || "Staff"`.** Employee prefabs carry a `PlacedObject` component too (for the hover-popup), so they self-register with `PlacedObjectRegistry` just like a placed building even though they never go through `PlaceCommand`. Without this exclusion, every hired employee's body double-charges its legacy `ObjDataSO.hourlyCost` on top of `PayrollService`'s own wage — confirmed empirically (the math matched to the dollar) before this was understood.
- **`RebuildFromRegistry()` must be called after every save load / scene start / manual rebuild** — call it everywhere `PlacementGrid.RebuildFromRegistry()` is called (currently: `GameContext.SyncAndBake`, `PlacementSystem`'s two load call sites, `ToolsWindowController`'s rebuild button and Clear-All). Objects restored from a save or placed by hand in the Editor scene are instantiated directly, bypassing `PlaceCommand` — `OnObjectPlaced` never fires for them, so without an explicit rescan they're invisible to hourly-cost tracking even though they're sitting right there costing money.
- **`OnMinutePassed` also calls `moneyService.RecordHourlySpend(wholeDollars)`** for every real per-GL_Line deduction — bookkeeping only (the dollar was already removed by `RemoveCapital` on the line above), tags it as a *recurring* hourly cost for the Spent Today panel. `PayrollService` does the same for wages. See MoneyService below.
- **Known gap, still not fixed:** one-time placement costs (`PlaceCommand`'s `_money.Deduct(_data.cost, _data.category)`) are tagged with the raw `ObjDataSO.category` string directly in `_lifetimeExpenses`/`_lifetimeDetail` — completely bypassing `FinanceCategory`/`GL_Line` for the **lifetime** totals. The Hourly tab's per-category breakdown still won't show one-time purchase costs (category strings like `"Grounds"`/`"Walls"` don't match any `FinanceCategory.ExpenseOrder` entry). This WAS fixed for the **Spent Today** panel specifically (see `MoneyService.Deduct()` below) but not for the lifetime/Hourly-tab view — would need the same `FinanceCategory`/`GL_Line` routing `Deduct()` callers don't currently use.

### PayrollService (`Assets/_Project/Scripts/1. FSM/1. Services/PayrollService.cs`)

Pays every `Active`, non-`SystemManaged` employee their `EmployeeRecord.hourlyWage` once per in-game hour, routed through `FinanceCategory.ForWageGLLine`. Increments `EmployeeRecord.totalWagesPaid`.

**Overtime rule (added 2026-06-25):** if `ShiftSchedule.IsOvertime(record.shift, currentHour)` is true, that hour is paid at **1.5×** the base rate. `Flexible`-shift employees have no fixed window and are never overtime.

### ShiftSchedule (`Assets/_Project/Scripts/Actors/EmployeeSystem/ShiftSchedule.cs`)

Single source of truth for shift windows, shared by `PayrollService` (overtime pay) and `ShiftStatusPanel` (the Time dropdown). Fixed windows, not yet configurable:
- Day: 08:00–16:00
- Evening: 16:00–00:00
- Night: 00:00–08:00
- Flexible: always "in shift," never overtime

This is a reasonable first-pass interpretation, not a confirmed design spec — revisit if the windows or overtime definition need to change.

### MoneyService (`Assets/_Project/Scripts/Core/Managers/TimeAndMoney/MoneyService.cs`)

Lifetime tracking (`_lifetimeExpenses`/`_lifetimeIncome` top-level totals, `_lifetimeDetail` = `category → detailKey → amount` powering the Hourly tab's tooltips) all flow through `RemoveCapital`'s two overloads sharing a private `ApplyRemoval()` core: the 2-arg version records under `reason`; the 3-arg version (`amount, category, detailKey`) records under the finer `detailKey` instead of `category`, to avoid double-recording the same dollar under two granularities.

**Two SEPARATE "today" counters power the Spent Today panel** — these used to be one conflated number and that was a real, user-reported bug (2026-06-25/26): buying 56 foundations showed as "$25,200 Total Hourly Expenses" with an empty Purchases list, because the old single `SpentToday`/`_spentTodayDetail` lumped one-time purchases in with recurring hourly costs, and the by-category list was only ever fed by the hourly loop. Fixed by splitting into:
- **`_spentTodayHourlyOnly`** — RECURRING costs only: `EconomyService.OnMinutePassed` (object upkeep) and `PayrollService` (wages) both call `RecordHourlySpend(amount)` as bookkeeping alongside their real `RemoveCapital` call. Powers Spent Today's "Total Hourly Expenses" line.
- **`_spentTodayByObjectCategory`** — ONE-TIME purchase costs only, keyed by raw `ObjDataSO.category`. Populated exclusively by **`Deduct(int amount, string category)`** — every `PlaceCommand`/`MoveCommand`/`DeleteCommand` one-time cost adjustment goes through this method, and it always carries a real category string, so it's the correct/only hook point. Powers Spent Today's "Purchases" section + "Total Purchases" row.

Both reset in `ResetDailySpending()` alongside the legacy `_spentToday` int (which still exists and still means "everything," just isn't displayed as a single number anywhere anymore). If you add a NEW one-time-cost call site, route it through `Deduct()` (not raw `RemoveCapital`) so it's correctly counted as a purchase, not an hourly cost.

**Lease/Mortgage** — `EconomyService.ChargeLease()`, fired on `OnDayChanged`: `(PlacementGrid.Width × Height) / 10` dollars, once per in-game **day** (not hourly, unlike everything else — rent is conventionally billed per period). Explicitly a quick stand-in per the user's request ("may not be a permanent feature") — easy to rip out or replace with a real property/lease system later.

### The four TopBar reporting panels

All implement `ITopBarPanel` (`IsVisible`/`Show()`/`Hide()`/`Root` — `Root` exposes the panel's own VisualElement) and share visual building blocks from `FinanceUIKit.cs`. `TopBarUI.ToggleExclusive()` ensures only one is ever open at a time, and `TopBarUI.PollPanelAutoClose()` (checked every 100ms) auto-closes whichever panel is open once it's been unhovered — neither its trigger label nor its own `Root` — for 1500ms. Hovering either the label or the panel resets that idle clock.

| TopBar label | Panel class | Shows |
|---|---|---|
| Capital | `CapitalSummaryPanel` | Revenue breakdown (green header / light-yellow text) + **one** rolled-up Total Expenses line + Net Profit. No per-category expense drill-down here. |
| Hourly | `FinancialBreakdownPanel` | **Expenses only** (red header / white text) — full per-category breakdown with hover-to-expand tooltips (Wages tiers, Maintenance by area, MHE Costs by vehicle, etc). The Income section and Net Profit row were removed entirely — Revenue/Net Profit live on Capital instead. |
| Spent Today | `SpentTodayPanel` | "Total Hourly Expenses" — recurring costs only, one number, not broken out. Below it, a "Purchases" section: one-time costs broken out by raw `ObjDataSO.category`, with a "Total Purchases" row. See MoneyService above for why these are two genuinely separate counters now. |
| Time | `ShiftStatusPanel` | Hours left in whichever shift window currently contains the in-game time, + live count of `Active` employees currently working outside their own shift (overtime). |

**Tooltip retract mechanism (FinancialBreakdownPanel's per-category hover tooltips, NOT the same thing as the panel auto-close above)** — rewritten 2026-06-25 after two patch attempts both failed under sustained use. The original design created a fresh `IVisualElementScheduledItem` per mouse-enter/leave event and called `Pause()` on the previous one each time; this degraded after repeated hover cycles into tooltips getting permanently stuck open. **Do not reintroduce that pattern.** Current design: a `TooltipEntry` (`Row`, `Tooltip`, `Category`, `Hovered` bool) per expandable row, tracked in a `List<TooltipEntry>`, with **one** shared idle-timer (`_idleMs`) polled by **one** scheduled item created once in the constructor (`_panel.schedule.Execute(PollIdle).Every(100)`) that runs for the panel's entire lifetime, closing after 1500ms unhovered. No per-interaction timer creation/cancellation.

**UI Toolkit scheduling gotcha, learned the hard way (twice):** `IVisualElementScheduledItem.ExecuteLater(ms)` reschedules an item that has **already fired once** — it does NOT delay the first execution. To delay a first run, use `.StartingIn(ms)` instead. Both the tooltip retract above AND the panel auto-close (`TopBarUI.PollPanelAutoClose`) initially used `ExecuteLater` and silently didn't work — the panels stayed open until manually re-clicked, which read as a missing feature rather than a one-word API mistake.

### Overtime actions — "Ask to Work OT" / "Send Home" (added 2026-06-25)

Two new options in the Employee Info card's Actions dropdown (`EmployeeInfoUI`), handled by a new static `EmployeeOvertimeService`:
- **Ask to Work OT** — applies an instant morale penalty (5 points normally, 12 if morale is already below 40 — "low morale takes a bigger hit"), and marks today as an overtime day on the employee's `EmployeeWorkSchedule` (`SetOvertime`). Does NOT gate whether overtime pay/fatigue actually happens — `PayrollService` already pays 1.5x automatically by hour regardless of consent (see ShiftSchedule above); this button is the morale-cost flavor action of formally asking, not a gameplay gate.
- **Send Home** — flat 10-point morale penalty (`// TODO: scale by difficulty` — explicitly a placeholder per Tad's note, not implemented yet). Does nothing else right now (no pay/status change) — kept deliberately minimal to match the literal ask.

**`EmployeeStatSystem.cs`'s daily fatigue/morale tick (`TickDay`/`TickDayForAll`) is NOT wired to run automatically anywhere** — confirmed by grep, it's only ever called from its own Editor debug context menu ("Tick Day for All Employees"). Its overtime fatigue gain is now `_fatiguePerWorkDay * _overtimeFatigueMultiplier` (multiplier defaults to 2 — an exact "doubled" relationship, replacing an old unrelated flat constant), and `GetCurrentDayOfWeek()` is now `public static` (still uses real-world `DateTime.Now`, not in-game day — a known placeholder) so `EmployeeOvertimeService` can share it instead of duplicating the bug. Net effect: the "doubled fatigue during overtime" rule is implemented correctly but has **zero visible effect in actual play** until this daily tick gets wired to a real day-change event — flagged to Tad, not silently fixed (wiring it up is a separate decision: should it fire on `GameEvents.Time.OnDayChanged`, and should `GetCurrentDayOfWeek` be fixed to use `SimulationTimeService.Day` at the same time, since they're related bugs).

### ShiftManagerPanel — first draft (2026-06-25/26, UI ONLY, not wired to gameplay; visual pass + polish 2026-06-26)

`Assets/_Project/Scripts/UI_UX/ShiftManagerPanel.cs`, bound to the **5** key (built/owned by `TopBarUI` like the four panels above, but NOT one of them — it's a full-screen modal, not a TopBar dropdown, so it's outside `ToggleExclusive`/`PollPanelAutoClose`). Lets the player define named shifts (free-text name, e.g. "Day Shift", "Clean Shift", "Night Shift") with a Start/End time dropdown per day of week (Sun=0..Sat=6 — **note this differs from `EmployeeWorkSchedule`'s Mon=0..Sun=6 convention**, unreconciled since this isn't wired to that system yet), in 30-minute increments 00:00–23:30, or "Closed". Constructor now takes `ITimeService` (`new ShiftManagerPanel(root, _timeService)` in `TopBarUI`) so the header row can show absolute day numbers (see below).

**Cell state model:** each Start/End cell is one of `NotSet` (blank, never touched — red background, .65 alpha), `Closed`, or a real time. A day where **both** Start and End are still `NotSet` silently resolves to `Closed` on Save & Close / a row's own "Enter Info" (the "simplify the process" default) — no error. A day left **half-filled** (one side set, the other blank) blocks submission. Setting Start to a real time **auto-populates End as +8.5 hours later** (a standard no-OT shift, clamped to 23:30 — no midnight wraparound modeled), which the player can still override manually. A Start-after-End on a day where both sides are real times, or any cell still `NotSet`, renders both that day's Start and End boxes in red (.66 alpha) and blocks submission with a toast.

**Per-row actions:** Name field placeholder reads "Shift Name (req.)"; typed name also mirrors into that row's "Start:"/"End:" labels. **Enter Info** (blue, matches the Start:/End: label color) validates/commits just that one row — "Shift Name is Required" / "Shift Name in Use" / "Please correct any errors before submitting the Schedule" toasts as appropriate. **Remove** (orange, far right, flush with the grid's right edge) now opens a small "Confirmation" / "Are you sure?" Yes/No sub-modal before deleting — no more instant delete. **Save & Close** (fire-hydrant red `#C1272D`) re-validates everything. The **✕** button is a separate **cancel/discard** path — "Confirmation" / "Exit? Changes may be lost." — Yes closes immediately with no validation (throws away unsaved edits), No cancels; it no longer silently behaves like Save & Close.

**Visuals (2026-06-26 pass):** restyled to match the Employee Roster / Hiring Board navy-and-blue aesthetic with Lilita One throughout (loaded via `AssetDatabase.FindAssets` in editor / `Resources.Load` in builds, same pattern as `LoadingScreenManager`); day headers show the full day name plus an absolute "Day NN" line underneath (computed from `ITimeService.Day` offset by each column's distance from "today"); title is centered; panel anchors **top-center** (not vertically centered) specifically so it can grow downward as shifts are added without running out of screen room — modal/shiftsContainer/each shift block all set `flexShrink = 0` and there's deliberately no `maxHeight`/scroll cap.

**Column-width gotchas worth knowing if you touch this file again:** the header banner row, the name/Enter-Info/Remove row, and each Start/End row are all pinned to the *same explicit* width (`LabelColWidth + DayColWidth*7 + SeparatorWidth*6`) because a plain `VisualElement` row otherwise defaults to stretching to fill its parent's cross-axis width, which silently let rows drift out of alignment with each other. Separately, `DropdownField` has its own content-driven min-width (popup arrow + longest time string) that can win over an explicit `width` and quietly grow a "fixed" column — fixed by wrapping each dropdown in a fixed-width `cellWrapper` (`overflow: Hidden`) with the dropdown's own `minWidth = 0`; the per-cell border was also moved off the dropdown onto a decorative `position: Absolute` overlay with zero layout weight so a border can't reintroduce the same drift. A related one: a column-flow `VisualElement`'s child does **not** auto-stretch along the vertical/main axis by default (only the horizontal/cross axis does) — the dropdown was leaving a gap at the bottom of its wrapper whenever the row stretched the wrapper taller than the dropdown's own natural height; fixed with `dropdown.style.flexGrow = 1`. That last fix compiled clean but has not yet been visually re-confirmed in-editor.

**Explicitly scoped as UI-only per Tad:** nothing here drives actual employee arrival/departure/overtime/attendance — `EmployeeRecord.shift`/`ShiftSchedule.cs`/`PayrollService` are completely unchanged and still what actually runs. Schedules built here are also **not persisted** — in-memory only, reset every Play session. A "Remove" button per shift and a "+ Add Shift" button were added as minimal necessary usability (not explicitly requested, but the UI is unusable without them).

**Planned follow-up, not built yet:** a second "side UI" for assigning individual employees to one of these named shifts (the Shift Manager only defines shift *templates* — Sun-Sat windows under a name — it doesn't touch any specific employee). When this and the gameplay wiring above both land, this whole system would replace `ShiftSchedule.cs`'s fixed Day/Evening/Night/Flexible windows with the player-defined ones.

**Confirmed, not changed:** Employee Roster (`EmployeeRosterUI.cs`) dragging was checked during this pass — it's already wired via the same shared `DraggableWindow` helper used here and by the Hiring Board (`DraggableWindow.cs`'s own doc comment specifically cites the roster's stretched top+bottom anchoring as the motivating case it was built to handle). No code change was needed there.

### Keybindings (rebound 2026-06-26; 6–8 revised 2026-07-29)

**1** = Dev Console (`ToolsWindowController`), **2** = Hiring Board, **3** = Employee Roster,
**4** = Employee List, **5** = Shift Manager, **6** = **Wholesale Contracts** (`ContractsPanel`),
**7** = Work Queue, **8** = New Item / Slotter. All via `Keyboard.current.digitNKey`, not the numpad.
Quicksave/load (F5/F6/F9) are unrelated F-keys, untouched.

**6 was Slot Assignment until 2026-07-29.** `SlotAssignmentPanel` now has no number key and is opened
by clicking a rack (`PlacementStateMachine` → `ShowForAisle`).

#### Panel exclusivity — how it actually works, and how it silently didn't

Opening any panel closes the currently open one. That is `UIKeyBindingManager.ToggleUI`, and it only
works for panels that are **registered** — which requires implementing `IUIPanel` AND a
`RegisterUI(n, panel)` call. Two long-standing bugs both traced to this:

1. **Panels 5, 7, 8, 9 were never registered.** `UIKeyBindingManager.Instance` was assigned only in
   `Awake`, but `UIBootstrapper.Awake` → `TopBarUI.Init` runs *earlier*, so every registration there
   hit a null Instance and was skipped without a word. `Instance` is now a **self-creating property**
   (adopts an existing scene instance first). Symptom was subtle: those panels still opened via their
   own key handling, but `CloseAll()` — i.e. **Tab** — ignored them.
2. **Keys 5–8 bypassed the manager entirely**, calling `panel.Toggle()` directly in `TopBarUI.Update`,
   so nothing ever closed anything. All now route through `ToggleUI`.

**Shift Manager exception:** it can hold unsaved edits, so it gets a veto. `ShiftManagerPanel.
RequestClose(onClosed)` closes silently when clean, and when dirty raises the existing
`ShowConfirmation` dialog; the requested panel opens inside the callback so it can't appear on top of
an unanswered prompt.

**Adding a new panel?** Implement `IUIPanel` (`Show`/`Hide`/`IsOpen`), call `RegisterUI`, and route its
key through `ToggleUI`. Skipping any of the three fails quietly.

### Inventory System — Milestone 1 (CORE COMPLETE — 2026-06-27)

**✅ COMPLETED: Core Data Structures**
- `PalletData.cs` — Pallet entity (SKU, qty, location, age, expiration); expiration detection
- `SkuData.cs` — Master product data (SO): pricing, shelf-life, Ti/Hi/CaseWeight, stacking rules, demand
- `SkuImporter.cs` — CSV-based importer (no COM deps); reads Days Supply + ItemSetup; cross-references by SKU; auto-generates pricing/shelf-life/stacking rules
- `InventoryService.cs` — Pallet lifecycle: create, move, pick, destroy; location indexing; daily spoilage checks; event publishing
- `ShipmentData.cs` + `ShipmentLineItem.cs` — Inbound tracking (supplier, ETA, line items, costs)
- `OrderData.cs` + `OrderLineItem.cs` — Order tracking (fulfillment, SLA, revenue, profit calc)
- `TestDataGenerator.cs` — Runtime test shipment/order generation from SKU database

**✅ COMPLETED: Excel Integration**
- **99 SKU assets** created from ItemFilesForForkIT.xlsx (Days Supply + ItemSetup sheets)
- Cross-reference validated: matched all Days Supply items with ItemSetup dimension data
- All assets populate Ti, Hi, CaseWeight, pricing, shelf-life categories
- Assets saved to: `Assets/_Project/Data/Inventory/SKUs/SKU_*.asset`

**✅ COMPLETED: Service Integration**
- InventoryService registered in GameContext.Awake(), initialized at startup
- Subscribes to OnDayChanged for daily spoilage detection
- Event system: OnPalletReceived, OnPalletMoved, OnPalletPartialPicked, OnPalletDestroyed, OnSpoilageDetected
- SkuDatabase loaded at runtime via `inventoryService.LoadSkuDatabase(skus)`

**⚠️ KNOWN ISSUE**
- Shadow atlas set to 16384×16384 to accommodate 240+ shadow maps; source unclear (no lights visible in Main.unity)
- Need to investigate on next session if warning persists

**📚 Documentation**
- `GAMEPLAY_LOOP_DESIGN.md` — 6-phase loop with mechanics, roles, 6-milestone roadmap, success metrics
- `MILESTONE_1_TECHNICAL_DESIGN.md` — Architecture, API, UI/UX specs, testing strategy, acceptance criteria
- `EXCEL_DATA_IMPORT_GUIDE.md` — Step-by-step workflow for CSV export and import

---

## Core Gameplay Loop (Active Development 2026-07-03)

**COMPLETE PALLET/CASE LIFECYCLE:** Vendor PO → Inbound → Putaway → Replenishment → Order Selection → Shipping → Invoicing

**See:** `GAMEPLAY_LOOP_SPECIFICATION.md` in memory for full design. This is the definitive spec Tad authored.

### Dummy Data (MVP Testing)

**SKU:** 035-12345 (Dummy Item)
- Description: Generic Cases
- Ti/Hi: 1/1 (one case per pallet)
- Weight: 50 lbs/case
- Cost: TBD / case
- Sell Price: Cost + margin

**Trailer:**
- Capacity: 12 pallets
- Layout: 2 front + 10 middle (side-by-side) + 2 rear (equidistant)
- No physics on pallets (visual only in trailer)

**Chep Pallet Prefab:**
- Visual representation (loaded cases)
- 12 instances per inbound trailer

### The 6 Chunks (Implementation Order)

**CHUNK 1: INBOUND PROCESS — data/event backbone BUILT 2026-07-03, not yet compile-verified (Unity Editor wasn't connected via MCP this session — open the Editor and check for `error CS` before relying on this).**

**What's built:**
- `WorkQueueSystem` (`Assets/_Project/Scripts/Core/Labor/WorkQueueSystem.cs`) — the critical-path task queue every chunk depends on. `WorkTask` (Id, Type, RequiredRole, PalletId, Description, Status: Pending/Assigned/Complete). `CreateTask()`, `GetPendingTasksForRole()`, `TryClaimNextTask()` (FIFO claim), `CompleteTask()`. Registered as an `IService` in `GameContext.Awake()` alongside the other services. `WorkTaskType` enum has stubs for all 6 chunks (Receive/Putaway/Replenish/OrderSelect/Load) even though only Putaway is produced today.
- `LoadIDGenerator` (`Assets/_Project/Scripts/Core/Labor/LoadIDGenerator.cs`) — static utility, generates unique 10-digit numeric Load IDs (dedup via a `HashSet` of issued IDs).
- `PalletData.LoadId` — new settable string property (null for pallets created before Load IDs existed, e.g. hand-placed in the Editor via `PalletInventoryTracker`).
- `InventoryService.ReceivePalletWithLoadId(skuId, quantity, shelfLifeDays)` — new method, parallels the existing `ReceiveShipment`/`RegisterPhysicalPallet` but assigns a Load ID and drops the pallet at receiving staging (0,0).
- `ReceivingService` (`Assets/_Project/Scripts/Core/Labor/ReceivingService.cs`) — static bridge: `ProcessReceiving(ShipmentData)` walks a shipment's line items, calls `ReceivePalletWithLoadId` for each (one pallet per line item, matching `TruckController.LoadShipment`'s existing distribution), then creates a `Putaway` WorkTask per pallet targeted at `EmployeeRole.ReachTruckOperator`. Marks the shipment `Received`. Guards against double-processing via `shipment.Status != InTransit`.
- **Wired into `TruckController`:** when a docked truck's existing `unloadDuration` timer (7s, already there — the visual pallets from `LoadShipment` were already spawned on dock) expires, `TruckController.Update()`'s `Docked` case now calls `ReceivingService.ProcessReceiving(AssignedShipment)` right before `BeginDeparture()`. This is the full inbound loop: PO (`ShipmentService.CreatePurchaseOrder`, already existed) → truck spawns/docks (already existed) → **NEW:** pallets get Load IDs + inventory records + Putaway tasks queued, right as the truck starts to leave.

**Deliberately NOT built this pass (prototype-first, per Tad's "small chunks" approach):**
- No Dock Stocker offload animation or Receiver clipboard/RF-gun animation — the truck's pre-existing unload timer stands in for physical offload. Animations are explicitly Tad's domain to drive later ("deeply involved" per his spec, especially for Order Selection in Chunk 4).
- No employee actually consumes a WorkTask yet — `WorkQueueSystem` only tracks task existence/status. Chunk 2 (Putaway) is what makes a Reach Truck Operator actually claim and act on these tasks.
- Guard-assigns-PO-to-trailer step is implicit (PO creates and spawns its own truck via `ShipmentService`/`TruckYardManager`) rather than a separate guard interaction.

**Next up: CHUNK 2 (Putaway)** — `PutawayLogic` (Pick vs Reserve based on order demand), `PickSlot`/`ReserveSlot` components on rack cells, and the first real employee-claims-a-WorkTask flow (Reach Truck Operator consumes the Putaway tasks this chunk now produces).

**CHUNK 2 (Offload / dock-stocker) — BUILT 2026-07-03, scripted-choreography first pass, NOT yet compile- or play-verified.** Moves all 12 pallets off a docked trailer into staging lanes with a manned dock stocker.
- **`TrailerOffloadController`** (`Assets/_Project/Scripts/Core/Labor/TrailerOffloadController.cs`) — self-bootstrapping service (hidden `DontDestroyOnLoad` object, like the other dock services). Polls (0.5s) for a `TruckController.AwaitingOffload` truck **plus** an idle, manned dock stocker (an `MHEOperatorSlot` whose `CurrentOperator.Record.role == DockStockerOperator`). On a match it `ClaimForOffload()`s the truck, **commandeers the DS** (disables its patrol `AiNavigation` + `NavMeshAgent` so the controller drives the transform directly — same scripted style as `TruckController`'s yard route; the operator rides along as a DS child), then per pallet: line up behind it → drive forks under → lift the `Forks` child by `ForkLiftHeight` (reparent pallet to forks) → reverse out onto the dock → 180° spin → drive forks-first to the next open Inbound/Both lane slot → lower → drop → `RegisterPhysicalPallet` + assign Load ID + file a `Putaway` `WorkTask`. When all 12 are done it restores the DS (re-enable agent/nav, warp, resume patrol) and `CompleteOffload()`s the truck so it departs.
- **Movement is scripted transform choreography** (chosen over NavMesh — Tad's call — for frame-precise fork alignment and to avoid fighting the patrol AI that owns the agent). Trade-off: no obstacle avoidance on the dock→lane hop.
- **All geometry is TUNABLE constants at the top of the file** (`DriveSpeed`, `TurnSpeed`, `ForkLiftHeight=0.5`, `ForkEngageDistance`, `LineUpBackDistance`, `BackOutExtra`, `LaneStackStep`, `InvertTrailerAxis`, `ForkChildName="Forks"`). Because the controller is a hidden bootstrapped object there's no Inspector — **these are code constants tuned over chat and recompiled**; the maneuver's directions/distances are educated guesses and WILL need iteration in-game (esp. `InvertTrailerAxis` — flip it first if the DS drives away from the trailer). Promote to a scene component with `[SerializeField]`s if live slider tuning becomes worth it.
- **`TruckController` dock lifecycle reworked:** a docked truck no longer departs on the old fixed `unloadDuration` timer. It now sets `AwaitingOffload` and waits in `Docked` until `CompleteOffload()` is called, OR until `offloadFallbackTimeout` (45s) elapses **unclaimed** — the fallback path does the old bulk `ReceivingService.ProcessReceiving` + departs so a missing/unmanned DS can't wedge the dock. New public API on `TruckController`: `AwaitingOffload`, `DockedAt`, `LoadContainer`, `ClaimForOffload()`, `CompleteOffload()`.
- **Receiving/putaway-task creation moved to per-pallet-on-drop** (was bulk-on-dock-timer in Chunk 1). Each staged pallet gets its own inventory record + Load ID + Putaway task as it lands in the lane. The bulk `ReceivingService.ProcessReceiving` now only runs on the no-DS fallback path.
- **`LaneNamingService.AllLanes()`** added — returns every distinct `(door, lane-letter)` pair, so the controller can scan for the first Inbound/Both lane (`InventoryService.LaneAcceptsPutaway`) with a free slot (`TryGetNextFreeSlot`). Reminder: lanes default to `LaneUsage.Both` until a setup UI says otherwise, so any placed `Flr-ShipLane` tiles already accept inbound putaway.
- **Prerequisites to actually see it run:** (1) a dock stocker placed on the dock with a hired **DockStockerOperator** aboard (the existing hire/auto-board flow boards them — the controller needs the slot OCCUPIED, so if the operator is still walking over it just waits); (2) at least one row of `Flr-ShipLane` tiles placed as the staging lane; (3) the `palletVisualPrefab`/`Ghost While Docked Renderers` Inspector assignments from Chunk 1 done so there are pallets to move. Spawn a truck with the InboundTest panel and watch.

**Manual test tool — `InboundTestPanel`** (`Assets/_Project/Scripts/UI_UX/InboundTestPanel.cs`, added 2026-07-03): self-bootstrapping DEBUG-only OnGUI overlay (top-left, y=50 to clear the TopBar, always on in Play mode) with a button that calls `ShipmentService.CreatePurchaseOrder` with a 12-line-item test PO (SKU `035-12345`) — same hotkey pattern as the P key. Lets you watch the whole inbound loop live: truck spawns → gate queue → guard inspection (trailer doors open via existing `GuardController` behavior) → drives to dock → sits `Docked` with its pre-built cargo → `ReceivingService` fires as the timer ends → departs. The panel also lists live `ShipmentService.PendingShipments` (status flips `InTransit`→`Received`) and `WorkQueueSystem.Tasks` (the Putaway tasks created per pallet) so you can confirm Load IDs/receiving worked without reading the console. Remove or gate behind a build flag before shipping — not intended as a real gameplay UI.

**Visible cargo — built 2026-07-03, needs ONE manual Inspector step before it'll show anything.** Previously the trailer's `Trailer/LorryTrailer/Load` container had **zero `PalletBuilder` children** — `TruckController.LoadShipment()` always hit its "no PalletBuilder components found" branch and silently returned, so no cargo ever rendered regardless of SKU data. Also nothing anywhere ever called `InventoryService.LoadSkuDatabase()`, so `GetSkuData()` always returned null even for real SKUs. Fixed both:
- Created `Assets/_Project/Resources/Inventory/SKUs/SKU_035-12345.asset` (a real `SkuData` asset — Ti/Hi 1/1, non-perishable, `_casePrefab` pointing at the existing `Cs_Reg_Brn.prefab`). Placed under `Resources/` specifically so it's loadable at runtime (the other 99 Excel-imported SKUs live outside `Resources` and still aren't loaded — separate follow-up if they need real case visuals too).
- `GameContext.Awake()` now calls `inventoryService.LoadSkuDatabase(Resources.LoadAll<SkuData>("Inventory/SKUs"))` right after `inventoryService.Initialize()` — this call never existed anywhere before.
- `TruckController.LoadShipment()` rewritten: no longer searches for pre-existing `PalletBuilder`s. It now **procedurally instantiates a new `[SerializeField] palletVisualPrefab`** 12 times under `Load`, in 2 rows of 6 (`SlotLocalPosition()` — row 0 = left at `-palletLateralOffset`, row 1 = right at `+palletLateralOffset`, columns spaced by `palletRowSpacing` centered on the Load anchor), cycling through the shipment's line items so a 12x-same-SKU test shipment fills every slot. Each instance gets its `PlacedObject`/`BuildingData` stripped (same reasoning `PalletBuilder.Build()` already applies to the cases it spawns — otherwise 12 phantom (0,0) registry entries per truck) before resolving `SkuData.CasePrefab` and calling `PalletBuilder.Build(deductMoney: false)`.
- **`driverDoorOpenAngle`/`passengerDoorOpenAngle`** (new tunable fields, default ±170°) replace the old hardcoded ±65° swing — a proper "barn door" opening (doors rest flat against the trailer side) instead of just ajar, per Tad's ask.

**⚠️ `palletVisualPrefab` is a new field, currently unassigned — deliberately left for a manual step rather than hand-edited via YAML.** `ChepStack.prefab` (the intended value) is itself a multi-layer nested prefab variant chain; hand-deriving its correct root `fileID` for a raw YAML reference was judged too risky to guess blind without Editor confirmation. **To finish this: open `Truck_SavageDev.prefab`, select the root TruckController component, and drag `Assets/_Project/Prefabs/Inventory/ChepStack.prefab` into the new "Pallet Visual Prefab" field.** Until that's set, `LoadShipment()` logs a clear warning and leaves the trailer empty rather than failing silently. `palletLateralOffset`/`palletRowSpacing` are now **both 0.35** (tightened 2026-07-03 from 0.75/1.6 — the old spread clipped through the trailer edges); `LoadShipment` also now finds the `Load` container via the `Trailer/LorryTrailer/Load` path OR a recursive `FindDeepChild(transform,"Load")` fallback (the container is inside the nested trailer FBX so the exact path can drift), and every pallet is childed under that `Load` object.

**Docked finishing touches (2026-07-03) — barn doors open while docked + trailer ghosts:**
- Door open angle raised to **±185°** (`driverDoorOpenAngle`/`passengerDoorOpenAngle`, was ±170) — full flat "barn door" swing so a docked dock-stocker can reach the cargo and it's visible from inside the warehouse.
- `OnDocked()` now calls `OpenTrailerDoors()` + `SetDockedGhost(true)`; `BeginDeparture()` calls `SetDockedGhost(false)` + `CloseTrailerDoors()`. (Previously the doors only opened during the guard's gate inspection, then closed before docking — so a docked trailer was sealed.)
- **`SetDockedGhost`** swaps a set of assigned renderers to `Resources/Materials/GhostLoweredWall.mat` (the same see-through material lowered walls use) while docked, restoring each renderer's original `sharedMaterials` on departure. Handles multi-slot renderers and is idempotent. Original materials cached per-renderer in `_originalMaterials`.
- **⚠️ Second manual Inspector step (same reasoning as `palletVisualPrefab`): the new `_ghostWhileDockedRenderers` (Renderer[]) field is unassigned.** The trailer mesh + left/right Savage decal objects live inside the binary `Truck_SavageDev.fbx`, so their exact child names/`fileID`s aren't extractable from YAML to wire blind. **Assign it: open `Truck_SavageDev.prefab`, and drag the trailer mesh renderer + the two Savage decal renderers into the TruckController's "Ghost While Docked Renderers" list.** Until assigned, docking still opens the doors but the trailer stays solid (no ghost).

**Slotting (Pick Slot Assignment) — BUILT 2026-07-04, compile-verified via Unity MCP reflection check (console tool itself was broken this session — see MCP bridge notes below), not yet play-tested.** Prerequisite data/UI layer for Chunk 2 (Putaway) — deliberately built BEFORE any Putaway consumption logic per Tad: "I want the putaway logic to be based around the location of the pick slot and then get the reserves of that item as close to it as possible." Assignment is manual (a UI panel, not auto-assigned on receipt); a SKU may be assigned to multiple pick slots.

- **`SlotRegistry`** (`Assets/_Project/Scripts/Racking/SlotRegistry.cs`) — self-bootstrapping scan registry, same shape as `LaneNamingService`/`DockNumberingService` (hidden `DontDestroyOnLoad`, recomputes on `OnObjectPlaced`/`OnObjectDeleted` + 1s heartbeat). Every **live** rack has exactly 2 active `TextMeshPro` labels (the aisle-facing pair — the far face's group is disabled); the label TEXT itself ("01-02-00") is parsed directly for aisle/bay/level-char/position rather than re-derived from geometry, since that text is the only place the position digit (0/1) is ever stored. Pick vs Reserve = numeric vs alphabetic level-char (matches `LocationNameGenerator`'s own convention). Exposes `PickSlots`/`ReserveSlots`/`TryGet(address)`/`TryGetNearestReserveSlot(pickAddress)` (XZ distance between rack root positions — a stub for Chunk 2 to consume, nothing calls it yet).
- **`SlotAssignmentService`** (`Assets/_Project/Scripts/Core/Inventory/SlotAssignmentService.cs`) — plain static `Dictionary<address, skuId>`, same in-memory-only shape as `LaneConfigRegistry`. Deliberately has no dependency on `SlotRegistry` — the UI is responsible for only assigning live addresses and pruning assignments whose rack has since been deleted (`SlotAssignmentPanel.PruneOrphanedAssignments`, run on every rebuild).
- **`SlotAssignmentPanel`** (`Assets/_Project/Scripts/UI_UX/SlotAssignmentPanel.cs`) — bound to the **`6`** key (TopBarUI, same `Keyboard.current.digitNKey` + `UIModalGuard` pattern as Shift Manager's `5`). Programmatic UI Toolkit panel (ShiftManagerPanel's pattern, not RackSetupUI's uxml pattern — avoids that pattern's three documented timing footguns; this panel is a data-list-and-dropdown utility, not bespoke art). Two tabs: **By Location** (every Pick slot grouped by aisle, dropdown to assign/reassign, Clear button) and **By SKU** (pick a SKU, see/unassign all its slots, plus the **"Available Pickslots"** button below).
- **`PickSlotOverlayController`** (`Assets/_Project/Scripts/Racking/PickSlotOverlayController.cs`) — spawned on demand by "Available Pickslots" (not a persistent singleton). Recolors every live Pick slot's in-world TMP label: **red** = already assigned to some SKU (regardless of physical pallet occupancy — occupancy tracking doesn't exist until Putaway is built), **yellow** = unassigned but the candidate SKU's full pallet is too tall for that level, **green** = unassigned and fits. A small `BoxCollider` (trigger) + marker component is added to each label just for the overlay's duration (same click-detection shape as `ChevronController`: `Camera.main` + `Mouse.current` + `Physics.Raycast`); clicking a label commits the assignment immediately, no separate confirm step.
- **Height-fit math**: `SkuData.RequiredPalletHeight() = PalletBuilder.GetPrefabDimensions(CasePrefab).y * HiCount + 0.16f` (0.16m = pallet height, Tad's constant), compared against `ObjDataSO.objHeight` (already existed, already authored per rack asset — no new data needed). **`HiCount` = layers per pallet**, confirmed by reading `PalletBuilder.Build()`'s actual usage (`layers = manualHi`) — the inline comments on `SkuData.HiCount`/`SkuImporter`'s `Hi` field ("units per pallet layer") are backwards relative to how the code actually uses it; worth a comment fix sometime, not a behavior bug. `PalletBuilder.GetPrefabDimensions` was promoted from `private` to `public static` to enable reuse (it was already stateless).
- **Known limitation, not silently solved:** in-memory only, same as `ShiftManagerPanel`'s schedules — `SlotAssignmentService` is lost on domain reload/app restart. A future `Export()`/`Import()` pair (same shape as `LaneConfigRegistry`'s) is the natural follow-up once there's a save slot for it. `GameContext.Awake()` calls `SlotAssignmentService.ClearAll()` defensively in case a same-process reload path ever exists.
- **Not yet play-tested in Editor** (only compile-verified) — still needs: confirming the label recoloring actually reads clearly (the in-world "label" may be a TMP text mesh plus a separate background plate renderer — this only recolors the TMP `color`, not a plate, if there is one), confirming the `BoxCollider` click-target size feels right, and a real assign/unassign/reopen pass with committed racks in-scene.

**CHUNK 2: PUTAWAY PROCESS**
- Work queue: "Put pallet [Load ID] away"
- Reach Truck Operator drives pallet jack
- Determines Pick Slot vs Reserve based on order demand — **now backed by `SlotRegistry`/`SlotAssignmentService` above** instead of needing new data
- Sets down pallet, updates location
- Inventory system updated

#### 2026-07-18 Session — Reach-truck putaway "stairwell" livelock fixed; capacity issue still open

**IN PROGRESS — code changes committed/pushed on branch `PreRack_2ndAttempt`, NOT yet play-verified.** Continue here on the other machine.

**Symptom Tad reported:** reach truck picking up from a staging lane would, at ~the 4th/5th slot in, drive deeper and deeper and "nudge the pallet up like a stairwell" until it floated in space. I mis-diagnosed it TWICE as an approach/aim bug (rewrote the fork-insert drive) before pulling `Editor.log` telemetry, which showed the truth:

**Actual root cause = a no-rack-space LIVELOCK, not the drive.** Log sequence: 8 pallets put away fine, then `[PutawayLogic] Limbo fallback: no Available reserve slots in the entire building` (warehouse full; those SKUs also had *no pick slot assigned*, so all flooded reserve). Then the RTO **physically picked up** the pallet and only AFTER it was on the forks did `PutawayLogic.AssignPutawayDestination` return null → the abort did `SetParent(null, worldPositionStays:true)`, dropping the pallet at the lifted fork height (+~0.08m). Re-queue → re-pick → +0.08m per loop = the stairwell climb. Two tasks for the same exit slot also churned against each other.

**Fixes applied to `ReachTruckOperator.cs`:**
1. **Resolve + reserve the rack destination BEFORE the physical pickup** (moved `AssignPutawayDestination` ahead of the drive/lift). No destination → the pallet is never touched: park it in the lane, block it, bail.
2. **`_blockedUntil` dict + `NoDestinationBackoff` (15s)** — a pallet that found no destination isn't re-claimed immediately (checked in BOTH claim paths in `TryClaimAndStart`: the `alreadyAssigned` resume and the pending loop). Kills the livelock; retries every 15s in case space frees.
3. **Every post-pickup abort restores the pallet's captured original pose** (`originalPalletPos/Rot`) and calls `CancelPutaway(toAddress)` to release the reserved slot → a pallet is never left floating.
4. **`task.AssignToLocation(toAddress)` is stamped only AFTER the pallet is seated on the forks** — preserves the save/load contract (`MHEOperatorPersistenceService` routes `ResumeDeliverToRack` vs `ResumeTask` off `task.ToLocation` ⇔ a carried-pallet snapshot; stamping it pre-pickup would misroute a save taken in that window).
- Kept an earlier **closed-loop acquire** rewrite (homing to a staging point 0.6m in front → square up → short capped `InsertToGrab` with retry). It WORKS (the 8 successes went through it) and is more robust than the old open-loop `DriveForksFirst`+`DriveToGrab`, which are now **dead code** in this file. Not the bug, but fine to keep.

**STILL OPEN — the real blocker to actually finish Chunk 2:** running out of reserve slots after only **8** pallets is suspiciously low for the rack setup in Tad's screenshots. Next step: determine whether `SlotRegistry.ReserveSlots` is under-registering, `LocationStatusRegistry` is marking most slots unavailable, or pick slots simply aren't assigned (Slot Assignment panel = key **`6`**). Confirm via `Editor.log` after a run.

**Bridge/verification note (important, wasted time this session):** the Unity MCP bridge here (CodeMaestro executor) supports `manage_editor` / `manage_scene` / `find_gameobjects` / resources, but **`read_console`, `manage_console`, `execute_code`, `validate_script`, `refresh_unity` all fail** ("Unknown/unsupported command" or "No Unity Editor instances found"). To verify compiles/runtime: read `%LOCALAPPDATA%/Unity/Editor/Editor.log` from disk (grep `error CS`, `[ReachTruckOperator]`, `[PutawayLogic]`, `[TrailerOffload]`), and compare `Library/ScriptAssemblies/Assembly-CSharp.dll` mtime vs the source `.cs` mtime to confirm a build actually happened.

**Also changed `TrailerOffloadController.cs` (dock stocker) same session, also unverified in-play:** (a) trailer grab order is now depth-then-Y-descending so it takes the **top** of a stack first (was leaving top pallets hanging); (b) lane-fill `TryFindLaneTarget` rewritten to **walk in from the entry and stop at the first occupied slot** (stack the frontier if it has room, else the slot before it) so the DS can't route *through* occupied lane positions.

#### 2026-07-25 Session — Root cause of "capacity issue" found: LocationData (Inspector) and LocationStatusRegistry (real gatekeeper) are two separate stores that only sync one-way

**Answers the "running out of reserve slots after only 8 pallets" question left open 2026-07-18 above — it was never really a capacity/SlotRegistry problem.** `SlotRegistry`/`LocationRegistry` were registering slots fine. The actual bug: `PutawayLogic` only ever checks `LocationStatusRegistry` (a static `Dictionary<string,LocationStatus>`, not visible anywhere in the Inspector, but IS the thing saved/loaded via `PlacementSystem.cs`'s `save.locationStatuses`). `LocationData` (the MonoBehaviour on each rack slot child — what you actually see in the Inspector) is a fresh, **non-persisted** object every session that defaults to `Available`. `LocationData.Reserve()/Occupy()/Release()` correctly mirror into the registry, but `PutawayLogic.AssignPutawayDestination()`/`CompletePutaway()` and `ReplenishmentService.CreateReplenishTask()` (which locks both a pick and reserve slot the instant a task is *created*, before any truck claims it) write **directly** into the registry, bypassing `LocationData` entirely. Net effect: the registry (what the reach truck actually obeys) and the Inspector (what you look at) can silently disagree, and the Inspector always loses the argument.

**Live-diagnosed via `Unity_execute_code` reflection against Tad's actual running scene (186 total rack slots: 146 reserve, 40 pick):**
- **2 slots permanently stuck `Reserved`** (`01-02-D0`, `01-08-C0`) with zero matching WorkTask anywhere in the 66 live tasks — orphaned. Root cause: a `Reserved` lock written into the registry survives a save (`PlacementSystem.cs` exports it) and gets re-imported on the next load, but if the owning task didn't survive (didn't stamp `ToLocation` before the save, or its coroutine died to a deleted vehicle/fired employee/domain reload), nothing is ever left to call `CompletePutaway`/`CancelPutaway` on it again — permanently "full" to `PutawayLogic` forever.
- **50 of 186 slots (27%!) showed `LocationData.Status = Available` with a blank `PalletId` while the registry said `Occupied`.** This is almost certainly `ReachTruckOperator.DeliverPalletToRack` calling `locationTr.GetComponent<LocationData>()` on whatever Transform `FindLocationTransform` resolved — and `FindLocationTransform` has documented fallback branches ("last resort: bare direct child, no anchor" / global `GameObject.Find`) that return a Transform **without** a `LocationData` component for racks whose prefab doesn't match the expected label structure (see the `Rack-FullYellow48` mirrored-label-group note earlier in this doc). When that happens, the `if (locationData != null)` guard silently no-ops the Inspector-side `Occupy()` while `_putawayLogic.CompletePutaway()` right below it unconditionally marks the registry Occupied anyway. Net result: a real, correctly-placed pallet's slot can permanently read "Available" in the Inspector.
- **Reserve availability at time of check: 0 of 146 available** (144 Occupied + 2 orphaned Reserved) — console showed `PutawayLogic` repeatedly hitting limbo fallback ("No available reserve found ... using limbo fallback") for multiple SKUs. Whether those 144 "Occupied" reserves are all genuinely full (real pallets — meaning the reach truck's behavior was correct all along and this was purely a display bug) or include some phantom/stale entries (registry says Occupied, no physical pallet backs it) was **not fully confirmed** — Unity exited Play Mode mid-session when the fix's script edits triggered a domain reload (per [[domain-reload-kills-live-services]]), ending the live diagnostic before that specific check could run. Re-verify next session: re-enter Play, reload the save, and spot-check a few of the addresses above (e.g. `01-02-A0`/`01-02-A1`/`01-02-B0`) against whether a pallet is actually physically sitting there.

**Fixes applied (compiled clean, verified via `read_console`):**
1. **`LocationStatusRegistry.ReleaseUnclaimedReservations(ICollection<string> claimedAddresses)`** (new method) — releases any `Reserved` address not referenced as a `FromLocation`/`ToLocation` by a non-`Complete` WorkTask. Wired into `PlacementSystem.cs` right after `LocationStatusRegistry.Import(save.locationStatuses)` on load, using the just-restored `workQueueSystem.Tasks`. Cleans up orphaned Reserved locks every time a save is loaded, going forward.
2. **`LocationData.SyncStatusDisplay(LocationStatus status)`** (new method, sets `_status` directly, does NOT write back to the registry) — called from `LocationRegistry.Recompute()` (already running on its existing ~1s heartbeat) for every live address, pulling the true value from `LocationStatusRegistry.Get(address)`. This is a blanket fix: it keeps the Inspector honest relative to the registry regardless of which upstream code path set the registry value, so this exact class of "Inspector says Available, real gatekeeper disagrees" confusion can't recur even from some other future write path.

**Still open / needs next-session confirmation:** whether any of the 144 "Occupied" reserves are phantom (no real backing pallet) rather than genuinely full. If Tad finds any address that shows "Available, no pallet physically there" even AFTER these fixes and a fresh reload, that's a third, distinct bug (bad occupancy data with no orphan-reservation or display-staleness explanation) and needs its own investigation — likely starting with `LocationRegistry.ReconcilePhysicalOccupancy()`'s 0.25m physical-proximity tolerance possibly being too tight for some rack/pallet placements.

**CHUNK 3: REPLENISHMENT PROCESS**
- Pick slot monitored for occupancy
- Threshold: <2 cases remaining
- Work queue: "Replenish [location]"
- Reach Truck Operator fills from reserve
- Pick slot stays stocked

**CHUNK 4: ORDER SELECTION PROCESS**
- Customer order arrives (e.g., 12 pallets)
- Empty outbound trailer backed into assigned door
- Order assigned to door/trailer/staging lane
- Work queue: "Order [ID] ready for selection"
- Order Selector picks cases (hand animation)
- Max 2 pallets per selector jack
- Cases placed on jack, staged to outbound lane
- Inventory updated (staging lane location)

**CHUNK 5: SHIPPING PROCESS**
- Work queue: "Load order [ID]"
- Loader animation (walking + placing)
- Pallets moved from staging to trailer
- Trailer status: "Ready to depart"
- Driver departs (visual: trailer exits yard)
- Order status: "Shipped"
- Triggers invoicing

**CHUNK 6: INVOICING PROCESS**
- Customer charged: Cost of Goods + $1/case handling fee
- Revenue credited to MoneyService + FinanceCategory (Sales)
- Order marked "Invoiced"
- Daily summary updated

### Work Queue System (Cross-Cutting)

All actions driven by work queue entries:
- Signals employees via events
- Tracks task progress
- Auto-closes when complete
- Prioritization: TBD (FIFO initially)

### Key Components (TBD During Architecture Review)

- `POSystem` — Purchase order creation/tracking
- `TrailerArrivalEvent` — PO match, signals dock
- `ReceiverAnimationService` — Clipboard + RF gun
- `LoadIDGenerator` — 10-digit pallet IDs
- `InventoryEntity` — Pallet in system (SKU, qty, load ID, location)
- `WorkQueueSystem` — Task queuing + assignment
- `PickSlot` — Component on rack cells; replenishment triggers
- `ReserveSlot` — Component on reserve racks
- `OutboundTrailerAssignment` — Trailer ↔ Door ↔ Staging Lane
- `OrderSelectionTask` — Order picking progress
- `OrderSelectorAnimation` — Hand-picking + jack load
- `OutboundStagingLane` — Per-door staging zone
- `LoaderTask` + `LoaderAnimation` — Loading animation
- `InvoicingService` — Calculate charges, credit revenue

---

## TODO

Things that need to be built, in rough priority order. Move items here as they come up and remove them when done.

**Detailed gameplay loop design:** See `GAMEPLAY_LOOP_DESIGN.md` for complete mechanics, phasing, and technical architecture.

**NEXT UP — Lighting System (real-time fixtures)** — see [IDEAS → Lighting System](#lighting-system-next--starting-2026-07-03). Purchasable/placeable light fixtures with real-time lights that illuminate the warehouse and affect worker safety/accuracy; enables safe night-shift work. Tad's last building-block feature before the core loop.

**Phase 1: Receiving & Putaway (Data layer DONE — UI/Services NEXT)**
- [x] Pallet data structure and InventoryService — COMPLETE
- [x] 99 SKU assets with cross-referenced Excel data — COMPLETE
- [x] Test data generator (shipments + orders) — COMPLETE
- [ ] ShipmentService — Manage inbound queue, trigger receiving tasks
- [ ] Receiving task UI — Display incoming shipments, "Scan & Receive" workflow
- [ ] Putaway mechanics — Task assignment, employee moves pallets to storage
- [ ] Storage location tracking — Cell-based capacity, pallet validation

**Phase 2: Order Selection & Fulfillment**
- [ ] Customer order generator
- [ ] Order queue UI and prioritization
- [ ] Picking task assignment to employees
- [ ] Inventory decrement on item pick
- [ ] Partial-order handling (backorder, split, cancel)

**Phase 3: Staging, Loading & Shipping**
- [ ] Staging area with capacity constraints
- [ ] Truck scheduling and assignment UI
- [ ] Load planning (assign orders to trucks)
- [ ] Truck departure trigger and visual feedback

**Phase 4: Revenue & Financial Integration**
- [ ] Revenue calculation on shipment departure
- [ ] Invoice UI showing order → revenue detail
- [ ] Daily profit/loss reporting integration with FinanceCategory

**Phase 5: Perishable Spoilage**
- [ ] Expiration date tracking per pallet
- [ ] Daily spoilage check and contamination state
- [ ] Write-off loss calculation

**Phase 6: Employee AI for Warehouse Tasks**
- [ ] Task assignment system (Receive, Putaway, Pick, Load tasks)
- [ ] Pathfinding to task locations in warehouse
- [ ] Task priority queuing
- [ ] Task completion detection (pallet moved, order picked, truck loaded)

**Post-MVP (Deferred until core loop is solid)**
- [ ] Ordering system — suppliers, lead times, restock triggers
- [ ] Move save files out of `Assets/_Saves/` to `Application.persistentDataPath` (required before any real build/release)
- [ ] Replace `FindFirstObjectByType` calls in `PlacementStateMachine.Start()` with proper scene references
- [ ] Review and playtest difficulty balance (starting capital + sell-back rate per level)
- [ ] Wire `ShiftManagerPanel` schedules into actual gameplay (replace `ShiftSchedule.cs`'s fixed windows), add persistence
- [ ] Reconcile day-of-week conventions: `ShiftManagerPanel` uses Sun=0..Sat=6, `EmployeeWorkSchedule` uses Mon=0..Sun=6
- [ ] Wire `EmployeeStatSystem.TickDay` to day-change event (currently dormant)

---

## BUGS & ISSUES

Known problems that need fixing. Add to this list as issues are discovered.

### CRITICAL — Post-Refactor Audit (2026-06-26) — FIXED ✅

- **EVENT UNSUBSCRIPTION IN BUILDSTATE** — ✅ CONFIRMED ALREADY FIXED in refactor. Input callbacks are properly unsubscribed in BuildState.OnExit() (line 138-139).

- **STATIC EVENT MEMORY LEAK IN EMPLOYEESPAWNER** — ✅ FIXED (2026-06-26). `MHEPlacementEvent.OnMHEEquipmentPlaced` subscriptions are now tracked in a dictionary and unsubscribed when the employee is removed via `OnEmployeeRemoved()` handler. This prevents dead closures and memory leaks from accumulating over playtime.

- **SERVICELOCATOR RETURNS NULL INSTEAD OF THROWING** — ✅ FIXED (2026-06-26). `ServiceLocator.Get<T>()` now throws `InvalidOperationException` when a service isn't found, making initialization failures fail fast and clearly instead of silently returning null.

### IMPORTANT — Post-Refactor Audit (2026-06-26)

- **FindAnyObjectByType in Hot Path** (`EmployeeAssignmentService.cs` lines 71-76) — O(n) search called on every employee assignment. Should cache spawner reference at initialization.

- **Try-Catch Silent Swallow** (`DeleteCommand.cs` line 237) — Bare catch block with no logging swallows all exceptions. Add exception logging to aid debugging.

- **HiringBoardUI Event Unsubscription Missing** — UI callbacks registered in `OnEnable()` but not unregistered in `OnDisable()`. Toggling panel causes duplicate callbacks. Add `OnDisable()` with callback unregistration.

### EXISTING ISSUES

- Save files currently write to `Assets/_Saves/` — this works in the Editor but will break in a built player. Must be moved to `Application.persistentDataPath` before shipping.
- `SimulationTimeService` time scale (1 real-second = 1 in-game-minute) is a placeholder — needs tuning/configurability before gameplay feels right.
- **ToolsWindow (`Assets/3. UI/7.ToolsWindow/`) is non-functional** — none of the following work: tilde toggle, X close button, tab switching, panel drag. Mouse scroll in the panel also bleeds through to the game camera. Root cause likely: PanelSettings misconfiguration, `Start()` silently failing before event wiring runs, or UIDocument not blocking input. Needs full debug pass.
- **One-time placement costs aren't `FinanceCategory`-categorized for lifetime totals** — `PlaceCommand`'s `_money.Deduct(_data.cost, _data.category)` tags the deduction with the raw `ObjDataSO.category` string into `_lifetimeExpenses`/`_lifetimeDetail`, which doesn't match any `FinanceCategory.ExpenseOrder` entry. The dollars are real and now correctly shown in the **Spent Today → Purchases** list (fixed 2026-06-25/26), but the **Hourly tab's lifetime per-category breakdown still won't include them**. See [Economy & Financial Reporting System](#economy--financial-reporting-system).
- **`EmployeeStatSystem`'s daily fatigue/morale tick is dormant** — `TickDay`/`TickDayForAll` are never called except from their own Editor debug context menu. The new overtime-doubles-fatigue rule is implemented correctly inside it but has zero effect in actual play until it's wired to a real day-change event. `GetCurrentDayOfWeek()` is also still a real-world-`DateTime.Now` placeholder, not tied to the in-game calendar.

---

## Session 2026-07-29 — Contracts (demand origin), order archiving, and a cluster of UI-wiring bugs

### Contract system — where demand comes from in a real build

Replaces the Dev Console's "Create Test Order" button as the player-facing source of orders (that
button stays as a debug override and should NOT be removed).

- **`ContractData`** (`Core/Inventory/`) — SO holding one customer's commercial terms. Deliberately a
  SEPARATE asset from `CustomerData`: that is identity (name/description/icon, 26 assets already
  authored), this is an offer, and the same customer should be able to appear as a small starter
  account early and a punishing one later.
  - `ContractKind.Recurring` — a standing account; rolls mixed case-pick orders daily at `CutoffHour`.
  - `ContractKind.OneOffWholesale` — signed once, delivers once. **Full pallets only**: every line item
    is exactly `Ti x Hi` cases of one SKU, never a partial layer or loose case (Tad's explicit rule).
  - `PayRateMultiplier` scales `SellValue` and IS honoured. **`LateFeePercent` is displayed on cards
    but NOT enforced** — `OrderService.OnDayChanged` still charges the flat `LateFeePercentClerk`
    (25%). Known loose end.
- **`ContractRegistry`** — `Resources`-loaded by name (`ContractRegistry.Load()`) so it resolves in a
  BUILT PLAYER; `AssetDatabase` lookups are editor-only. Asset lives at
  `Assets/_Project/Resources/ContractRegistry.asset`.
- **`OrderArrivalService`** (`IService`, registered LAST in `GameContext` since it resolves the others
  out of the locator) — subscribes to `OnHourChanged`, not `OnDayChanged`: orders landing at a cutoff
  hour create the daily rhythm; midnight arrivals give the player no deadline to feel. `OnDayChanged`
  remains right for the E1 fine sweep.
  - `SignedContract.LastGeneratedDay` is persisted via a new `SaveData.contracts`. Without it a
    save/load either re-runs a day's arrivals or silently skips one.
  - **`Active` means "taken", nothing else.** A delivered wholesale deal stays Active forever; what
    stops it re-firing is the `IsWholesale` skip in `OnHourChanged`. Clearing `Active` (the original
    mistake) made a delivered one-off read as never-signed to `IsSigned`, so the card kept its Sign
    button and every click produced another full trailer.
- **`ContractsPanel`** (key 6) — card list using `CustomerData.Icon` directly (all 26 already
  assigned). Draggable by its title bar; centres on first open only. Matches the house palette
  (navy #141C26, blue border #5C9BC4, orange #B5743A, Lilita One).
  - Card values are written `+$X` — a leading tilde for "approximately" reads as a MINUS sign at this
    font size and made every contract look like a cost.

**KNOWN GAP:** wholesale orders have full-pallet quantities but fulfilment still runs through the
case-pick selector, which walks a 12-pallet trailer off one case at a time. The mechanic that moves
whole pallets from reserve to the staging lane does not exist. This is the main thing standing between
wholesale and it feeling like wholesale.

### Terminal-order archiving (prerequisite for automatic arrivals)

`OrderService` used to keep Shipped/Cancelled orders in `_activeOrders` forever — 67 observed in one
session, nearly all terminal. That list feeds `WorkQueuePanel.BuildLiveSignature` (a string
concatenation over every active order, **every 250ms**), the stage-ownership gates, and
`TryPlanStageSpread`, and it is serialised into every save.

- `Archive(order)` moves an order to `_orderHistory` **the instant it goes terminal** — from
  `ShipOrder` (before `TryReleaseDoorIfClear` asks whether the door still has work, so a just-shipped
  order cannot hold its own trailer) and from `CancelOrders` (safe: that loop walks `orderIds`, not
  `_activeOrders`).
- Bounded by COUNT (`MaxArchivedOrders = 250`), not age — `OrderData` records no closed-on day, and
  save size tracks record count anyway.
- `Export` writes active + history into the SAME `OrderSnapshot` list; `Import` sorts them back by
  Status. No schema change, and **old saves migrate themselves** on next load.

### Pallet rotation — the 180 degree flip, and the fix that made it worse

Symptom: pallets visibly spun 180 degrees on the Y axis when picked up.

**Wrong fix (reverted):** yawing `ForkCarryLocalEuler` to 180. The carry pose applies to EVERY pallet,
so it just moved the flip onto all the pallets that were previously correct.

**Root cause:** lane `DepthAxis` is +Z for every lane and trailers dock at negative Z, so a pallet
**in a trailer** and a pallet **in a lane** genuinely sit 180 degrees apart in world space. Any
ABSOLUTE carry pose is right for exactly one source and wrong for the other.

**Real fix:** `NearestFacing(want, currentForward)` in both `TrailerLoadController` and
`TrailerOffloadController` — seats the pallet square to the carrier but picks whichever of the two
180-degree variants is already closer. A pallet's footprint is symmetric under a 180 yaw, so both park
identically; only the transition was ever visible. Applied at all four transitions (pickup and
set-down, both controllers). Facing must be captured BEFORE parenting.

### Bottom HUD

- **Keybind legend** — orange strip (#B5743A @ 0.15 alpha) above the bar, built in `BuildMenuUI`,
  anchored at `BottomBarHeight` (122 = bar's 120px + 2px border). Strip and every child are
  `PickingMode.Ignore`.
- **Dev HUD docked into the bar.** `BuildMenuUI.Instance.BottomBar` (not `.Root` — parenting to the
  document root still left it absolutely positioned and floating). Drag and close button removed; the
  graphics-preset button stays.
- **PERFORMANCE RULE:** anything with per-frame-changing text inside the bottom bar MUST have a
  **fixed width**. The FPS label at auto width resized every frame and forced a full re-layout of the
  bar (ten category buttons + utility row): **frame rate went 34 -> 2**. Fixed widths restored it to 41.
- **Match sizes by MEASURING, not by copying USS numbers.** `.buildmenu-category-button` is `104px` in
  the sheet but resolves to 112-113 depending on panel scaling. `MatchCategoryButtonHeight()` reads a
  live button's `resolvedStyle.height` after layout.

### Toast always-on-top

`UIToast` lives on the **TopBar** GameObject and its `Awake` is what sets that document's
`sortingOrder` to 999999. But every full-screen panel is built at runtime into **that same document's
root**, so they are siblings of the toast and z-order is SIBLING ORDER — `sortingOrder` never applied.
`Show()` now calls `BringToFront()`, per-show because a panel created later would otherwise overtake
it again.

### Other fixes this session

- **Lane straddle (was stranding pallets + billing for them):** an order's pallets could land in two
  different lanes while `OrderData.AssignedLane` records only one; the loader scans one lane, so the
  other pallet was never loaded, yet `ShipOrder` billed the full picked quantity. New
  `InventoryService.CountFreeStagingSlotsInLane` / `TryFindStagingLaneForPallets` pick ONE lane with
  room for the whole order, resolved once in `FinishOrder`; per-pallet placement is locked to
  `AssignedLane` and cannot overflow.
- **Phantom staged orders after load:** staged pallets are NOT persisted while order status/door/lane
  ARE. `OrderService.ReconcileStagedOrdersAgainstScene()` (called at the END of
  `PlacementSystem.ApplySaveData`, after pallets AND trucks restore) returns Staged/Loading orders
  with no matching pallet to Pending, refiles an Open OrderSelect task, and cancels lane-keyed Load
  tasks nothing needs. `Loaded` orders are reported only — voiding earned revenue is not the guard's
  call. **Real persistence for staged pallets and in-flight trucks still does not exist.**
- **Multi-customer release:** `TryPlanStageSpread`/`ReleaseOrdersToStages` give each customer its own
  STAGE (a stage cannot be shared — staging overflows A->B->C within it);
  `ReleaseOrdersToLoadingBatch` splits by door, one trailer each.
- **Work Queue From/To columns:** Open rows show first/last pick face via new `OrderPickPath`, which
  `OrderSelectionTaskDriver.TryFindBestPickLocation` now also calls so preview and reality cannot
  diverge. Released rows show staging lane -> `Door N`.
- **`MoneyFlightFx`** — UI-Toolkit money sweep on close-out. `FloatingMoneyText` is world-space and
  therefore invisible behind a full-screen modal, which is why close-out felt like it banked nothing.

### Tooling gotchas (cost real time — read before debugging)

- **`Unity_ValidateScript` is a SYNTAX check only.** It returned clean for a file whose
  `SimulationTimeService` reference was unresolvable. "Validates clean" does NOT mean compiles.
  **Real compile gate:** run a trivial `Unity_RunCommand` naming the types just edited — it builds
  against the real game assemblies.
- **`Unity_ReadConsole`'s type filter is broken** — `Types: ["Error","Warning"]` returns 0 entries
  regardless, and it returns OLDEST-first. Use `FilterText` (e.g. `"error CS"`).
- **`SimulationTimeService` is in `GameCore.Economy`**, not `GameCore.Services`.
- **`Unity_RunCommand` blocks `System.Reflection`** — use `AssetDatabase.FindAssets("t:Type")` and
  `SerializedObject` to reach private serialized fields.
- **Verify the thing, not the value you set.** Several fixes this session were reported as done while
  broken because the code was checked and the behaviour was not — registration that never ran, a panel
  parented to the wrong element, a height that did not match. Read the runtime log or measure against
  the real target.

### Still open after this session

1. **End-to-end run never completed** — the stuck-selector backstop (`AiNavigation` no-progress) and
   the loader `DriveInToGrab` travel-cap fix from 2026-07-28 remain UNPROVEN.
2. Full-pallet fulfilment mechanic (the big one).
3. Wholesale offers should REFRESH rather than being permanently spent once delivered (agreed, not
   built).
4. ~~`ContractData.LateFeePercent` not enforced by the fine sweep.~~ **FIXED 2026-08-01** — see below.
5. `ShipOrder` bills `QuantityPicked` with no check that pallets actually made it onto a truck — this
   is what let the lane-straddle bug stay silent.
6. Nothing guards against multiple outbound trucks per door.
7. Dev HUD card sits to the RIGHT of the utility buttons; may want moving into the gap before them.
8. Only 3 `ContractData` assets exist (Starter / HighVolume / WholesaleTrailer). A fuller spread
   across the 26 customers still needs authoring.

---

## Session 2026-08-01 — Contracts panel becomes three tabs; DockScheduleService (appointment book)

**Live-verified in Play mode against Tad's real save** (5 outbound doors, 3 contracts, 2 running
accounts + 1 delivered wholesale). Compiles clean; every claim below was measured, not inferred.
Only the legend reposition (last edit) is unproven — a one-line move of an already-rendered element.

### `ContractsPanel` — Offers / Accounts / Schedule (key `6`)

Retitled **OUTBOUND CONTRACTS**. Customer icons (`CustomerData.Icon`) on all three tabs: 72px on
Offers cards, 44px on Accounts rows, **18px immediately left of the name** on Schedule chips.

- **Offers** — `AvailableOffers` ONLY. Previously the tab listed everything and greyed out what was
  taken, so a delivered wholesale deal squatted there permanently at 45% opacity. An offer you can act
  on and an account you're being judged on are different objects.
- **Accounts** — three stat tiles (committed cases/day, orders delivered, on-time %) over one row per
  running account, then a `COMPLETED DEALS` group for spent wholesale. Per-row: day held, next drop
  time, shipped/late counts, fees paid, earned-to-date, and the only **Cancel** button in the game
  (`OrderArrivalService.Cancel` existed and nothing called it). Cancelling stops FUTURE arrivals only
  — orders already on the board keep their due dates and still fine you.
- **Schedule** — 12 two-hour block rows × N door columns, day pager (−2/+7 around today), legend,
  live capacity readout. Past blocks dim. Full blocks flag `FULL` in red.

**Interaction is select-then-place, not drag.** Click a chip to pick it up, click any open slot to
drop it, click the chip again to abandon. Pointer capture inside a `ScrollView` fights the scroller,
and two-click also works when source and target blocks aren't both on screen.

**Chip layout gotcha (found by screenshotting, not by reading):** name + door in ONE label meant a
long company name pushed `· D3` off the end under `overflow: Hidden`. Name now flexes and clips; the
door number is a separate `flexShrink = 0` element. The door is the one fact on the chip you can't
infer from anything else.

**The footer bug from the screenshot.** `Signed.Count(s => s.Active)` counted a delivered wholesale as
a running account (reported 3 when 2 were). `Active` means "TAKEN", nothing more — a spent one-off
stays Active forever, which is what keeps the Sign button off its card. New
`OrderArrivalService.RunningAccounts` / `DeliveredWholesale` split it properly. Measured live: 2 and 1.

### `DockScheduleService` (new `IService`, `Core/Inventory/`)

**A block reserves a TRAILER AT A DOOR, not paperwork.** That distinction is the whole design. A
contract's `CutoffHour` still governs when orders ARRIVE on the Work Queue; this decides when the
truck to carry them shows up. Door count only constrains anything if the thing being counted
physically occupies a door.

- `BlockHours = 2`, `BlocksPerDay = 12`. `DockAppointment` = day + block + door + kind + customer +
  contract + order ids.
- **Capacity = outbound-capable doors, not all doors.** A door counts only if ≥1 of its shipping lanes
  isn't `LaneUsage.Inbound` (default `Both`, so every door with lanes counts until told otherwise).
  Counting bare doors would let the player "fix" congestion by buying a door they can't stage into.
  Recomputed per call — lanes are placed/deleted through the build FSM at any moment.
- **Inbound POs consume the same capacity.** `ShipmentService.TrySpawnTruck` now calls
  `BookInboundNow`. A dock door doesn't know which way a trailer faces; without this the player books
  every door for outbound at 08:00 and a PO arrives with nowhere to go.
- **Auto-placement on `OrderService.OnOrderArrived`** — first free block from now, never past the
  order's due day (an appointment after the deadline looks handled while guaranteeing the fine).
  Orders sharing a customer AND contract share one trailer; a different contract gets its own, so a
  wholesale drop can't ride on the case-pick trailer.
- Persisted via `SaveData.dockAppointments`; old saves load with an empty schedule and refill.

**Measured live:** 5 bookings filled a block and were assigned doors 1–5 distinctly; the 6th was
refused with `"08:00–10:00 is full — all 5 door(s) are booked."`; a move into a full block failed and
left the original in place (no data loss); 5 real orders auto-placed into exactly 3 trailers.

### Late fees are real now

`OrderData.LateFeePercent` is stamped from `ContractData.LateFeePercent` at order creation and
`OrderService.OnDayChanged` charges THAT instead of the flat `LateFeePercentClerk` 25%. Copied rather
than looked up at fine time so the fine reflects the terms accepted when the order arrived, and so
`OrderService` needs no dependency on the contract system. **0 in a save = unset → falls back to 25%**,
so legacy orders behave exactly as before.

New `OrderData` fields: `ContractId`, `LateFeePercent`, `IsWholesale` (all round-tripped in
`OrderSnapshot`). New `OrderService.OnOrderFined(OrderData, int)` event.

### Per-contract performance is accumulated, not recomputed

`SignedContract` gained `OrdersDelivered` / `OrdersLate` / `RevenueEarned` / `LateFeesPaid`, fed by
`OrderArrivalService` subscribing to `OnOrderShipped` / `OnOrderFined`. **Accumulated deliberately:**
`OrderService.OrderHistory` is capped at 250 and trims oldest-first, so a recomputed "earned to date"
would silently FALL over a long game as early orders aged out. Attribution matches on `ContractId`
only, never falling back to `CustomerId` — one customer may hold several contracts and a wrong
attribution is worse than none. `RevenueEarned` uses billed `QuantityPicked`, matching `ShipOrder`.

### Tab polish + dev-button move (same day, second pass)

- **Tabs redrawn as folder tabs.** First pass drew inactive tabs as bare transparent text and they
  read as a subtitle — nothing suggested there was anything behind them. Now every tab has a card
  face, outline and rounded top corners; the active one is orange, **taller (38 vs 32), and sits 2px
  lower with NO bottom border** so it breaks through the divider and joins the content below. That
  break is what sells it — colour alone just looks like a highlighted word. Counts moved into their
  own dark pill (`Schedule 4/60` was scanning as a four-word title). Tab bar is `Align.FlexEnd` so
  tabs sit ON the divider regardless of individual height.
- **"Offers" tab renamed "Customers"** (enum `Tab.Customers`, `BuildCustomers`). Contracts will
  eventually appear over time rather than sit in a fixed list, so it's a customer board you watch,
  not a catalogue you shop.
- **Dev order triggers moved** out of the Tools window's OUTBOUND SIMULATOR section (deleted from
  `ToolsWindow.uxml`, `Wire` calls removed) onto a small muted **DEV** cluster at the right of the
  Contracts tab row. `CreateTestOrder()` / `SpawnOutboundTruckDebug()` are now **public** on
  `ToolsWindowController` and called through `ToolsWindowController.Instance`; they stay on that
  component because the `CustomerRegistry` they need is a serialized Inspector field, which a
  code-built panel has no way to supply. The cluster hides itself when `Instance` is null.
  **Do NOT remove the Dev Console's other buttons** — only the two outbound ones moved.

### Dev HUD sizing — the real cause was flex-shrink, not the button

Reported as "the FPS element still looks wrong." Measured: the card asked for 310px and **resolved to
262** — the bottom bar is a flex row and was squeezing it, while the fixed-width labels inside refused
to shrink, so the preset button spilled **39px past the card's right edge**. It looked like a button
sizing bug and was a container bug.

- `_panel.style.flexShrink = 0` (the fix), card width 300, and the inner widths are now named
  constants that must sum within it: `CardPadding(12) + FpsLabelWidth(100) + FpsLabelGap(12) +
  StackWidth(160) + CardPadding(12) = 296`. Change one, check the sum.
- FPS font **34 → 29** (~15% down). Cell label `marginBottom` 6 → 10 to lift it clear. Preset button
  is now a fixed 160×40 with `whiteSpace = Normal` so "Mode: Toaster" wraps instead of overrunning.
- Verified after the fix: card 300px wide, button fits with 13px spare, 12px gap to the utility row,
  28px clear of the bar's right edge — nothing else on the bar moved.

### Third pass (same day) — PO purge, toast z-order, TEST CUSTOMER

**Finished POs are retired now.** `ShipmentService.PendingShipments` never dropped anything, so every
departed truck left a red `[Departed]` row in the Dev Console's inbound list forever and every one was
re-serialised into every save — the same leak `OrderService.Archive` fixed for orders. New
`ShipmentService.PurgeCompleted()` removes Departed/Received/Cancelled, called from
`TruckController.BeginDeparture` (immediate — the row vanishes as the truck pulls out), from
`OnDayChanged` (backstop for trucks destroyed mid-route, which never reach BeginDeparture), and at the
end of `Import()` (**this is what clears an existing save's backlog on first load**). Safe because a
truck holds a direct `AssignedShipment` reference, not a list lookup. Verified: 0 finished POs left in
the list after loading Tad's save.

**Toast z-order — the previous fix was raising the wrong element.** `ToastLabel` is NESTED inside a
wrapper in the UXML, so `_toast.BringToFront()` only reordered it against its siblings *inside that
wrapper*; the wrapper itself stayed where it was in the root's child list, still under the panels. New
`RaiseAboveEverything()` walks the whole ancestor chain calling `BringToFront()` at each level.
Measured after the fix: common ancestor `TopBar-container` (21 children), toast branch at index 20 vs
modal branch at 18 — and confirmed visually with a toast drawn over the open Contracts modal.
Deliberately NOT reparenting the label to the root: the wrapper may carry USS that positions it.

**`TEST CUSTOMER` replaces `TEST ORDER`, and it now adds an OFFER, not orders.** Tad's call. It puts
one new signable customer on the Customers tab — the debug stand-in for reputation-driven arrival.
- `ContractData.CreateRuntime(...)` — new static factory that builds a ContractData in memory instead
  of from an authored asset. **This is the API the reputation system will use**; the dev button is
  just its first caller. `contractId` becomes `.name`, because `ContractId => name`.
- `OrderArrivalService.AddOffer(contract)` — appends to the catalog, refusing a duplicate ContractId
  rather than shadowing (two entries under one id would make `GetContract` depend on list order).
- `ToolsWindowController.CreateTestCustomerOffer()` — picks a customer with no offer already on the
  board, rolls varied terms (1-in-4 wholesale), returns the company name. Lives there because the
  `CustomerRegistry` is a serialized field on that component.
- **CAVEAT, documented in code:** a runtime contract isn't in the `ContractRegistry`, so it does NOT
  survive save/load. Signing one and reloading leaves a `SignedContract` whose id resolves to nothing;
  `OnHourChanged` already warns and skips. Fine for a debug trigger — the real feature must persist
  generated offers alongside `SaveData.contracts`.
- The DEV cluster moved out of the tab row into the **Customers tab content** (top-right, beside the
  intro text). It was on screen while reading the Schedule, where it means nothing.

Verified live: two presses produced `The Cracker Barrel Caravan` and `Puff & Stuff Pastries` with
genuinely different terms (x1.44 pay / 34% fee / 98 cases/day vs x1.38 / 23% / 182), both immediately
signable, tab badge going 0 → 2.

**Dev HUD, second pass:** FPS 29 → **26**, and the Cell label is now `MiddleCenter` inside its
`StackWidth` box so it sits centred over the preset button instead of jammed left.

### Fourth pass — Fill Rate shorts popup + Schedule horizontal scroll

**`OrderShortsPopup`** (`UI_UX/OrderShortsPopup.cs`) — click the Work Queue's **Fill Rate** cell to see
what was CUT. "16 / 23" says an order shipped short but not WHAT was short, and that's the
operationally useful part: one SKU 8 cases light is a different problem from eight lines 1 case light.
Lists only lines with a shortfall (a fully-picked line isn't "cut" and listing it buries the two that
were), sorted worst-first, with a footer totalling lines short / cases cut / revenue not billed.

- Floats over the Work Queue rather than replacing it — it's an inspector for a row you're still
  reading. Draggable by its title bar, red ✕ top-right.
- The Fill Rate cell is underlined by hover-colour and carries a tooltip; an unmarked clickable cell
  in a table of dead ones is a hidden feature. `evt.StopPropagation()` so the click can't reach the
  row checkbox.
- Built lazily on first click (`_shortsPopup ??= new ...`) — most sessions never open it.

**Closing it needed a new concept: AUXILIARY panels.** `UIKeyBindingManager._uiPanels` is keyed by
hotkey number and all nine slots are taken, but Tab (→ `CloseAll`) only iterates that registry. New
`RegisterAuxiliary` / `UnregisterAuxiliary` / `CloseAuxiliaries()` / `AnyAuxiliaryOpen` handle
hotkey-less sub-popups: `CloseAll` now closes them first, and `TopBarUI`'s escape chain calls
`CloseAuxiliaries()` at the TOP — **its bool return is what stops the same Escape falling through and
opening the pause menu behind the popup.** Any future sub-popup should register the same way.

**Schedule tab scrolls horizontally with a stationary frame.** Two changes:
1. New `_tabHeader` element in the modal, **between the tab bar and the ScrollView**. The Schedule
   tab's day switcher and colour legend go there, so they don't slide away when the grid scrolls.
   Other tabs leave it empty and it collapses. Measured: scrolling right 200px moved the header 0px.
2. `_content.mode` flips to `VerticalAndHorizontal` on the Schedule tab only, AND the grid's cells
   became **fixed widths with flexShrink 0** (`TimeColWidth 92 / SlotWidth 168 / FullFlagWidth 44`).
   **This second half is the load-bearing one:** slots were `flexGrow 1 / flexBasis 0`, so they shared
   whatever width existed and squeezed to nothing rather than overflowing — the horizontal scrollbar
   could never appear no matter how many doors there were. Verified at 5 doors: row 1013px vs 978px
   viewport, h-scroller displayed.

Verified live end-to-end: popup listed exactly the two short lines out of three (Canned Tomato 12 cut,
Bread 8 cut, Water fully picked and correctly omitted), footer `2 line(s) short · 20 case(s) cut ·
$220 not billed` matching by hand; Tab and Escape both closed it; a 429-case order scrolled within the
popup's own list.

### Contract arrival by REPUTATION — designed 2026-08-01, NOT built

Tad's direction for how contracts should appear once the loop is real. Recording it here so the
Customers tab gets built toward it rather than away from it.

- Offers **appear over time, semi-randomly**, rather than all being present from turn one. The
  Customers tab becomes something you check, and a good offer is a moment.
- **Frequency scales with the business's reputation AND game difficulty.** A well-run DC attracts more
  and better customers; a bad one dries up. Difficulty scales the curve.
- Reputation inputs Tad named, in his order: **late orders · cancelled orders · drivers left waiting
  to be offloaded at inbound · mispicked orders · damaged cases.** The last two don't exist yet.
- Everything the first three need is already recorded: `SignedContract.OrdersLate` /
  `OrdersDelivered` (per-account and summable), `OrderService.OnOrderCancelled`, and inbound wait is
  derivable from `TruckController`'s dock lifecycle (`AwaitingOffload` → `CompleteOffload`).
- The natural shape is a `ReputationService : IService` holding a rolling score, persisted like
  `SignedContract`'s stats are, driving how often `OrderArrivalService.AddOffer` fires and how good
  the rolled terms are. **Not started — but both halves of the plumbing now exist**
  (`ContractData.CreateRuntime` + `AddOffer`), and `ToolsWindowController.CreateTestCustomerOffer`
  is a working reference implementation of the roll.
- **Persistence is the one real gap.** Generated offers vanish on reload (see the caveat above). Needs
  a `List<ContractSnapshot>`-style store of generated offers in `SaveData` before this ships.

### Still open after this session

1. **Nothing makes a truck actually show up for its appointment.** The book is real, persisted, and
   capacity-enforced, but `TruckYardManager` doesn't read it — outbound trailers still arrive however
   they did before. That wiring is the next step and is where the schedule starts to bite.
2. Full-pallet fulfilment (unchanged, still the big one) — `OrderData.IsWholesale` is now the flag it
   will key off.
3. Wholesale offers should REFRESH rather than being permanently spent (unchanged). Delivered deals at
   least no longer clutter Offers — they live under Accounts → Completed deals.
4. Accounts aggregates per CONTRACT. One customer holding two contracts shows as two rows. Correct,
   but if that ever looks wrong it's the place to group.
5. `ScheduleDaysBack = 2` matches `DockScheduleService.KeepPastDays`; changing one needs the other.
6. Reputation-gated contract arrival (designed above, not built) — the thing that makes the Customers
   tab worth opening more than once.
7. Only 3 `ContractData` assets, so the Customers tab currently shows its empty state on Tad's save.
   Reputation-driven arrival will need a much deeper catalogue across the 26 authored customers.

---

## Purchasing & Vendor System — Phases 1, 2, 4 BUILT 2026-08-18; 3 partial, 5 designed

> **See `PURCHASING_DESIGN.md` in the repo root for the full document.** This is a pointer, not a
> summary — the design lives there so it's editable as one piece.

The inbound half of the loop, and currently its weakest link. Today: one hardcoded supplier
(`"PLAYER_SUPPLIER"` / `"Wholesale Supply"`, `PurchasingPanel.cs:968`), one never-moving price per
SKU, and a truck that always carries exactly what was ordered. Tad: *"a two dimensional pick items
from a list like you're at a restaurant."*

**The agreed direction: vendor tiers gated by a Reputation score.**

Four load-bearing decisions, so they don't get re-litigated:

1. **ONE Reputation score, two consumers.** The vendor reputation is the SAME number as the
   customer-side reputation designed in the 2026-08-01 session above — it gates which customers offer
   you contracts AND which vendors answer your calls. One `ReputationService`, one number the player
   reads, both halves of the game feeding the same spine. Do not build two.
2. **Salvage, not contraband.** The Buccaneer-style "rare vendor with illegal goods" is reskinned to
   unmanifested salvage / close-out loads bought sight-unseen. Same thrill, no
   inspection/seizure/fines second failure system, and it's authentically warehouse. It's also the
   natural entry vector for the deferred rat system (ship a contaminated pallet = −40 rep).
3. **Tier 3 launches AMBIENT.** Caviar/seafood/rare beef are all cold chain, which needs
   Perishable/Frozen storage + refrigerated rooms + power upkeep. Tier 3 ships with high-value
   ambient goods (truffle oil, saffron, aged spirits, single-origin coffee); cold chain is its own
   later milestone that reuses the tier structure.
4. **The Anti-Obsolescence Rule.** Tiers differ on a **risk/velocity** axis, never strictly-better.
   Staples = thin margin, constant demand, forgiving. Specialty = fat margin, lumpy demand,
   slot-hungry, punishing to scratch. Otherwise ~25 of the 31 SKUs become dead content by hour three.

**Cut:** the Oblivion-style persuasion/barter minigame (Tad self-rejected; the fantasy here is
operational mastery, not social skill).

**Two findings from the design review worth knowing before touching this code:**

- **Receiving variance is plumbed but structurally impossible.** `ShipmentLineItem.Overage`/`.Shortage`
  are computed and logged, but `UpdateReceivedQuantity` is called from exactly ONE place
  (`ShipmentReceivingCoordinator.cs:96`) with the quantity of the pallet that physically arrived —
  and the trailer is built from the PO. Received always equals ordered. The gun is built and unloaded.
- **Current SKU margins run BACKWARDS from the intended tiering.** Water 5→12 is a 140% markup and
  Salt 10→18 is 80%, while Maple Syrup 55→85 and Honey 42→65 are both 55%. The cheap staples are
  currently the fattest margins. A repricing pass is required — and it's where the tiers actually get
  their character, not cleanup.

**Build order:** (1) ~~make purchasing a bet~~ **DONE**; (2) `VendorData` + `VendorRegistry` mirroring
`ContractData`/`ContractRegistry`, plus the repricing/tier-assignment pass; (3) `ReputationService`;
(4) the Broker (salvage loads, `PalletStatus.QAHold` write-offs); (5+) cold chain, rats.

### Phase 1 — what's actually in the code now (live-verified 2026-08-18)

- **`MarketService`** (`Core/Inventory/MarketService.cs`) — new `IService`. Per-SKU daily price on a
  MEAN-REVERTING random walk clamped to 0.70x-1.35x of authored `BuyValue`, 7 days of history for the
  sparkline, and the 3-a-day expiring spot-deal board. **Mean reversion is load-bearing** — a pure
  random walk wanders to the clamp and the sparkline becomes a flat line against a wall.
- **Registered in `GameContext` AFTER `inventoryService.LoadSkuDatabase(...)`, not with the other
  services.** It seeds every price from the SKU database on `Initialize`, and an empty database at
  that moment means a market with no prices and no deals for the entire session.
- **`ShipmentLineItem.Dropped` + `ShipmentService.ApplySupplierVariance`** — 25% of PLAYER POs arrive
  short by 1-3 pallets. Rolled ONCE at dispatch (`_varianceApplied` guards a re-spawn), before
  `TruckController.LoadShipment` reads the manifest, so the physical trailer, the PO list and the
  receiving records agree from the first frame. Never more than half a load, never a single-pallet
  PO, never non-player freight. **The player is CREDITED for what didn't arrive** — being charged for
  freight that never came reads as the game stealing from you; the interesting loss is the missing
  stock and the fill rate it costs.
- **`Dropped` is a FLAG, not a deletion.** `Quantity` is what was ORDERED; removing the line would
  erase the evidence anything was missing. Left in place, `ReceivedQuantity` stays 0 and `Shortage`
  finally reports something real — the first time that field has ever been non-zero.
- **Every price on the panel goes through one `UnitPrice(sku)` helper** — item card, line cost, order
  total, and the PO's actual `ShipmentLineItem`s — so the number quoted and the number charged cannot
  diverge. Falls back to `BuyValue` if the market isn't running (usable, not free).
- **`WAREHOUSE:` line** under the trailer meter reads `LocationStatusRegistry` (what the reach truck
  obeys), NOT `LocationData` — see the split-brain note earlier in this file.
- **UI gotcha:** the item card's price row MUST be `flexWrap = Wrap` with `flexShrink = 0` children.
  The card body is what's left after a 116px icon and a 150px cost plate — under 250px at two
  columns — so cost + trend chip + sparkline do not fit on one line. Without wrapping the chip
  renders as "NORM" and the sparkline is clipped away entirely.

**Bug found in verification, worth remembering:** `EnsureDealsForToday` originally re-read the clock
instead of using the day carried by `OnDayChanged`. Those two can disagree, and when they did it
expired the board against the NEW day then refused to rebuild it against the OLD one — the spot-deal
board would have emptied at the first midnight and never come back. Anything reacting to
`OnDayChanged` should trust the event's day, not re-read `SimulationTimeService.Day`.

**Already in the code and unused, relevant here:** `PalletData.AreaCategory { Grocery, Perishable,
Frozen }`, `PalletData.PalletStatus.QAHold`, `SignedContract.SatisfactionPercent` (the per-counterparty
standing precedent to mirror for vendors), `ContractData.CreateRuntime` + `OrderArrivalService.AddOffer`
(the runtime-counterparty-generation pattern).


### Phase 2 — vendors, reputation, repricing (live-verified 2026-08-18)

- **`VendorData` / `VendorRegistry`** (`Core/Inventory/`) mirror `ContractData`/`ContractRegistry`.
  Registry asset at `Assets/_Project/Resources/VendorRegistry.asset`, vendors under
  `Assets/_Project/ScriptableObjects/Vendors/`. **Loaded from Resources BY NAME** so it resolves in a
  built player.
- **Seven vendors on a LADDER, not at the band boundaries:** BulkBasin 0, Cornerstone 0, Halloran 60,
  Fairweather 100, Meridian 160, VesselVine 250, Ambrose 340. Deliberate — unlocking in three lumps
  at 100/300/600 would make the roster feel static between bands.
- **Only THREE differentiating fields, all consumed:** `PriceMultiplier`, `ShortShipmentChance`
  (drives `ShipmentService.ShortShipChanceFor`, replacing the flat 25%), `MinimumOrderCases`.
  **On-time % and net-30 were drafted and CUT** — no late-delivery or trade-credit mechanic exists,
  and unconsumed fields are how Overage/Shortage sat dead for months. Add them WITH their mechanic.
- **`ReputationService`** was pulled forward from Phase 3 out of necessity: gating with no score
  source means 13 buyable SKUs forever. Score 0-1000, bands Unknown/Known/Respected/Preferred/
  Untouchable at 0/100/300/600/850, persisted via `SaveData.reputation`. **Three of five designed
  inputs wired** (OnOrderShipped / OnOrderFined / OnOrderCancelled). Dock wait time and contamination
  are NOT wired — no events exist for them yet.
- **Reputation toasts only on BAND CHANGE**, not per event — "+5 reputation" on every shipped order is
  noise on a number that moves all day.
- **REPRICED all 31 SKUs** (0 unmapped). T1 30-40%, T2 50-56%, T3 100-117%. Catalogue splits 13/14/4.
  This is what makes the tiers mean anything — before it, Water was a 140% markup and Maple Syrup 55%.
- **One PO is one vendor.** `OnSelectVendor` clears the basket on switch. `SelectedVendor()`
  re-anchors to the first unlocked house whenever the selection stops being valid, so a reputation
  drop or an edited asset can't leave the panel pointing at a vendor that won't serve you.
- **Spot deals are NOT vendor-gated on purpose** — separate channel, and an occasional early taste of
  a Tier 3 item is a feature.
- **UI gotchas, both cost a screenshot to find:** the vendor bar and its row need `flexShrink = 0` or
  the wrapped second row draws straight over the trailer meter below it. And chip name/terms labels
  must be `WhiteSpace.Normal` — at 148px wide, `NoWrap` clipped "Fairweather Trading Co." to
  "Fairweather Tradi" and, worse, hid the "min 240" clause that REFUSES the PO.
- **Locked-chip label says the NUMBER, not the band.** Naming the band a threshold sits in produced
  "Needs 250 rep (Known)" for a player who was already Known.


### Phase 4 — The Broker (salvage loads), live-verified 2026-08-18

`BrokerService` + `Vendor_Broker` asset, gated at **600 reputation**. 35%/day chance of one
unmanifested trailer, expires after 2 days.

- **CONTENTS ARE ROLLED AT OFFER TIME AND PERSISTED** (`SaveData.broker` carries them). Not at
  reveal. The trailer really does contain something specific before the player decides, so a reload
  cannot reroll a bad load — a gamble you can save-scum isn't one.
- Mix measured over 3,897 pallets: 59.4/20.1/15.2/5.4 vs targets 60/20/15/5
  (ordinary/short-dated/damaged/jackpot).
- **Short-dated stamps a real 4-day `ShelfLifeDays`** and rides the EXISTING receive path into a real
  expiration day + the spoilage system. No new plumbing — that pipeline finally has a user.
- **Damaged sets `Dropped = true`** so no pallet is built and none reaches inventory (refused at the
  door), but the line item STAYS on the manifest carrying `Salvage = Damaged`. That's what lets the
  PO list say "3 damaged, written off" instead of just showing a smaller trailer.
- **Jackpot pool = SKUs no unlocked vendor carries**, so it's by construction something you can't buy.
- **`ShipmentData.IsSalvage` hides the manifest** in the PO list until `AnyReceived` is true. This
  concealment IS the product.
- **The Broker is filtered out of `VendorRegistry.Unlocked`/`Locked`** — he has no catalogue, and as a
  supplier chip he'd read "0 item(s) available for ordering", which looks like a bug. Still reachable
  via `GetById`, which is how BrokerService reads his reputation gate.
- `ApplySupplierVariance` **skips salvage** — the load is already the gamble; short-shipping it on top
  would charge twice for the same uncertainty and be indistinguishable from the junk you knowingly bought.

**BUG FOUND BY MEASURING, worth remembering as a pattern.** The asking price originally excluded
damaged pallets, so it could never exceed usable value — **300 of 300 sampled loads were profitable**
and the gamble had literally no downside. Pricing on the FULL manifest (junk included) is the whole
risk. Now 40-92% of full manifest value: **88% profitable / 12% losses / +42% avg margin / worst
−46%** over 800 loads. Lesson: a risk mechanic is worth simulating a few hundred times before
believing it works — it read correctly and was inert.

**Known limitation:** damaged product is refused at the door rather than physically arriving and
needing disposal (the more interesting version, but it needs a disposal mechanic).
`PalletMasterRecord` has no status field, so `PalletStatus.QAHold` remains unused — that's where it
belongs when disposal lands.


---

## Session 2026-08-18 (late) — three loop-blocking bugs, all found by measuring

Went looking for "what else is there to do" and found the outbound half of the loop dead and the
financial reporting lying. None of these were visible from reading the code; all three were found by
querying the running game.

### 1. A freshly-hired Receiver or Order Selector never started working

`EmployeeSpawner` gave an automatic assignment to the MHE roles only (`TryBoardExistingMHE` for
ReachTruck / DockStocker / Loader). **Every other role spawned on Patrol with no task driver
attached** — so a hired Order Selector walked around forever while OrderSelect tasks piled up, and the
ONLY way to make them work was to find them in the Roster and pick their assignment out of a dropdown
by hand. Nothing said so.

Measured live: an order sat `Pending` behind an Available OrderSelect task with **zero
`OrderSelectionTaskDriver` components anywhere in the scene**. The one Receiver who did work had been
hand-assigned at some point and had it persisted via `record.currentAssignment`.

**Fix:** fresh non-MHE hires now get `record.role.RoleSpecificAssignment()` applied at spawn — the
same map the Roster and Info card already use, so a hire starts in exactly the state that dropdown
would have produced. Verified: hiring an OrderSelector now yields `assignment=OrderSelection` with the
driver attached.

### 2. Nothing ever said "you have no one who can do this job"

A queue full of Available tasks and nobody employed who can take them is silent and fatal.
`WorkQueueSystem.WarnAboutUnstaffedWork` (on the existing hour tick) now toasts once per role per day
when Available tasks require a role **no active employee holds**.

**Keyed on ROLE-NOT-HIRED, not on task age** — deliberately. A task waiting because everyone is busy
is a queue working correctly; a task waiting because the role doesn't exist in the building is a dead
end, and only the second is worth interrupting for.

**A TASK'S RequiredRole IS NOT ALWAYS THE ONLY ROLE THAT CAN DO IT — this warning got that wrong on
its first pass.** `OrderService` files Load tasks as `EmployeeRole.Loader`, but
`TrailerLoadController` (line ~194) accepts a **DockStockerOperator** too: they drive the same
equipment, and `RoleSpecificAssignment` maps both to `DriveDockstalker`. Comparing roles exactly meant
the warning nagged "you haven't hired a Loader" every day at a player whose dock stocker could load
the trailer perfectly well — a false alarm on a warning whose entire value is that it only fires when
something is genuinely impossible.

`WorkQueueSystem.CanServe(employeeRole, requiredRole)` now encodes the substitution, mirroring
TrailerLoadController's own operator check as the authority. **If any controller ever learns to accept
a substitute role, add it there too.** Every other consumer is an exact match (ReachTruckOperator for
Putaway/Replenish/PalletPick, DockStockerOperator for offload, Receiver, OrderSelector) — verified by
reading each consumer's role check, not assumed.

### 3. ⭐ `SetMoney` on save load was booking the whole balance delta as a "Debug" EXPENSE

**This one poisoned every financial panel in the game.** `PlacementSystem.ApplySaveData` called
`moneyService.SetMoney(save.money)`, and `SetMoney` records the delta as a real transaction. The game
boots at the difficulty's starting capital ($120k Clerk) and then loads a save holding less — so
**every single load charged the gap to lifetime expenses.**

Measured on a real save: **$26,731 of $26,879 lifetime expenses — 99.4% of everything the player had
apparently ever spent — was one load correction.** It also inflated `ExpensesThisHour`,
`ExpensesThisWeek` ($38,746) and everything built on them. The genuine operating costs underneath were
Wages $124, Groundskeeping $11, Maintenance $10, MHE $2.

**There was never a balance problem.** The 30:1 "burn" was an artifact. New
`MoneyService.RestoreCapital(int)` assigns the balance and fires `OnMoneyChanged` **without touching
any ledger or counter**; `ApplySaveData` uses it. Verified after a real load: Debug portion **$0**,
total lifetime expenses **$237**, all of it real.

**Lesson worth keeping: restoring a balance is not a transaction.** Any future save-restore of a
running total needs the same treatment.

### Also: reputation now gates CUSTOMERS as well as vendors

The second half of "one reputation, two consumers". `ContractData._reputationRequired` (0 in every
asset authored before this, which is correct — those are starter accounts),
`OrderArrivalService.AvailableOffers` filtered by it, and a new `ReputationLockedOffers` rendered as a
greyed card on the Customers tab: *"WON'T DEAL WITH YOU YET — Needs 300 reputation (Respected) — you
have 170. 130 to go."* Same reasoning as locked vendors: a door you can see is a goal.

`CurrentReputation` is resolved LAZILY, not cached at Initialize — `OrderArrivalService` is
constructed before `ReputationService` in `GameContext`, and a field captured at init would be null
forever, silently reading as reputation 0 and locking every gated customer out of the game permanently.

Authored ladder: `Contract_Starter` 0, `Contract_HighVolume` 250, `Contract_WholesaleTrailer` 400.
Runtime BULK offers stay at 0 — walk-in business doesn't check your references.
