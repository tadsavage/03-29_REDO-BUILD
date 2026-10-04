using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
// touch: force reimport 2026-09-30 (5)

/// <summary>
/// Scans the modular-avatar drop folder for FBX/model files and rebuilds AvatarPartLibrary from
/// their child meshes, categorising each by the <c>gender_slot_variant</c> naming convention.
///
/// Workflow for the artist: drop (or re-export over) an FBX in the drop folder. The library
/// auto-rebuilds (see the AssetPostprocessor below), or run Tools ▸ Modular Avatar ▸ Scan &amp;
/// Rebuild Library manually. A scan is a FULL rebuild, so it naturally handles added, changed,
/// and removed parts in one pass.
/// </summary>
public static class ModularAvatarImporter
{
    // 2026-09-27: renamed/reorganized (uncommitted, live in the Project window) from the old
    // "Modular_Staff" flat drop folder to this one with named subfolders (BODY_MODELS, PROPS_MODELS,
    // Z-AOD_WORKSHOP). FindAssets is recursive, so pointing DropFolder here scans all of them —
    // Z-AOD_WORKSHOP is explicitly excluded below (it holds the master Blender workspace file, not
    // individual parts).
    // 2026-10-02 (Avatar 2.0): the ONLY scan root. Old Modular_Staff_Models and _Avatar_System are
    // deliberately no longer read, so stale parts can never leak into the AOD.
    public const string DropFolder  = "Assets/_Project/__Avatar_System2.0";

    // 2026-09-30: the new per-body-type pipeline (see the ModularAvatarSystem skill doc) exports
    // FBX files under _Avatar_System/<Gender>/Bodies/<Type>/03_FBX/ instead of the old flat
    // BODY_MODELS drop folder. Scanned as a SECOND root alongside DropFolder — additive, not a
    // replacement, since PROPS_MODELS (hair/hats/gloves) still lives under the old DropFolder and
    // isn't part of this migration yet.
    public const string NewPipelineRoot = DropFolder;   // same root; kept so existing references compile

    // Finished, submitted parts no longer live in a second SCAN root — see ModularAvatarFinalizer.
    // A submitted part becomes a standalone AvatarPartAsset (loaded below, into lib.finalizedParts)
    // plus a real prefab under ModularAvatarFinalizer.BodyPrefabFolder/PropsPrefabFolder. Those
    // prefab folders are a write target for the finalizer, never a scan root for this importer.

    public const string LibraryPath = "Assets/_Project/Resources/Resource_AvatarSystemAssets/AvatarPartLibrary.asset";

    [MenuItem("Tools/Modular Avatar/Scan & Rebuild Library")]
    public static void ScanAndRebuildMenu() => ScanAndRebuild(verbose: true);

    /// <summary>Headless equivalent of clicking "Update" on every already-finalized row and
    /// "Submit" on every raw/unreviewed row in the AOD panel, in one pass (2026-09-30). Added so
    /// this can be driven from a menu command / automation instead of requiring interactive clicks
    /// in the AODPanel EditorWindow. Reuses ModularAvatarFinalizer's exact TryUpdateFromRawSource /
    /// TryFinalize methods — the same validate-then-write logic the UI button calls — so results are
    /// identical to doing it by hand, just batched.</summary>
    [MenuItem("Tools/Modular Avatar/Finalize All Pending")]
    public static void FinalizeAllPendingMenu()
    {
        var lib = ScanAndRebuild(verbose: false);
        if (lib == null) { Debug.LogWarning("[ModularAvatar] Finalize All Pending: no library/drop folder."); return; }

        int updated = 0, updateFailed = 0, submitted = 0, submitFailed = 0;

        foreach (var asset in lib.finalizedParts.ToList())
        {
            if (asset == null) continue;
            if (ModularAvatarFinalizer.TryUpdateFromRawSource(lib, asset, out string err))
                updated++;
            else
            {
                updateFailed++;
                Debug.LogWarning($"[ModularAvatar] Update failed for '{asset.ObjectName}': {err}");
            }
        }

        foreach (var part in lib.parts.ToList())
        {
            if (ModularAvatarFinalizer.TryFinalize(lib, part, out string err, out _))
                submitted++;
            else
            {
                submitFailed++;
                Debug.LogWarning($"[ModularAvatar] Submit failed for '{part.objectName}': {err}");
            }
        }

        // Rescan so parts/finalizedParts reflect the new state immediately (a finalized part no
        // longer shows up as raw, an updated asset's identity fields are current).
        ScanAndRebuild(verbose: false);

        Debug.Log($"[ModularAvatar] Finalize All Pending: {updated} updated ({updateFailed} failed), " +
                  $"{submitted} submitted ({submitFailed} failed).");
    }

