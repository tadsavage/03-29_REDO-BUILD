# MAESTRO.md — Session Handoff

**Written:** 2026-06-02 (approximate)
**Last Commit:** `75216d9c` — "@work - on emplyees"
**Project:** Warehouse/Facility Builder Sim — Unity 6 URP

---

## What We Just Did

### Last Commit: Massive Cleanup + Employee System Work
The most recent commit (`75216d9c`) was a large sweep touching **515 files**:

**Removals (housekeeping):**
- Removed unused `Nebula - Free low poly car pack` entirely (materials, meshes, prefabs, textures, scenes)
- Deleted `Assets/_Recovery/` — ~50 stale recovery scene snapshots
- Deleted `Assets/_Saves/` — autosave/quicksave/slot JSON files (now generated at runtime)
- Removed `BuildPhaseTasks.json`, `Project_Overview.md`, `AI Toolkit/Temp/` images
- Removed `Safety Cone` FBX duplicates and `Assets/models/blenderFiles/Men/IC.blend`

**Employee System (the actual feature work):**
- `EmployeeGenerator.cs` — modified
- `EmployeeIdentity.cs` — modified
- `EmployeeRecord.cs` — modified
- `EmployeeData.cs` (ScriptableObject) — modified, plus `WorkerFemale_EmployeeData.asset`
- `PlacementFinalizer.cs` — small fix
- `DeleteCommand.cs` — modified
- `FXPool.cs`, `GraphicsPresetManager.cs`, `NoWaypointIndicator.cs`, `TruckController.cs` — various tweaks
- **New prefabs added/updated:** `Truck_SavageDev.prefab`, BossNew, Exterminator, IC Clerk, Security, Truck Driver, WorkerFemale, WorkerMale
- New icon: `IC Clerk_preview.png`
- `employee_names.json` — updated name list
- `HUD.uxml` — tweaked
- `UIBootStrapper.cs` — one line added
- Font: `WorkSans-SemiBold SDF.asset` added
- `PP_Good/Toaster/Ultra.asset` — PostProcessing profile adjustments
- `Main.unity`, `MainMenu.unity` — scene changes

### Devlog Context (prior sessions this week)

**May 29:**
- Dev Console panel built (UI Toolkit, Two Point Hospital aesthetic)
- Unified ToolsWindow (Pallet Builder + Dev Console tabs, shared UXML)

**May 30-31:**
- Build bar/placement fully restored (ground mask + UI raycast fixes)
- Nav agent fixes (obstacle bake timing, rat nav, AiNavigation)
- Rat life system (breeding, scavenging, wall-hugging, curiosity)
- Guard shack lights & gate animation
- Security guard avatar fix (was T-pose from wrong rig import)
- ObjDataSO cost rebalance (all 68 assets, real 2024 pricing)
- GPU instancing audit & fix (37 embedded FBX materials fixed)
- Cyclone fence Blender generator script

---

## Current Project State

```
Branch: TestBranch5
Remote: https://github.com/tadsavage/03-29_REDO-BUILD
Clean: Check `git status` — likely clean or near-clean after the big commit
```

### Key Systems at a Glance

| System | Status | Key Files |
|--------|--------|-----------|
| **Employee System** | In progress — last commit was focused here | `Assets/1. Scripts/7. EmployeeSystem/` |
| **Placement/Build FSM** | Working (restored May 30) | `Assets/1. Scripts/1. FSM/` |
| **Nav Mesh / Pathfinding** | Working after fixes | `NavMeshManager.cs`, `AiNavigation` |
| **Rat Life System** | Working | `RatBehavior.cs` |
| **Economy** | Balanced (68 SOs with real-world prices) | `Assets/1. Scripts/3. ScriptableObjects/ObjData/` |
| **Dev Console / ToolsWindow** | Working | `Assets/3. UI/7.ToolsWindow/` |
| **Save/Load** | Working (F5 quicksave, F9 quickload) | FSM command stack serialization |
| **Gate Animation** | Working | `Gate_Open_Close.cs` |
| **GPU Instancing** | All materials now instancing ON | 28 + 37 materials fixed |

### Project Structure Quick Reference

```
Assets/
  1. Scripts/
    1. FSM/          — Placement state machine + commands
    3. ScriptableObjects/ — ObjDataSO, EmployeeData
    6. Utility/      — FXPool, TruckController, etc.
    7. EmployeeSystem/ — EmployeeGenerator, Identity, Record, etc.
  2. Prefabs/Workers/ — Boss, Clerk, Exterminator, Security, etc.
  3. UI/
    2. TopHUD/       — Runtime HUD
    5. DevPanel/     — Standalone dev console (replaced by ToolsWindow)
    7. ToolsWindow/  — Unified pallet builder + dev console
  5. Models/         — FBX models + BlenderFiles/
  6. Art/Materials/  — 28 materials (all instancing ON)
  8. Scenes/
    Main.unity       — Game scene
    MainMenu.unity   — Menu scene
```

---

## Immediate Next Steps

1. **Open Unity and verify `Main.unity` loads clean** — the scene file shrank from ~7MB to ~177KB in the last commit (likely removed baked data / unnecessary references)
2. **Check compilation** — the last commit touched many scripts; make sure Unity compiles without errors
3. **Test employee spawning** — prefabs and employee system were heavily modified; spawn some workers and verify they appear, animate, and nav correctly
4. **Review `Assets/notes.txt`** — has the running todo list; the last checked-off items were about rats, exterminator, female clerk, and wall color mismatches
5. **Next feature target** — based on `notes.txt`, the remaining wall color mismatch fix (underside of top ledge) is the last unchecked item
6. **Push to remote** if you want the cleanup commit on GitHub: `git push origin TestBranch5`

---

## Quick Commands

```bash
# Check git state
git status
git log --oneline -5

# Open project in Unity
# Just open the folder in Unity Hub / Unity 6

# If Unity complains about scene references, try:
# Assets → Open Scene → Main.unity
```
