# Modular Avatar — Session Handoff (2026-06-19)

> Read this first when resuming on another machine. Claude's local memory does NOT sync across
> machines — this doc + the repo + the saved `.blend`/FBX are the bridge.
> **This supersedes the 2026-06-17 version** (that one was about the male animation "explosion"
> blocker, which is now RESOLVED — males walk + faces are correct).

## TL;DR status
- ✅ **Males**: fully working — correct faces, hair, body, animation.
- ✅ **Female face-on-back bug**: ROOT CAUSE FOUND + FIX APPLIED IN BLENDER (see below). Pending save + re-export.
- ⏳ **Female torso waist gap**: diagnosed precisely, fix is a 2-min manual Blender step (see below). NOT yet done.
- ✅ Female renames worked: `female_body_white`, `female_legs_BlackLeggings/JeansBlue`,
  `female_feet_BootsBlack/BootsBrowm` (note typo "Browm"), chest/head/vest/eyebrows/face all scan fine.

## ⚠️ FIRST THING ON THE OTHER MACHINE
1. **The face fix lives only in the open Blender session unless the `.blend` was SAVED and synced.**
   The blend is `Assets/5. Models/BlenderFiles/Modular_Staff.blend` (holds BOTH rigs:
   `ArmatureMaleWorker` + `WorkerFemale`). Make sure it (and the re-exported FBX) actually travels
   (commit/push or cloud-copy — the drop-folder FBX is git-untracked).
2. If unsure the fix survived, **verify / re-apply it** (it's fully reproducible — see next section).
3. ⚠️ A Blender **undo wiped the face fix once this session** (the modifier-repoint IS undoable). So
   after applying, **save immediately**.

## FIX #1 — Female faces on the back of the head (FOUND + APPLIED)
**Cause:** the 8 female facial meshes had their **Armature MODIFIER pointing at `ArmatureMaleWorker`**
(the male rig) instead of `WorkerFemale`, even though parented to WorkerFemale and weighted to a
"Head" vgroup. Classic duplicate-the-male-meshes-and-forget-to-repoint mistake. Bind pose looked
fine (both Head bones nearly identical), so it only broke **when animated** — the face followed the
male rig the female Animator never drives → stranded/displaced ("on the back").

The 8 meshes: `female_eyes`, `female_face_Frown`, `female_face_Neutral`, `female_face_Smile`,
`female_face_TongueOut`, `female_eyebrows_BrowsMad`, `female_eyebrows_BrowsNeutral`,
`female_eyebrows_BrowsSad`.

**Fix (applied this session; reproducible):** repoint each one's Armature modifier `Object` →
`WorkerFemale`. Everything else (body/chest/vest/legs/feet/hair/hats) was already correctly bound to
WorkerFemale — so **hair was never the bug**; "no hair" was the mangled face / hat-or-bald draws.
Blender Python to re-apply if needed:
```python
import bpy
fem = bpy.data.objects['WorkerFemale']
for n in ["female_eyes","female_face_Frown","female_face_Neutral","female_face_Smile",
          "female_face_TongueOut","female_eyebrows_BrowsMad","female_eyebrows_BrowsNeutral",
          "female_eyebrows_BrowsSad"]:
    for m in bpy.data.objects[n].modifiers:
        if m.type=='ARMATURE': m.object = fem
```
Verified by posing WorkerFemale's `Head` bone 40°: `female_face_Neutral` now travels with the head
(centroid moved 0.19u; before the fix it stayed put). **Then save the .blend.**

## FIX #2 — Female torso "midriff void" (DIAGNOSED, NOT DONE — do this by hand)
`female_body_white` is **not** missing its lower half — it's built as **disconnected vertical chunks**
with an **empty ring at the waist (local z ≈ 0.84–1.0)** between the pelvis chunk (z 0.60–0.84) and
the chest chunk (z 1.0+). That hollow band is the void that shows through under a cropped vest.
(Width profile: solid at 0.60–0.84, **empty 0.84–1.0**, solid 1.0+.)

**Do NOT auto-extrude the bottom** — a script tried that, grabbed the pelvis-bottom cap, and pulled it
to the knees (z 0.47). It was reverted; mesh is back to original 302 verts, undamaged.

**Correct fix (manual, ~2 min):**
1. Select `female_body_white` → Edit Mode → edge select.
2. **Alt-click** the open loop at the **top of the pelvis** chunk (~z 0.84); **Shift-Alt-click** the
   open loop at the **bottom of the chest** chunk (~z 1.0).
3. **Edge ▸ Bridge Edge Loops** to fill the waist.
4. Assign the new verts to the **`Spine`** vertex group (weight 1.0) so they deform.
   (Solidify modifier with Rim-Fill will thicken the new faces automatically.)

## Apply pipeline (after the Blender fixes)
1. **Save** `Modular_Staff.blend`.
2. **Re-export** over `Assets/5. Models/BlenderFiles/Modular_Staff/Female_Modular_Staff.fbx` with the
   usual export preset (the Unity `.meta` import config is preserved on re-export).
3. Unity auto-rescans on import, or run `Tools ▸ Modular Avatar ▸ Scan & Rebuild Library`.
4. **Regenerate the portraits** in `Assets/Sprites/Portraits/` — the existing PNGs were rendered with
   the broken avatars (every female face mangled). They're the real verification: males = clean,
   females should now match after the fix.

## Architecture recap (current)
- Two FBX in drop folder `Assets/5. Models/BlenderFiles/Modular_Staff/`: `Male_Modular_Staff.fbx`
  (guid af0100804cd85a841985e99bad95faf8) + `Female_Modular_Staff.fbx`
  (guid eb264ac023d997b41810917d35ed1fec). Splitting into two FBX is fine — the code handles one
  bucket or many sources. No code change is needed for any of this.
- Library: `Assets/Resources/ModularAvatar/AvatarPartLibrary.asset` (scanned by
  `Assets/10. Editor/ModularAvatarImporter.cs`, naming convention `gender_slot_variant`).
  Only `female_eyes`/`male_eyes` get skipped (2-segment names) — harmless, they always render.
- In-game: `EmployeeSpawner.ApplyModularAvatar` builds the avatar (`ModularAvatarAssembler.Build`),
  hides the worker SMRs, nests the avatar, gives it its OWN humanoid avatar + the **worker's**
  animator controller, retargets via `ModularAvatarRig`. The old "explosion" blocker is RESOLVED.
- Preview tool: `Tools ▸ Modular Avatar ▸ Spawn 8 Random Avatars` — but note this shows **bind pose
  (T-pose), where the female face looked CORRECT even while broken.** The face bug only shows when
  ANIMATED — verify via the photo-booth portraits, not the preview spawns.

## Environment notes (this machine — may differ on the other)
- Live Unity MCP relay this session was **`unity-mcp` (Code Maestro)**; `unityMCP` and
  `advanced-unity-mcp` were DOWN. Relay drops on recompile/domain-reload; Unity restart revives it.
- **Blender MCP** (addon server) worked well for inspection + the modifier fix.
- Toggle on `EmployeeSpawner`: `_useModularAvatars` (default ON; OFF = original animated worker model).

## To resume
Open Claude Code in this repo and say: *"Read MODULAR_AVATAR_HANDOFF.md — verify the female face fix
survived, then do FIX #2 (bridge the waist) and re-export."*
