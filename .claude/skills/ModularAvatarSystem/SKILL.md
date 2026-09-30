---
name: ModularAvatarSystem
description: Ground-up rebuild of the warehouse-sim avatar pipeline — 6 body types (Reg/Tall/Portly x M/F), slot-based clothing with body-part masking to prevent clipping, staged Store-Bought -> Blender -> FBX -> AOD-prefab workflow. Use whenever working on avatar bodies, clothing, rigs, the AOD tool, or anything under Assets/_Project/Avatar_System.
---

# Modular Avatar System (2026-09-29 rebuild)

This replaces the old single-body-per-gender random-assembly avatar system
(`AvatarPartLibrary` / `ModularAvatarAssembler` / `ModularAvatarImporter` /
`ModularAvatarFinalizer` under `Assets/_Project/Scripts/Actors/ModularAvatar/`
and `Assets/_Project/Scripts/Actors/Editor/`). That system is being superseded,
not deleted outright — see "Relationship to the old system" below. Tad is a
hobbyist; explain tradeoffs plainly, don't assume game-dev jargon lands.

## The problem this solves

The old system had one body mesh per gender and randomly toggled accessory
slots on/off. It had no concept of body *types* (everyone was the same build)
and no deliberate clothing-vs-body clipping control — clothes just layered on
top of whatever body parts were already visible, Skyrim-clipping-style, with
no way to say "this outfit hides the torso mesh underneath it."

The new system fixes both:
1. **6 body types**: Male/Female x Regular/Tall/Portly (portly = shorter +
   heavier). Each body type has its own rig — proportions differ enough
   (especially Portly) that sharing one skeleton and rescaling isn't viable.
2. **Explicit body-part masking per clothing item**, authored once when the
   clothing prefab is built via the AOD, and read by runtime code at
   spawn/equip time. This is the actual clipping fix.

## Body composition

Each body type's rig carries 8 separate skinned mesh parts, all bound to the
one shared skeleton for that body type:

`Head, Neck, Torso, Arms, Hands, Waist, Legs, Feet`

(Hair is NOT a body part — it's authored and toggled like a clothing item,
same as the old system's "hair" slot, because there are many hairstyles per
body and it needs to swap independently.)

**Head and Hair are always enabled.** No clothing configuration may disable
them — enforce this in code (not just convention) wherever body-part masks
are applied, e.g. clamp/force those two flags true rather than trusting every
caller to set them correctly.

## Clothing -> body part masking

Every clothing prefab, when created or updated through the AOD, stores which
of the 8 body-part slots should be shown or hidden on the wearer. Example from
Tad: coveralls are a one-piece jumpsuit, so when worn: Head, Hands, Hair, Arms
stay enabled (skin still shows at the extremities); Torso, Waist, Legs get
hidden (coveralls fully cover them); Feet is driven by whatever shoe/boot
clothing item is equipped in the Feet slot, not by the coveralls themselves.

This mask is baked into the clothing prefab at author time (AOD Create/Update
button), not computed at runtime — runtime code just reads it back and
applies it to the spawned body's part renderers.

## Folder structure

Root: `Assets/_Project/Avatar_System/` (created 2026-09-29, all folders
currently empty). `Assets/_Project/Prefabs/Modular_Staff_Prefabs/` remains the
one standardized prefab destination — the AOD writes finished prefabs there,
never under Avatar_System.

```
Assets/_Project/Avatar_System/
├── Scripts/
│   ├── Editor/              -- AOD tool (editor window, prefab writer) — new architecture, TBD
│   └── Runtime/             -- body-part-slot enum, body-part visibility component, clothing mask reader — TBD
├── UI/                      -- AOD editor UI assets (UXML/USS or IMGUI, TBD)
├── Docs/                    -- naming convention notes, slot reference — not code
├── Male/
│   ├── Bodies/
│   │   ├── Regular/  { 01_StoreBought/  02_Blender/  03_FBX/ }
│   │   ├── Tall/     { 01_StoreBought/  02_Blender/  03_FBX/ }
│   │   └── Portly/   { 01_StoreBought/  02_Blender/  03_FBX/ }
│   └── Clothing/
│       ├── 01_StoreBought/
│       ├── 02_Blender/
│       └── 03_FBX/
│           ├── Head/  Hair/  Torso/  Arms/  Hands/  Waist/  Legs/  Feet/
└── Female/
    (mirrors Male exactly)

Assets/_Project/Prefabs/Modular_Staff_Prefabs/
├── Body_Prefabs/
│   ├── Male/    { Regular/  Tall/  Portly/ }
│   └── Female/  { Regular/  Tall/  Portly/ }
├── Clothes_Prefabs/
│   ├── Male/
│   └── Female/
└── Props_Prefabs/           -- pre-existing, untouched, for non-clothing props
```

