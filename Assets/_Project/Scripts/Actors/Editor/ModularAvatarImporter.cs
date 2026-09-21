using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
    public const string DropFolder  = "Assets/_Project/Models/BlenderFiles/Modular_Staff";
    public const string LibraryPath = "Assets/Resources/ModularAvatar/AvatarPartLibrary.asset";

    [MenuItem("Tools/Modular Avatar/Scan & Rebuild Library")]
    public static void ScanAndRebuildMenu() => ScanAndRebuild(verbose: true);

    public static AvatarPartLibrary ScanAndRebuild(bool verbose)
    {
        if (!AssetDatabase.IsValidFolder(DropFolder))
        {
            if (verbose)
                Debug.LogWarning($"[ModularAvatar] Drop folder not found: {DropFolder}. " +
                                 "Create it and drop your modular FBX(s) in there.");
            return null;
        }

        var lib = LoadOrCreateLibrary();
        lib.sources.Clear();
        lib.parts.Clear();

        // All model assets (FBX, .blend, etc.) in the drop folder.
        var guids = AssetDatabase.FindAssets("t:Model", new[] { DropFolder });
        int fbxCount = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Skip Blender source files (.blend, .blend1) — only FBX/OBJ exports should be sources.
            // Having both the .blend and the .fbx in the folder causes duplicate sourceIndex entries
            // which breaks SkinnedMeshRenderer bone references during cross-source merging.
            string pathLower = path.ToLower();
            if (pathLower.EndsWith(".blend") || pathLower.EndsWith(".blend1")) continue;

            // Skip loose "*Workshop*" staging files sitting at the drop folder's own root (e.g.
            // Gender_Neutral_Workshop.fbx) — these are Blender reference/pose-testing exports, not
            // real avatar sources: no skeleton, and they duplicate a mesh (e.g. man_construction_
            // worker) that already lives correctly in MEN/man_body_main.fbx. Scanning them in creates
            // a second "body" candidate with no armature, which can get randomly picked as the
            // PRIMARY source and leave the avatar with no working skeleton at all.
            if (Path.GetFileNameWithoutExtension(path).ToLower().Contains("workshop")) continue;

            // The old, all-in-one Male_Modular_Staff.fbx / Female_Modular_Staff.fbx (kept under
            // XXX_Obsolete_Humanoids for chest/legs/feet/eyebrows/face/vest, which have no MEN/WOMEN
            // replacement yet) each carry a "body" part too — but per Tad, the new body_main.fbx
            // files now OWN the "body" slot for both genders. Their old body variants are dropped
            // below so a spawned avatar can't randomly pick the legacy body mesh instead.
            bool isLegacyHumanoidFile = path.Replace('\\', '/').Contains("/Obsolete_Humanoids/", System.StringComparison.OrdinalIgnoreCase)
                                     || path.Replace('\\', '/').Contains("/XXX_Obsolete_Humanoids/", System.StringComparison.OrdinalIgnoreCase);

            // New drop-folder exports (MEN/WOMEN/_GENDER_NEUTRAL) come out of Blender missing two
            // things every OLDER modular part already had: a Humanoid rig (so the Animator can
            // retarget the worker's walk/idle clips onto it — without it the character sits frozen
            // in bind pose, i.e. a permanent T-pose) and a real textured material (Blender's own
            // material names don't match any project asset, so Unity generates a blank untextured
            // one instead of finding the shared palette material everything else uses). Fixed here,
            // at scan time, so a fresh export just works without a manual Inspector pass — same
            // self-healing spirit as the rest of this importer. Must run BEFORE loading `root` below:
            // it can trigger a reimport, which would otherwise leave `root` pointing at a stale prefab.
            if (!isLegacyHumanoidFile) FixNewExport(path);

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

                if (isLegacyHumanoidFile && part.slot == "body")
                {
                    if (verbose)
                        Debug.Log($"[ModularAvatar] Skipping legacy body part '{t.name}' in " +
                                  $"{Path.GetFileName(path)} — body_main.fbx now owns the body slot.");
                    continue;
                }

                lib.parts.Add(part);
            }
        }

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();

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
    private const string SharedPaletteMaterialPath = "Assets/polyperfect/Common/Materials/atlas-source-LPAP.mat";

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

        bool changed = false;

        // 1. Humanoid rig — only for files that actually carry a skinned skeleton (a real body).
        // Hair/hat/prop exports have no bones at all; forcing Humanoid on those does nothing useful
        // and would just log a spurious "invalid avatar" warning for no benefit.
        bool hasSkeleton = probe.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Any(s => s.bones != null && s.bones.Length > 0);
        if (hasSkeleton)
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
            var currentAvatar = probe.GetComponent<Animator>()?.avatar;
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
    private static AvatarPartLibrary.Part ParseName(string name, int sourceIndex)
    {
        var seg = name.Split('_');
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
        string variant = string.Join("_", seg.Skip(2));   // keep the rest as the variant name
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
            Directory.CreateDirectory(dir);   // creates Assets/Resources/ModularAvatar if needed

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
                arr.Any(p => p.Replace('\\', '/').StartsWith(DropFolder + "/")
                          && (p.EndsWith(".fbx") || p.EndsWith(".blend") || p.EndsWith(".obj")));

            if (TouchesDrop(imported) || TouchesDrop(deleted) || TouchesDrop(moved) || TouchesDrop(movedFrom))
                EditorApplication.delayCall += () => ScanAndRebuild(verbose: true);
        }
    }
}
