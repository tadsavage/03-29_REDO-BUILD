using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The "Submit"/"Update" backend for the Avatar Object Database (AODPanel). Validates a raw scanned
/// part's structure, then produces (or overwrites) a real, game-ready finalized prefab plus a
/// standalone AvatarPartAsset carrying its metadata.
///
/// Deliberately CANNOT fix the two classes of bug that motivated it (a body part with no
/// SkinnedMeshRenderer at all, or one with bad skin weights) — it can only DETECT and reject the
/// first (a structural check) and neither Unity nor this code can detect the second at all (bad
/// weights still produce a technically-valid SkinnedMeshRenderer; only a human looking at it posed
/// in Play Mode can tell). That's why this is a manual Submit step rather than something the folder
/// watcher does automatically — see EmployeeSpawner's _floorWorkersUseModularBodyIfAvailable flag for
/// the other half of this same safety principle.
/// </summary>
public static class ModularAvatarFinalizer
{
    public const string FinalizedAssetFolder = "Assets/_Project/Models/BlenderFiles/Modular_Staff_Models/AOD_Objects";
    public const string BodyPrefabFolder     = "Assets/_Project/Prefabs/Modular_Staff_Prefabs/Body_Prefabs";
    public const string PropsPrefabFolder    = "Assets/_Project/Prefabs/Modular_Staff_Prefabs/Props_Prefabs";

    /// <summary>Validates and finalizes a raw scanned Part into a real prefab + AvatarPartAsset.
    /// Both first-time Submit and a later Update call this same method — the output paths are derived
    /// purely from objectName, so re-running it always overwrites the same two files rather than
    /// duplicating them. Returns false with a human-readable <paramref name="error"/> if validation
    /// fails; nothing is written to disk in that case.</summary>
    public static bool TryFinalize(AvatarPartLibrary lib, AvatarPartLibrary.Part rawPart,
        out string error, out AvatarPartAsset resultAsset)
    {
        resultAsset = null;
        error = null;

        if (lib == null || rawPart == null) { error = "No part to finalize."; return false; }

        var sourcePrefab = lib.PrefabFor(rawPart);
        if (sourcePrefab == null)
        {
            error = $"'{rawPart.objectName}' has no source prefab to finalize — rescan the drop folder and try again.";
            return false;
        }

        if (!ValidateStructure(sourcePrefab, rawPart.objectName, rawPart.slot, out error))
            return false;

        EnsureFolders();
        string folder = RouteFolderForSlot(rawPart.slot);
        string prefabPath = $"{folder}/{rawPart.objectName}.prefab";
        string assetPath  = $"{FinalizedAssetFolder}/{rawPart.objectName}.asset";

        var temp = ModularAvatarAssembler.IsolatePart(lib, rawPart);
        if (temp == null)
        {
            error = $"Could not isolate '{rawPart.objectName}' from its source prefab — the mesh name may not match exactly.";
            return false;
        }

        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath, out bool success);
        Object.DestroyImmediate(temp);
        if (!success)
        {
            error = $"Failed to save prefab to {prefabPath}.";
            return false;
        }

        var asset = AssetDatabase.LoadAssetAtPath<AvatarPartAsset>(assetPath);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<AvatarPartAsset>();

        asset.SetIdentity(rawPart.objectName, rawPart.gender, rawPart.slot, rawPart.variant);
        asset.SetPrefab(savedPrefab);
        asset.allowedRoles = new System.Collections.Generic.List<EmployeeRole>(rawPart.allowedRoles);
        asset.colorVariants = rawPart.colorVariants;
        asset.defaultWeight = rawPart.defaultWeight;
        asset.verifiedInGame = rawPart.verifiedInGame;
        asset.hiddenBodySlots = new System.Collections.Generic.List<string>(rawPart.hiddenBodySlots);

