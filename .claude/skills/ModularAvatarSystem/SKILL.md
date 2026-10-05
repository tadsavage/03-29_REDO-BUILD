---
name: ModularAvatarSystem
description: The warehouse-sim modular avatar pipeline as it is ACTUALLY built (Avatar 2.0, updated 2026-10-04) — Female Regular body in 5 skinned parts on ONE rig + neck/face/hair slots, Body-default fallback, AOD tool (Blender FBX -> finalized prefab), hide masks, live in-game rebuild, foot contact shadows. Use whenever working on avatar bodies, clothing, rigs, the AOD panel, the assembler, portraits, or anything under Assets/_Project/__Avatar_System2.0.
---

# Modular Avatar System (Avatar 2.0 — current state 2026-10-03)

**START HERE (handoff 2026-10-03):** the full session log, the working Blender->Unity recipe, gotchas and the exact pending steps are in the repo `CLAUDE.md` section
"Session 2026-10-02 -> 10-03 - AVATAR 2.0 REBUILT, WORKING END TO END". The sections below are the detailed rules; anything about the OLD pipeline (coveralls, woman_bodyA_Cauc, AOD_Objects, `_Avatar_System`) is history.

## CURRENT STATE / NEXT STEPS (updated 2026-10-04; the 2026-10-03 "pending" list is obsolete)
- Git is at `db3d598a4` (2026-10-04 22:27): face slot + gag, eye darting/jaw tuning, Pimp My Employee clothing tabs, portrait booth fixes (see the 2026-10-04 addendum at the bottom).
- **Hair in the working `.blend`:** four separate meshes `Female_Hair_Mem_Black/Blonde/Red/White` (129 verts / 149 faces each, ONE Armature modifier -> `DeformationSystem`, vertex groups `Head_M` + `Neck_M`, material `lambert2.001`). On 2026-10-04 Tad found
  the exported hair had missing faces on the side locks; the four hair objects in `Female_Body_Reg-White.blend` were replaced with the ones from commit `279cd807f` ("321321"), saved as `1. Blender/body_backup.blend` (reference copy; a pre-swap copy of the working file was kept outside git).
  The side locks are several separate pieces joined only along non-manifold edges (3-4 faces on one edge), so "Recalculate Normals" does nothing; if faces look missing, the locks genuinely lack walls, they are not flipped. Backface culling in the Blender viewport (Shading overlay) shows what Unity will show.
  **A hair swap/append from another .blend pulls in the SOURCE file's armature as a second object** (`DeformationSystem.001`) because the hair is parented to it: re-parent to the scene's armature and delete the extra one before saving.
