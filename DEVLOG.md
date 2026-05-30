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
