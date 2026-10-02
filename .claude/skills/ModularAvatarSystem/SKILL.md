---
name: ModularAvatarSystem
description: The warehouse-sim modular avatar pipeline as it is ACTUALLY built (updated 2026-10-01) — Female Regular body split into 5 skinned parts, swappable clothing, AOD tool (Blender FBX -> finalized prefab), per-clothing body-part hide masks, live in-game rebuild. Use whenever working on avatar bodies, clothing, rigs, the AOD panel, the assembler, portraits, or anything under Assets/_Project/_Avatar_System or Prefabs/Modular_Staff_Prefabs.
---

# Modular Avatar System (current state — 2026-10-01)

Tad is a hobbyist; explain tradeoffs plainly, don't assume game-dev jargon lands.
Tad's role: planning, Blender modeling/rigging, testing, feedback. Claude's role: design decisions,
folder/naming conventions, C# (editor + runtime), Unity-MCP-driven setup and verification.

## SCOPE RIGHT NOW: FEMALE REGULAR ONLY

Everything is being built around ONE body type — **Female / Regular** — until it is fully working.
Male is deliberately parked (Tad, 2026-10-01: "not ready to work on males yet"):
- `AODPanel.HideMaleParts = true` hides male cards + the Male filter chip (non-destructive; nothing
  deleted from disk; flip to false to bring them back).
- `EmployeeSpawner.ModularBodyExists` returns false for any non-female gender, so male employees stay
  on the fixed Polyperfect models regardless of what gets cataloged.
- The other 5 body types (Female Tall/Portly, Male Regular/Tall/Portly) are the later "repeat the
  proven pattern" step. Do not start them before Tad says so.

## How it actually works (end to end)

**1. Blender (Tad).** Start from a store-bought character, modify it to fit the warehouse. The female
body is ONE `.blend` (`_Avatar_System/Female/Bodies/Regular/02_Blender/female_regular_body.blend`) split
into separate meshes bound to ONE shared armature: torso (`woman_bodyA_Cauc`), `head`, `hands`, `arms`,
`feet`. Clothing/replacers (coveralls, boots, gloves, sleeves, hair) are separate meshes on the same rig.
Export FBXs into `03_FBX` (bodies) or `Clothing/03_FBX/<Slot>/`.