- **Old clothing brought onto the 2.0 rig (2026-10-04), each its own FBX in `2. FBX`, all skinned to `DeformationSystem`, weights capped at 4 + normalized, material `AA_PP_ALBEDO`:** hard hats `Neutral_Hat_HardhatOrange/Red/White/Yellow` (rigid, 100% `Head_M`),
  boots `Female_Feet_BootsBlack/Brown/Gray`, gloves `Female_Hands_GlovesBlack/Blue/Tan`, sleeves `Female_Arms_SleevesBlack` (2.0 has no `Female_Arms_Body`, the arms live in the torso mesh, so sleeves overlay the torso arms: check clipping),
  coveralls `Female_Torso_CoverallsBrown/Gray` (blue already lives in `female_regular_body.fbx`; brown/gray copy the blue's weights, same geometry), hair `Female_Hair_BobBlack/BobBlonde/BobPink/BobRed/Harley` (rigid, 100% `Head_M`).
  The old FBXs share the 2.0 rig's bone names and positions, so no rebuild was needed: apply the import transform, delete the old armature copy, parent to the 2.0 armature. NOT yet checked in Unity (colors, facing, weight-cap look): Scan, Finalize, then set them up in the AOD.
- Tad's plan: more hair types, then clothing (torso/legs interchangeable, one-piece coveralls hiding torso+legs, own FBX per garment, hide masks). Male + other body types stay parked.
- **Employee role `Loader` was REMOVED 2026-10-04** (dock stockers do the loading). `EmployeeRole` values are PINNED (3 is retired, never reuse): roles are stored as ints in saves and assets, so `EmployeeRoleExtensions.Normalize` maps a leftover 3 to `DockStockerOperator`
  (applied on load in `EmployeeRecord`, `HiringCandidate`, and saved WorkTasks). Load tasks are filed as `DockStockerOperator`; the old `allowedRoles`/RoleConfig/RoleIconLibrary/RolePoolConfig entries for 3 were stripped from the assets.

## AVATAR 2.0 PATHS (read first — supersedes any older path in this file)
Tad restarted the pipeline from scratch ("Avatar 2.0"). **The ONLY working root is
`Assets/_Project/__Avatar_System2.0`** (two leading underscores, `2.0`). The old `_Avatar_System`,
`Models/BlenderFiles/Modular_Staff_Models` (PROPS_MODELS, AOD_Objects, Z-AOD_WORKSHOP) and
`Prefabs/Modular_Staff_Prefabs` are NOT read by the AOD any more (stale-data isolation); do not point
anything back at them unless Tad says so.
- `ModularAvatarImporter.DropFolder = "Assets/_Project/__Avatar_System2.0"` is the single scan root
  (`NewPipelineRoot` is an alias kept so old references compile). Subfolders are scanned recursively.
- Per body type: `__Avatar_System2.0/<Gender>/<Type>_Body_Type/{1. Blender, 2. FBX, 3. Prefab (post AOD Submit)}`.
  Today only `Female/Regular_Body_Type` exists (`Male/` is an empty placeholder). Put the `.blend` in `1. Blender`
  (never scanned), exported FBXs in `2. FBX`.
- `ModularAvatarFinalizer` Submit/Update writes BOTH the finalized prefab and the `AvatarPartAsset` `.asset` into
  `Female/Regular_Body_Type/3. Prefab (post AOD Submit)`. **Hardcoded to Female Regular** (`FinalizedAssetFolder`,
  `BodyPrefabFolder`, `PropsPrefabFolder`); per-gender/body-type routing is still TODO. Those assets are reloaded into
  the library on every `ScanAndRebuild` (full rebuild: clears `sources`/`parts`, reloads `finalizedParts`).
- Runtime catalog + weights moved: `Assets/_Project/Resources/Resource_AvatarSystemAssets/{AvatarPartLibrary,AvatarWeightConfig}.asset`
  (loaded via `Resources.Load("Resource_AvatarSystemAssets/...")`). Hair/clipboard/scan-gun fallbacks load from the same
  folder; those files are not there yet. All `_Project/Resources` subfolders are `Resouce_UI` (sic), `Resource_AvatarSystemAssets`,
  `Resource_Fonts`, `Resource_Prefab`, `Resource_SOs`.
- The library was emptied at the restart (`sources`/`parts`/`finalizedParts` all `[]`). Everything below describing
  specific parts (coveralls, boots, gloves, hair, `woman_bodyA_Cauc`, `AOD_Objects/...`) is HISTORY from the old pipeline
  and must be re-created in 2.0, one piece at a time: body, then hands, then walk test.
- Do not trust older folder names below ("Folder structure (real)", "03_FBX", "01_StoreBought") — they describe the old tree.
  `01_StoreBought`/`Z-AOD_WORKSHOP`/"workshop" are still skipped by the scanner, and `.blend` files are skipped.
- **FIRST 2.0 EXPORT (2026-10-02, verified in Unity):** `2. FBX/female_regular_body.fbx` = 5 meshes (`Female_{Torso,Legs,Hands,Head,Feet}_Body`) on ONE
  armature (72 bones), exported with preset `Unity_Avatar` from Blender 5.2.2. Blender prep that was needed: the Group/Main empties
  (rot 90 / scale 0.01) and a x100 parent-inverse on the head were baked away so armature + meshes are loc 0 / rot 0 / scale 1 with no
  empty parents; every mesh got one Armature modifier -> `DeformationSystem` (the torso had none, the hands' pointed at nothing); weights
  capped at 4 influences + normalized (Unity uses 4). Unity result: Humanoid avatar `isHuman=True`, 5 SkinnedMeshRenderers x 72 bones,
  root rot 0 / scale 1, height 1.78 m, **faces +Z with Left hand at -X (correct)** - so the OLD "left bones on +X" problem is gone.
  **The `(0,180,0)` root-yaw stop-gap in `EmployeeSpawner.ApplyModularAvatar` was REMOVED (2026-10-02, localRotation is identity).**
  Importer change: `IsBodySlotName` now also accepts slot `torso` so the Humanoid rig lands on this FBX.
  **`Body` fallback DONE + verified (2026-10-02):** `ModularAvatarAssembler.IsBodyDefault` (variant `Body` in any body slot, incl. `torso`; `body` is the
  legacy alias, `IsTorsoSlot`). In `Build()` a body slot picks a wardrobe item (non-`Body`, role+weight filtered) and otherwise shows the slot's `Body`
  default (ignores role/weight); the nude torso always anchors the rig/Animator even when a garment replaces it; masking aliases `body`<->`torso`
  and still hides a defaulted slot another part covers (empty != hidden). `Body` parts skip the old head/hands orientation stop-gaps.
  Finalizer validates + routes all body slots; AOD chips use `torso`. Verified: 25/25 builds = 5 parts, 1 Root_M, 1 Animator, root rot 0;
  live in Play Mode the avatar walks facing forward (Left hand on worker -X). NOT yet exercised: a real clothing item replacing/hiding a Body
  default (no clothing exists in 2.0 yet), and the AOD still shows weight controls on `Body` parts (ignored by the assembler). Parts must be
  Submitted (`Tools > Modular Avatar > Finalize All Pending`) before `ModularBodyExists` lets floor workers use them. Blender
  file was saved; pre-cleanup backup is `%TEMP%\Female_Body_Reg-White_PRE-CLEANUP.blend`.
- **HAIR + LEGACY FLIPS (2026-10-03):** the old orientation stop-gaps (head/hair/hands/hard-hat 180 deg flips, `ApplyLegacyOrientationFixes`) are now OFF - they turned the Avatar 2.0
  black bob BACKWARDS. Rigid (unskinned) parts attach as just the mesh object on Head_M/Neck_M (no spare armature copy). First hair: the store-bought bob, named `woman_hair_BOB` in the first export and renamed `Female_Hair_BobBlack` by Tad (mirrored,
  weighted 100% to `Head_M` so it moves rigidly with the head; UVs on the near-black palette cell). **A skinned mesh with ZERO weights collapses to the origin in Unity - weight every vertex.**
  **Exports MUST use the `Unity_Avatar` preset**: a manual export at 01:07 had every node at scale 100 and exploded all avatars; after ANY FBX re-export re-run Finalize All Pending for ALL parts
  (finalized prefabs keep bone lists from the FBX they were made from) and restart Play (compiling mid-play corrupts live avatars).
- **A part never hides its own slot (fixed 2026-10-03):** `ApplyBodyPartMasking` ignores a hide-list entry equal to the part's own slot. Before this, ticking `feet` on `Female_Feet_Socks-Gray` in the AOD made the socks
  disable THEMSELVES in game (renderer off) while looking fine in the prefab. A garment replaces its slot's nude `Body` default just by being picked; hide boxes are only for hiding a DIFFERENT slot.
- **BARE CHANCE (2026-10-03):** the nude `Body` part's AOD weight is the share of employees who wear NOTHING in that slot: P(bare) = BodyWeight / (BodyWeight + sum of the slot's clothing weights). socks 100 + Body 100 = 50% bare; Body 0 = never bare.
  Needed because clothing weights only compete with EACH OTHER (a lone item at any weight > 0 is worn by everyone). Verified over 300 builds (50% / 75% / 0%). The AOD labels a `Body` part's slider "BARE CHANCE". Defaults: feet Body=1 (~1% bare), legs Body=0.
- **MATCHING SETS (2026-10-03):** `ModularAvatarAssembler.LinkedSlotPairs = (feet, legs)`. If exactly ONE slot of the pair is dressed and the other slot offers the SAME variant name (case-insensitive, e.g. both `Stockings-Gray`),
  the other slot wears it too (either direction; role filter applies, the partner's weight is ignored). Both bare or both dressed = untouched. A bare slot does not pull its partner bare, so with feet bare + legs stockings the feet get
  stockings: bare feet only happen when the legs are bare too. Add more pairs to that array. Tad tunes the AOD weights himself - do not overwrite them (feet Body 0 / stockings 75, legs Body 0 / stockings 50 at 2026-10-03).
- **NSFW / DIRTY DEV (2026-10-03):** the NSFW tag is part of the Blender MESH NAME: `gender_slot_variant_nsfw` (4th segment `nsfw` = tagged; blank/absent = not), e.g. `Female_Head_Gagged_NSFW`. `DirtyDev.IsNsfwName` is the single
  rule; nothing is stored per part. `DirtyDev.Enabled` (PlayerPrefs `DirtyDev`, default OFF = NSFW HIDDEN, so every fresh build is safe) is flipped by the AOD title-bar button (red = OFF, green = ON, caption "Dirty Dev currently ON/OFF").
  OFF hides tagged content everywhere: `AvatarPartLibrary.SlotsFor/VariantsFor` skip tagged parts (so the assembler, spawner and Pimp My Employee never see them), AOD cards/counts hide them, the override lookup ignores them; ON shows them.
  Toggling in Play rebuilds all live avatars + portraits. Nothing is deleted. Any scene object can also be tagged with the `NsfwObject` component (hides its renderers while OFF). Importer strips `_nsfw` (and a trailing blank segment) from the variant.
  **RENAME MIGRATION GOTCHA:** renaming a mesh in Blender creates a NEW part on the next export; the old finalized asset (old name, NOT tagged) stays visible until deleted, and AOD weights start fresh on the new asset. After re-exporting renamed meshes: Scan, Finalize, copy the AOD weights across, then delete the stale untagged assets + prefabs.
- **GAG MOUTH MOTION (2026-10-03):** `GagMouthMotion.cs`, attached by `EmployeeSpawner.ApplyModularAvatar` when the assembled HEAD part's variant contains "gag" (`Build` now reports `chosenOut["head"]`). It overrides the jaw bone in LateUpdate:
  slow ~1 s groans (open about -Z on `Jaw_M`, up to 24 deg / ~3.4 cm), bursts of 1-3, pauses 0.45-1.4 s, eased + SmoothDamp'ed, scaled game time. ROOT CAUSE of the old random mouth twitch on everyone: the Walking clip holds `Jaw Close` = 1.03 while
  every other clip holds 0, so crossfading walk<->stand/turn opens/closes the jaw. (Not fixed for non-gagged characters; zero the Jaw Close curve in the walk clips or clamp it if wanted.) Tuning constants are at the top of the file.
- **NECK SLOT (2026-10-02):** `neck` is an optional ACCESSORY slot (NOT a body slot: no `Body` default, not validated as skinned, removed from `BodySlots`; const `ModularAvatarAssembler.NeckSlot`).
  `OptionalSlotChance["neck"]=0.35`; in `EditableOverrideKeys` + AOD `EmployeeCategories` ("Neck Items"); rigid (unskinned) pieces parent to `Neck_M`; `ApplyBodyPartMasking`
  still lets a garment hide it (hide list entry `neck`, now disables ANY Renderer, not only SkinnedMeshRenderer). Importer/assembler also accept `Female_Neck.Collar` (one `_` then a `.`)
  as gender_slot.variant. First item: `Female_Neck_Collar` (skinned, 72 bones). Verified: 20/60 builds wear it, 0 builds with a wrong body/skeleton.
- **BODY = ONE FBX (Tad, 2026-10-02):** always export armature + all 5 `Female_*_Body` meshes into `female_regular_body.fbx`. A subset export overwrites it and
  the other parts disappear from Unity (happened once; recovered by re-exporting all five + Finalize All Pending). Clothing/hair/hats = one FBX each.
  Cap weights at 4 + Normalize All in Blender first. AOD Update only re-reads the FBX - verify the FBX mtime changed after exporting.
- Blender is 5.2.x on both PCs (5.1 and 4.3 were uninstalled); the preset goes in `%APPDATA%\Blender Foundation\Blender\5.2\scripts\presets\operator\export_scene.fbx\`.

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

## How it actually works (end to end) — OLD PIPELINE TEXT, history only
Everything from here to "Slots and the hide-mask rules" describes the pre-Avatar-2.0 pipeline (`_Avatar_System`, `woman_bodyA_Cauc`, `AOD_Objects`, `03_FBX`). The flow (Blender -> FBX -> importer -> AOD Submit -> assembler) is the same;
the paths, part names and the `body` slot name are not. Use the Avatar 2.0 sections above for anything concrete.

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
female -> modular, male -> fixed Polyperfect. Other roles (Boss, Security, ...) are fixed models. (The old `Loader` role was removed 2026-10-04; dock stockers do the loading.)

## Folder structure — OLD pipeline (history; replaced by Avatar 2.0 paths at the top)

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
Assets/Resources/ModularAvatar/{AvatarPartLibrary, AvatarWeightConfig}.asset  <- (old location, now Resources/Resource_AvatarSystemAssets)
```

NEW (Avatar 2.0):
```
Assets/_Project/__Avatar_System2.0/
  Female/Regular_Body_Type/{1. Blender, 2. FBX, 3. Prefab (post AOD Submit)}   <- prefabs + AvatarPartAssets land in 3.
  Male/                                                                          <- parked, empty
Assets/_Project/Resources/Resource_AvatarSystemAssets/{AvatarPartLibrary, AvatarWeightConfig}.asset
```
Code: `Scripts/Actors/ModularAvatar/` (runtime: library, asset, assembler, rig, preview stage) and
`Scripts/Actors/Editor/` (importer, finalizer); UI in `Scripts/UI_UX/AODPanel.cs`.

## Blender workflow (one armature per body type)
Full checklist: `Assets/_Project/__Avatar_System2.0/Docs/BLENDER_CHECKLIST.md`; full design + TODO list:
`Assets/_Project/__Avatar_System2.0/Docs/AVATAR_SLOTS_AND_BODY_FALLBACK.md` (read it before touching slots/importer/assembler).
Summary: ONE armature (`DeformationSystem`) per body type with every body part and clothing part a separate mesh skinned
to it (identical rig, rest pose and armature transform in every file); apply rotation/scale, exactly one armature per
export (no `.001` duplicates), mesh names `Gender_Slot_Variant`, body facing +Z in Unity.
Export ONLY with the saved FBX preset **`Unity_Avatar`** (source in git: `BlenderPresets/Unity_Avatar.py`; install into
`%APPDATA%\Blender Foundation\Blender\5.2\scripts\presets\operator\export_scene.fbx\`; must be UTF-8 WITHOUT BOM). Values:
Forward -Z, Up Y, FBX Units Scale, bake-space-transform off, Selected Objects, Armature+Mesh, deform bones only, no leaf
bones, no animation. The axis values (Forward -Z, Up Y) are VERIFIED in Unity (2026-10-02: body faces +Z, Left hand at -X).
Hidden Blender objects are silently skipped by "Selected Objects" exports: unhide first. The preset applies modifiers (a Mirror modifier is baked at export; the Armature modifier is not), so mirrored weights must be symmetric.
The preset file must be UTF-8 without a BOM (PowerShell 5.1 `Set-Content -Encoding utf8` adds one and Blender then rejects the preset).

## Slot design decided 2026-10-02 — the `Body` variant (IMPLEMENTED and verified 2026-10-02/03, see the Avatar 2.0 section above)
- No separate Body slot. Each body part has its own slot and the nude base skin of that slot is the part whose variant is
  `Body`: `Female_Torso_Body`, `Female_Legs_Body`, `Female_Arms_Body`, `Female_Hands_Body`, `Female_Feet_Body`,
  `Female_Head_Body` (Tad already renamed his Blender meshes this way; the old name was `Female_Body_Reg`).
- `Body` = reserved variant = default skin: if a slot has no equipped part and is not hidden by another part, show that
  slot's `Body` part, so an avatar is never missing a mesh (worst case, nude). "Empty" and "hidden by another part" are
  different and must stay distinct. A `Body` variant must never be picked as normal wardrobe.
- Torso and legs are split into separate meshes (slots `torso`, `legs`) so shirts, pants and shorts are interchangeable.
- Tad exports the body only for now (torso + legs + armature); hands, feet, head come later.
- DONE: the torso slot is `torso` (`body` kept as a legacy alias), the Body fallback lives in `ModularAvatarAssembler.Build`, the importer's `IsBodySlotName`/`FixNewExport` send the Humanoid rig to the body FBX, and the AOD uses `torso`.
  `neck` was settled as an optional accessory slot (no `Body` mesh); `face` was added later. Still open: `waist` (legacy, unused).
- Blender can be driven from Claude via the BlenderMCP add-on socket `127.0.0.1:9876` (`get_scene_info`, `execute_code`;
  `print()` output only, `result` is not returned).

## Gotchas (each cost real time)

- **Skeleton merge (FIXED 2026-10-02, verified in Edit mode only).** Every part FBX ships its own armature copy, and
  those copies do NOT share the body's rest pose: sleeves/boots/gloves/coveralls are rotated 180 deg about Y (their "_L"
  bones physically sit on the body's right side), the head is turned 90 deg, hair/bare arms/bare hands match. The old
  `Build()` matched `FindDeep(temp, objectName)` on the prefab ROOT (same name as the mesh), saw no SkinnedMeshRenderer
  there, and reparented the whole prefab, so every part kept a private skeleton + Animator (4-6 `Root_M` copies).
  Now `TryRebindToBody`: finds the real SMR, moves ONLY the mesh object under the avatar root, re-points `bones[]` and
  `rootBone` at the body's skeleton, and destroys the part's armature. Details that matter:
  - Bones are matched by name, but for `_L`/`_R` bones it picks whichever of {same name, opposite side} is CLOSER at rest
    (`SwapSide`). Name-only matching made the right arm follow the left arm's animation (arm up, hands detached).
  - Bindposes are re-based: `newBp = bodyBone^-1 * correction * partBone * oldBp`. A skinned vertex is
    `bone.localToWorld * bindpose * v`, so this keeps each part looking exactly as authored at rest and then follows the
    body. The rebased mesh is cloned ONCE and cached (`ReboundMeshCache`, keyed by mesh + body prefab + correction kind);
    parts whose rest already matches the body (hair, bare arms/hands) use the original mesh, no clone.
  - Result: 1 `Root_M`, 1 Animator, ~90 transforms per avatar; rendered idle + walking frames look right.
  - Verify with a RunCommand that builds avatars and counts `Root_M` + prints `smr.bones` parents.
  - **The part-orientation stop-gap (head 180 about Y, hands offset, see below) is now baked into the BINDPOSES
    (`CorrectionMatrix`) for rebound parts, not applied as a root transform** — a root transform does nothing to a
    skinned mesh bound to the body's bones. `ApplyOrientationFixes` skips rebound parts and still handles the unskinned
    hard hat and the old `neutral_hands_gloves*` prefabs.
- **Body avatar must be Humanoid (FIXED 2026-10-02).** The MaleStaff controller's clips are humanoid, but
  `female_bodyA_base_Caucasian.fbx` imported as Generic, so the Animator did nothing. Cause: the importer decided "body
  source" by `slot == "body"`, but the torso mesh `woman_bodyA_Cauc` parses as slot `bodya`. `IsBodySlotName` now accepts
  both, so `FixNewExport` flips that FBX to Humanoid / CreateFromThisModel (avatar `isHuman=True`). Do NOT use
  CopyFromOther(_MainRigAvatar) — it fails ("Parent for 'DeformationSystem' differs ... 'Main'"). Note the auto avatar
  maps only 26 bones (no Chest/fingers beyond proximal) — fine so far, revisit if finger/chest animation looks off.
- **Animated avatar faced backwards (old pipeline; the yaw was REMOVED 2026-10-02 once the Avatar 2.0 rig faced +Z with Left at -X — do not re-add it).** The body rig has its "Left" bones on +X while
  the mesh faces +Z, so Unity's Humanoid retarget thinks the avatar faces -Z and turns every ANIMATED pose 180 deg
  (rest/T-pose still faces +Z, which hid it). `EmployeeSpawner.ApplyModularAvatar` now sets the avatar root's local
  rotation to (0,180,0). Verified by render: faces + bib pockets toward the camera, natural walk. **Do NOT swap Left/Right
  in the Humanoid mapping** — tried it, the arms go straight up. Proper long-term fix is Blender: re-export the rig with
  Left on -X (then remove the yaw). Don't misjudge facing from wrist X coordinates or boot-vs-ankle offsets; render it
  and look for the face / bib pocket vs parallel strap backs.
- **Part orientation (stop-gap 2026-10-01).** Rendering the assembled avatar from 4 sides showed the body,
  coveralls, hair and boots face +Z (the avatar's forward) but the HEAD face and the HARD HAT brims point -Z, and the
  HANDS/GLOVES are rolled ~180 degrees. Tad's hand-tuned corrections: **head = pos (0,0,0), rot (0,180,0)**; **every hands
  part (gloves + bare hands) = pos (0, 0.02762616, 0.268777), rot (349.341, 0, 0)**; hard hats turn 180 about the
  avatar's origin axis so they stay aligned with the head. For skinned parts these are folded into the bindposes (see
  above); for the old `neutral_hands_gloves*` prefabs (-90X / 0.01-scale root) they are still root-transform offsets.
  **2026-10-02 correction: hair and headphones ALSO need the head's 180 flip** (earlier note said they were correct — that
  was judged from position offsets, which can't show orientation; the shape can: bangs over the forehead / bulk behind =
  right). Skinned hair gets it via `CorrectionKind` (same as head); unskinned hair/headphones/hard hats via the
  RotateAround branch in `ApplyOrientationFixes`. Boots and coveralls are correct.
  **Delete the rule once the meshes are corrected in Blender and re-exported.**
  The AOD's per-part preview/thumbnails show the part AS AUTHORED (uncorrected); only Build() output is corrected.
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
1. Folders — done (Avatar 2.0 tree).
2. Female Regular body in Blender: 5 `_Body` meshes on one armature, `Unity_Avatar` preset — done and verified in Unity.
3. Body-part runtime + AOD, `torso` slot + `Body` fallback, neck/face slots, NSFW tag, Pimp My Employee tabs — done.
4. Hair/clothing in 2.0 — the old hats, boots, gloves, sleeves, coveralls and hair were re-rigged and exported 2026-10-04 (see the state section at the top); Unity check + AOD setup are next.
5. NEXT: Scan + Finalize the new FBXs and set them up in the AOD. Then polish Female Regular, THEN repeat for the other 5 body types. Males are parked until Tad says go.

## Addendum 2026-10-04 - face slot, Pimp My Employee clothes, portraits
- **`face` slot** (optional, 50%): accessories on the face (gag now; blindfold/piercings later) are separate skinned meshes `Female_Face_<Name>[_NSFW]`; the head mesh is never swapped. Gag detection = chosen `face` variant contains "gag" (fallback: head variant) -> `GagMouthMotion` + `FaceExpressionController.EnableDarting`.
- **Never move humanoid-mapped bones** (`Eye_L/R`, `EyeEnd`) in Blender - the Animator snaps them back and eyeballs poke out. Custom `Eyelid*` bones are free. After bmesh edits on FBX meshes clear custom split normals (`customdata_custom_splitnormals_clear`) or you get dark smears.
- **Pimp My Employee keys:** `hair, hat.hardhat, hat.headphones, facialhair, neck, face, torso, hands, legs, feet` (`ModularAvatarAssembler.EditableOverrideKeys` + `AODPanel.EmployeeCategories`). Add a key in BOTH places, plus `OverrideCategoryInfo` if the slot name differs.
- **Portraits:** after ANY look change call `EmployeeSpawner.RefreshLookAndPortrait(identity)` (never just `RefreshAvatarAppearance`). The portrait booth stand-in prefab lacks `EmployeeIdentity`; `EnsureBoothIdentity` supplies it - without it portraits show the placeholder body. Framing aims at the head bone (`StillAimBelowHead`, `LiveAimBelowHead`, `ModularLiveFov`). `EmployeePhotoBooth.OnPortraitUpdated` refreshes open lists.
- **Verification recipes:** render a gagged avatar with a temp camera at `Time.timeScale = 0.0001`; dump a portrait with `CustomAvatarCache[...].texture.EncodeToPNG()`; capture logs with `Application.logMessageReceived` inside `execute_code`.
- A same-frame `transform.Find("ModularAvatar")` after a refresh can return the OLD (pending-destroy) avatar.
