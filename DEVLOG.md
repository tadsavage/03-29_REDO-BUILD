# Devlog — Warehouse Sim 2026

Running log of daily development sessions: what was discussed, worked on, and decided.

---

## 2026-05-29

**Session summary:** Project setup and orientation.

- Navigated to the `03-29_REDO-BUILD` Unity project.
- Reviewed `CLAUDE.md` — confirmed project is a Unity 6 URP warehouse/facility builder sim with a placement FSM, grid system, economy, and save/load foundation.
- Established this devlog (`DEVLOG.md`) to track daily progress for posterity.
- No code changes made this session.

---

## 2026-05-29 (continued)

**Session: Dev Console Panel**

Built a polished runtime dev/cheat console panel using UI Toolkit (UXML/USS), styled in a Two Point Hospital aesthetic — dark navy background, bright saturated section accents, flat pill-style stat cards, and chunky colored buttons with a 3D "press" effect.

**Files created:**
- `Assets/3. UI/5.DevPanel/DevPanelUI.uxml` — layout
- `Assets/3. UI/5.DevPanel/DevPanelUI.uss` — TPH-style stylesheet (Fredoka font)
- `Assets/3. UI/5.DevPanel/DevPanelController.cs` — MonoBehaviour controller

**Files modified:**
- `CommandHistory.cs` — added `UndoCount` / `RedoCount` properties

**Features:**
- Toggle with backtick (`` ` ``) or the persistent ⚙ DEV pill button (top-right)
- Panel is draggable by clicking and dragging the header bar
- **Economy** (cyan): Balance / Hourly / Today + `+$1K` `+$10K` `+$100K` `ZERO` buttons
- **Time** (amber): Sim time + speed display + `PAUSE` `1×` `2×` `5×` buttons
- **World** (green): Placed object count + Undo/Redo stack depth + `REBUILD GRID` `CLEAR ALL`
- **FSM** (pink): Current state name + stack depth, live in Play mode

**Setup in Unity (one-time):**
1. Create a new GameObject → add `UIDocument` + `DevPanelController`
2. Set UIDocument Source Asset → `DevPanelUI.uxml`
3. Assign a PanelSettings asset with sort order ~50 (renders on top of game UI)

---

## 2026-05-29 (continued)

**Session: Tools Window — unified Pallet Builder + Dev Console**

Discovered the user had already built a nicer combined window (`Assets/3. UI/7.ToolsWindow/`) in the UI Builder — a tabbed panel with Pallet Builder and Dev Console tabs, styled to match the existing Palia/gold game aesthetic. Replaced the standalone `DevPanelController` approach with a new unified `ToolsWindowController`.

**Files created:**
- `Assets/3. UI/7.ToolsWindow/ToolsWindowController.cs` — unified controller; replaces both `DevPanelController` and the old `PalletBuilderUI`

**Files removed:**
- `Assets/1. Scripts/6. Utility/ToolsWindowController.cs` — duplicate causing `CS0111` compile error

**Files fixed:**
- `DevPanelController.cs` — null guards in `Start()` and `Update()` to silence `NullReferenceException` from stale scene reference
- `DevPanelUI.uss` — removed unsupported `:last-child` pseudo-class selectors

**ToolsWindowController features:**
- Singleton (`Instance`) — `PalletBuilder.ToggleUI()` was already pre-wired to call it
- Backtick (`` ` ``) opens/closes the Dev Console tab
- Clicking any pallet opens the Pallet Builder tab for that pallet
- Full dev console: Economy / Time / World / FSM stats + cheat buttons
- Full pallet builder: prefab dropdown, dimensions, Ti-Hi override, BUILD
- Draggable by the title bar; tabbed strip switches between panels

**Setup:** GameObject → `UIDocument` (Source: `ToolsWindow.uxml`) + `ToolsWindowController` + PanelSettings (Sort Order 50).

---

## 2026-05-30 / 2026-05-31

**Session: Build bar restoration, rat life, guard shack lights & gate**

Long session covering several systems.

### Build bar / placement fully restored
- Root cause found via Unity MCP live inspection: `_groundMask` on `RaycastController` was zeroed out by a domain reload, so all ground raycasts returned no hit.
- Fixed `CheckIfPointerOverUI()` — replaced `EventSystem.RaycastAll` (which caused false positives from UIDocument TemplateContainers) with `UIInputGuard.IsPointerOverUIToolkit()` using `RuntimePanelUtils.ScreenToPanel` + `panel.Pick()` with proper filtering (skips root, Ignore-mode elements, TemplateContainers).
- Fixed `BuildMenuUI.OnEnable()` and `RaycastController.Start()` to set UIDocument roots and TemplateContainers to `PickingMode.Ignore` so PanelRaycaster doesn't block game-world raycasts.
- User needs to reassign Ground layer mask in Inspector if it ever resets again.