### Pipeline stages (why 3 folders deep for every body/clothing leaf)

1. **01_StoreBought** — raw purchased assets as downloaded, before any
   editing. Reference only; never referenced directly by Unity code.
2. **02_Blender** — the "library" of `.blend` files: store-bought assets
   renamed to the project's naming convention and heavily modified/rigged/
   skinned here. This is the source-of-truth working file per body type or
   per garment. One `.blend` per body type is expected to contain ALL 8 body
   parts as separate objects bound to that body type's one shared armature
   (matches "under the same rig and skinned together" — export once, not
   part-by-part).
3. **03_FBX** — the exported, game-ready FBX pulled into Unity. For bodies,
   one FBX per body type (containing all 8 skinned part children). For
   clothing, one FBX per garment, filed under the category subfolder matching
   the body-part slot(s) it primarily covers.

The AOD then reads from `03_FBX` and writes the finished prefab to the
standardized Prefabs location (never leaves a prefab under Avatar_System).

## Naming convention (proposed, confirm with Tad before scripting the parser)

Because folder path already encodes gender + body type + pipeline stage, FBX
child object names only need to encode **slot_variant**, e.g. `torso_main`,
`head_main`, `hands_main` for a body's parts, or `torso_Coveralls`,
`feet_SteelToeBoots` for clothing. Don't repeat gender/bodytype in the name —
it's redundant with the folder and was a source of churn in the old
`gender_slot_variant` convention (see `ModularAvatarImporter.ParseName`'s
scar tissue around synonyms like "man"/"male").

## Relationship to the old system

Reusable ideas/patterns from the old `ModularAvatar` code (do NOT reuse the
classes wholesale — they assume one body per gender and random assembly,
which no longer holds):
- `EnsureFolder`-style recursive folder creation in editor code
  (`ModularAvatarFinalizer.EnsureFolder`).
- Drop-folder `AssetPostprocessor` auto-rescan pattern
  (`ModularAvatarImporter.Watcher`) — could trigger AOD re-validation when a
  FBX changes in a `03_FBX` folder.
- The general "Submit/Update through an editor tool, which validates
  structure and writes a real prefab" shape (`ModularAvatarFinalizer.
  TryFinalize`) — the new AOD's Create/Update button should follow the same
  validate-then-write discipline (e.g. reject a body part with no skinned
  mesh, same check `ValidateStructure` already does).
- `AvatarWeightConfig`'s per-(role, gender, slot, variant) weight rule
  pattern, IF the new system still wants weighted-random NPC dress-up on top
  of deliberate player-authored outfits. Not needed for Step 1/2 (first body).

NOT reusable as-is: `AvatarPartLibrary` (single body per gender baked into its
`CoreSlots`/`HeadPositionSlots` assumptions), `ModularAvatarAssembler` (prunes
one shared body prefab down to random picks — the new system instead composes
a chosen body type + deliberately-configured clothing masks), `IAvatarPart`
(no concept of body type or part-visibility masks).

The old `Resources/ModularAvatar/AvatarPartLibrary.asset` and
`AvatarWeightConfig.asset`, and old `Prefabs/Modular_Staff_Prefabs/
Body_Prefabs` and `Props_Prefabs` contents, are left untouched for now —
existing NPC spawning may still depend on them until the new system reaches
feature parity. Don't delete them without checking `EmployeeSpawner` /
`ModularAvatarRig` usage first.

## Workflow checkpoints (staged rollout — do not skip ahead)

1. **Folders** (done 2026-09-29) — this structure.
2. **First body** — Tad builds one body type (likely Male Regular) in
   Blender with Claude's help; establishes the real naming convention, rig
   setup, and export settings other 5 body types will copy. Do this before
   writing any AOD/runtime code — the code should be designed against a real
   exported FBX, not a guess.
3. **Body-part runtime component + AOD body import** — once one body type
   exists in `03_FBX`, write the runtime component that exposes 8 toggleable
   part renderers, and the AOD action that imports a body FBX into a
   `Body_Prefabs/<Gender>/<Type>` prefab.
4. **First clothing item + masking** — build one garment, wire the AOD's
   part-mask UI (checkboxes per slot, Head/Hair locked on), confirm
   Create/Update bakes the mask into the clothing prefab and a runtime script
   can read it back and toggle the worn body's part renderers.
5. **Remaining 5 body types + clothing library** — repeat step 2-4's proven
   pattern.

Tad's role: planning, Blender modeling/rigging, testing, feedback. Claude's
role: game design decisions, folder/naming conventions, C# (editor + runtime)
implementation, Unity-MCP-driven setup. Don't write AOD/runtime C# ahead of
step 2 — there's no real FBX to validate it against yet.
