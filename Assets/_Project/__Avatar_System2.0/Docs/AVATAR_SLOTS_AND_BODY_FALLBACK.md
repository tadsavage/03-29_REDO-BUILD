# Avatar slots, the `Body` fallback variant, and the Blender export pipeline

Written 2026-10-02 (end of session, before moving to another PC). Companion to `BLENDER_CHECKLIST.md` (the
step-by-step list) and `.claude/skills/ModularAvatarSystem/SKILL.md` (how the code works today).

**PATHS UPDATED 2026-10-02 for Avatar 2.0:** the only working root is `Assets/_Project/__Avatar_System2.0` (`<Gender>/<Type>_Body_Type/{1. Blender, 2. FBX, 3. Prefab (post AOD Submit)}`). Wherever this doc says `_Avatar_System/.../03_FBX` read `__Avatar_System2.0/Female/Regular_Body_Type/2. FBX`; the old tree is no longer scanned.

**Status (updated 2026-10-04): DESIGN IMPLEMENTED.** The `torso` slot, the `Body` fallback, the neck slot (accessory, no `Body` mesh) and a `face` slot are in the code and verified in Unity (2026-10-02/03, see SKILL.md).
Section 7 below is kept as the record of what was needed; its items are done unless marked otherwise. Axis values are verified in Unity.

---

## 1. Decisions made this session

1. **No separate "Body" slot.** The base body is not its own slot. Every body part has its own slot, and the
   nude base skin for that slot is a part whose **variant is `Body`**.
   - `Female_Torso_Body`, `Female_Legs_Body`, `Female_Arms_Body`, `Female_Hands_Body`, `Female_Feet_Body`,
     `Female_Head_Body`.
2. **`Body` is a reserved variant meaning "the default skin for this slot".** If nothing is equipped in a slot
   (and no other slot hides it), the avatar shows that slot's `Body` part. This is the failsafe: an avatar can
   never have a missing mesh, the worst case is a nude one.
   - No shoes equipped: show `Female_Feet_Body`. No gloves: show `Female_Hands_Body`. No shirt: show `Female_Torso_Body`.
3. **Torso and legs are split into two meshes** so shirts, pants and shorts are interchangeable
   (`Torso` slot = shirts/jackets/coveralls tops, `Legs` slot = pants/shorts). A one-piece (coveralls) simply
   hides the other slot it covers (see section 5).
