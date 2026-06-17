using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ModularAvatarVerify
{
    const string FbxPath = "Assets/5. Models/BlenderFiles/Modular_Staff/Modular_Staff.fbx";

    [MenuItem("Tools/Modular Avatar/Verify Rig (write report)")]
    public static void Verify()
    {
        var sb = new StringBuilder();
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) { Write("ERROR: importer null for " + FbxPath); return; }

        sb.AppendLine("== ModularAvatar rig verify ==");
        sb.AppendLine("animationType=" + importer.animationType);
        sb.AppendLine("avatarSetup=" + importer.avatarSetup);
        sb.AppendLine("sourceAvatar=" + (importer.sourceAvatar != null ? importer.sourceAvatar.name : "NULL"));

        // Avatar sub-asset
        var all = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        var avatar = all.OfType<Avatar>().FirstOrDefault();
        if (avatar == null) sb.AppendLine("Avatar sub-asset: NONE FOUND");
        else sb.AppendLine("Avatar name=" + avatar.name + " isHuman=" + avatar.isHuman + " isValid=" + avatar.isValid);

        // Skinned mesh count on the model
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (go != null)
        {
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var mrs = go.GetComponentsInChildren<MeshRenderer>(true);
            sb.AppendLine("SkinnedMeshRenderers=" + smrs.Length + "  (static MeshRenderers=" + mrs.Length + ")");
            sb.AppendLine("first SMRs: " + string.Join(", ", smrs.Take(5).Select(s => s.name)));
        }
        else sb.AppendLine("model GameObject: NULL");

        // Human bone mapping for the key bones (legs + head)
        var hd = importer.humanDescription;
        string[] keys = { "Head", "Neck", "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot", "LeftToes", "RightToes" };
        sb.AppendLine("-- importer.humanDescription.human (" + (hd.human != null ? hd.human.Length : 0) + " entries) --");
        if (hd.human != null)
        {
            foreach (var k in keys)
            {
                var m = hd.human.FirstOrDefault(h => h.humanName == k);
                sb.AppendLine("  " + k + " -> " + (string.IsNullOrEmpty(m.boneName) ? "(unmapped)" : m.boneName));
            }
        }

        // Import errors/warnings captured by the importer
        sb.AppendLine("-- importer.animationRetargetingWarnings --");
        // (only meaningful when importAnimation; left for reference)

        Write(sb.ToString());
    }

    static void Write(string content)
    {
        string outPath = Path.Combine(Application.dataPath, "..", "modular_avatar_verify.txt");
        File.WriteAllText(outPath, content);
        Debug.Log("[ModularAvatarVerify] wrote report to " + Path.GetFullPath(outPath) + "\n" + content);
    }
}
