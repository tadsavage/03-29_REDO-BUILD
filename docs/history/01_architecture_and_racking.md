# Architecture detail, racking, NavMesh MHE drive, dock lanes (Jun-Jul 2026)

> Archived from CLAUDE.md on 2026-11-02 (verbatim). Historical detail — read only when working in this area. The lean CLAUDE.md holds the rules that always apply.

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