4. **Naming stays `Gender_Slot_Variant`** (Tad's names in Blender are now `Female_{Slot}_Body`). The importer reads the
   MESH OBJECT name. `woman`/`female` and `man`/`male` are synonyms; `neutral` counts for both. Pick one word and use
   it everywhere.
5. **No `Reg` in names.** It looked like a body-size tag that breaks the 3-part pattern. The armature and the folder
   (`Female/Bodies/Regular`) already say which body type it is. (The old mesh was `Female_Body_Reg`; renamed.)
6. **Body-only export first.** Only the body (torso + legs, plus the armature) is exported now. Feet, hands and head
   come later once the body is verified in Unity.
7. **One saved export preset, same axis settings every time** (`Unity_Avatar`, section 4). It now lives in git.

## 2. Why the `Body` variant beats a dedicated Body slot

- One rule instead of special cases: *slot empty -> show that slot's `Body` part.*
- Boots replace `Feet_Body` just by being in the same slot. The "hides body parts" boxes are then only needed when a
  garment covers a DIFFERENT slot.
- A dedicated `body` slot would duplicate `torso` and force "if torso clothing is worn, hide the body".

## 3. Slot model (target)

Parts in the SAME slot are alternatives (exactly one pick per slot). Today's code slot list
(`ModularAvatarAssembler.BodySlots`) is `body, head, neck, arms, hands, waist, legs, feet`; hair is not a body slot.

Target slots for Female Regular:

| Slot | `Body` default (nude) | Example replacements |
|---|---|---|
| Torso | `Female_Torso_Body` | shirts, jackets, coverall tops |
| Legs | `Female_Legs_Body` | pants, shorts |
| Arms | `Female_Arms_Body` | sleeves |
| Hands | `Female_Hands_Body` | gloves |
| Feet | `Female_Feet_Body` | boots, shoes |
| Head | `Female_Head_Body` | (never hidden) |
| Hair, Hat, ... | none (optional) | hair, hard hats, headphones |

Open question for Tad: does the existing `waist` and `neck` slot stay, merge into torso/legs, or get their own
`Body` meshes? Every slot that can be empty must have a `Body` mesh or it leaves a hole.

## 4. Blender export setup

### Environment
- Blender **5.2.2 LTS**, `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`.
- Scene structure: `Main` (empty) > `Group` > `Geometry` (empties) and the armature `DeformationSystem`; the part
  meshes are parented to `DeformationSystem` and each has one Armature modifier pointing at it. Exactly one armature.
- Working .blend (old 2.0 folder): `Assets/_Project/__Avatar_System2.0/Female/Regular_Body_Type/1. Blender/Female_Body_Reg-White.blend`.
  That folder is a scratch area, NOT scanned for import ("DO_NOT_IMPORT_FROM_HERE"). Exports go to
  `__Avatar_System2.0/Female/Regular_Body_Type/2. FBX` (2.0 layout; the .blend itself belongs in `1. Blender`).

### The preset `Unity_Avatar`
Source of truth in git: `BlenderPresets/Unity_Avatar.py` (repo root). Install on any PC by copying it to
`%APPDATA%\Blender Foundation\Blender\5.2\scripts\presets\operator\export_scene.fbx\` (see `BlenderPresets/README.md`).
Then File > Export > FBX > preset dropdown > `Unity_Avatar`.

| Setting | Value | Why |
|---|---|---|
| Forward / Up | `-Z` / `Y` | Standard Unity setup; a body modeled facing -Y in Blender should arrive facing +Z |
| Apply Scalings | FBX Units Scale, scale 1.0, Apply Unit on | Predictable 1:1 size in Unity |
| Bake Space Transform | off | Avoid baking axis conversion into vertices |
| Limit to | Selected Objects, Armature + Mesh | Only the chosen part plus the rig |
| Only Deform Bones | on | No control/helper bones in Unity |
| Add Leaf Bones | off | No stray `_end` bones |
| Armature node type | Null | |
| Bake Animation | off | Parts export in the rest pose |
| Smoothing | Face; modifiers applied | |
| Path mode Auto, textures not embedded | | Materials remap to `atlas-source-LPAP` in the importer |

**These axis values are verified in Unity** (2026-10-02, first real export `female_regular_body.fbx`: faces +Z, Left hand at -X, Humanoid avatar valid).
If they ever change, re-export EVERYTHING with the same values.

### Test result (2026-10-02)
A test export of the body mesh + `DeformationSystem` through the preset succeeded, and the real five-mesh body export was then imported into Unity and checked (see above).
Later (2026-10-04) the four hair meshes were swapped for the copies saved in `1. Blender/body_backup.blend` (commit `279cd807f`) after the exported hair showed missing faces on the side locks.

### Gotchas found while setting this up
- **UTF-8 BOM breaks presets.** Writing the preset with Windows PowerShell's `Set-Content -Encoding utf8` adds a BOM;
  Blender then fails with "invalid non-printable character U+FEFF". The committed file has no BOM. Edit it with an
  editor that saves "UTF-8 without BOM", or re-save it from Blender's preset menu.
- **Hidden objects are not exported.** `Selected Objects` silently skips anything hidden in the viewport (the feet
  and head were hidden, so they were left out). Unhide before selecting.
- A preset only stores the export dialog's settings. It cannot select objects or apply rotation/scale; that is still
  the manual checklist.

### Blender MCP (how Claude talked to Blender)
Blender 5.2.2 had the BlenderMCP add-on running (`scripts/addons/blender_mcp.py`), listening on
`127.0.0.1:9876`. Claude had no Blender MCP tools in this session, so it sent JSON over that socket from PowerShell:
`{"type":"get_scene_info","params":{}}` and `{"type":"execute_code","params":{"code":"..."}}`. The `result`
variable is NOT returned from `execute_code`; use `print(...)` and read the printed output. The add-on must be started
in Blender (N-panel > BlenderMCP > Connect) on each new PC/session.

## 5. Hide-mask rules (carry-over, to be updated for the new slots)

Hide lists are only for a part hiding a DIFFERENT slot. Current rules (2026-10-01):
- Coveralls: hide torso(`body`), `arms`, `waist`, `legs`. Hands/feet/head/hair stay.
- Body torso hides arms; arms weight 0 for now (coveralls carry sleeves); black sleeves cover arms.
- Gloves replace hands; boots replace feet. Feet/neck/hair/hats hide nothing.
- Invariant (code-enforced): **Head and Hair are never hidden.**
With the `Body` fallback the rule becomes: a slot that is hidden by another part shows nothing; a slot that is merely
EMPTY shows its `Body` part. These two cases must stay distinct.

## 6. Folder structure

```
Assets/_Project/__Avatar_System2.0/              <- ONLY scan root (Avatar 2.0)
  Female/Regular_Body_Type/{1. Blender, 2. FBX, 3. Prefab (post AOD Submit)}
  Male/                                         <- parked
Assets/_Project/__Avatar_System2.0/Docs/             <- this file, BLENDER_CHECKLIST.md
(old tree Female/Bodies/Regular/{01_StoreBought,02_Blender,03_FBX} is no longer scanned)
BlenderPresets/   <- repo root, outside Assets (so Unity ignores it)
```

## 7. Code work for the design (DONE 2026-10-02/03; list kept for reference)

1. **Parser/slot naming.** Today the torso mesh is `woman_bodyA_Cauc` and parses as slot `bodya`; a hand-edited
   `AOD_Objects/woman_bodyA_Cauc.asset` forces `slot: body`. New names `Female_Torso_Body` parse naturally as slot
   `torso`, variant `body`. Rename `body` -> `torso` in `BodySlots`, `IsBodySlotName` and any `slot == "body"` check, and
   retire/replace the old bodyA asset. `FixNewExport` uses the same parser to decide which FBX gets the Humanoid rig
   (the body source), so verify the Humanoid import still lands on the right FBX after the change.
2. **Add the `Body` fallback to `ModularAvatarAssembler.Build`:** for every body slot with no pick and not hidden by
   another part, use that slot's part whose variant is `body`. Make sure `PickVariant` never picks a `Body` variant as
   a normal wardrobe item (exclude it from the random pool, or give it weight 0 and bypass the weight-0 rule).
3. **AOD:** flag `Body` variants automatically (read-only default marker), and let the hide config distinguish
   "empty" from "hidden".
4. **Add a `Legs` clothing folder** and split the current coveralls into top/bottom or keep them as a one-piece that
   hides `Legs`.
5. **Verify** (Edit mode and Play): build avatars with no clothing and confirm every slot shows its `Body` mesh; build with
   coveralls/boots/gloves and confirm the old behavior is unchanged.
6. Keep the existing known issues in mind (SKILL.md Gotchas): the avatar root yaw (0,180,0), the part-orientation
   correction matrices, and the duplicate-armature refusal. Each can be removed once the Blender exports are clean.

## 8. Next steps for Tad (Blender) — updated 2026-10-04

Done: torso/legs split, all five `_Body` meshes exported in one FBX and working in Unity.
1. Re-export the hair (`Female_Hair_Mem_Black/Blonde/Red/White`, now the copies from `body_backup.blend`) and check them in Unity.
2. Clothing, one FBX per garment (torso and legs interchangeable; a one-piece hides the other slot): weights capped at 4 + normalized, `Unity_Avatar` preset, own filename, never `female_regular_body.fbx`.
3. After every export: Scan, Finalize All Pending, look at the part from the front and the side.
