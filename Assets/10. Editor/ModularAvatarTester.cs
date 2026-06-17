using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor preview for the modular avatar system: spawns a row of randomly-assembled avatars in
/// the scene so you can eyeball the variety. Purely a dev tool — spawns are parented under a
/// "__AvatarPreview" object and removed by the Clear menu (they are not saved gameplay objects).
/// </summary>
public static class ModularAvatarTester
{
    private const string PreviewRootName = "__AvatarPreview";
    private const float   Spacing = 2.0f;

    [MenuItem("Tools/Modular Avatar/Spawn 8 Random Avatars")]
    public static void SpawnEight() => Spawn(8);

    [MenuItem("Tools/Modular Avatar/Spawn 1 Random Avatar")]
    public static void SpawnOne() => Spawn(1);

    private static void Spawn(int count)
    {
        var lib = AssetDatabase.LoadAssetAtPath<AvatarPartLibrary>(ModularAvatarImporter.LibraryPath);
        if (lib == null || lib.PartCount == 0)
        {
            // Try a fresh scan first — maybe it just hasn't been built yet.
            lib = ModularAvatarImporter.ScanAndRebuild(verbose: true);
            if (lib == null || lib.PartCount == 0)
            {
                EditorUtility.DisplayDialog("Modular Avatar",
                    "No avatar parts found. Drop your modular FBX into\n" +
                    ModularAvatarImporter.DropFolder + "\nthen run Scan & Rebuild Library.", "OK");
                return;
            }
        }

        var genders = lib.Genders().ToList();
        if (genders.Count == 0) { Debug.LogWarning("[ModularAvatar] Library has no genders."); return; }

        var root = GameObject.Find(PreviewRootName) ?? new GameObject(PreviewRootName);
        int existing = root.transform.childCount;
        var rng = new System.Random();

        for (int i = 0; i < count; i++)
        {
            string gender = genders[rng.Next(genders.Count)];
            var avatar = ModularAvatarAssembler.Build(lib, gender, rng);
            if (avatar == null) continue;
            avatar.transform.SetParent(root.transform, false);
            avatar.transform.localPosition = new Vector3((existing + i) * Spacing, 0f, 0f);
            Undo.RegisterCreatedObjectUndo(avatar, "Spawn Avatar");
        }

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log($"[ModularAvatar] Spawned {count} preview avatar(s) under '{PreviewRootName}'.");
    }

    [MenuItem("Tools/Modular Avatar/Clear Preview Avatars")]
    public static void Clear()
    {
        var root = GameObject.Find(PreviewRootName);
        if (root != null) Object.DestroyImmediate(root);
    }
}
