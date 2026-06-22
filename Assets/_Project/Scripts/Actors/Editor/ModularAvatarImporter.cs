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
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;

            int sourceIndex = lib.sources.Count;
            lib.sources.Add(new AvatarPartLibrary.SourceModel { fbxPath = path, prefab = root });
            fbxCount++;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform) continue;
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

    /// <summary>Parse "gender_slot_variant" (variant may contain further underscores). Null if invalid.</summary>
    private static AvatarPartLibrary.Part ParseName(string name, int sourceIndex)
    {
        var seg = name.Split('_');
        if (seg.Length < 3) return null;

        string gender = seg[0].ToLower();
        if (gender != "male" && gender != "female") return null;

        string slot    = seg[1].ToLower();
        string variant = string.Join("_", seg.Skip(2));   // keep the rest as the variant name
        if (string.IsNullOrEmpty(slot) || string.IsNullOrEmpty(variant)) return null;

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
