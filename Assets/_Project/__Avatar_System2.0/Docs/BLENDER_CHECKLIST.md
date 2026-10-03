# Blender → Unity checklist (avatar parts)

Updated 2026-10-02 (paths: Avatar 2.0, root `Assets/_Project/__Avatar_System2.0`). Design background and open questions: `AVATAR_SLOTS_AND_BODY_FALLBACK.md` (same folder).

One body type = **one armature**. Every body part and every piece of clothing is a separate mesh skinned to it.

## Naming (read this first)
Every mesh object is named `Gender_Slot_Variant` (the importer reads the **mesh object name**, not the file name).

- **Slots:** `Torso`, `Legs`, `Arms`, `Hands`, `Feet`, `Head`, plus clothing/prop slots (`Hair`, `Hat`, ...).
  Shirts, pants and shorts are separate items: shirts go in `Torso`, pants and shorts in `Legs`.
- **`Neck` slot (added 2026-10-02):** collars, headphones worn round the neck, scarves, necklaces, ties... Name them `Female_Neck_Collar`, `Neutral_Neck_Headphones`, etc.
  It is an optional ACCESSORY slot (about 1 in 3 employees wears one; weight/roles are set per item in the AOD, and the Pimp My Employee editor has a "Neck Items" tab).
  No `Body` mesh is needed for it. A neck item can be skinned to the rig (weight it to Neck_M/Head_M/Chest_M like the collar) or rigid (no Armature modifier; the game then
  attaches it to the Neck_M bone). A garment (turtleneck etc.) can hide it with the "hides body parts" Neck box in the AOD. Headphones worn ON the head stay slot `Hat`.
- **Variant `Body` is reserved.** `Female_Torso_Body`, `Female_Legs_Body`, `Female_Arms_Body`, `Female_Hands_Body`,
  `Female_Feet_Body`, `Female_Head_Body` are the nude base skin for that slot. If nothing else is equipped in the slot,
  the game shows the `Body` part, so an avatar never has a missing mesh. One `Body` mesh per slot per body type.
- Clothing examples: `Female_Torso_CoverallsBlue`, `Female_Legs_JeansBlue`, `Female_Feet_BootsBrn`, `Neutral_Hat_HardhatRed`.
- `woman`/`female` and `man`/`male` mean the same thing to the importer; `neutral` works for both. Use the SAME word
  everywhere. Do not add a body-size tag like `Reg` to the name; the armature and folder already identify the body type.

## Setting up the .blend
- [ ] Start from the body type's single armature (`DeformationSystem`, e.g. `Female/Regular`). Never copy, re-parent or rebuild the rig per part.
- [ ] Every part is its own mesh object, built on top of that armature (head, torso, legs, hands, arms, feet, then clothing, hair, hats).
- [ ] Each mesh has an **Armature modifier** pointing at that one armature, with vertex weights on the deform bones only.
- [ ] Name every mesh `Gender_Slot_Variant` (see Naming above).
- [ ] Every slot a character can leave empty has a `..._Body` mesh. No `Body` mesh = a hole when the slot is empty.
- [ ] Keep the body facing the same direction in every file (it must arrive in Unity facing **+Z**, like the body does).

## Before every export
- [ ] **Apply** rotation and scale on the meshes and the armature (Ctrl+A).
- [ ] The scene has **exactly one armature**. No `DeformationSystem.001` or other duplicates; Unity's importer refuses a file with duplicate object names.
- [ ] Armature is in rest/bind pose, same pose as the other files for this body type.
- [ ] **Unhide** the part you are exporting. Hidden objects cannot be selected, so they are silently left out of "Selected Objects" exports.
- [ ] Select ONLY the part(s) you are exporting, plus the armature. (Currently body-only: `Female_Torso_Body` + `Female_Legs_Body` + armature. Feet, hands and head are exported later.)
- [ ] File → Export → FBX, pick the preset **`Unity_Avatar`** from the preset dropdown. Do not touch the axis settings.
      Mixed axis settings are what made the head, hard hats and hands arrive backwards or rolled.
- [ ] Preset source of truth is in git: `BlenderPresets/Unity_Avatar.py` (repo root). Install notes are in that folder's README.

## The body is ONE file (decided 2026-10-02)
`female_regular_body.fbx` holds ALL FIVE body parts (Torso, Legs, Hands, Head, Feet) + the armature. Every body export must select the
armature AND all five meshes, even if only one changed. The file is the only source Unity reads: exporting a subset overwrites it and the
missing parts vanish (the finalized prefabs of the missing parts get lost too). Clothing/hair/hats are different: one FBX each, own filename.
Before exporting: Weights > Limit Total = 4, then Normalize All (Unity caps at 4 bones and renormalizes, so Blender looks different otherwise).
After exporting: check the FBX modified time changed, then AOD Update (or `Tools > Modular Avatar > Finalize All Pending`). Update only re-reads the FBX.

## After export
- [ ] FBX goes in `__Avatar_System2.0/<Gender>/<Type>_Body_Type/2. FBX` (e.g. `Female/Regular_Body_Type/2. FBX`); keep the `.blend` in `1. Blender` (never scanned). Submit/Update output lands in `3. Prefab (post AOD Submit)`.
- [ ] Check the Unity console for errors on the import; fix in Blender and re-export rather than patching in Unity.
- [ ] In the AOD: Rescan, then Submit/Update, set weight and the "hides body parts" boxes.
- [ ] Look at the part on an avatar from the front **and** the side before calling it done (face, hat brim, thumbs, boot toes all point forward).

## Why this matters
The game currently attaches each part with its own copy of the skeleton, so characters stay in a T-pose. Parts exported identically on one armature are what allow binding them all to the body's skeleton so they animate. When that is done, the head/hat/glove correction rules in `ModularAvatarAssembler.ApplyOrientationFixes` can be deleted.
