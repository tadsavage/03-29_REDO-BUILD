# Avatar / AOD / Modular Avatar pipeline (Sep-Oct 2026)

> Archived from CLAUDE.md on 2026-11-02 (verbatim). Historical detail — read only when working in this area. The lean CLAUDE.md holds the rules that always apply.

## Session 2026-09-27 — AOD / Modular Avatar / "Pimp My Employee" — CROSS-MACHINE HANDOFF SNAPSHOT

**Tad is switching to a different computer and wants to resolve the skeleton/rig problem there
tonight.** This section is the complete state dump for that — what was built, what's confirmed
broken, and exactly what the fix is. Read this section top to bottom before touching any of it.

### What was built this session (all compiled clean; all but one item live-verified in Play Mode)

**1. Avatar Object Database (AOD) — full 3-column redesign.** `Assets/_Project/Scripts/UI_UX/AODPanel.cs`
   (programmatic UI Toolkit, opens via Ctrl+Shift+A or the "AOD" TopBar button, Editor-only tool).
   - Panel now opens fully opaque and stretches to `top:0`, covering the game's TopBar entirely (Tad:
     "I don't want to see that when I'm in this UI") — see `Show()`, which calls
     `_resize.FillScreenExact()` then manually re-extends `_modal.style.top`/`height` upward by
     whatever strip `FillScreenExact` reserved for the TopBar.
   - Left column: compact (108px) cards, each showing a REAL BAKED 3D THUMBNAIL of the part (not a
     flat color swatch) over a slot-colored bar, with the part name overlaid at the bar's bottom edge.
   - Center column: big (380x380) live rotatable 3D preview — left-click-drag horizontally yaws
     (Y axis), vertically pitches (X axis). Big title label under it.
   - Right column: single scrolling region (Tad: "the whole right side needs one big scroll bar") —
     allowed-roles chips at the top (moved up per Tad's mockup), DEFAULT WEIGHT as a 0–100% slider
     (was a raw float), then COLOR VARIANTS as a 64-swatch flat-color palette grid sampled from
     `Assets/polyperfect/Common/Textures/atlas-albedo-LPAP.png` (confirmed this is the FLAT/simple one —
     `atlas-source-LPAP.png`/`atlas-gradient-LPAP.png` both carry a per-cell shading gradient, which is
     literally the material that caused the very first session's clipboard-material-revert bug).
     Clicking a swatch toggles it as a color variant on the part (`AvatarPartLibrary.ColorVariant`
     gained a `Color color` field alongside the old `Material material` field).
   - **`AODPreviewStage.cs`** (`Assets/_Project/Scripts/Actors/ModularAvatar/`) — the render rig
     backing the center preview. Root cause of the long-standing "always renders solid black" bug
     was FINALLY found and fixed this session: `ShowPart()` matched the target mesh by
     `t.name == part.objectName`, but `Instantiate()` (plus the code's own `"Preview_"` rename)
     changes the CLONED root's name — for any single-mesh-root prefab (most parts) that exact-name
     match could never succeed, so the method silently returned before ever framing the camera. Fixed
     via `CleanInstanceName()` (strips `(Clone)`/`Preview_` before comparing). Also added:
     `ShowAssembledInstance(GameObject)` (adopts a fully-assembled avatar for whole-body preview,
     used by "Pimp My Employee" below) and a SEPARATE thumbnail-bake stage
     (`GetOrCaptureThumbnail`/`ClearThumbnailCache`) so baking a grid thumbnail never disturbs
     whatever's live-rotating in the big preview. Camera clear-flags/background-color were
     independently confirmed to NEVER apply in this project's URP setup (tested: setting
     `backgroundColor` to solid red had zero effect) — matches a documented precedent in
     `ItemCreatorPanel.BuildBackdrop()`; worked around the same way, with a physical lit/emissive
     Cube behind the stage instead of relying on `clearFlags`.

**2. "Pimp My Employee"** — right-click... no, **left-click employee → Actions dropdown → new
   "Pimp My Employee" entry** (`EmployeeInfoUI.cs`) opens the AOD in a special per-employee edit mode
   (`AODPanel.ShowForEmployee(EmployeeIdentity)`), closing the info card behind it.
   - Scoped DELIBERATELY to 4 cosmetic categories only (Tad: "haven't figured out clothing or skin
     color yet"): Hairstyle, Hard Hat, Headphones, Facial Hair — see
     `ModularAvatarAssembler.EditableOverrideKeys` = `{"hair","hat.hardhat","hat.headphones","facialhair"}`.
   - `EmployeeRecord.cs` gained `avatarOverrides` (a `List<AvatarOverride>{key, objectName}` — NOT a
     Dictionary, since this project's JSON save system doesn't round-trip Dictionaries) plus
     `GetAvatarOverride`/`SetAvatarOverride`/`ClearAvatarOverride`/`AvatarOverridesDict()`.
   - `ModularAvatarAssembler.Build()` gained a new overload taking `overrides` +
     `out Dictionary<string,Part> chosenOut`. **Load-bearing design point:** overrides are applied as
     a POST-PASS after the entire normal random-pick loop runs completely unmodified — this is
     deliberate, so that editing e.g. just the hairstyle can never shift the rng sequence and
     accidentally change which vest color/body/eyebrows a re-seeded rebuild produces. See
     `ApplyCategoryOverride()`.
   - `EmployeeSpawner.cs` gained `UsesModularAvatar(EmployeeRecord)` (false for anyone still on a
     fixed-FBX role/gender combo — the AOD shows an honest "isn't modular yet" message for those
     instead of a broken editor) and `RefreshAvatarAppearance(EmployeeIdentity)` (destroys+rebuilds
     just the `"ModularAvatar"` child in place, no full respawn).
   - **Live-verified end-to-end** (2026-09-26 in Play Mode): opened AOD on "Aaron Miller" (male
     Receiver), picked an orange hardhat, hit Apply — confirmed via reflection that his LIVE
     in-scene mesh hierarchy actually gained `neutral_hat_hardhat-orange`.
   - **KNOWN GAP:** the currently-equipped scan gun/clipboard (see below) are NOT re-applied by
     `RefreshAvatarAppearance` — if an employee is actively receiving when you Pimp them, verify the
     props still show up after the appearance refresh; not tested this session.

**3. Top bar fonts** — all 8 `font-size` values in `Assets/_Project/Scripts/UI_UX/TopHUD/TopBar.uss`
   reduced 15% (text was overflowing its boxes slightly): 20→17, 21→18, 22→19, 29→25, 28→24 (x2), 30→26, 14→12.

**4. Male clipboard/scan-gun rotation — STOPGAP FIX ONLY, see the real problem below.**
   `Assets/_Project/Scripts/Actors/EmployeeSystem/ReceivingEquipmentService.cs` anchors a clipboard
   (left hand) and RF scan gun (right hand) to `HumanBodyBones.LeftHand`/`RightHand` with a single
   hand-tuned local position+rotation, previously shared by both genders. Male employees held both
   props at wrong/floating angles; female was fine. Added `MaleClipboardLocalEuler`/
   `MaleRfGunLocalEuler` — a per-gender branch in `Equip()` (`bool isMale = ...gender != Female`) —
   re-tuned live via repeated screenshot comparison against Megan Barnes (female Receiver) until Aaron
   Miller (male Receiver) held both props naturally. **This is a band-aid, not a real fix — see below.**

### THE REAL PROBLEM (this is what Tad wants solved tonight)

**Confirmed root cause, measured live in Play Mode:** rebound both a male and female Receiver's
Animator to T-pose (`Animator.Rebind()`, a clean bind-pose so poses are directly comparable) and read
`GetBoneTransform(HumanBodyBones.LeftHand).localRotation`:

```
Aaron (male)   Wrist_L local euler = (4.66, 348.60, 357.45)
Megan (female) Wrist_L local euler = (355.34, 348.60, 2.55)
```

X and Z are negated between them (355.34 ≈ 360−4.66, 2.55 ≈ 360−357.45), Y is identical. **The male
and female body meshes are bound to their skeletons with a mirrored bind pose**, despite having
identical bone names (`Wrist_L`, `Wrist_R`, `Elbow_L`, etc.) — this is why one fixed local hand-prop
offset can only ever be correct for one gender at a time, and is almost certainly the underlying cause
behind other subtle animation/retarget oddities between the two body meshes generally, not just this
one prop bug.

**Tad's question, and the answer:** "I thought the plan was to use the same main rig for both female
and male... that was the plan correct? `Assets/polyperfect/Low Poly Animated People/- Prefabs/_MainRig.prefab`"
— **Yes, that was and is the correct plan.** Investigated fresh this session (an Explore subagent dug
through the actual FBX/meta files, not just code):

- **`_MainRig.prefab`** → `_MainRig.fbx` (guid `a4a3ba617ed9c5946bb76844ec1ef299`) is Polyperfect's
  ORIGINAL canonical rig: one shared skeleton
  (`Main/DeformationSystem/Root_M/.../Spine1_M/Chest_M/Neck_M/Head_M` and
  `.../Scapula_L/R/Shoulder_L/R/Elbow_L/R/Wrist_L/R`) with ALL ~150 of Polyperfect's `man_*`/`woman_*`
  meshes bound to that ONE skeleton. Its own Wrist_L/Wrist_R T-pose rotations are nearly identical
  between left/right (NOT mirrored) — this is the correct reference every avatar should match.
- **The existing FIXED-avatar roles already do this correctly** — `man_construction_worker.prefab` /
  `woman_construction_worker.prefab` (used by `EmployeeSpawner.FixedAvatarFor` for Boss/Security/
  Admin/generic-floor-worker-overlay roles) both reference that exact same `_MainRig.fbx` guid. This
  part of the architecture is fine and matches the plan.
- **The MODULAR avatar bodies are the problem.** `male_body_floor.fbx` and `female_body_floor.fbx`
  (`Assets/_Project/Models/BlenderFiles/Modular_Staff/AVATAR_WORKER_MODELS/`) are separate custom
  Blender exports, NOT derived from `_MainRig`, and not bound to each other's skeleton either, despite
  matching bone names:
  - `male_body_floor.fbx` retains a `Main → DeformationSystem → Root_M` wrapper (matching
    `_MainRig`'s own hierarchy shape) AND its `.meta`'s `lastHumanDescriptionAvatarSource` points at
    `_MainRig.fbx`'s avatar guid — so male was AT SOME POINT compared against/derived from `_MainRig`.
  - `female_body_floor.fbx` has NO `DeformationSystem`/`Main` wrapper at all (goes straight to
    `Root_M`) and its `.meta` has never touched `lastHumanDescriptionAvatarSource` (all zero/default)
    — authored through a completely different, unrelated pipeline.
  - Both currently import as `avatarSetup: 1` ("Create From This Model" — Unity auto-guesses the
    Humanoid bone map from each one's own hierarchy independently). Neither uses "Copy From Other
    Avatar." Nothing in code (`AvatarPartLibrary`/`ModularAvatarAssembler`/`EmployeeSpawner`)
    references `_MainRig` anywhere — it's currently orphaned as far as the modular system is
    concerned.
- **This exact class of bug has recurred before under different asset names.** Old memory files
  (`modular-avatar-rig-fix.md`, `modular-avatar-system.md`, `modular-avatar-female-head-retarget.md`,
  ~95-101 days old) describe an EARLIER, now-superseded setup (`WorkerNewAvatar`/`ArmatureMaleWorker`/
  `WorkerFemale` from `Assets/5. Models/...`) with the identical shape of problem: two
  independently-authored skeletons, same bone names, different rest poses. None of those old notes
  mention `_MainRig.prefab` by name — Tad's recollection of "one shared master rig" is correct in
  spirit and IS how the fixed-avatar roles work, but the modular male/female bodies were apparently
  never actually built that way, or drifted away from it since.

### THE FIX — options, cheapest first

1. **Unity-import-only (try this FIRST, but verify live before trusting it):** for both
   `male_body_floor.fbx` and `female_body_floor.fbx`, set the Model Importer's Rig tab → Avatar
   Definition = "Copy From Other Avatar", pointing BOTH at the same Avatar (e.g. `_MainRig`'s own, or
   whichever generated Avatar Unity produces for `_MainRig.fbx`). **Caveat, worth understanding before
   relying on this:** Humanoid retargeting normalizes ANIMATED poses across skeletons with different
   bind poses BY DESIGN — but `ReceivingEquipmentService` reads
   `Animator.GetBoneTransform(...).localRotation` directly, which is a raw skeleton value, not
   something retargeting necessarily normalizes the same way across two differently-bound meshes.
   This might fully fix it, might partially fix it, or might do nothing for this specific
   prop-anchoring symptom even if it fixes general animation quality — TEST LIVE (equip a male
   Receiver, screenshot, compare to female) before deleting the stopgap code below.
2. **Real/permanent fix, Blender-side:** re-skin/re-export `male_body_floor` (and ideally
   `female_body_floor` too, so both are symmetric and future-proof) using `_MainRig`'s actual
   armature/`DeformationSystem` as the deform skeleton, so both meshes' `Wrist_L` (etc.) bones end up
   with IDENTICAL rest-pose local rotations — to each other AND to `_MainRig`'s own. This is what
   "one shared master rig" actually means architecturally, matches the industry-standard pattern
   already recommended earlier in this project (Sims CAS / GTA Online peds: one skeleton, many
   meshes), and is the only option that fixes the root cause rather than one specific symptom.
3. **Once (1) or (2) actually fixes it, delete the stopgap:** remove
   `MaleClipboardLocalEuler`/`MaleRfGunLocalEuler` and the `isMale` branch in
   `ReceivingEquipmentService.Equip()` — both genders should go back to sharing the single tuned
   constant, exactly like before separate-rig male/female modular bodies existed.

### Quick orientation for picking this back up

- Live-diagnose bone rest pose with: `Animator.Rebind()` then
  `GetBoneTransform(HumanBodyBones.LeftHand).localRotation.eulerAngles` — compare male vs female,
  expect them to MATCH (not mirror) once fixed.
- To re-verify the prop-holding fix visually: `ReceivingEquipmentService.Equip(identity)` on a live
  male AND female Receiver in Play Mode, then `mcp__unityMCP__manage_camera` screenshot
  (`view_position`/`view_target`, NOT `view_rotation` — that one behaves unintuitively) from roughly
  3 units in front of each, at ~1.6-2.6 world Y. `Unequip(identity)` after to clean up test state.
- All 5 code files touched this session:
  `Assets/_Project/Scripts/UI_UX/AODPanel.cs`,
  `Assets/_Project/Scripts/Actors/ModularAvatar/AODPreviewStage.cs`,
  `Assets/_Project/Scripts/Actors/ModularAvatar/AvatarPartLibrary.cs` (added `ColorVariant.color`),
  `Assets/_Project/Scripts/Actors/ModularAvatar/ModularAvatarAssembler.cs` (added the overrides
  overload + `EditableOverrideKeys`/`OverrideCategoryInfo`/`ApplyCategoryOverride`),
  `Assets/_Project/Scripts/Actors/EmployeeSystem/EmployeeRecord.cs` (added `avatarOverrides`),
  `Assets/_Project/Scripts/Actors/EmployeeSystem/EmployeeSpawner.cs` (added `UsesModularAvatar`/
  `RefreshAvatarAppearance`, wired overrides into `ApplyModularAvatar`),
  `Assets/_Project/Scripts/UI_UX/EmployeeInfoUI.cs` (Actions dropdown entry),
  `Assets/_Project/Scripts/UI_UX/TopHUD/TopBar.uss` (font sizes),
  `Assets/_Project/Scripts/Actors/EmployeeSystem/ReceivingEquipmentService.cs` (male rotation stopgap).

---

## Session 2026-09-27 (continued, other machine) — Cross-machine handoff resolved: abandoned the male/female "body_floor" modular pipeline entirely

Picked up the CROSS-MACHINE HANDOFF SNAPSHOT above on a second machine. The handoff's diagnosis
(mirrored bind pose between `male_body_floor.fbx`/`female_body_floor.fbx`) turned out to describe
files that were **not actually present on this machine** — investigated why, found the real fix was
simpler than the handoff's Blender re-skin plan, and Tad chose to abandon that pipeline rather than
repair it.

### Git archaeology: the files existed, just not on disk here

`male_body_floor.fbx`/`female_body_floor.fbx` (+ matching `WORKER_AVATARS/*.prefab`) were genuinely
committed by the other-machine session (`141a18264`, 2026-09-25) and are present in this machine's
`HEAD` (`5b32ba220`, 2026-09-26 — confirmed via `git ls-tree`). But `git status` showed all 8 of
those files as **locally deleted, uncommitted** here, alongside 48 unrelated portrait PNGs (56
deletions total, no LFS in this repo) — the most recent reflog entry was a clean `pull --ff`, so the
pull itself succeeded and something removed them from disk afterward, cause unknown (not a repo/LFS
problem). Lesson: when a teammate's handoff note describes a file that "should" exist per a session
log, verify with `git ls-tree`/`ls` before trusting the note — a clean pull doesn't guarantee the
working tree still matches it.

### The actual root cause of the reported bug (male avatar missing) was simpler than the handoff's

Independent of the mirrored-rig investigation: `EmployeeSpawner.FixedAvatarFor` had males for
Receiver/ReachTruckOperator/DockStockerOperator/OrderSelector deliberately falling through
(`=> null`) to the random modular assembler (changed 2026-09-26, same day as the handoff, "for real
hair/vest/color variety"). But `AvatarPartLibrary.asset` has never had a `male`+`body` part — the
`woman_construction_worker` "body" entry it does have pointed at a mesh living *inside*
`female_body_floor.prefab` (traced via `sourceIndex`), not at the standalone Polyperfect
`Assets/polyperfect/.../woman_construction_worker.prefab`. So the modular assembler had zero body
candidates for gender=male and built a bodiless avatar — the floating-hardhat screenshot. Two
different assets share the name "woman_construction_worker": the modular AOD part (inside the now-
deleted `female_body_floor.prefab`) and the real Polyperfect character prefab
(`Assets/polyperfect/Low Poly Animated People/- Prefabs/woman_construction_worker.prefab`) that
`_floorWorkerAvatarModelFemale` was actually wired to. Only the second one has a genuine
`SkinnedMeshRenderer` (verified by grepping the prefab's YAML) — confirmed the CLAUDE.md handoff's
"plain unskinned MeshRenderer" claim was about the *first* one, not the fixed-avatar female's asset.

### Decision: abandon body_floor, both genders share the Polyperfect fixed-avatar path

Tad's call, since the modular body pipeline never had a working pair on either side: revert
`FixedAvatarFor`'s male branch to return `man_construction_worker.prefab` (same treatment female
already got), instead of pursuing the handoff's "Copy From Other Avatar"/re-skin repair plan.
`_floorWorkerAvatarModel` (male) was already sitting unused, already wired in `Main.unity` to that
exact Polyperfect prefab — the fix was a one-line switch-case change, no scene edit needed
([EmployeeSpawner.cs:603-615](Assets/_Project/Scripts/Actors/EmployeeSystem/EmployeeSpawner.cs:603)):

```csharp
EmployeeRole.Receiver or EmployeeRole.ReachTruckOperator or EmployeeRole.DockStockerOperator
    or EmployeeRole.OrderSelector
    => PoolOrSingle(null, female ? _floorWorkerAvatarModelFemale : _floorWorkerAvatarModel),
```

**Cleanup committed alongside it (`fed926d`):** deleted `male_body_floor.fbx`/`female_body_floor.fbx`
+ their `WORKER_AVATARS/*.prefab` counterparts (the 8 files from the git-archaeology section above —
staged their pre-existing local deletion rather than restoring them), and hand-removed the stale
`woman_construction_worker`/`female_body_floor.prefab` source+part entries from
`AvatarPartLibrary.asset`. Left the unrelated 48-portrait-PNG churn and a modified font SDF asset
alone — pre-existing local state, not part of this fix. Re-ran
`Tools ▸ Modular Avatar ▸ Scan & Rebuild Library` afterward: rebuilt clean to 14 FBX / 5 parts
(`female/hair`, `male/hair`, `neutral/hat` ×3) — **zero body-slot parts for either gender now**,
confirming the hand-edit matched what a real rescan produces and the modular assembler can never
again produce a bodiless avatar for these four roles.

### The male/female clipboard-rotation stopgap was a downstream symptom of the same abandoned rig — removed

`ReceivingEquipmentService.Equip()` had a `MaleClipboardLocalEuler`/`MaleRfGunLocalEuler` branch
(the "stopgap" mentioned in the handoff snapshot above) that existed specifically to compensate for
`male_body_floor.fbx`'s mirrored bind pose. With that file gone and both genders now on the
Polyperfect pair, the branch became actively wrong rather than merely unnecessary. Verified live
before touching it (this is the check the handoff's own "Quick orientation" section prescribed):
entered Play Mode, found all 11 live employees, called `Animator.Rebind()` +
`GetBoneTransform(HumanBodyBones.LeftHand/RightHand).localRotation` on every one — **male and female
alike all read the identical `(355.34, 348.60, 2.55)`**, no mirroring. Removed the male branch
(`e903322`), restoring a single shared `ClipboardLocalEuler`/`RfGunLocalEuler` for both genders.
Had to `manage_editor stop` before the code edit would actually compile — Unity's MCP bridge cannot
compile mid-Play-mode (a gotcha already documented earlier in this file for the racking system;
applies here too). Re-verified post-fix by live-equipping a fresh male (Aaron Miller) and female
(Megan Barnes) Receiver and screenshotting both — clipboard/scan gun sit naturally in both cases, no
floating or clipping.

### Current state of the modular avatar system, for the next session

- `AvatarPartLibrary` has real parts for `hair` (1 male, 1 female) and `hat` (3 neutral) only. No
  `body` slot exists for either gender — every role that used to depend on a modular body
  (Receiver/ReachTruckOperator/DockStockerOperator/OrderSelector) is now on the fixed Polyperfect
  path for both genders, same as Boss/Security/IC/TruckDriver/Admin/generic-worker already were.
- The AOD ("Pimp My Employee") editor built earlier this session still works for its 4 scoped
  categories (hair, hard hat, headphones, facial hair) — those never depended on the body slot.
- If a real modular body ever gets built again, it needs a skin-weighted pair for BOTH genders from
  the same rig lineage (ideally derived from `_MainRig.fbx` the way the fixed-avatar Polyperfect
  prefabs already are) — checking bind-pose parity via the `Animator.Rebind()` + `GetBoneTransform`
  technique above, on both genders, BEFORE wiring any hand-anchored prop logic against it, would have
  caught this whole saga at its source days earlier.

### Follow-up, same day — the modular body IS coming back, via the existing fixed-avatar prefabs

Tad wants to actually rebuild the modular body now (strip the baked-in hardhat off
`man_construction_worker`/`woman_construction_worker` in Blender so hats/hair become swappable
again), and asked where to work and whether the AOD would recognize it. Two things confirmed live
before any Blender work started, and a workflow decision:

**Confirmed live: `man_construction_worker.prefab`/`woman_construction_worker.prefab`
(`Assets/polyperfect/Low Poly Animated People/- Prefabs/`) are ALREADY perfect modular-body
candidates, no re-skinning needed.** Both use the exact same `Animator.avatar` asset
(`_MainRigAvatar`) and both read the IDENTICAL `Wrist_L` bind-pose local rotation
`(355.34, 348.60, 2.55)` — no mirroring, confirming they're genuinely `_MainRig`-derived and already
correctly bound. Each has exactly 1 `SkinnedMeshRenderer` (named `man_construction_worker` /
`woman_construction_worker` respectively) — well under the importer's 20-mesh "bulk reference pool,
skip" threshold, so it won't be rejected as a dump file.

**`ModularAvatarImporter.ParseName()` already has a special-case remap for this exact mesh name**
(`Assets/_Project/Scripts/Actors/Editor/ModularAvatarImporter.cs`, ~line 370): a mesh named
`man_construction_worker` parses via the plain gender_slot_variant rule to `gender=male,
slot=construction, variant=worker` — an existing `if (slot == "construction" && variant == "worker"
&& gender != "neutral")` block remaps that specifically to `slot=body, variant=ConstructionWorker`.
This was clearly built in anticipation of exactly this file being used as the body, it just never
got followed through until now.

**The one real catch, confirmed by Tad's own instinct:** the hardhat on `man_construction_worker` is
NOT a separate object — it's sculpted into the single skinned mesh along with the rest of the body.
Using it as-is means every modular avatar built from it comes with a permanently fused hardhat
regardless of what the AOD's separate hardhat accessory does (double-hat/clipping risk). The actual
Blender work needed is small: open the existing mesh, delete the baked-in hardhat geometry, leave
skin weights/UVs/bind pose untouched, export as a new/updated body asset. Whether hair exists under
the helmet (vs. bald scalp) or needs to be added is unconfirmed — check when in Blender, since it
determines whether the AOD's hairstyle-swap category has anything to attach to.

**Workflow decision — no importer code changes needed.** Tad wanted to work entirely inside
`Assets/polyperfect/Low Poly Animated People/` without exporting "lots of little files everywhere."
Walked through why editing `_MainRig.fbx` itself can't be a scan target (not a watched folder; would
trip the bulk-pool skip regardless, since it bundles ~150 characters) — but that doesn't matter,
because `ModularAvatarImporter.PrefabFolder` (`Assets/_Project/Prefabs/WORKERS`) already exists,
already scans recursively, and already has exactly the right subfolder structure sitting unused:
```
Assets/_Project/Prefabs/WORKERS/
├── WORKER_ACCESSORIES/   (already in use — hair/hat props)
├── WORKER_AVATARS/       (target for the new stripped-hardhat body prefabs)
└── WORKER_CLOTHES/       (future clothing/skin work)
```
**Final workflow:** edit `_MainRig` in Blender as much as wanted (single self-contained working file,
Tad's preference) → export/copy the FINISHED individual character prefab or FBX into
`WORKER_AVATARS/` → rescan (auto-triggers on save via the `AssetPostprocessor`, or Tools ▸ Modular
Avatar ▸ Scan & Rebuild Library / the AOD panel's "Rescan Folder" button) → shows up in AOD ready to
tag. Both `.prefab` and `.fbx` work in that folder (the scanner checks both types there
specifically). One combined export can hold multiple named accessory children too — the "no little
files" constraint is about not needing a separate exported file per item, not about folder count.

**Also already in place from earlier the same day, no further action needed:** `EmployeeSpawner.
ModularBodyExists(gender)` (added in the AOD/Pimp-My-Employee session above) is a LIVE gate, not a
one-shot check — the moment a `body` part actually appears in `AvatarPartLibrary` for a gender, the 4
floor-worker roles automatically start using the modular assembler for that gender on the very next
spawn/hire, with zero code change required. Verified this specific mechanism live (injected a fake
in-memory body part, confirmed `UsesModularAvatar` flipped true for that gender only) before this
session even started the Blender-workflow discussion.

### Next-day follow-up (2026-09-27, later) — the new bodies actually made it into the AOD, then two real bugs surfaced getting them into the game

Tad's Blender work landed: `man_body_warehouseCaucasian.fbx` / `woman_body_warehouseCaucasian.fbx`
now exist in the AOD (drop folder renamed to `Assets/_Project/Models/BlenderFiles/Modular_Staff_Models/`
with `BODY_MODELS`/`PROPS_MODELS`/`Z-AOD_WORKSHOP` subfolders — `ModularAvatarImporter.DropFolder`
updated to match, scans recursively so all three are covered). Reported symptom: "we have male and
female body in the AOD yet the modular staff system is reverting back to the legacy" — hiring a
Receiver/OrderSelector/etc. still produced the old `WorkerMan`/`WorkerFemale` look instead of the new
body.

**Bug 1 — the bodiless-avatar safety net was false-positiving on a REAL body, discarding it every
time.** Live-diagnosed via `Unity_execute_code`: `ModularAvatarAssembler.Build()` correctly picked
`man_body_warehouseCaucasian` as the primary part and assembled a real avatar — but
`EmployeeSpawner.ApplyModularAvatar`'s safety check (added earlier this session specifically to catch
bodiless avatars) scans the assembled hierarchy for any child transform whose name contains `"_body_"`.
`Build()` renames the assembled root to `Avatar_{gender}_{variant}` right before that check runs, and
because `man_body_warehouseCaucasian`/`woman_body_warehouseCaucasian` are single-mesh-ON-THE-ROOT
sources (same shape as the standalone hair/hat files), the body's renderer ends up sitting on that
now-renamed root — which no longer contains `"_body_"` anywhere in its name. `hasBody` read false,
the real avatar got destroyed, and the code fell back to the legacy worker mesh — every single time,
for both genders, for all 4 floor roles.

**Fix:** `ModularAvatarAssembler.Build()` now reports the chosen body `Part` directly through its
`chosenOut` dictionary (`chosenOut["body"] = chosen.FirstOrDefault(p => p.slot == "body")`) instead of
`EmployeeSpawner` re-deriving "is there a body" from post-rename object names
([ModularAvatarAssembler.cs:190-200](Assets/_Project/Scripts/Actors/ModularAvatar/ModularAvatarAssembler.cs:190),
[EmployeeSpawner.cs:515-534](Assets/_Project/Scripts/Actors/EmployeeSystem/EmployeeSpawner.cs:515)).
Live-verified post-fix: spawned Receiver/OrderSelector × male/female all correctly get a `ModularAvatar`
child now, legacy mesh hidden underneath as designed.

**Follow-up finding, same diagnosis pass: `man_body_warehouseCaucasian` had a plain `MeshRenderer`+
`MeshFilter`, not a `SkinnedMeshRenderer`** — no skin binding to the armature at all, so it rendered
correctly (hardhat/vest/gloves all showed up right — screenshot-confirmed) but frozen in a permanent
T-pose. Flagged to Tad as a Blender-side gap: the mesh needs an Armature modifier bound to
`DeformationSystem` (Ctrl+P → Armature Deform in Blender) before export.

**Bug 2 — Tad's re-export attempt to fix the skinning introduced a duplicate armature, which then
infinite-looped the importer.** Reported as a wall of repeating console spam:
`Rig Error: Avatar creation failed: Ambiguous Transform 'DeformationSystem/Root_M' and
'DeformationSystem.001/Root_M' found in hierarchy for human bone 'Hips'`, hundreds of identical
lines, stack always through `ModularAvatarImporter.FixNewExport → ScanAndRebuild → Watcher`.

Live-diagnosed by scanning every FBX in the project (`AssetDatabase.FindAssets("t:Model")`, 516
files) for duplicate `DeformationSystem`-named children: `man_body_warehouseCaucasian.fbx`'s
re-export has its **entire skeleton duplicated** — every single bone from `Root_M` down to each
finger joint exists twice (168 transforms where a clean rig has 84), i.e. two full armatures got
merged into one export. `woman_body_warehouseCaucasian.fbx` has a milder version — one duplicate
mesh object, skeleton itself is clean. Classic Blender mistake: an armature got copied
(Shift+D) or appended from another file during a weight-transfer/testing step and the extra one was
never deleted before export.

**Why it looped forever, not just failed once:** `FixNewExport`'s existing "stale humanoid mapping"
repair (written earlier for a *different*, legitimate case — a rename between exports leaving a
cached-but-now-wrong bone map) treats "the current Avatar isn't valid" as "my cached mapping must be
stale, clear it and reimport." For a genuine renamed-bones case that self-heals in one pass. For an
*ambiguous* hierarchy the Avatar can NEVER become valid no matter how many times it's rebuilt — so the
two conditions fed each other forever: reimport → still ambiguous → still invalid Avatar → "must be
stale" → reimport again, on every scan/heartbeat, hammering the console and the editor.

**Fix (`ModularAvatarImporter.cs`):** `FixNewExport` now checks for duplicate object names anywhere
in the hierarchy *before* touching any importer settings — a real rig should never have two transforms
with the identical name. If found, it logs ONE clear error naming the file and the duplicate names
(gated by a static `_loggedAmbiguousArmature` HashSet so it's once per file per domain session, not
every heartbeat) and returns immediately, skipping the Humanoid/material repair pass entirely rather
than ever calling `SaveAndReimport()` on content that can't be fixed by reimporting. Verified live:
cleared the console, re-ran `ScanAndRebuild` — exactly one log line per broken file, no repeat spam,
no reimport loop.

**Current status, what's actually needed to finish this:** both body FBXs need another Blender pass —
`man_body_warehouseCaucasian.fbx` needs its duplicate armature deleted entirely (keep whichever one
actually drives the mesh's skin weights) and `woman_body_warehouseCaucasian.fbx` needs its duplicate
mesh object cleaned up — **and this time make sure the remaining mesh actually has an Armature
modifier / skin weights** (the original T-pose problem from earlier the same day), not just a
correctly-shaped but still-unskinned mesh. Re-export both, then re-run the scan and confirm via
`Animator.GetComponentsInChildren<SkinnedMeshRenderer>` on the imported prefab that `bones.Length > 0`
before assuming it's actually fixed — a clean-looking hierarchy with no ambiguity errors is not the
same thing as a mesh that will actually animate.

---

## Session 2026-09-30 — New `_Avatar_System` pipeline, first working Female Regular body, real assembler bugs fixed, live AOD rebuild + portrait capture

Ground-up rebuild picking up where the 2026-09-27 sessions above left off. Superseded the
`man_body_warehouseCaucasian.fbx`/`woman_body_warehouseCaucasian.fbx` duplicate-armature mess above
entirely rather than continuing to repair it — Tad re-modeled from scratch in a new pipeline.

### New folder structure

`Assets/_Project/_Avatar_System/<Gender>/Bodies/<Type>/{01_StoreBought,02_Blender,03_FBX}` (Tad
renamed the root from `Avatar_System` → `_Avatar_System` to match the `_Project`/`_Recovery`/`_Saves`
underscore convention). `ModularAvatarImporter` now scans **two** roots — the old `DropFolder`
(`Assets/_Project/Models/BlenderFiles/Modular_Staff_Models`) AND a new `NewPipelineRoot`
(`Assets/_Project/_Avatar_System`) — additive, not a replacement; `PROPS_MODELS` (hair/hats/gloves)
still lives under the old root and isn't part of this migration. **`01_StoreBought` staging folders
are explicitly excluded from scanning** — raw pre-edit reference material, never a real part source.
Without this exclusion the untouched store-bought base (e.g. `woman_construction_worker_Rig.fbx`) gets
scanned as its own competing "body" candidate alongside the actually-edited one, reintroducing a
two-bodies bug.

### First working body: Female Regular

Source: `_Avatar_System/Female/Bodies/Regular/03_FBX/female_regular_body.fbx`. The torso mesh is
literally named `woman.bodyA` — a dot, no slot token at all (unlike every other part, which follows
`gender_slot_variant`). Required a new special-case in both `ModularAvatarImporter.ParseName` and
`ModularAvatarAssembler.ParsesAsPart`: a name matching `<gender>.<variant>` (dot, no underscore) is
now recognized as slot `"body"`.

Finalized parts, all pointing at this one FBX: `woman.bodyA` (body), `woman_hands_bodyA` (hands),
`woman_hair_BOB` / `woman_hair_bob-black` / `woman_hair_bob-blonde` (hair, female always gets one —
never bald), `woman_feet_bootsBlack` (feet). **No separate head mesh** — a `woman_head_bodyA` part
existed briefly but turned out to be a stale/broken artifact (see bug #3 below) with no matching
current source to repair it against, and the torso's own neck/head area looks fine without one per
Tad — deleted rather than replaced. Revisit if/when a real head mesh gets modeled.

### Real bugs fixed (in the shared assembler/importer code — affect ALL employees, not just this body)

1. **`ModularAvatarAssembler.WeightedPick` had a `pool.Count == 1 → return it unconditionally`
   shortcut** that completely bypassed weight. A slot with exactly one candidate (e.g. female
   `"feet"` with only `woman_feet_bootsBlack` defined) always showed up in-game even after being set
   to 0% in the AOD's weight editor. Removed — weight is now always checked regardless of pool size.
2. **Zero-total-weight fallback changed from "pick uniformly at random anyway" to "return null"**
   (slot renders nothing). Per Tad: 0% in the AOD must mean "never shown," not "ignored because
   nothing else scored higher." Required propagating null through `PickVariant` and null-guarding
   every `chosen.Add(...)` call site in `Build()` (vest, hardhat, headphones, the main per-slot loop).
3. **`IsolatePart` (the Submit/Update backend) only prunes sibling meshes that `ParsesAsPart`
   recognizes.** Before fix #1's dotted-name case landed and compiled, isolating
   `woman_hands_bodyA`/`woman_hair_BOB`/`woman_feet_bootsBlack` each *also* kept a full stray copy of
   the torso (`woman.bodyA`) bundled inside — because at isolate-time it wasn't yet recognized as a
   prunable part. Visible symptom: the Hierarchy showed a nested nested-under-nested nested
   `DeformationSystem > woman.bodyA + woman_head_bodyA` structure under one child, and that one
   didn't animate (two full rigs stacked, only one actually driven). Re-running Update after the
   parser fix compiled cleaned all of them to proper single-mesh isolates. **If a future part looks
   "doubled up" after Submit, suspect a parser-timing issue like this — re-run Update once the fix is
   confirmed compiled, don't assume the source FBX is broken.**
4. **`"head"` was hardcoded into `ModularAvatarAssembler.DeprecatedSlots`** (a 2026-09-27 fix for an
   old legacy-asset double-stacking bug, see above). Removed, and `"head"`+`"hands"` added to
   `CoreSlots` (always included when present) — the legacy source that motivated the exclusion is no
   longer scanned at all, so it's safe to treat `head` as a normal slot again.
5. **FBX main-object fileIDs in this Unity version (6000.6.0f1) are large 64-bit hashes**
   (e.g. `919132149155446097`), confirmed empirically by cross-referencing a known-good
   `AvatarPartLibrary.sources[].prefab` entry — NOT the classic legacy `100000` constant some older
   Unity tooling assumes. Don't hand-guess these in a YAML edit; look up an existing reference to the
   same asset type in the project first.

### New tooling/features

- **`ModularAvatarImporter.FinalizeAllPendingMenu`** (`Tools ▸ Modular Avatar ▸ Finalize All
  Pending`) — headless equivalent of clicking Update on every already-finalized row and Submit on
  every raw row in the AOD panel, in one pass. Reuses `ModularAvatarFinalizer.TryUpdateFromRawSource`/
  `TryFinalize` directly (the same methods the UI buttons call), added because driving the AOD
  panel's actual buttons isn't scriptable from outside the Editor UI.
- **`EmployeeSpawner.RefreshAllModularAvatars()`** — iterates every registered `EmployeeIdentity`,
  calls the existing `RefreshAvatarAppearance` on each (rebuilds their `"ModularAvatar"` child from
  current AOD data, same stable per-employee seed) and forces a fresh portrait via
  `EmployeePhotoBooth.GeneratePortraitForRecord`. Wired into `AODPanel.Submit()`, gated on
  `Application.isPlaying` — hitting Submit/Update in the AOD while playing now updates already-hired
  employees live (both their in-world look and their portrait) instead of only affecting future hires.
- **`EmployeePhotoBooth.CapturePortrait` now actually uses the modular avatar system.** Previously
  (see the "now-removed ApplyModularAvatar" comments already in that file from 2026-09-21/22) it
  photographed a fixed placeholder body per role, completely independent of `AvatarPartLibrary` — so
  a portrait never reflected the assembled look. Fixed: when
  `EmployeeSpawner.UsesModularAvatar(record)` is true, `CapturePortrait` calls
  `EmployeeSpawner.RefreshAvatarAppearance(identity)` on the temp capture instance before posing/
  shooting it (reuses `ApplyModularAvatar`'s existing hide-base-mesh + attach + Rebind logic
  wholesale). Two things that path doesn't already handle, because a continuously-rendered live
  employee never needed them, had to be added specifically for this one-shot synchronous capture:
  `AnimatorCullingMode.AlwaysAnimate` on the modular avatar's own Animator (same T-pose-avoidance
  reasoning as every other fix already in this file), and an explicit `Play("Waving", ...)` call on
  that same Animator (it shares the worker's own Animator Controller — `ApplyModularAvatar` assigns
  it directly — so the state exists there too; the existing param-mirroring `ModularAvatarRig`
  component never gets a `LateUpdate` to run before the synchronous capture happens, so state has to
  be forced directly rather than relying on it). **Not yet visually confirmed**: the camera framing
  (`LookAt`/light offsets, tuned to the old placeholder body's height) may need adjustment for the
  new body's proportions — untested against the actual proportions of `female_regular_body`.

### Unity MCP tooling gotchas hit this session (this project's official `Unity.AI.MCP` server specifically)

- **The Editor needs OS focus to reliably persist changes.** A code edit compiles, a menu item
  reports success, `AssetDatabase.SaveAssets()` runs inside the called method — and the file on disk
  still doesn't reflect it, sometimes for several retries, until the Editor window is manually
  clicked into. If a scan/rebuild result doesn't match what the code should produce, try that before
  assuming the C# is wrong.
- **`Unity_GetConsoleLogs` works reliably; `Unity_ReadConsole`'s type filter does not** (matches the
  pre-existing note that an empty `ReadConsole` result is never proof of a clean console — this
  server has a working alternative under a different tool name).
- **`Unity_ManageGameObject`'s `Create` action silently no-ops for raw `.fbx`/Model assets** — returns
  `success: true` with a plausible instanceID, but the created GameObject is an empty placeholder
  (`Transform` only, default bounds). Only works for real `.prefab` files (the response message text
  differs too — check it, not just `success`). No working substitute found for instantiating an FBX
  directly via this bridge; drove `ModularAvatarFinalizer`'s real methods via a custom `[MenuItem]`
  instead.
- **Structured/array tool parameters are broken** (`ApplyTextEdits`/`ScriptApplyEdits`'s `edits`,
  `ManageAsset`'s `properties`) — always fail with a JValue/JObject deserialization error regardless
  of shape. Used the plain filesystem `Edit`/`Write` tools directly on `.cs`/`.asset` files instead,
  relying on Unity's own file watcher (once focused, see above) to pick up the change.
  `Unity_RunCommand` (arbitrary C# execution) is effectively non-functional here too — every code
  shape tried either failed to compile (the tool's own namespace-wrapping conflicts with top-level
  statements) or compiled with zero observable side effects. Use `Unity_ManageMenuItem` against a
  real `[MenuItem]` method for anything that needs code execution.

---

## Session 2026-10-02 → 10-03 — AVATAR 2.0 REBUILT, WORKING END TO END (cross-machine handoff)

**State in one paragraph:** the modular-avatar pipeline was restarted from scratch ("Avatar 2.0") and now works end to end for **Female / Regular**: 5 body parts + a neck piece + the first
hair, all on ONE shared rig, all animating (walk/idle), flat-shaded, with ground contact shadows. Everything below was verified in Unity (Play Mode screenshots + scripted builds). Male and the other 4
body types are PARKED. The detailed rules live in the skill `.claude/skills/ModularAvatarSystem/SKILL.md` (read it first); this section is the "what happened + what to do next" log.

### Paths (the only ones that matter now)
- Working root: `Assets/_Project/__Avatar_System2.0/` (two underscores). `Female/Regular_Body_Type/{1. Blender, 2. FBX, 3. Prefab (post AOD Submit)}`, `Docs/` (BLENDER_CHECKLIST.md,
  AVATAR_SLOTS_AND_BODY_FALLBACK.md), `Textures/` (flat material + its 3 textures). `ModularAvatarImporter.DropFolder` scans ONLY this root. The old `_Avatar_System/`, `Models/BlenderFiles/Modular_Staff_Models/`,
  `Prefabs/Modular_Staff_Prefabs/` are legacy and NOT read.
- `.blend`: `1. Blender/Female_Body_Reg-White.blend` (never scanned). Body FBX: `2. FBX/female_regular_body.fbx`. Runtime catalog/weights: `Assets/_Project/Resources/Resource_AvatarSystemAssets/`.
- `_Project/Resources` was reorganised: `Resouce_UI` (sic), `Resource_AvatarSystemAssets`, `Resource_Fonts`, `Resource_Prefab`, `Resource_SOs`; ~35 scripts rewired to those `Resources.Load` paths.

### The workflow that works (Blender 5.2.x → Unity)
1. **Blender scene:** ONE armature `DeformationSystem` (72 bones) at loc 0 / rot 0 / scale 1, no parent empties (the old `Main`/`Group` wrapper with 90deg/0.01 scale was baked away), every mesh parented to it with ONE Armature modifier,
   names `Gender_Slot_Variant`. **Before export: Weights > Limit Total = 4, then Normalize All** (Unity caps at 4 bones and renormalizes; skipping this makes Blender and Unity disagree). **Weight EVERY vertex** (a skinned mesh with
   zero weights collapses to the origin in Unity - found with the hair).
2. **Export ONLY with the saved FBX preset `Unity_Avatar`** (source in git `BlenderPresets/Unity_Avatar.py`; install to `%APPDATA%\Blender Foundation\Blender\5.2\scripts\presets\operator\export_scene.fbx\`; UTF-8 no BOM;
   Forward -Z, Up Y, FBX Units Scale, Selected Objects, Armature+Mesh, deform bones only, no leaf bones, no animation). A manual export without the preset arrived with every node at scale 100 and exploded all avatars.
3. **THE BODY IS ONE FILE:** always select the armature + ALL body meshes (Torso, Legs, Hands, Head, Feet) + the neck/hair pieces you want, and overwrite `female_regular_body.fbx`. Exporting a subset OVERWRITES the file and the other parts vanish
   from Unity (it happened once; recovered by re-exporting everything). So far the collar and hair ride in the body file and that works; clothing may get its own FBX later (open decision).
4. **Unity:** `Tools > Modular Avatar > Scan & Rebuild Library`, then **`Tools > Modular Avatar > Finalize All Pending`** (same as AOD Submit/Update). The AOD "Update" button ONLY re-reads the FBX - if the FBX mtime did not change after your
   export, the export did not land. After ANY FBX re-export re-run Finalize for ALL parts (finalized prefabs hold bone lists from the FBX they were made from).
5. Unity compiles while NOT in Play Mode; compiling mid-play corrupts live avatars. Stop Play, edit code, refresh, restart Play.

### What was built / changed this session (all compiled clean, verified)
- **Humanoid rig:** `ModularAvatarImporter.IsBodySlotName` accepts slot `torso` so the torso file gets the Humanoid avatar (CreateFromThisModel; do NOT Copy-From-Other-Avatar). Faces +Z, Left hand on -X (correct) so the old
  `(0,180,0)` avatar yaw in `EmployeeSpawner.ApplyModularAvatar` was REMOVED.
- **`Body` fallback (design from the other PC, now implemented):** variant `Body` = reserved nude default for a body slot (`ModularAvatarAssembler.IsBodyDefault`); wardrobe replaces it, an empty slot shows it, a slot hidden by another
  garment shows nothing. The nude torso always anchors the rig. Slots now: torso, legs, hands, head, feet (+ hair, neck). `body` is the legacy alias of `torso`.
- **Neck slot:** optional accessory (`ModularAvatarAssembler.NeckSlot`), 35% spawn chance, rigid pieces ride `Neck_M`, garments can hide it, AOD "Neck Items" tab. First item `Female_Neck_Collar` (skinned).
  Importer/assembler also accept the typo `Female_Neck.Collar` (one `_` then a dot).
- **Hair:** first hair is the store-bought bob (named `woman_hair_BOB` until Tad renamed it to `Female_Hair_BobBlack` in Blender), mirrored half-mesh, 100% weighted to `Head_M`, UVs on the near-black palette cell.
  The legacy 180deg head/hair/hands/hard-hat orientation fixes (`ApplyLegacyOrientationFixes = false`) were turned OFF - they put the bob on backwards. Rigid parts now attach as just the mesh (no spare armature copy).
- **Flat material:** the importer forces `Assets/_Project/__Avatar_System2.0/Textures/AA_atlas-LPAP.mat` (copies of the polyperfect albedo/emission/specular). The old "source" atlas has a gradient inside each colour cell so a flat skin
  patch rendered as 17 shades; albedo is flat (1 colour). To revert, point `SharedPaletteMaterialPath` back at `atlas-source-LPAP.mat`.
- **Foot contact shadows:** `FootContactShadow.cs` + `Shaders/FootContactShadow.shader` + `Resources/Resource_AvatarSystemAssets/FootContactShadow.mat`. Soft blob under each foot (fades as the foot lifts) + faint body blob; auto-attached to every
  employee by `EmployeeSpawner`; works in all graphics presets. Tuning constants at the top of the file.
- **Walk animation:** controllers `MaleStaff` (Walk_Male) and new `FemaleStaff` (Walk_InPlace_Female) from the Polyperfect pack; `EmployeeSpawner.ControllerFor` picks by gender. Walk speed 3.0 -> **1.4 m/s** (worker prefabs in
  `OBSOLETE ASSETS/Workers_Obsolete/`, `AgentAnimation`, `GuardController` defaults). Employees now take ~2x as long to walk anywhere - raise the number if it feels slow (above ~1.5 the calm clips look hurried).
- **Graphics tone-down (Tad: too saturated, edge bleeding, too much DOF):** `PP_Toaster` sat 18->6, contrast 20->8, exposure .6->.45; `PP_Good` 5->3 / 5->4 / .5->.45; `PP_Ultra` 16->6 / 6->5 / .45->.4, bloom .3@.5 -> .12@.9; FSR sharpness
  (URP_Toaster) .92->.4 (halos looked like bloom); DOF halved (Ultra aperture 3->6; Good start/end 48/90 -> 96/180; Gaussian radius can't go below 0.5); yard backdrop blur cut ~53% (VP_YardBackdrop radius 1->0.5, start/end x1.15;
  `BackdropBlur.mat` _MaxBlurRadiusPixels 8->3.9).
- **Lighting / sun bug (`DayNightCycle.cs`):** the sun angle assumed a 06:00-18:00 day but the scene sets sunset 21:00, so from 18:00-21:00 the sun was BELOW the horizon yet lit, shining UP and throwing forklift/rack shadows up the walls.
  Fixed with `SunPitch(hour)` (follows sunrise/sunset hours) + `aboveHorizon` fade (full strength only ~30deg up). Scene: sun intensity 2->1, `horizonSunColor` (1,.55,.25)->(1,.74,.55), `dayAmbient`/ambient x0.8.
- **UI:** the bottom-bar Inbound button icon broke when `Art/Icons/Play Bar Icons` moved to `Sprites/Icons/Play Bar Icons` (path-only USS url); now GUID-based `ContractsThumb` (Tad's final pick; the image has "Contracts" printed on it).

### Gotchas learned (each cost real time)
- Blender MCP (`execute_blender_code`): `bpy.data.libraries.load(...)` PULLS the parent armatures/empties into `bpy.data` as orphans - clean them (`bpy.data.objects.remove`, purge orphan armatures/meshes) or avoid it. The MCP can't run
  select/transform operators while Blender is in Edit Mode ("context is incorrect") - the user may be editing; look before acting.
- Unity MCP: `execute_code` works for C# snippets, but compile/refresh must finish first (check the newest `Library/ScriptAssemblies/*.dll` mtime is newer than the .cs mtime; the assembly name varies: GameCore.Simulation, GameCore.Actors.Editor, Assembly-CSharp).
- Saving prefabs via `PrefabUtility.SaveAsPrefabAsset` reserializes extra fields on the old worker prefabs - for one-line changes edit the YAML text instead.
- Editor-generated noise that should NOT be committed: `ProjectSettings/TimeManager.asset` (runtime timeScale), `Assets/_Saves/quicksave*`, `AA_LowPolyCommon*.mat`/`AA_LowPolyStylized.mat` re-saves, `Sprites/Portraits/*`.

### STATE AT HANDOFF (read this before touching anything)
- **Blender and Unity were both closed at the end of this session, and NOTHING from this session is committed to git** - commit + push before switching machines (code, `Textures/`, `FootContactShadow.*`, `FemaleStaff.controller`, the `.blend`, FBX,
  finalized prefabs/assets, scene, PP profiles).
- The `.blend` (saved 01:22) has the bob renamed `Female_Hair_BobBlack` and a second new hair `Female_Hair_Dom`. The last FBX export (01:19) contains `Female_Hair_Dom` but NOT the bob, so Unity has finalized `Female_Hair_Dom` and a STALE `woman_hair_BOB`
  (asset + prefab in `3. Prefab (post AOD Submit)`). **Next export must select the armature + all body meshes + the collar + BOTH hairs**, then Scan + Finalize, then delete the stale `woman_hair_BOB.asset/.prefab` (+ .meta) so there aren't two bobs.
  `Female_Hair_Dom` has not been reviewed by Claude yet (check weights/orientation/UVs like the bob: every vertex weighted, limit 4, normalized).
- Old Blender 5.1/4.3 installs were uninstalled but their folders under `C:\Program Files\Blender Foundation\` still need an admin delete. On a new machine install Blender 5.2 + the preset + the BlenderMCP add-on.

### NEXT (Tad's plan)
1. More hair types (same recipe: weight 100% to `Head_M` or skin to head bones, UVs on a palette cell, name `Female_Hair_X`, export with the preset, Finalize). Open: colour variants vs separate meshes per colour; AOD weights per hair.
2. Then a CLOTHING design discussion: torso/legs slots (shirts/pants/shorts interchangeable), coveralls as a one-piece that hides torso+legs(+arms), hide-mask rules (`HiddenBodySlots`; code already aliases `torso`/`body` and can hide rigid neck pieces),
   whether clothing gets its own FBX (recommended) and how it rebinds to the body skeleton (`TryRebindToBody`), the still-open `Waist`/`Arms` slots, portrait framing for the new body. Male + the other 5 body types come after Female Regular is finished.
3. Known small things: the AOD still shows weight controls on `Body` parts (ignored by the assembler); finalizer output paths are hardcoded to Female Regular; AOD preview thumbnails show parts as authored.

### Addendum 2026-10-03 (later) — NSFW naming, Dirty Dev, gag mouth, stockings pairing, bare chance (supersedes the "STATE AT HANDOFF" hair notes above)
- **NSFW is a MESH-NAME tag:** `gender_slot_variant_nsfw` (e.g. `Female_Head_Gagged_NSFW`; blank/absent 4th+ segment = not NSFW). Single rule: `DirtyDev.IsNsfwName`. **Dirty Dev** (AOD title-bar button, red OFF / green ON, caption "Dirty Dev currently ON/OFF") defaults OFF = NSFW HIDDEN
  (PlayerPrefs `DirtyDev`, so every fresh build is safe). OFF hides tagged parts from the assembler, spawner, Pimp My Employee and the AOD grid; nothing is deleted. Any scene object can use the `NsfwObject` component.
- **Current finalized parts (11):** Feet_Body, Hands_Body, Head_Body, Legs_Body, Torso_Body, Hair_Mem (safe) + 5 tagged: `Feet_Stockings_Gray_NSFW`, `Legs_Stockings_Gray_NSFW`, `Hands_Black-Gloves_NSFW`, `Head_Gagged_NSFW`, `Neck_Collar_NSFW`.
  Their AOD weights were copied from the old untagged assets (75 / 50 / 49.6 / 25.3 / 49.5). The OLD untagged versions were deleted; a backup is in `%TEMP%\avatar_stale_backup_1107` (local machine only).
  Verified over 150 builds: Dirty Dev OFF = 0 NSFW meshes visible and no missing body parts; ON = all appear, stockings always paired.
- **Matching sets:** feet<->legs wear the SAME variant name together (`Stockings_Gray` on both); **bare chance:** a `Body` part's AOD weight = share who wear nothing in that slot; **gag:** `GagMouthMotion` (slow groans) attaches when the head variant contains "gag";
  **a part never hides its own slot** (fixed self-hiding socks). See the skill for each.
- **Mesh renames create NEW parts:** after renaming a mesh in Blender and re-exporting, Scan + Finalize creates the new asset with default weight 1; copy the AOD settings from the old one and delete the old (untagged) asset + prefab.
- Still uncommitted in git (nothing from this whole session is committed): commit + push before switching machines.

### Addendum 2026-10-04 — New eye system + FaceExpressionController (Unity side built, mesh export PENDING)
- **Eyes (Blender, `testcopy`, not yet on the real heads):** faceted sphere eyeball recessed 1.8 mm (Eye_R/L bones), skin-coloured upper/lower lid shells that wrap round the eyeball (bones `EyelidUpper_R/L`, `EyelidLower_R/L`, children of Head_M, pivot at the recessed eye centre), thick upper lash band + 3 cube lashes + straight outer tail + tear-duct wedges. Rebuild script (local only): `%TEMP%\eyebuild.py` with `EYE_PARAMS` overrides. Baked idle: upper lid edge ~17 deg above centre, lower ~26 deg below; fully closed = rotate upper ~23 deg / lower ~26 deg.
- **`FaceExpressionController`** (`Scripts/Actors/EmployeeSystem/`): attached by `EmployeeSpawner.ApplyModularAvatar` to every modular avatar (Fear for a gagged head, else Neutral). Moods: Neutral, Surprise, Fear, Anger, Confusion, Tired, Sleeping, Sad. `SetMood(mood, holdSeconds)`. Pose table = brows (world-space metres), lids (degrees of closing), gaze, jaw. Random blink every 6-14 s (mood scales rate/speed; none when Sleeping), gaze saccades, fear startle spikes. Missing bones are skipped, so lids only start working after the new head FBX is exported. Replaces the deleted `GagFaceMotion`. Jaw is left to `GagMouthMotion` when present.
- Lid close direction is derived geometrically (not from bone axes); **verify the lids close the right way after the first export.**

### Addendum 2026-10-04 (later) — LESSON: never move humanoid-mapped bones (Eye_L/R, EyeEnd) in Blender
Moving `Eye_R/L` (+`EyeEnd`) onto the eyeball centre made in-game employees look eyelid-less with bulging eyes. The Humanoid Animator writes each mapped bone's position from the Avatar's stored skeleton (the original rig positions, frozen in the FBX importer's HumanDescription), so live `Eye_R` snapped back ~6 mm while the eyeball mesh was skinned to the NEW position -> eyeballs displaced out of the lids. Fix: Eye/EyeEnd bones restored to their original positions (eyeball mesh is unchanged; gaze pivots ~6 mm off-centre, which is harmless). The custom lid bones (`Eyelid*`) are NOT humanoid-mapped, so they can sit on the true eye centre. Diagnose by comparing a prefab bone's `localPosition` with the live employee's.

### Addendum 2026-10-04 (end of day) — Face-slot gag, eye system, Pimp My Employee clothes, portraits fixed

**Progress (all verified in Play Mode unless noted):**
- **Eyes/brows/lids** rebuilt (concentric lid shell, low-poly eyeball, lashes) and copied identically onto `Female_Head_Body`; nose + eye-area faces grafted from the old gagged head. `FaceExpressionController` drives moods, blinks, brows, lids, gaze; gagged workers also **dart their eyes left/right every ~7-13 s** (`EnableDarting`). Gagged mood split: 15% Tired, rest 50/50 Anger vs Fear (hash of employee GUID, demo only).
- **Gag is now a `face`-slot ACCESSORY** (`Female_Face_Gag_NSFW`, 50% spawn via `OptionalSlotChance["face"]`), not an alternate head; the head is always `Female_Head_Body`. `IsExpressionFaceName` keeps the legacy mood-swap/prune from disabling it. The old gagged head (`OLD_Head_Gagged_NSFW`) is retired (a stale renderer of that name can still appear in a built avatar - clean up when convenient).
- **Gag mouth:** `GagMouthMotion` holds the jaw ~16 deg open (lower lip sits under the ball; upper lip untouched) plus groans up to ~24 deg. Tuning lives in the `GagMouthSettings` ScriptableObject (`Resources/Resource_AvatarSystemAssets/GagMouthSettings.asset`) - edit live in the Inspector.
- **Strap:** a rebuilt head-hugging strap (`POST-STRAP-v1.blend` in %TEMP%) looked worse (ends drifted off ball/rings) and was REVERTED to the pre-strap `.blend`. Current strap: aligned at the front, but the rear section floats/looks jagged under the hair. Weights are mostly Head_M; rings/ball follow lip bones, so a little drift while the jaw opens is expected.
- **Pimp My Employee now covers clothes:** `EditableOverrideKeys` gained `face, torso, hands, legs, feet` (tabs: Face / Gag, Torso / Top, Gloves, Legs / Pants, Feet / Socks; `neck` already existed). NSFW items need Dirty Dev ON. "Body" = bare, "None" = empty slot. Stockings are separate feet + legs parts (pick both). Only parts that exist in the library show - torso options in the library: `Torso_Body`, `Torso_CoverallsBlue`, `Torso_Corset_NSFW`.
- **Portraits (big fix):** Hiring Board / Roster / Employee List / Info card / hover popup all read `EmployeePhotoBooth.CustomAvatarCache`. Portraits were silently photographing the PLACEHOLDER body because the booth stand-in prefab (`woman_body_WarehouseWorker`) has no `EmployeeIdentity`, so the modular-avatar step was skipped. Now `EnsureBoothIdentity` adds one (booth children are system-managed, never enter the roster). New single entry point **`EmployeeSpawner.RefreshLookAndPortrait(identity)`** = rebuild avatar + re-shoot portrait + refresh sprite + restart live feed; used by Pimp My Employee Apply and `RefreshAllModularAvatars` (which also re-shoots Hiring Board candidates). `EmployeePhotoBooth.OnPortraitUpdated` makes open Roster/List/Hiring Board redraw. The animated Info feed now dresses the model with the modular look too.
- **Framing:** modular avatars are framed by aiming at the HEAD bone (`StillAimBelowHead` 0.31 m; live feed `LiveAimBelowHead` 0.20 m, `ModularLiveFov` 19). Sliding the model does NOT work - the still camera's aim point is model-relative and cancels it.

**Lessons:** (1) A `Destroy`ed child is still found by `transform.Find` the same frame - test with a frame gap or `DestroyImmediate`. (2) To debug a capture path, hook `Application.logMessageReceived` inside `execute_code` (Editor.log lags). (3) Never trust "compiled" for portrait work - render the sprite from `CustomAvatarCache` to a PNG and look at it.

**Plan / next:**
1. Gag strap rear section: replace only the back half (ears -> nape) with a flat band that sits on the head/neck; keep the front half. Consider weighting strap ends partly to jaw/lips so they follow the rings.
2. Model more tops/pants/stockings variants; each is one more mesh in `female_regular_body.fbx` (preset export, then Scan + Finalize). Set AOD weights per item.
3. Re-check Fear vs Surprise, Confusion/Tired/Sad expressions visually; optionally serialize eye-dart and mood poses into assets; stat-driven moods.
4. Hair types (colour variants vs separate meshes), then male + other body types.
5. Verify live in the real UI: open Hiring Board / Roster / Employee Info and confirm portraits redraw after Pimp My Employee Apply.
6. Housekeeping: commit + push (large uncommitted batch; `_AvatarBackups/` at repo root is not git-ignored - decide ignore vs commit); cached portraits of existing staff refresh on look change / AOD Update.


### Addendum 2026-10-05 — ONE master FBX, new face behaviour, caps/headphones/jeans/belt/shades/necklace, hair trimmed under hats

**Workflow change (supersedes the per-part-FBX notes above):** every part of a body type now lives in ONE Blender file and ONE export. `Female_Body_Reg-White.blend` holds all meshes under the single `DeformationSystem`; export selects the armature + every mesh and overwrites `2. FBX/female_regular_body.fbx` with the `Unity_Avatar` preset. The 17 separate FBXs (hair colours, boots, gloves, coveralls, hardhats) were imported into the blend and DELETED. Then `Scan & Rebuild Library` + `Finalize All Pending`. AOD settings (weights/roles/colour variants) live on the finalized `AvatarPartAsset`s and survive every re-export.
- Never leave a SECOND FBX with the same mesh names in the folder (e.g. a Ctrl+E default export next to the `.blend`, or a stray `Female_Body_Reg-White.fbx`): the scanner lists every FBX, finalizing binds to the FIRST source found (`1. Blender` sorts before `2. FBX`), and duplicates hide. Two were found this session and moved to `%TEMP%`.
- A stray PASTED object with its own armature (`Female_Head_Gagged_NSFW` + `DeformationSystem.001`) got into one export — duplicate armature = Unity import error. Export by explicit selection and exclude anything not parented to the one `DeformationSystem`. (Delete the stray in the blend; it is Tad's paste, left alone.)
- **Importer fixes (`ModularAvatarImporter`):** `FixNewExport`'s duplicate-name check now ignores the file ROOT (single-mesh FBX = root + same-named child, harmless; it produced 18 false-positive errors, and the early bail-out had been silently skipping the material remap for those files — once it stopped, finalized prefabs pointing at the old internal materials went null until re-finalized). Bulk-pool skip threshold raised 20 → 100 (the master FBX has 40+ meshes).
- **Materials gotcha (cost an hour):** a mesh exported with an EMPTY material slot arrives in Unity as the default `Lit` (grey). Always `mesh.materials.append(AA_PP)` in Blender before export, and after Finalize check no renderer has `sharedMaterial.name == "Lit"`. Hair/caps/headphones went grey twice this way.
- **After any FBX re-export you must stop Play, then Scan + Finalize** — finalized prefabs hold bone lists from the FBX they were built from. Unity holds a file lock on the FBX; the export retry loop handles it.
- Weights: after any bmesh subdivide, weights are un-normalised (totals up to 2.5). Limit to 4 bones and normalise before export. New clothing is skinned by copying weights from the NEAREST body vertices (3-nearest inverse-distance, per reference mesh) and given an Armature modifier — without that modifier the FBX exporter writes no skin and the part freezes in T-pose.

**Face / animation (`FaceExpressionController`)**
- Blink every 2.5–5 s. Look-around every 2.5–6 s (eyes ±32°, hold 1–2.5 s) and the HEAD follows (Head_M 65% / Neck_M 35%, head turns 0.9× the eye yaw, ~0.22 s lag). 18% of glances look DOWN at the shoes, 15% look UP at the sky. Sleeping does none of it. Constants at the top of the file.
- Smile every 9–22 s, 1.6–3.6 s long, driven by `OuterMouth_L/R` + `Cheek_L/R` bones and a small lower-lid squint. Skipped when gagged or not Neutral. Strength constants `SmileCornerUp/Out`, `SmileCheekUp`, `SmileSquintDeg` (currently 0.0315 / 0.0255 / 0.0225 / 6.75 — 0.75× of the first tuned "10×" value).
- Gag strap weights: the rear strap (y > -0.04) now copies the weights of the nearest skin vertices (~62% Neck_M / 26% Head_M / 12% Jaw_M). 100%-Head_M clipped into the nape once the head started tilting/turning; the original jaw/neck mix pulled it forward when the jaw opened.

**New parts (all in the master FBX, all finalized)**
- `Neutral_Hat_HeadphonesRed/Yellow` — the ORIGINAL legacy mesh, NO offset: it sits at neck height (z 1.46–1.55) with the band behind the neck and cups on the collarbones, which is exactly how the old pipeline wore them. Skinned from nearest torso/head verts. (A homemade ear-level headband and an ear-level fit of the original were tried and rejected — they stacked with caps/hair into "three hats".)
- `Neutral_Hat_CapRedWhite / BlueYellow / PurpleOrange / DarkGreen` — from Polyperfect `hat-cap.fbx`, fitted by Tad in Blender (scale 0.8, X -7.48°), then widened 12% + 5% so the cap clears the hair. Crown and bill use different palette cells. Assembler: variant starts with "cap" → `hat.cap` override key + AOD "Cap" tab; independent roll `CapChance = 0.60` only when no hardhat landed.
- `Neutral_Legs_JeansDkBlue` (legs, mirror modifiers applied), `Neutral_Waist_Belt-ActionMans` (NEW `waist` slot, 40%), `Neutral_Glasses_ShadesBlack` (NEW `glasses` slot, 30% — its own slot so shades stack with the gag), `Neutral_Neck_Necklace-ActionMan` (neck slot). Each was bbox-fitted to the female body, pushed out of the body mesh with a BVH nearest-point pass (0 vertices inside), then skinned. AOD tabs: "Glasses / Shades", "Belt". New parts default to a LOW AOD weight (jeans 9/400, necklace 4/400) — set weights in the AOD.
- Only the 6 MODULAR female workers can wear any of this; men and the other roles are still fixed Polyperfect prefabs.
- **Hair under hats:** `ModularAvatarAssembler.TrimHairUnderHats` — when a cap/hardhat is worn, each `_Hair_` mesh is cut at the hat's rim (triangles whose centre is above rim + `HatTrimMargin` 4 mm are dropped) PLUS one extra ring of triangles touching the kept ones (so low-poly hair reaches under the brim; vertices welded by position because the meshes are hard-edged). Trimmed meshes cached per (mesh, rim). Mesh-space UP is Blender Z (not Y), detected from the hat bounds.
- Experiment removed: a "HotHead" sculpt of the female head (red lips, flared nose, arched lashes) was built and then deleted at Tad's request.

**Tooling notes**
- Blender MCP: leave the file in Object Mode, deselected, overlays ON, Select Box tool, all objects visible — my screenshot helpers turned overlays/xray/hide off and it made Blender feel "unselectable". Loading the export preset = exec the preset file with `import bpy` stripped and a fake `bpy.context.active_operator`.
- The game camera rig overrides any Camera you move; to look at a worker render a temp `Camera` into a `RenderTexture` and write a PNG.
- `Unity_RunCommand`/`execute_code` mid-Play compiles are unsafe; always `manage_editor stop` before a re-export rebuild.

**Open / next:** delete the stray `Female_Head_Gagged_NSFW` in the blend; set AOD weights for the new parts; male body + other body types; the one hair triangle that pokes beside a cap bill on Mem hair; gag-strap rear shape; verify caps/headphones/hair-trim on live staff (all renders so far were test lighting).


### Addendum 2026-10-06 — Toaster perf profiling, blur volume, rats, DebugLogSwitch, two-holster gun, HOT + Vanilla heads

**Perf investigation ("Toaster dropped from ~30 to 10-15 fps")** — profiled in the Editor (Unity MCP `Unity_RunCommand` + `ProfilerDriver`/frame-time sampler; A/B-disable one system at a time). Measured on a fresh load, Toaster, timeScale 1: baseline 14.7 ms (68 fps).
- **Biggest cost: `RatBehavior.UpdateBreeding`** called `FindObjectsByType<RatBehavior>()` per rat per frame (9 rats = 9 scene scans/frame, ~4.4 ms = 30%). Fixed: static `s_allRats` list, mate-proximity check 4x/s with sqrMagnitude, `_fullyGrown` skips the per-frame scale/speed write. -> 10.3 ms (97 fps). That file had not changed since Aug 23: cost scales with rat count (they breed to 15) and scene size, not with this week's avatar commits.
- **AOD preview camera** (`AODPreviewStage`) rendered a 1024x1024 RT every frame for the whole session (~0.9 ms in the profiler). Now `enabled=false` at setup, true in `ShowPart`/`ShowAssembledInstance`, false in `Clear()`.
- NavMesh agents cost ~0.5 ms total (the earlier "4 ms" was rats skipping their Update when their agent is off). Foot shadows / face controller / animators / lights / volumes / avatar meshes each <=0.5 ms. Shadows ~0.
- **Not reproduced:** Tad's 10-15 fps. 97 fps here. Blender closed, Gizmos off on his side. Ask for a profile of his own slow live session (or check rat count after a long session, Profiler window open, game speed).
- Tooling: results from a dynamic `Unity_RunCommand` MonoBehaviour can't be read back by a later command (the dynamic type is invisible) -> write results to a temp file and wait with Monitor. Game starts at `Time.timeScale = 0` (paused): set it to 1 before measuring. `Debug.Log` volume is tiny (~0.2/s) - muting logs is NOT a perf fix.

**Toaster blur:** `YardBackdropVolume` (profile `VP_YardBackdrop`) has an active DepthOfField and sits on top of every preset's own profile, so Toaster stayed blurred even though `PP_Toaster` has DoF/Bloom off. `GraphicsPresetManager.SetSceneBlurVolumes(bool)` (called from ApplyUltra/Good/Toaster) disables every non-global scene Volume whose profile has an active DoF on Toaster and re-enables on Ultra/Good (name containing "PhotoBooth" is skipped so portraits keep their look). Toaster lighting was already minimal (`BackdropDepthLighting` switches the 60 backdrop lights off on Good/Toaster; 1 cascade, soft/additional-light shadows off). Still soft by design: FSR at render scale 0.77, FXAA, half-res textures (`globalTextureMipmapLimit=1`).

**Rats:** only come from saved placements of `PrefabSOs/Rat.asset` (ObjDataSO id 59). Removed the 9 from `Assets/_Saves/quicksave.json` (backup in %TEMP%). Re-running an old save that contains them brings them back.

**`DebugLogSwitch`** (`Scripts/1. FSM/Core/Managers/DebugLogSwitch.cs`): `Debug.unityLogger.filterLogType = Warning` at startup (info logs muted, warnings/errors shown). Tools > Logging > Show Debug.Log toggles; PlayerPrefs `ShowDebugLogs`. The ~294 Debug.Log calls were deliberately left in the code.

**Gun = waist slot, both hips:** `Neutral_Waist_Gun-ActionMan` is the two holsters+pistols from Polyperfect `woman-actionhero.fbx` (the "Action Man" is that single fused mesh; Tad had hand-cut the belt/necklace/shades/jeans, the gun was never cut). Right side of the character is -X in the blend (`Hip_R` x=-0.082). Fit = scale 1.774/1.83, rigid X shift (~8 mm) so nothing clips, weights copied (3-nearest, max 4) from Legs/Torso. **Do not use mesh normals for inside tests on this body:** Torso normals are unreliable and the mirror-applied jeans have flipped normals on one half; use radial distance from the leg axis (x=+/-0.09, y=-0.01). Gun and belt are variants of the same slot, so a worker gets one or the other (belt+guns together would need a code change).

**Heads:** `Female_Head_HOT` is now exported (weights limited to 4 + normalised on that head only; it has a Mirror modifier applied on export) and `Female_Head_Vanilla` = Tad's `Female_Head_Reg-OLD.blend` (the original Polyperfect head, closed-eye slits, no eyeballs). The old file's head was offset (0.30, 0.044, 0.20) from the body (armatures identical), so it was translated back by bbox centre; UVs copied from the nearest current-head polygons (skin/brows/lips cells), 30 weight groups kept (those existing as deform bones), AA_PP material, parented to `DeformationSystem`. Asset: weight **100**, same `allowedRoles` as Body/HOT, `hiddenBodySlots` empty. **Head weights now Body=0, HOT=1, Vanilla=100** -> Vanilla ~99% of new female staff, HOT ~1% (a `Body` part's weight is the "bare" share; with 0 nobody rolls the bare head). Adjust in the AOD.

**Two-machine reality (important):** Tad's Blender/Unity run on a different clone than the one Claude sessions here edit. A file saved there is invisible here until he commits+pushes (this happened with `Female_Head_Reg-OLD.blend`); work done here is invisible there until he Fetches+Pulls (he thought Vanilla "never made it" - the remote had it in blend, FBX (4,059,116 bytes) and prefab folder). When he says "I don't see it": `git ls-tree origin/<branch>`, check the committed FBX/blend contents, then give pull steps. Pulling here needed `git stash push -u` (local portrait PNG churn) - stash `pre-pull local state` still holds old portraits + verifiedInGame ticks; `mesh_dump.txt`/`hot3.png` were moved to %TEMP%\untracked_aside.

**Headless Blender (works with Blender closed):** `blender.exe -b file.blend --python script.py -- --save`. Preview-render first (Workbench engine, ortho camera, `ob.color`) and only then save + export (exec the `Unity_Avatar` preset with `import bpy` stripped + fake `active_operator`; export armature + every mesh parented to it). Back up blend+FBX to %TEMP% before each save. Build scripts used this session lived in %TEMP% (`build_gun2.py`, `build_vanilla.py`) - the approach is described above if they need redoing.

**Open / next:** set AOD weights for HOT/Vanilla/guns/new parts; Toaster FSR/FXAA/texture softness if Tad still wants it sharper; verify caps/headphones/hair-trim/guns on live staff; NavMesh "not close enough to the NavMesh" warning burst at load (staff + guard); rats still run their Update while paused; male body + other body types.


### Addendum 2026-10-07 — Safety vest, trims, hair-under-hats, the stale-FBX trap, Pimp-My-Employee vest tab

**Safety vest** (`Female_Vest_Safety-Blue / -Orange / -Yellow`, `vest` slot, mandatory, built from Polyperfect `woman-paramedic`, Tad later made 3 variants + more topology). Because meshes are skinned and clothing sits on the body, the **body under the vest is hidden at runtime** instead of weight-painting each outfit:
- `ModularAvatarAssembler.TrimBodyUnderVest` → `BuildTrimmedUnderShell`. **Current mode = `VestUseExactMask`:** Tad selected the faces to hide on `Female_Torso_CoverallsBlue` in Blender; their positions (height above vest bottom, |lateral|, depth) are in `VestHideMask.cs` (`DataExact`, 550 records; Blue/Gray/Brown share ONE topology so one list serves all). Each torso triangle is hidden iff its NEAREST record is a "hide" record. `VestUseHideMask`/rib/hip/pin rules are older fallbacks (kept, off).
- Regenerate after a topology change: read the selection from Blender, rebuild `DataExact`. `DataRef` = the very first hand selection (old Blue topology).
- Trim only runs while a vest is worn. Positions are matched in vest-relative coordinates, so they survive re-exports.
- **Pimp My Employee** now has a "Safety Vest" tab (`EditableOverrideKeys` + `AODPanel.EmployeeCategories`): swap vests or pick None.

**Hair under hats (final rules, Tad 2026-10-07):** hard hats hide NOTHING. Under a ball CAP only: Bobs **and Harley** hide the 14 crown faces (`BobCrownBoxes` + `BobCrownCentres`, 2 cm centre-proximity test); Mem hair hides nothing. All other general hair trimming (rim cut, ring, hat-surface test) was removed. `MemTopBoxes` is unused leftover data.

**⭐ THE STALE-FBX TRAP (cost most of a day):** a default Blender export `1. Blender/Female_Body_Reg-White.fbx` (same name as the .blend) sat next to the .blend. Finalize binds each part to the FIRST source it finds, and `1. Blender` sorts before `2. FBX`, so ALL finalized prefabs pointed at that stray file (Read/Write off → every runtime trim saw "isReadable false" and silently did nothing; Blender edits seemed to update only the AOD thumbnail). Fix: delete the stray; export ONLY to `2. FBX/female_regular_body.fbx` with the `Unity_Avatar` preset. Check with: every prefab's `AssetDatabase.GetAssetPath(sharedMesh)` ends with `female_regular_body.fbx`.
- **Play Mode runs with Domain Reload OFF** (EnterPlayModeOptions 3): static caches survive Play sessions. `ModularAvatarAssembler.ClearMeshCaches()` (RuntimeInitializeOnLoadMethod SubsystemRegistration + `RefreshAllModularAvatars`) clears the rebound/hair/torso caches.
- **Scripts do not recompile while in Play Mode** — several "no change" reports were just old code. Stop Play, wait, re-Play.
- `Object.GetInstanceID()` is an error in this Unity (use name hash / EntityId).

**Face/accessory fits done in Blender (all exported):** gag strap + shades re-fit to the Vanilla head, strap weights Data-Transferred from the head, belt pulled onto the body and custom normals cleared, Mem hair re-weighted for head turns. Armature object was rebuilt once (old object would not draw bones in Object Mode).

**Blender-session hazards:** my scripted exports/saves can unhide/select everything in Tad's live session (restore state); that Blender needs the `Unity_Avatar` preset installed to export.

**Open:** Gray/Brown hem spikes at the back; hip hole between coverall hem and jeans is real geometry (open edges), not the trim; Black Mem mesh still old; commit hygiene — Portraits/BOXOPHOBIC .meta churn is local noise.


### Addendum 2026-10-08 — Animation unification + Male Regular body category
- **MHE riders no longer swap animator controllers.** `MHEOperatorSlot` sets the int param `DriveStyle` (0 foot, 1 sit-drive DS/RT, 2 pallet jack) on the rider's Animators; `MaleStaff` and `FemaleStaff` controllers hold the `Drive_Sit` / `Drive_PalletJack` states (added by `Tools/Add Drive States To Staff Controllers`; re-run it after `Tools/Setup MaleStaff Animator`, which clears Any State transitions). DockStocker/PalletJack controllers are now unused markers (name decides style).
- **Finalizer outputs next to the source FBX** (`<Gender>/<Body>/2. FBX/x.fbx` -> `<Gender>/<Body>/3. Prefab (post AOD Submit)/`); the library loads finalized parts from every such folder (`ModularAvatarFinalizer.AllFinalizedFolders`). Male lives in `__Avatar_System2.0/Male/{1. Blender,2. FBX,3. Prefab}` (no Regular_Body_Type level yet). 14 male parts finalized (torso Coveralls x4, head, hands, feet x4, hair x4). **Male modular avatars ENABLED 2026-10-08** (female-only gate removed in `EmployeeSpawner.ModularBodyExists`). Nude `Male_{Torso,Legs,Head,Hands,Feet}_Body` defaults exist. Female-body-fitted clothing (vests, gloves, jeans, belt, gun, necklace) was retagged gender `female` in its AvatarPartAsset so males don't wear it; hats/shades/headphones stay `neutral`, and `TryRebindToBody` now falls back to the nearest ancestor bone (EyelidUpper_R -> Head_M) when the male rig lacks a bone. Verified 160 male builds: no missing body parts, all headgear on the main skeleton. Not yet seen in Play Mode.
- Editor-script gotcha: a C# compile error leaves the OLD assembly running silently; check `Logs/Editor.log` (project-relative, not %LOCALAPPDATA%) for `error CS` when a "new" method is missing.
- **Male FBX must be exported with the `Unity_Avatar` preset** (FBX UnitScaleFactor 100 + useFileScale). A first male export without it imported with every node at scale 100 (avatar ~100 m tall). Headless re-export that fixed it: load the .blend with `blender.exe -b`, select armature + all meshes parented to it, exec the preset with a fake `active_operator`, export to `Male/2. FBX/Male_Regular_Body.fbx`, then Scan + Finalize All. Male AOD tuning: `Male_Torso_Body` and `Male_Feet_Body` weight 0 (never bare), male coveralls hide `torso`+`legs` like the female ones. Verified live: hired male Receiver walks with clipboard + scan gun, hardhat/hair render correctly.


### Addendum 2026-10-09 — AOD header/filter-bar redesign, employee-edit rework, preview + scene grading
All in `AODPanel.cs` (UI is built in C# with inline styles, no UXML/USS), `AODPreviewStage.cs`, `ModularAvatarAssembler.cs` and the 4 post-processing profiles in `Assets/Settings`.
- **Header/filter bar:** `HeaderScale = 1.5` (title bar, row labels, chips). `Chip(label, header, sizeMul, tint, caps)`; header chips use Lilita One (the Nunito file is a variable font with no real bold) with INVERTED colors (light fill, dark text). GENDER chips: Male light-blue, Female pink, Neutral light-green. ROLE chips: 0.85x size, caps, two rows (row 2 = Supervisor/Boss/Security/Inventory Control/Exterminator/HR/Admin). SLOT chips take `ColorForSlot` lightened 25% toward white. Labels GENDER/ROLE/SLOT/SHOW share `HeaderLabelSize`. Missing Data Only + Rescan Folder + **Finalize All** (runs `ModularAvatarImporter.FinalizeAllPendingMenu` by reflection via `RunImporter`) sit on the GENDER row, right end; Rescan/Finalize are Chips in `aod-action-col`. Dirty Dev and min/max/close were deliberately left at original size/colors.
- **Per-slot colors** (`ColorForSlot`, also tile backgrounds): face magenta, glasses violet, feet teal, hands lime, head cyan, legs brown, neck yellow (+ existing hair/hat/vest/torso). Selected tile border is 2x thick (4*CardScale).
- **Slot row is single-select in BOTH modes;** clicking the picked slot un-selects it = show every slot.
- **Employee ("Pimp My Employee") mode rewritten:** category tab row DELETED. Filter rows drive it: gender = employee's gender + Neutral (other grayed, locked), role = employee's role only (others grayed, locked), Missing-Only locked. `_employeeSlot` null = all editable slots. `ApplyEmployeeFilterState` sets this up, `ResetFiltersAfterEmployee` + `Hide()` fully reset (identity, overrides, slot, selected part, right panel). Options come from `VariantsFor(gender, slot)` filtered by `AllowsRole` (worn part always kept). `EmployeeCategoryKeyFor/KeysForSlot` map a part to its override key; picking one hat part clears the other hat categories (hardhat/cap/headphones share the `hat` slot). `"head"` was added to `EditableOverrideKeys` so heads are swappable.
- **Bare-body = NONE tile:** in feet/hands/head/legs/torso the `Body` part (female legs: `Reg`) is the none tile in BOTH modes and BOTH genders - `IsBareBodyPart`, `CardDisplayName` prints NONE over its real thumbnail; it sorts first and is "selected" when the slot is empty. Slots without a bare part (hat, glasses, vest...) get a plain None card in single-slot view; the vest also gets one in the all-slots view.
- **Garments that hide slots:** a worn part's `HiddenBodySlots` (coveralls hide torso+legs) switches off anything else in those slots, so swapping legs under coveralls looked like "nothing happens". `RemoveGarmentsCovering` now takes the covering garment off (back to bare) and toasts. Rotation by drag now works in employee mode (preview is the whole avatar).
- **Preview stage:** full-avatar camera 1.5x closer (`FrameOnBounds(bounds, zoom)`), lights key 1.4 / fill 0.7, plus a LOCAL URP Volume (box collider at the stage, layer 31, camera `volumeLayerMask` = that layer) with ColorAdjustments saturation -30 (`PreviewSaturation`) so scene volumes/bloom do not leak in.
- **Game look:** all four profiles (`PP_Ultra/Good/Toaster`, `GlobalPostProcessProfile`) now have ColorAdjustments saturation -32 and postExposure lowered 0.15 (0.25/0.30/0.30/0.35). Original saturation was +6/+3/+6/+5.
- **Gotchas:** AODPanel.cs is CRLF - a Python text-mode rewrite silently converts to LF (restore CRLF afterwards, e.g. with sed appending a carriage return to each line). Static reading of "it does nothing" bugs can mislead: reproduce the assembler through `Unity_RunCommand` (no `System.Reflection` allowed there) before blaming the UI.
- **Open:** male/female "Body" naming differs (male all `Body`, female legs `Reg`); normal library view still lists every part; Hands/Head bare parts rely on the `Body`/`Reg` naming convention.

---


