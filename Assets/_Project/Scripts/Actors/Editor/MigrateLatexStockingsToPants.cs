using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot migration for the 2026-10-11 Blender rename of the latex legs/feet from "Stockings" to
/// "Pants" (Female_Legs_Stockings_{Color}_Latex_NSFW -> Female_Legs_Pants_{Color}_Latex_NSFW, and the
/// matching Feet pieces).
///
/// A renamed mesh finalizes into a NEW AvatarPartAsset with default settings, and the old asset
/// keeps pointing at a mesh that no longer exists. Run this AFTER Scan &amp; Rebuild Library + Finalize
/// All Pending has created the new parts: it copies the AOD settings (roles, weight, colour
/// variants, verified flag, hidden body slots) from each old asset to its new one, then deletes the
/// old asset and prefab. An old part is only deleted when its replacement exists, so running it
/// early is harmless (it reports what is still missing and changes nothing for that part).
///
/// Safe to delete this file once the migration has been run.
/// </summary>
public static class MigrateLatexStockingsToPants
{
    private const string Folder = "Assets/_Project/__Avatar_System2.0/Female/Regular_Body_Type/3. Prefab (post AOD Submit)";
    private static readonly string[] Colors = { "Black", "Red", "White" };
    private static readonly string[] Slots = { "Legs", "Feet" };

    [MenuItem("Tools/Modular Avatar/Migrate Latex Stockings To Pants")]
    public static void Run()
    {
        int migrated = 0, waiting = 0;

        foreach (var slot in Slots)
        foreach (var color in Colors)
        {
            string oldBase = $"{Folder}/Female_{slot}_Stockings_{color}_Latex_NSFW";
            string newBase = $"{Folder}/Female_{slot}_Pants_{color}_Latex_NSFW";

            var oldAsset = AssetDatabase.LoadAssetAtPath<AvatarPartAsset>(oldBase + ".asset");
            if (oldAsset == null) continue; // already migrated

            var newAsset = AssetDatabase.LoadAssetAtPath<AvatarPartAsset>(newBase + ".asset");
            if (newAsset == null)
            {
                waiting++;
                Debug.LogWarning($"[MigrateLatex] No finalized '{System.IO.Path.GetFileName(newBase)}' yet - " +
                                 $"run Scan & Rebuild Library, then Finalize All Pending, then run this again.");
                continue;
            }

            var src = new SerializedObject(oldAsset);
            var dst = new SerializedObject(newAsset);
            foreach (var field in new[] { "allowedRoles", "colorVariants", "defaultWeight", "verifiedInGame", "hiddenBodySlots" })
            {
                var sp = src.FindProperty(field);
                var dp = dst.FindProperty(field);
                if (sp == null || dp == null) { Debug.LogError($"[MigrateLatex] Field '{field}' not found."); continue; }
                dst.CopyFromSerializedProperty(sp);
            }
            dst.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(newAsset);

            AssetDatabase.DeleteAsset(oldBase + ".asset");
            AssetDatabase.DeleteAsset(oldBase + ".prefab");
            migrated++;
            Debug.Log($"[MigrateLatex] {slot} {color}: settings copied (weight {newAsset.DefaultWeight}), old Stockings asset + prefab deleted.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (migrated > 0) EditorApplication.ExecuteMenuItem("Tools/Modular Avatar/Scan & Rebuild Library");
        Debug.Log($"[MigrateLatex] Done: {migrated} migrated, {waiting} still waiting on Finalize.");
    }
}