    public static AvatarPartLibrary ScanAndRebuild(bool verbose)
    {
        var scanRoots = new[] { DropFolder }.Where(AssetDatabase.IsValidFolder).ToArray();
        if (scanRoots.Length == 0)
        {
            if (verbose)
                Debug.LogWarning($"[ModularAvatar] Neither drop folder exists ({DropFolder}, {NewPipelineRoot}). " +
                                 "Create one and drop your modular FBX(s) in there.");
            return null;
        }

        var lib = LoadOrCreateLibrary();
        lib.sources.Clear();

        // Load every already-finalized part FIRST — these are the permanent source of truth and are
        // never touched by this scan (see ModularAvatarFinalizer). Raw candidates that already match
        // a finalized objectName are skipped below rather than re-added as unreviewed duplicates.
        lib.finalizedParts = AssetDatabase.FindAssets("t:AvatarPartAsset", new[] { ModularAvatarFinalizer.FinalizedAssetFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<AvatarPartAsset>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null)
            .ToList();
        var finalizedNames = lib.finalizedParts.Select(a => a.ObjectName.ToLowerInvariant()).ToHashSet();

        // AOD metadata (allowedRoles/colorVariants/defaultWeight) is entered by hand through the
        // Avatar Object Database UI, not by this scan — a rescan must NOT wipe it. Snapshotted here
        // by objectName (the one stable identity a part has across rescans) and re-applied to each
        // freshly-parsed Part below, before the old list is discarded. Only ever applies to RAW parts
        // now — a finalized AvatarPartAsset's metadata lives on its own file, untouched by this scan.
        var oldMetadataByName = lib.parts
            .GroupBy(p => p.objectName.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());
        lib.parts.Clear();

        // DropFolder and NewPipelineRoot are scanned — a finished/submitted part is a real
        // AvatarPartAsset (loaded above), not a second scan root. t:Model matches raw FBX/OBJ exports.
        var guids = AssetDatabase.FindAssets("t:Model", scanRoots).Distinct();
        int fbxCount = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Skip Blender source files (.blend, .blend1) — only FBX/OBJ exports should be sources.
            // Having both the .blend and the .fbx in the folder causes duplicate sourceIndex entries
            // which breaks SkinnedMeshRenderer bone references during cross-source merging.
            string pathLower = path.ToLower();
            if (pathLower.EndsWith(".blend") || pathLower.EndsWith(".blend1")) continue;

            // Z-AOD_WORKSHOP holds the master Blender workspace file (workspace.blend) and a bulk
            // multi-costume reference-pool FBX artists pull individual pieces FROM — never a source
            // of individual parts itself. Explicit path check (not a filename substring) so it can't
            // silently stop working if something in there someday lacks "workshop" in its own name.
            if (path.Replace('\\', '/').Contains("/Z-AOD_WORKSHOP/", System.StringComparison.OrdinalIgnoreCase))
                continue;

            // 01_StoreBought holds raw, pre-Blender-editing reference material under the new
            // per-body-type pipeline (see the ModularAvatarSystem skill doc) — never a real part
            // source. Without this, "woman_construction_worker_Rig.fbx" (the untouched store-bought
            // base) got scanned as its own competing "body" candidate alongside the actually-edited
            // "woman.bodyA" from 03_FBX, reintroducing the exact two-bodies bug this pipeline exists
            // to prevent.
            if (path.Replace('\\', '/').Contains("/01_StoreBought/", System.StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip loose "*Workshop*" staging files sitting at the drop folder's own root (e.g.
            // Gender_Neutral_Workshop.fbx) — these are Blender reference/pose-testing exports, not
            // real avatar sources: no skeleton, and they duplicate a mesh (e.g. man_construction_
            // worker) that already lives correctly in MEN/man_body_main.fbx. Scanning them in creates
            // a second "body" candidate with no armature, which can get randomly picked as the
            // PRIMARY source and leave the avatar with no working skeleton at all.
            if (Path.GetFileNameWithoutExtension(path).ToLower().Contains("workshop")) continue;

            // Skip bulk reference-pool files (renamed 3 times already — "_AllAvatars.fbx" →
            // "Avatar_Pool.fbx" → "Avatars_All_Workspace.fbx" — so a name check keeps breaking).
            // These are multi-costume bundles Tad pulls individual pieces FROM by hand in Blender
            // (re-exporting each piece as its own gender_slot_variant file), not real modular parts
            // themselves. Detected by shape instead of name: a real part file has a handful of
            // meshes; a bulk pool has dozens of full-costume SkinnedMeshRenderers bundled together.
            // Costume names like "man_actionhero" mostly fail the gender_slot_variant pattern and
            // get skipped anyway, but several ("man_casual_shorts", "woman_naval_officer", ...)
            // happen to have 3+ underscore segments and get misfiled as bogus slot/variant parts.
            const int BulkPoolMeshThreshold = 20;
            var probeForBulkCheck = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (probeForBulkCheck != null &&
                probeForBulkCheck.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > BulkPoolMeshThreshold)
            {
                if (verbose)
                    Debug.Log($"[ModularAvatar] Skipping '{Path.GetFileName(path)}' — looks like a bulk " +
                              "reference pool (20+ skinned meshes), not a single modular part.");
                continue;
            }

            // XXX_Obsolete_Humanoids is a DIFFERENT ART STYLE entirely, not a fallback part source —
            // per Tad, do not pull ANYTHING from it (previously this only stripped its "body" slot
            // and let everything else through, which was wrong: the whole folder is off-limits).
            if (path.Replace('\\', '/').Contains("/Obsolete_Humanoids/", System.StringComparison.OrdinalIgnoreCase)
                || path.Replace('\\', '/').Contains("/XXX_Obsolete_Humanoids/", System.StringComparison.OrdinalIgnoreCase))
                continue;

            // New drop-folder exports (MEN/WOMEN/_GENDER_NEUTRAL) come out of Blender missing two
            // things every OLDER modular part already had: a Humanoid rig (so the Animator can
            // retarget the worker's walk/idle clips onto it — without it the character sits frozen
            // in bind pose, i.e. a permanent T-pose) and a real textured material (Blender's own
            // material names don't match any project asset, so Unity generates a blank untextured
            // one instead of finding the shared palette material everything else uses). Fixed here,
            // at scan time, so a fresh export just works without a manual Inspector pass — same
            // self-healing spirit as the rest of this importer. Must run BEFORE loading `root` below:
            // it can trigger a reimport, which would otherwise leave `root` pointing at a stale prefab.
            // (Obsolete_Humanoids files never reach this line — skipped above — so no legacy check needed.)
            FixNewExport(path);

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;

            int sourceIndex = lib.sources.Count;
            lib.sources.Add(new AvatarPartLibrary.SourceModel { fbxPath = path, prefab = root });
            fbxCount++;

            // Includes the root transform itself: a simple prop (hair, hat, headphones) is often
            // exported as a single mesh with no children at all, so the mesh sits ON the root —
            // skipping it (as this loop used to) meant those props never made it into the library
            // no matter how they were named.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                bool hasMesh = t.GetComponent<MeshFilter>() != null || t.GetComponent<SkinnedMeshRenderer>() != null;
                if (!hasMesh) continue;   // skip the armature, bones, empties

                var part = ParseName(t.name, sourceIndex);
                if (part == null)
                {
                    if (verbose)
                        Debug.LogWarning($"[ModularAvatar] '{t.name}' in {Path.GetFileName(path)} " +
                                         "doesn't match gender_slot_variant — skipped.");
                    continue;
                }

                // Already finalized under this exact objectName — don't re-add it as an unreviewed
                // raw duplicate. The finalized AvatarPartAsset (loaded above) is authoritative.
                if (finalizedNames.Contains(part.objectName.ToLowerInvariant()))
                    continue;

                if (oldMetadataByName.TryGetValue(part.objectName.ToLowerInvariant(), out var oldPart))
                {
                    part.allowedRoles  = oldPart.allowedRoles;
                    part.colorVariants = oldPart.colorVariants;
                    part.defaultWeight = oldPart.defaultWeight;
                    part.verifiedInGame = oldPart.verifiedInGame;
                }

                lib.parts.Add(part);
            }
        }

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();

        // Announce newly-discovered parts — this is how Tad finds out a Blender re-export actually
        // picked up his new work, without having to manually diff the library himself. Compares
        // against the SAME oldMetadataByName snapshot the merge above used, so "new" here means
        // "wasn't in the library before this scan", not "changed since last scan".
        var newNames = lib.parts.Select(p => p.objectName.ToLowerInvariant()).ToHashSet();
        var addedNames = newNames.Except(oldMetadataByName.Keys).ToList();
        if (addedNames.Count > 0)
        {
            var addedParts = lib.parts.Where(p => addedNames.Contains(p.objectName.ToLowerInvariant())).ToList();
            string names = string.Join(", ", addedParts.Select(p => p.objectName));
            Debug.Log($"<color=#3B82F6><b>✨ AOD: {addedParts.Count} new object(s) added</b></color> — {names}. " +
                      "Open the AOD to assign roles/gender/slot metadata — until reviewed these are " +
                      "flagged as missing data, and the next hire or two will be biased to use them " +
                      "so you can confirm they look right in-game.");
        }

        if (verbose)
        {
            var bySlot = lib.parts.GroupBy(p => $"{p.gender}/{p.slot}")
                                  .OrderBy(g => g.Key)
                                  .Select(g => $"{g.Key} ({g.Count()})");
            Debug.Log($"[ModularAvatar] Rebuilt library: {fbxCount} FBX, {lib.parts.Count} parts.\n" +
                      string.Join("\n", bySlot));
        }
        return lib;
    }

    // The new MEN/WOMEN/_GENDER_NEUTRAL exports were built against PolyPerfect's Low Poly Animated
    // People rig/toolkit (their bone naming — Root_M, Hip_L, Spine1_M, Head_M, DeformationSystem,
    // etc. — matches that pack exactly), so their UVs sample PolyPerfect's OWN atlas texture, not
    // the Imphenzia palette (AA_LowPolyCommon) the older obsolete-file parts use. Per Tad: the
    // correct material is the SOURCE atlas specifically (atlas-source-LPAP.png), not the pack's own
    // pre-baked atlas-LPAP.mat (which points at atlas-albedo-LPAP.png, a derived/processed variant)
    // — no material asset for the source texture existed yet, so one was created alongside it
    // (Assets/polyperfect/Common/Materials/atlas-source-LPAP.mat, cloned from atlas-LPAP.mat's
    // shader/settings with only the texture swapped).
    // AVATAR 2.0 (2026-10-02): switched to the FLAT atlas material (AA_atlas-LPAP.mat -> atlas-albedo-LPAP.png). The
    // "source" atlas carries a shading gradient inside every color cell, so a flat skin patch rendered as ~17 different
    // shades across neighbouring polygons (measured: albedo = 1 color, source = 17). Blender's own material
    // (AA_PP_ALBEDO) already uses the albedo atlas, so this also makes Unity match what Tad sees in Blender.
    // The material + its 3 textures are COPIES kept inside the Avatar 2.0 root (Textures/) so the avatar system no longer
    // depends on the polyperfect pack's folder. To go back to the gradient look, point this at
    // Assets/polyperfect/Common/Materials/atlas-source-LPAP.mat.
    private const string SharedPaletteMaterialPath = "Assets/_Project/__Avatar_System2.0/Textures/AA_atlas-LPAP.mat";

    // See the duplicate-name guard at the top of FixNewExport — tracks paths already warned about
    // this domain session so a genuinely broken export logs once, not on every scan/heartbeat.
    private static readonly HashSet<string> _loggedAmbiguousArmature = new();

    /// <summary>
    /// Repairs the two things a fresh Blender export from the new MEN/WOMEN/_GENDER_NEUTRAL
    /// pipeline is missing relative to the older, working modular parts — see the call site comment
    /// for why. Idempotent (only reimports if something actually needed changing), so re-running a
    /// scan on an already-fixed file is a no-op.
    /// </summary>
    private static void FixNewExport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;

        var probe = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (probe == null) return;

        // Leading-underscore files (e.g. _ClipBoard.fbx, _Scangun) are Tad's own convention for
        // one-off HELD PROPS with their own hand-authored materials, not gender_slot_variant modular
        // body/hair/hat parts that share the PolyPerfect body atlas — ParseName already refuses to
        // catalogue them as parts for exactly this reason. Forcing their materials onto
        // SharedPaletteMaterialPath below fights the real per-prop material Tad made in Blender
        // (confirmed: this is what was reverting a manual remap on _ClipBoard to atlas-source-LPAP
        // on every reimport/rescan). Skip the whole repair pass for them — no skeleton either, so
        // section 1 would no-op anyway.
        if (Path.GetFileNameWithoutExtension(path).StartsWith("_"))
            return;

        // Duplicate object names anywhere in the hierarchy (most commonly a leftover second
        // armature — "DeformationSystem" + "DeformationSystem.001" — from a Blender export that
        // still had a stray copy of the rig in the scene) make Unity's Humanoid auto-mapper throw
        // "Ambiguous Transform ... found in hierarchy for human bone 'Hips'" and fail Avatar
        // creation. Detected BEFORE touching the importer settings and bailed out early: the
        // "stale humanoid" repair below (section 1) treats "Avatar didn't come out valid" as "my
        // cached bone map is stale, clear it and reimport" — which is the right response to a
        // genuine rename, but here the Avatar can NEVER become valid no matter how many times we
        // reimport (the ambiguity is a property of the content, not the importer's cached state),
        // so without this guard the two would loop forever: reimport → still ambiguous → still no
        // valid avatar → "must be stale" → reimport again. Logged once per path per domain session
        // (not every scan) so a genuinely un-fixed file doesn't spam every heartbeat.
        var dupeNames = probe.GetComponentsInChildren<Transform>(true)
            .GroupBy(t => t.name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (dupeNames.Count > 0)
        {
            if (_loggedAmbiguousArmature.Add(path))
                Debug.LogError($"[ModularAvatar] '{Path.GetFileName(path)}' has duplicate object name(s) " +
                    $"[{string.Join(", ", dupeNames)}] — almost certainly a leftover duplicate armature from " +
                    "the Blender export. Unity can't build a Humanoid Avatar from this until the duplicate " +
                    "is deleted and the file is re-exported. Skipping the Humanoid/material repair pass for " +
                    "this file so it doesn't spam reimport attempts.");
            return;
        }

        bool changed = false;

        // 1. Humanoid rig — only for the file that actually carries the "body" slot mesh (the
        // primary torso source that becomes Build()'s `root`/Animator target). Per-part body
        // exports (2026-09-30 workflow: female_bodyA_arms.fbx, _hands.fbx, _head.fbx, each with
        // only the bone chain Blender needed to weight THAT mesh) also carry a real skeleton, but
        // only a PARTIAL one — missing bones like Head/LeftUpperLeg/LeftLowerLeg that Humanoid
        // validation requires unconditionally. A partial skeleton can NEVER pass Humanoid
        // validation no matter how many times it's reimported, so forcing Human on every skinned
        // file (the old rule) hit the exact infinite loop the staleHumanoid repair below warns
        // about for duplicate-armature files: reimport -> still invalid -> "must be stale cache" ->
        // clear + reimport -> forever, flooding the console with "Required human bone 'X' not
        // found" (found live, 2026-09-30, right after Tad started exporting per-part body files).
        // Every OTHER chosen part (arms/hands/head/clothing/etc) is merged into the body's root by
        // NAME-matching bones at runtime (see ModularAvatarAssembler.Build's remappedBones loop) —
        // that mechanism never looks at the source file's own Animator/Avatar at all, so those
        // files have no need for a valid Humanoid rig of their own; Generic is correct for them.
        bool isBodySource = probe.GetComponentsInChildren<Transform>(true)
            .Any(t => (t.GetComponent<MeshFilter>() != null || t.GetComponent<SkinnedMeshRenderer>() != null)
                      && IsBodySlotName(ParseName(t.name, 0)?.slot));

        bool hasSkeleton = probe.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Any(s => s.bones != null && s.bones.Length > 0);

        // One-time cleanup: a per-part file that got incorrectly flipped to Human by the OLD rule
        // (before this fix) before its .meta's cached setting is corrected, Unity will keep
        // attempting — and failing — Humanoid validation on every future reimport forever, even
        // though FixNewExport itself no longer loops. Reset it back to Generic so the import is
        // actually clean, not just non-looping.
        if (hasSkeleton && !isBodySource && importer.animationType == ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }

        if (hasSkeleton && isBodySource)
        {
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            // Catches a STALE humanoid mapping left over from a PREVIOUS export of this same file —
            // confirmed to actually happen: re-exporting from Blender renamed several ancestor
            // objects (e.g. the old "DeformationSystem"/"Main" became "DeformationSystem.001"/
            // "Man_Main"), but Unity keeps reusing the OLD bone map baked into the importer's .meta
            // instead of re-running its auto-mapper, since the block above only fires on the very
            // FIRST Generic→Human flip. The resulting Avatar silently fails validation
            // (Animator.avatar.isHuman == false) even though animationType/avatarSetup both still
            // read "Human" — no console error, the only symptom is a frozen bind-pose T-pose in
            // game. Clearing the cached human/skeleton bone arrays and reimporting forces Unity to
            // re-run the same auto-mapper that built the mapping correctly the first time, now
            // against the CURRENT hierarchy.
            // NOTE: intentionally NOT `probe.GetComponent<Animator>()?.avatar` — the `?.` null-
            // conditional operator does a raw CLR reference check, bypassing UnityEngine.Object's
            // overloaded `==`. A "fake-null" Animator (component reference exists, native side does
            // not — seen here on freshly-loaded FBX assets mid Humanoid setup) slips past `?.` and
            // throws MissingComponentException the moment `.avatar` is touched. A plain `if (x !=
            // null)` uses the real overloaded check and catches this case correctly.
            var probeAnimator = probe.GetComponent<Animator>();
            var currentAvatar = (probeAnimator != null) ? probeAnimator.avatar : null;
            bool staleHumanoid = importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel &&
                                 (currentAvatar == null || !currentAvatar.isHuman);
            if (staleHumanoid)
            {
                importer.humanDescription = new HumanDescription
                {
                    human = new HumanBone[0],
                    skeleton = new SkeletonBone[0],
                    upperArmTwist = 0.5f,
                    lowerArmTwist = 0.5f,
                    upperLegTwist = 0.5f,
                    lowerLegTwist = 0.5f,
                    armStretch = 0.05f,
                    legStretch = 0.05f,
                    feetSpacing = 0f,
                    hasTranslationDoF = false,
                };
                changed = true;
            }
        }

        // 2. Shared palette material — Blender's own material name (lambert1, newManPoly, ...)
        // doesn't match any project asset, so the "BasedOnTextureName" search above comes up empty
        // and Unity generates a blank, untextured material INSIDE the FBX instead of finding the
        // right shared one. Remap any such FBX-internal material onto SharedPaletteMaterialPath via
        // Unity's official external-material-remap API — reimport-safe, unlike touching
        // sharedMaterials on an instance, and it survives every future reimport of this same file.
        var sharedMat = AssetDatabase.LoadAssetAtPath<Material>(SharedPaletteMaterialPath);
        if (sharedMat != null)
        {
            var existingRemaps = importer.GetExternalObjectMap();

            // Case A: a remap already exists but points at the WRONG material (e.g. an earlier
            // version of this constant pointed at AA_LowPolyCommon before Tad corrected it to the
            // PolyPerfect atlas) — correct it in place rather than leaving it stuck forever, since
            // once a remap exists the renderer never shows the original FBX-internal material again
            // for Case B below to catch.
            foreach (var kvp in existingRemaps.ToList())
            {
                if (kvp.Key.type != typeof(Material) || kvp.Value == sharedMat) continue;
                importer.AddRemap(kvp.Key, sharedMat);
                changed = true;
            }

            // Case B: a material still living INSIDE the fbx (never remapped at all) — the normal
            // case for a brand new export that has never been through this pass before.
            var internalMaterialNames = probe.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && AssetDatabase.GetAssetPath(m) == path)   // still living INSIDE the fbx = never remapped
                .Select(m => m.name)
                .Distinct();

            foreach (var matName in internalMaterialNames)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), matName);
                if (existingRemaps.ContainsKey(id)) continue;   // already handled by Case A above
                importer.AddRemap(id, sharedMat);
                changed = true;
            }
        }

        if (changed)
            importer.SaveAndReimport();
    }

    /// <summary>Parse "gender_slot_variant" (variant may contain further underscores). Null if invalid.</summary>
