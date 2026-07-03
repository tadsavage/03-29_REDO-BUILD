---
description: Working on the dock / shipping-lane setup system — dock door numbering and shipping-lane auto-naming. Use whenever the task mentions shipping doors, dock doors, door numbers, DockSlot, shipping lanes, lane names (1A/2B…), Flr-ShipLane tiles, LaneNo labels, or files DockSlot.cs / DockNumberingService.cs / LaneNamingService.cs / DoorNumberDisplay.cs.
---

# Dock Door Numbering + Shipping-Lane Naming ("lane setup")

Two linked always-on services that label the receiving dock:
1. **Door numbering** — every shipping door shows a stable number (1,2,3…).
2. **Lane naming** — every shipping-lane tile shows `<doorNumber><letter>` (e.g. `1A`, `2C`) — one name per lane, globally unique = one exact place in the warehouse.

Built 2026-07-03. Both are **self-bootstrapping** (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` → a hidden `DontDestroyOnLoad` GameObject). **No scene wiring, no guard shack, no manager to configure.** Files all in `Assets/_Project/Scripts/Gameplay/`.

## Core data model
- **Door** = a `ShippingDoor` prefab (ObjData id **63**) carrying a `DockSlot` (registers in static `DockSlot.All`), a `PlacedObject`, and a `DoorNumberDisplay` (holds a `textComponents` TMP array it sets via `Number`). `DockSlot` caches the display in Awake and pushes `DoorNumber` into it.
- **Lane** = one row of **`Flr-ShipLane`** tiles (ObjData id **35**, "Ship Lane", 1×1, category Floor, `isFloor`) running OUT from the door in the depth direction; ~5-8 tiles long. Lanes sit side-by-side along the wall. The `Flr-ShipLane` prefab already has a child GameObject **`LaneNo`** with a `TMPro.TextMeshPro` label (prefab default text "1A") — that's the on-tile display AND how a lane tile is self-identified (never hard-code id 35: look for a `LaneNo` child).
- The free ground tile is **`flr-YardTile`** (id 200, cost 0) — NOT a lane; it's the base floor.

## Door numbering — `DockSlot.AssignDoorNumbers()` (static)
**Numbers are permanent once assigned and NEVER renumber existing doors.** This is load-bearing: trucks, lane names, and employee/inventory destinations all reference door numbers, so we only ever **add or remove**, never rename (a manual double-click rename UI can come later, like racks).

- Persisted in the door's **`PlacedObject.customData`** (doors use customData for nothing else; it is saved & restored — see `PlacementSystem.SpawnFromSave` sets `po.customData` AFTER `Instantiate`, so `OnEnable` can't read it → the service re-runs post-load via heartbeat/events).
- A door with a persisted number keeps it. Doors without one get the **lowest unused** number (gap-fill/recycling like aisle numbers): 1-4 exist → new door = **5**; delete door 2 → next door placed reuses **2**.
- Pre-persistence saves (empty customData) bootstrap ONCE by position (X-then-Z), matching the original order. To retrofit a specific save, stamp `customData` per door entry (id 63) directly in the save JSON.
- `DoorNumber` setter is `private`; only `SetNumber(n, persist)` writes it.
- Callers: `DockSlot.OnEnable/OnDisable`, `DockNumberingService` (events + 1s heartbeat), `TruckYardManager.AssignDoorNumbers()` (delegates). All idempotent.
- **Known edge:** undo of a door delete after its freed number was reused can collide. Rare in the add/remove flow; accepted.

## Lane naming — `LaneNamingService.Recompute()`
Recomputes on `OnObjectPlaced`/`OnObjectDeleted` + a 1s heartbeat (heartbeat catches save-loads, which bypass placement events).

Algorithm (**this order matters**):
1. Collect lane tiles = active `PlacedObject`s with a `LaneNo` child (`transform.Find("LaneNo")`).
2. **Assign each tile to its NEAREST door first** (XZ distance to `DockSlot.transform.position`), then group **per door**. Critical: docks can sit on opposite walls (e.g. one at grid x=67 facing −X, another at x=36 facing +X) and two pads can share a wall-coordinate — a global grouping would merge them.
3. Per door, the **wall axis (which lanes sit side-by-side along) comes from THAT door's `transform.forward`**, not from global door spread: `wallIsZ = |fwd.x| >= |fwd.z|`. Lanes RUN in the facing/depth direction; they're separated along the perpendicular wall axis. Group the door's tiles by `round(wallCoord / cellSize)` = one lane.
4. Letter lanes along the wall, **A = the LOWEST wall-coordinate lane** (const `LetterFromHighWallCoord = false` → ascending; flip only for a weird dock). `IndexToLetters` is Excel-style (A…Z, AA…).
5. Write `door.DoorNumber + letter` into every tile's `LaneNo` label (all tiles in a lane show the same name).

**Do NOT** reintroduce a global `DoorsSpreadAlongZ`-style wall-axis pick — that was the 2026-07-03 bug (adding a door on a far/opposite wall in X flipped the axis and lettered lanes as columns).

## Verified geometry (reference)
- 4-door wall: doors grid x=67, y=38/45/53/61, rot=3 (face −X). Lane block x57-63 (depth) × y36-62; per-door clusters (Y gaps between) → 5,6,6,5 lanes = `1A-1E, 2A-2F, 3A-3F, 4A-4E`.
- 5th door: x=36, y=38, rot=1 (faces +X). Pad x40-44 (depth) × y36-39 → 4 lanes `5A-5D`, A at lowest Y.

## Gotchas / workflow
- **The Unity MCP bridge drops on every recompile** (and can't refresh/compile while the Editor is in **Play mode** — stop Play first). Verify compiles by reading `%LOCALAPPDATA%/Unity/Editor/Editor.log` for `error CS`. Use `FindAnyObjectByType` (not the obsolete `FindFirstObjectByType`).
- Lane names update live; to see them, be in Play mode with doors + `Flr-ShipLane` tiles placed (they don't exist in the edit-mode scene — they're runtime/save-driven; `PlacedObjectsContainer` is empty at edit time).
- Every ship-lane tile shows the name (repeats down a lane). If it looks cluttered, restrict to one tile per lane (e.g. nearest the door) — not done yet.

## Open / future
- Manual door rename UI (double-click, like racks).
- "Shipping/receiving PAD" object: a parent grouping one door's lanes (rack-collection style) that owns the lane collection + door reference, for inventory/task routing. Tad's idea; not built.
- Lane letter direction + one-label-per-lane are cosmetic tunables.

## Related
CLAUDE.md; memory: `shipping-lane-naming-system`, `door-numbering-decoupled-from-guardshack`, `mcp-bridge-daily-fix`. The racking system (`racking-system` skill) uses the same self-bootstrapping + world-geometry-labeling patterns.