**2. Naming is by MESH OBJECT name, not file name.** The importer parses the mesh names inside the FBX:
`gender_slot_variant` (e.g. `woman_torso_coverallsBlue`, `woman_feet_bootsBrn`, `neutral_hat_hardhat-red`).
**GOTCHA — the torso name:** the torso mesh is `woman_bodyA_Cauc`, which the importer's plain parser reads as
slot `bodya` / variant `Cauc` (NOT `body`). The finalized asset `AOD_Objects/woman_bodyA_Cauc.asset` was
hand-set to `slot: body`, and that is what works today (the scan skips it because it's already finalized).
A NEW torso variant (e.g. `woman_bodyA_Black`) would scan in as slot `bodya` and never be used as a body
until its `slot` is set to `body` in the asset. Adding a parser rule for `body*` was deliberately NOT done:
`FixNewExport` uses the same parser to decide which FBX gets the Humanoid rig, so changing it would change
import settings on a body that works. Decide that deliberately when the next torso variant is added.
(An older dotted form `woman.bodyA`, no slot token, is also recognized as `body`.)
FBX *file* names (`female_*`) are free-form and do NOT have to match the mesh/prefab names (`woman_*`).
`woman` and `female` (and `man`/`male`) are synonyms; `neutral` parts count for both genders.

**3. Import — `ModularAvatarImporter`** (Editor). A file watcher rescans on any FBX change in two roots:
the old `Models/BlenderFiles/Modular_Staff_Models` (hair/hat/glove props, `PROPS_MODELS`) and the new
`Assets/_Project/_Avatar_System` (note the leading underscore). Skips `01_StoreBought`, `Z-AOD_WORKSHOP`,
anything with "workshop" in the name, bulk pools (>20 skinned meshes), and Obsolete_Humanoids. Repairs
fresh exports (Humanoid rig on the body source only, material remap to `atlas-source-LPAP`), and refuses
files with duplicate object names (leftover duplicate armature) with one clear error. Menu:
`Tools > Modular Avatar > Scan & Rebuild Library` and `... > Finalize All Pending`.

**4. AOD — Avatar Object Database** (`UI_UX/AODPanel.cs`, Ctrl+Shift+A or the TopBar "AOD" button).
Per part you set: allowed roles, spawn weight (0-100%; **0% = never shown**), color variants (palette
swatches), and **"Hides body parts when worn"** (the clipping fix, below). **Submit/Update** calls
`ModularAvatarFinalizer`, which validates (a body part must have a SkinnedMeshRenderer with bones),
isolates that one mesh into a standalone prefab under
`Prefabs/Modular_Staff_Prefabs/{Body_Prefabs,Props_Prefabs}` and writes a per-part `AvatarPartAsset` into
`Models/BlenderFiles/Modular_Staff_Models/AOD_Objects/`. **Those `.asset` files are the source of truth the
game reads** (one file per part so git merges stay clean). The grid shows 2x-size cards (`CardScale`) with baked 3D
thumbnails on a pale, grayish avocado backdrop; all AOD text is Nunito Sans except the "AVATAR OBJECT DATABASE"
title (Lilita One). The card name strip is 100% transparent. Hands/gloves are shown as ONE glove filling the card.
The window opens maximized to the FULL screen height; the grid column is a fixed width (exactly 4 cards + scrollbar,
`gridColumn.style.width`) so the scrollbar hugs the cards and the preview column takes all remaining width.
The big preview rotates on the **Y axis only** (X tilt was tried and removed) around a pivot placed on the part's
own visual center; the pivot must be positioned BEFORE the part is parented under it. Unlit backdrops need
`Color.gamma` because the render textures are read back without sRGB encoding.
"Pimp My Employee" (click employee > Actions) edits one live employee's hair/hard hat/headphones/facial hair.

**5. Runtime — `ModularAvatarAssembler.Build`.** One part per slot, chosen by role filter + weight (seeded
from the employee GUID so a look is stable). Starts from the body's prefab, merges the other parts in by
re-binding bones BY NAME, hair/hat attach to the `Head_M` bone, then `ApplyBodyPartMasking`.
`EmployeeSpawner` attaches the result; `ModularAvatarRig` mirrors the worker's Animator parameters onto it
and swaps the face by mood. Pressing Submit/Update in the AOD **while playing** calls
`EmployeeSpawner.RefreshAllModularAvatars()` — every hired employee is rebuilt live and every portrait is
re-shot (`EmployeePhotoBooth`).

## Slots and the hide-mask rules

Body slots (`ModularAvatarAssembler.BodySlots`): `body` (the torso mesh), `head`, `neck`, `arms`, `hands`,
`waist`, `legs`, `feet`. **Hair is not a body slot** — it is authored/toggled like clothing.
Parts in the SAME slot are alternatives (one pick per slot): bare `hands` vs gloves, bare `arms` vs
sleeves, `torso` clothing vs nothing. So gloves/sleeves do not need to "hide" the bare version — they
replace it. Hide-lists are for a part hiding a DIFFERENT slot (coveralls hide the baked torso).

**Invariants (enforced in code, not convention): Head and Hair are never hidden.** `ApplyBodyPartMasking`
removes "head" from the hide set unconditionally; hair isn't a body slot so the pass never touches it.

Tad's current rules (2026-10-01):
- Coveralls (torso clothing): hide `body` (torso), `arms`, `waist`, `legs`. (Hands/feet/head/hair stay.)
- Body torso hides arms. Arms: weight 0 for now (coveralls carry the sleeves). Black sleeves cover arms.
- Gloves replace hands. Boots replace feet. Feet/neck/hair/hats hide nothing.
- Possible future change: feet as a separate mesh to avoid clipping with boots/shoes.
The hide config is baked into the part's asset at Submit/Update time; runtime just reads it.

## Verified behavior (live, 2026-10-01, 200 builds of random female avatars)
Torso hidden 200/200 (coveralls always chosen, visible), head 200/200, hands 200/200, feet 200/200.
**Hair was 0/200 — a real bug, fixed same day** (see Gotchas: unverified-weight-0 pool trap).

## The in-game gate
`EmployeeSpawner._floorWorkersUseModularBodyIfAvailable` (default now ON, and ON in `Main.unity`) decides
whether Receiver / ReachTruckOperator / DockStockerOperator / OrderSelector use the modular assembler.
It is a manual flag (not auto) because cataloging a WIP body once broke every worker at once. With it on:
female -> modular, male -> fixed Polyperfect. Other roles (Loader, Boss, Security, ...) are fixed models.

## Folder structure (real)

```
Assets/_Project/_Avatar_System/                       <- NOTE the underscore
  Female/Bodies/Regular/{01_StoreBought, 02_Blender, 03_FBX}
  Female/Clothing/03_FBX/{Arms, Feet, Hair, Hands, Torso}
  Male/...   (parked)
  Materials/                                          <- PolyPerfect atlas materials
Assets/_Project/Models/BlenderFiles/Modular_Staff_Models/
  PROPS_MODELS/        hair/hat/glove FBXs (old drop root, still scanned)
  Z-AOD_WORKSHOP/      master Blender workspace + bulk pool (never scanned)
  AOD_Objects/         finalized AvatarPartAsset .asset files  <- source of truth
Assets/_Project/Prefabs/Modular_Staff_Prefabs/{Body_Prefabs, Props_Prefabs}   <- finalized prefabs
Assets/Resources/ModularAvatar/{AvatarPartLibrary, AvatarWeightConfig}.asset  <- runtime catalog + weights
```
Code: `Scripts/Actors/ModularAvatar/` (runtime: library, asset, assembler, rig, preview stage) and
`Scripts/Actors/Editor/` (importer, finalizer); UI in `Scripts/UI_UX/AODPanel.cs`.

## Gotchas (each cost real time)

- **Part orientation (found + stop-gap 2026-10-01).** Rendering the assembled avatar from 4 sides showed the body,
  coveralls, hair and boots face +Z (the avatar's forward) but the HEAD face and the HARD HAT brims point -Z, and the
  HANDS/GLOVES are rolled ~180 degrees. `ModularAvatarAssembler.ApplyOrientationFixes` bakes the transforms Tad
  hand-tuned in the scene onto each part's ROOT GameObject: **head root = pos (0,0,0), rot (0,180,0)**; **every hands
  part (gloves + bare hands) root = pos (0, 0.02762616, 0.268777), rot (349.341, 0, 0)**; hard hats turn 180 about the
  avatar's origin axis so they stay aligned with the head. Applied as an OFFSET on the part's existing root transform
  (the old `neutral_hands_gloves*` prefabs have a -90X / 0.01-scale root, so overwriting would break them). The prefab
  roots themselves are all identity; the fix lives in code. Works because each part carries its OWN armature copy.
  **Delete the rule once the meshes are corrected in Blender and re-exported.** Headphones/hair/boots are correct.
  The AOD's per-part preview/thumbnails show the part AS AUTHORED (uncorrected); only Build() output is corrected.
- **Every merged part brings its own skeleton (open problem, found 2026-10-01).** `Build()` reparents the part's whole
  prefab root (it matches `FindDeep(temp, objectName)` on the ROOT GO first, because the finalized prefab root has the
  same name as its mesh), so the mesh is never rebound onto the body's skeleton: an avatar has 6 `Root_M` copies. Combined
  with the body's Animator avatar reading `isHuman=False`, nothing animates in game (permanent T-pose). Parts therefore
  cannot follow the animated body until this is fixed. Verify with a RunCommand that counts `Root_M` transforms.
- **Unverified + 0% weight trap (fixed 2026-10-01).** `PickVariant` prefers parts not yet "verified in game"
  so new items show up fast. A part that is unverified AND weight 0 is never picked, so it never becomes
  verified, and it used to be the ONLY candidate in the "unverified" pool -> pool total weight 0 -> slot
  empty (no hair on any woman). Fix: the bias only considers candidates with effective weight > 0
  (`EffectiveWeight`). If a slot is mysteriously empty, check for a zero-weight unverified part first.
- **Parser-timing "doubled" prefab.** `IsolatePart` only prunes siblings that `ParsesAsPart` recognizes. If a
  submitted part contains a stray copy of the torso, re-run Update after confirming the parser change compiled.
- **Duplicate armature** in an FBX (`DeformationSystem` + `DeformationSystem.001`) makes Humanoid Avatar
  creation fail; the importer now logs once and skips. Fix it in Blender and re-export.
- **FBX fileIDs** in Unity 6000.6 are big 64-bit hashes; never hand-guess them in YAML, copy from an existing reference.
- **Weight 0 means never shown** (a zero-total pool returns null, not a random fallback).
- **Portrait booth lights** are multiplied by `EmployeePhotoBooth.StudioLightBoost` (1.5) on top of the
  Inspector value; the camera framing was tuned for the old placeholder body.
- **Thumbnails are baked inside ONE frame** — use `DestroyImmediate` for every temp object in the bake, or the
  next thumbnail photographs the previous part's leftovers (deferred `Destroy` keeps them alive until frame end).
- **AOD preview/thumbnail** use a separate stage at y=400; `CameraClearFlags` color never applies in this
  URP setup, so the backdrop is a physical lit cube. Gloves ship as ONE mesh holding the pair; the
  thumbnail baker splits it and renders a single glove, camera fitted tight (`FitCameraTight`).

## Unity-MCP tooling notes
- **`Unity_RunCommand` WORKS** — but only as an `IRunCommand` class (`using
  Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;` `internal class CommandScript : IRunCommand { Title,
  Description, Execute(ExecutionResult result) }`, output via `result.Log(...)`). Top-level statements fail.
  Good for building avatars over many seeds and inspecting renderers, reading serialized flags, etc.
- Compiles do not happen in Play mode — stop Play first. After editing, `Assets/Refresh` via
  `Unity_ManageMenuItem`, then check `Library/ScriptAssemblies/Assembly-CSharp.dll` is newer than the source.
- The Editor sometimes needs OS focus to persist asset changes.
- Verify the BEHAVIOR (renderer enabled flags, a screenshot), not just that code compiled.

## Staged rollout
1. Folders — done.
2. First body (Female Regular) in Blender — done; naming/rig/export settings established.
3. Body-part runtime + AOD body import — done (assembler + finalizer + AOD).
4. First clothing + masking — done (coveralls/boots/gloves/hair; hide-masks baked and verified live).
5. NEXT: finish polishing Female Regular (portrait framing for the new proportions, arms/neck/feet details),
   THEN repeat for the other 5 body types and the wider clothing library. Males are parked until Tad says go.