### Nav agent fixes
- `PalletBuilder.Awake()`: moved `_placedObject` init from `Start()` to `Awake()` so `SpawnFromSave` can call `LoadBuildState()` before `Start()` runs.
- `NavMeshManager.BakeSynchronous()`: added 1.5s post-bake delay before firing `OnNavMeshReady` so 1422 carving obstacles can settle — this was the root cause of agents replanning every second and stuttering.
- `BuildingData.SetupNavigation()`: skip obstacle/modifier setup entirely for objects that already have a `NavMeshAgent` (rats, workers) — was adding a conflicting `NavMeshObstacle` to them.
- `RatBehavior.ScurryAwayRoutine()`: snap to NavMesh before re-enabling agent to fix "Failed to create agent" error.
- `AiNavigation`: `AgentTypeTag` auto-registration, per-instance stair cache, removed redundant `ResetPath()` from `ApplyAgentCosts`.

### Rat life system (RatBehavior.cs)
Full rewrite adding: age/growth (`RatFromScale`→`RatToScale`), breeding (two rats near each other → baby rat), time-of-day boldness (work hours = hide more), wall-hugging movement, scavenging from inventory items, investigation curiosity (approaches new placed objects), spatial 3D audio with `hearingDistance`, `LightPulse` script for pulsing emission lights.

### Guard shack lights & gate
- `GS_ArmLights` material: pure red `_BaseColor`, `_EmissionColor` cranked to R=500. Fixed yellow bleed at low intensity by forcing `_BaseColor` to `EmissionColor` every frame in `LightPulse`.
- `LightPulse.cs`: pulses between `MinIntensity` and `MaxIntensity` using sine wave at `PulseSpeed`. Added to both `Arm_Lights` and `Arm_Lights.001`.
- `Gate_Open_Close.cs`: trigger-based gate arm animation. Rotates pivot's local Z from `rot_down` to `rot_up` at `arm_speed` deg/s. Opens when any nav agent enters trigger, closes when all have exited. Assign `Gate_Arm_Animatable` to **Arm Pivot** in Inspector.

### Security guard
- `Security.fbx` avatar was set to `CopyFromOther` (wrong) causing no avatar to be generated → T-pose. Fixed to `CreateFromThisModel` with forced reimport → `SecurityAvatar` now generates correctly.
- Added `Animator` component to `Security.prefab` root (was missing from prefab, only existed on scene instance).
- Note: delete and re-place Security(Clone) from Build Menu to pick up the updated prefab.

---

## 2026-05-31

**Session: Cost rebalance, GPU instancing audit, cyclone fence generator**

### ObjDataSO cost rebalance (all 68 assets)
- Researched real-world 2024 market values for every item using BLS occupational wage data, Grainger/Uline industrial pricing, and commercial construction estimates.
- Updated all 68 ObjDataSO assets with new `cost` and `hourlyCost` values:
  - **Staff wages** aligned to BLS 2024 medians: workers $19/hr, female workers $18/hr (player decision), boss $44/hr, security $20/hr, exterminator $65/hr
  - **Vehicles** corrected to realistic prices: ReachTruck $22,000, Dockstocker $5,500, PalletJack $900, Truck $85,000
  - **All decor/flavor/floor/racking hourly costs set to $0** — these items have no ongoing per-hour operational cost
  - **Guard Shack hourly dropped from $350 → $5** (the guard's wage is billed separately via the Security staff SO)
  - Racking, barriers, walls, doors repriced to current commercial averages
- Update applied directly to YAML asset files and confirmed in Unity via SerializedObject inspection.

### GPU Instancing audit & fix
- Audited all materials project-wide for GPU instancing status.
- All 28 materials in `Assets/6. Art/Materials/` were already correct (instancing ON).
- Found **37 materials** embedded in model FBX files (`Assets/5. Models/`) with instancing OFF.
- Enabled GPU instancing on all 37 — this means all repeated meshes (racks, walls, barriers, vehicles) now batch into single draw calls at runtime instead of generating individual draw calls per object.
- Note: dynamic batching was not relevant here — fence/rack meshes exceed the 300-vertex limit. GPU instancing is the correct approach and is now active everywhere.

### Cyclone fence Blender generator (`gen_cyclone_fence.py`)
- Wrote a fully parametric Blender Python script saved to `Assets/5. Models/BlenderFiles/`.
- Generates a **2m tall low-poly stylized cyclone fence segment** (1m wide):
  - Diamond wire pattern (crossing diagonal cylinders)
  - Top and bottom galvanized rails
  - Left and right posts
  - Three 45° barbed wire arms flush with the top rail, each with barbs
- All parameters at the top of the file: `FENCE_HEIGHT`, `DIAMOND_COLS/ROWS`, `WIRE_SIDES`, `BARB_COUNT`, `BARB_ARM_ANGLE_DEG`, etc.
- `WIRE_SIDES=4` recommended — visually equivalent to round wire at game distance, saves significant tris vs WIRE_SIDES=6 across 150 segments.
- Pricing research: $45 purchase / $0 hourly (commercial chain-link, installed per metre, 2024 rates).
- ObjDataSO and prefab to be created once FBX is exported from Blender.

---