/// <summary>Parse "gender_slot_variant" (variant may contain further underscores). Accepts
    /// "male"/"man" and "female"/"woman" as synonyms (canonicalised to male/female so every
    /// downstream check against those two literals keeps working), plus "neutral" for a part
    /// that isn't gender-specific at all (e.g. hardhat/headphones) — AvatarPartLibrary folds
    /// neutral parts into BOTH genders' queries. Null if invalid.</summary>
    // The female torso mesh is "woman_bodyA_Cauc", which the plain gender_slot_variant parser reads as slot
    // "bodya" (not "body"). It is still the file that carries the full skeleton and must get the Humanoid rig,
    // so "bodya" counts here. (Only used for the Humanoid decision; part slots are untouched.)
    // Avatar 2.0 names the torso mesh "Female_Torso_Body" (slot "torso", variant "Body"), so "torso" counts too.
    private static bool IsBodySlotName(string slot) => slot == "body" || slot == "bodya" || slot == "torso";

    private static AvatarPartLibrary.Part ParseName(string name, int sourceIndex)
    {
        // Multi-part body exports (2026-09-30, e.g. "woman.bodyA") name the main torso mesh
        // "<gender>.<variant>" with NO slot token at all — every other sibling mesh in the same
        // file (woman_hands_bodyA, woman_head_bodyA) DOES carry an explicit slot, but the torso
        // itself is just "the body" and needs no disambiguation. Handled as its own case rather
        // than folded into the underscore-based rule below, since a dot here is a real, deliberate
        // naming choice (not a typo to reject) — always slot "body".
        if (name.Contains('.') && !name.Contains('_'))
        {
            var dotSeg = name.Split('.');
            if (dotSeg.Length == 2)
            {
                string dotGender = dotSeg[0].ToLower() switch
                {
                    "male" or "man"     => "male",
                    "female" or "woman" => "female",
                    "neutral"           => "neutral",
                    _ => null,
                };
                if (dotGender != null && !string.IsNullOrEmpty(dotSeg[1]))
                {
                    return new AvatarPartLibrary.Part
                    {
                        objectName  = name,
                        gender      = dotGender,
                        slot        = "body",
                        variant     = dotSeg[1],
                        sourceIndex = sourceIndex,
                    };
                }
            }
        }

        // Tolerate "Female_Neck.Collar" (a '.' typed where the second '_' belongs): exactly one underscore followed by a dot is read
        // as gender_slot.variant. The part keeps its ORIGINAL object name; only the parse is normalised.
        string norm = name;
        if (name.IndexOf('_') >= 0 && name.IndexOf('_') == name.LastIndexOf('_'))
        {
            int us = name.IndexOf('_'), dot = name.IndexOf('.', us);
            if (dot > us + 1 && dot < name.Length - 1) norm = name.Substring(0, dot) + "_" + name.Substring(dot + 1);
        }

        var seg = norm.Split('_');
        if (seg.Length < 3) return null;

        string rawGender = seg[0].ToLower();
        string gender = rawGender switch
        {
            "male" or "man"     => "male",
            "female" or "woman" => "female",
            "neutral"           => "neutral",
            _ => null,
        };
        if (gender == null) return null;

        string slot    = seg[1].ToLower();
        // Avatar 2.0 naming: gender_slot_variant[_nsfw]. A trailing blank segment ("Female_Torso_Body_") and a trailing "nsfw" tag
        // are not part of the variant; the tag itself is read from the object name by DirtyDev.IsNsfwName.
        var variantSegs = seg.Skip(2).ToList();
        while (variantSegs.Count > 1 && string.IsNullOrEmpty(variantSegs[variantSegs.Count - 1])) variantSegs.RemoveAt(variantSegs.Count - 1);
        if (variantSegs.Count > 1 && string.Equals(variantSegs[variantSegs.Count - 1], "nsfw", System.StringComparison.OrdinalIgnoreCase))
            variantSegs.RemoveAt(variantSegs.Count - 1);
        string variant = string.Join("_", variantSegs);   // keep the rest as the variant name
        if (string.IsNullOrEmpty(slot) || string.IsNullOrEmpty(variant)) return null;

        // The new body_main.fbx exports name their single combined mesh "<gender>_construction_
        // worker" (a themed body variant), which parses to slot "construction" under the plain
        // gender_slot_variant rule — an unrecognised slot nothing in ModularAvatarAssembler gates,
        // so it would otherwise attach to every avatar as an extra freebie mesh instead of standing
        // in for "body". Per Tad: this mesh IS the body. Remapped here rather than renamed in the
        // FBX itself, so the merge logic's FindDeep(part.objectName) still finds the real child by
        // its actual name — only the catalogued slot/variant change.
        if (slot == "construction" && variant.ToLower() == "worker" && gender != "neutral")
        {
            slot = "body";
            variant = "ConstructionWorker";
        }

        return new AvatarPartLibrary.Part
        {
            objectName  = name,
            gender      = gender,
            slot        = slot,
            variant     = variant,
            sourceIndex = sourceIndex,
        };
    }

    private static AvatarPartLibrary LoadOrCreateLibrary()
    {
        var lib = AssetDatabase.LoadAssetAtPath<AvatarPartLibrary>(LibraryPath);
        if (lib != null) return lib;

        string dir = Path.GetDirectoryName(LibraryPath);
        if (!AssetDatabase.IsValidFolder(dir))
            Directory.CreateDirectory(dir);   // creates the Resources avatar folder if needed

        lib = ScriptableObject.CreateInstance<AvatarPartLibrary>();
        AssetDatabase.CreateAsset(lib, LibraryPath);
        AssetDatabase.ImportAsset(LibraryPath);
        return lib;
    }

    // Auto-rebuild whenever a model in the drop folder is added / changed / moved / deleted.
    private class Watcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                                   string[] moved, string[] movedFrom)
        {
            bool TouchesDrop(string[] arr) =>
                arr.Any(p =>
                {
                    string np = p.Replace('\\', '/');
                    return (np.StartsWith(DropFolder + "/") || np.StartsWith(NewPipelineRoot + "/"))
                        && (np.EndsWith(".fbx") || np.EndsWith(".blend") || np.EndsWith(".obj"));
                });

            if (TouchesDrop(imported) || TouchesDrop(deleted) || TouchesDrop(moved) || TouchesDrop(movedFrom))
                EditorApplication.delayCall += () => ScanAndRebuild(verbose: true);
        }
    }
}
