# Modular Avatar — Session Handoff (2026-06-17)

> Read this first when resuming on another machine. It captures the full state of the modular-avatar
> animation work so we can continue without re-deriving everything. (Claude's local memory files do
> NOT sync across machines — this doc is the bridge.)

## Goal
Get the **male modular avatars** (assembled from `Modular_Staff.fbx` parts) to **walk** with the
worker AI, using the worker's existing `MaleStaff` humanoid animation controller. Women are next,
after the men work.

## Where we are
- ✅ **Rig/import fixed earlier:** weights were always clean (the "off-target leg" was an illusion —
  the Blender armature was in *Rest Position* display). Legs/feet bind correctly.
- ✅ **Animation pipeline works:** the assembled avatar gets its own `Animator`
  (avatar `Modular_StaffAvatar`, controller `MaleStaff`); `ModularAvatarRig` mirrors the worker
  Animator's params onto it. Confirmed via Editor.log: leg bones swing through a real walk cycle
  (e.g. `LowerLeg.R.localEuler` goes from rest to ~-50°).
- ❌ **BLOCKER — the mesh explodes when animated.** SkinnedMeshRenderer bounds blow up to
  ~61×98×50 units centered ~95 units in the air (vertices flung skyward = Tad's "flying pieces /
  alien shapes near the camera"). Renderer is enabled, mesh present — the *skinning* detonates.

## Root-cause analysis (important — don't repeat dead ends)
- The modular rig (`ArmatureMaleWorker`) is a **clean, consistent T-pose rig**. Compared to
  `WorkerNew`'s rig (imported into the blend for comparison, then removed): **legs, spine, chest,
  head, shoulders are IDENTICAL** (0° / same positions). The **only** difference is the **arms**:
  modular = T-pose (horizontal), WorkerNew = A-pose (~81° down).
- Because the modular rig is clean, the explosion is most likely a **Unity humanoid avatar / setup
  issue, not a rig defect.** (But verify with the isolation test below before committing to a fix.)
- Bone positions confirm different per-bone orientation between the two FBX exports, e.g. Hips local
  pos worker=(0,0.761,0) vs modular=(0,0,0.761) — so **bone-sharing onto the worker skeleton does
  NOT work** (bind-pose mismatch → skinning spikes).

### Things tried that DON'T work (skip these)
- **CopyFromOther → WorkerNewAvatar:** no explosion, but **no motion** (worker's rest pose ≠ modular
  rest pose → retarget produces near-rest output / T-pose).
- **Bone-remap modular meshes onto the worker skeleton by name:** spikes from per-bone rotation
  mismatch; also the modular neck chain `Bone_end_end`/`Bone_end_end_end` has no worker twin.
- **De-nesting the avatar (parent to scene root + follow):** nesting was never the cause; produced
  invisible / flying-pieces with the wrong avatar. Reverted to nested.
- **`updateWhenOffscreen = true`:** only made the *exploded* mesh visible (not a fix).

## NEXT STEP — the decisive isolation test (do this first)
In Unity:
1. Drag `Assets/5. Models/BlenderFiles/Modular_Staff/Modular_Staff.fbx` straight into the scene
   (raw model, no spawner).
2. Set its `Animator` **Controller** to **MaleStaff** (avatar is already assigned).
3. Press **Play**.

- **Raw model explodes** → it's the **FBX/avatar config** → fix in import settings (maybe delete the
  junk `Bone`/`Bone_end*` + `ikPole/ikTarget` bones in Blender for a clean humanoid skeleton, or
  re-do the humanoid avatar T-pose). The rig itself likely never needs re-skinning.
- **Raw model walks fine** → it's the **runtime assembler/spawner code** (`EmployeeSpawner.ApplyModularAvatar`
  or `ModularAvatarAssembler`) doing something wrong when it rebuilds the avatar.

(Tad's standing instinct: if needed, re-skin the parts onto the actual `WorkerNew` rig. That's the
fallback — but it requires reconciling the T-pose mesh with WorkerNew's A-pose arms, i.e. re-posing
the mesh, so only go there if the isolation test points at the avatar AND import-side fixes fail.)

## Key files
- `Assets/1. Scripts/7. EmployeeSystem/EmployeeSpawner.cs` → `ApplyModularAvatar` (nested approach:
  hides worker mesh, parents assembled avatar under the worker, gives it its own Animator using the
  FBX's own `Modular_StaffAvatar`, `updateWhenOffscreen=true`, adds `ModularAvatarRig`). Has a
  `[ModularAvatar] nested ...` Debug.Log.
- `Assets/1. Scripts/8. ModularAvatar/ModularAvatarRig.cs` → mirrors worker Animator params each
  LateUpdate; one-shot `[ModularRig] diag ...` log reporting leg rotation + SMR bounds/visibility.
- `Assets/1. Scripts/8. ModularAvatar/ModularAvatarAssembler.cs` → builds the avatar (instantiate
  body FBX, prune to chosen parts, reparent parts from other sources).
- `Assets/5. Models/BlenderFiles/Modular_Staff/Modular_Staff.fbx.meta` → currently
  `avatarSetup: 1` (CreateFromThisModel), humanoid map fixed to `Head→Head`, Neck mapping removed.
- `Assets/10. Editor/ModularAvatarVerify.cs` → throwaway menu `Tools ▸ Modular Avatar ▸ Verify Rig
  (write report)` (writes `<projectroot>/modular_avatar_verify.txt`). Delete when done.
- `Assets/5. Models/WorkerNew.fbx` → the worker's source model + `WorkerNewAvatar` (the clean
  reference rig). Controller on prefab: `b6bbbe3e31fbd2d4e802bb9ff509c69a`; SMR Animator base:
  `115ee44c755217f40a3b5f313f810a44` (MaleStaff).

## Environment gotchas (this machine — may differ on the other one)
- The Unity MCP bridge (`advanced-unity-mcp` / Code Maestro relay) **drops on every recompile/domain
  reload** and only a **full Unity restart** reliably revives it. Launch script:
  `C:\Users\tadsa\AppData\Local\Programs\CodeMaestro\UnityMcpRelay\launch.bat` (but it's the MCP
  host that manages it; a Unity restart is the real fix). Intermittent Unity licensing-404 errors
  were also seen.
- Because the bridge is flaky, diagnostics were read from **`Editor.log`** via PowerShell
  (`Get-Content $log -Tail N | Select-String ...`). `[ModularAvatar]` / `[ModularRig]` are the log
  tags. Debug.Log → Editor.log works even when the bridge is down.
- Toggle on `EmployeeSpawner`: `_useModularAvatars` (default ON). OFF = original animated worker.

## To resume on the other machine
Open Claude Code in this repo and say: *"Read MODULAR_AVATAR_HANDOFF.md and let's continue — start
with the isolation test."*