        if (isNew) AssetDatabase.CreateAsset(asset, assetPath);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        resultAsset = asset;
        return true;
    }

    /// <summary>Re-validates and re-finalizes an already-finalized part against its ORIGINAL raw
    /// source, if that source still exists in the drop folder (an "Update" after re-exporting the
    /// FBX). If no raw source remains, there's nothing to re-validate against — the asset's own
    /// metadata edits (roles/weight/colors) are already live since AODPanel writes them directly to
    /// the asset, so this is a no-op in that case.</summary>
public static bool TryUpdateFromRawSource(AvatarPartLibrary lib, AvatarPartAsset asset,
        out string error)
    {
        error = null;

        // lib.parts deliberately EXCLUDES anything already finalized (ScanAndRebuild filters it out
        // by objectName so a reviewed part never reappears as an unreviewed raw duplicate) — so
        // looking this asset up in lib.parts, as this used to do, ALWAYS misses it and silently
        // no-ops, even right after its source FBX was re-exported and rescanned. Live bug (found
        // 2026-09-29): a female body rig fix landed in the FBX and the drop-folder rescan picked it
        // up fine, but "Update" in the AOD reported success while never actually re-running
        // IsolatePart/SaveAsPrefabAsset, so the finalized prefab kept shipping the stale, broken
        // skeleton — T-pose in game despite Animator.avatar.isHuman still reading true.
        //
        // Look the source up directly in lib.sources instead, by finding which source FBX actually
        // contains a mesh-bearing transform named ObjectName — the same match IsolatePart itself uses
        // to locate the part inside its source.
        int sourceIndex = lib.sources.FindIndex(s => s.prefab != null &&
            s.prefab.GetComponentsInChildren<Transform>(true).Any(t =>
                t.name == asset.ObjectName &&
                (t.GetComponent<MeshFilter>() != null || t.GetComponent<SkinnedMeshRenderer>() != null)));
        if (sourceIndex < 0) return true; // no raw source left in the drop folder — nothing new to re-validate against

        var rawMatch = new AvatarPartLibrary.Part
        {
            objectName     = asset.ObjectName,
            gender         = asset.Gender,
            slot           = asset.Slot,
            variant        = asset.Variant,
            sourceIndex    = sourceIndex,
            allowedRoles   = asset.allowedRoles,
            colorVariants  = asset.colorVariants,
            defaultWeight  = asset.defaultWeight,
            verifiedInGame = asset.verifiedInGame,
            hiddenBodySlots = asset.hiddenBodySlots,
        };

        return TryFinalize(lib, rawMatch, out error, out _);
    }

    private static bool ValidateStructure(GameObject sourcePrefab, string objectName, string slot, out string error)
    {
        error = null;
        if (slot != "body") return true; // only body-slot parts require posability today

        // Find the transform that actually CARRIES the mesh and matches objectName — NOT just
        // "whichever transform's name matches", since an FBX can (and here, does) have a root node
        // and a mesh-bearing child sharing the exact same name; a name-only match can land on the
        // empty root and miss the real SkinnedMeshRenderer sitting one level down.
        Transform target = sourcePrefab.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => (t.GetComponent<MeshFilter>() != null || t.GetComponent<SkinnedMeshRenderer>() != null)
                                  && t.name == objectName);

        var smr = target != null ? target.GetComponent<SkinnedMeshRenderer>() : null;

        if (smr == null || smr.bones == null || smr.bones.Length == 0)
        {
            error = $"Cannot finalize '{objectName}': no SkinnedMeshRenderer with bones found. " +
                     "A body part must be a rigged/skinned mesh, not a static MeshRenderer — " +
                     "fix the Blender export (apply the Armature modifier before exporting) and re-export, then rescan.";
            return false;
        }
        return true;
    }

    private static string RouteFolderForSlot(string slot) => slot switch
    {
        "body" => BodyPrefabFolder,
        _      => PropsPrefabFolder, // hair/hat/facialhair/props default here
    };

    private static void EnsureFolders()
    {
        EnsureFolder(FinalizedAssetFolder);
        EnsureFolder(BodyPrefabFolder);
        EnsureFolder(PropsPrefabFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
