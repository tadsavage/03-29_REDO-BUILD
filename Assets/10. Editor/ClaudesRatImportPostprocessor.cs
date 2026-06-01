using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto-configures the import settings for Claudes_Rat.fbx so the rigged rat is
/// drop-in ready: Generic rig (avatar created from the model) plus split/looped
/// animation clips derived from the three baked FBX takes (RatIdle, RatSniff,
/// RatScurry).
///
/// Clips produced:
///   RatIdle  - looping breathing/idle           (take: RatIdle)
///   RatSniff - one-shot sit-up-and-sniff         (take: RatSniff)
///   RunStart - one-shot opening bound            (take: RatScurry, frames 0..19)
///   RunLoop  - looping moderate scurry           (take: RatScurry, frames 19..43)
///
/// Clip definitions are only written when the importer has no custom clips yet,
/// so any later manual edits in the Inspector are preserved across reimports.
/// </summary>
public class ClaudesRatImportPostprocessor : AssetPostprocessor
{
    const string TargetPath = "Assets/5. Models/BlenderFiles/PROPS/Claudes_Rat.fbx";

    void OnPreprocessModel()
    {
        if (assetPath != TargetPath)
            return;

        var importer = (ModelImporter)assetImporter;

        // --- Rig ---
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;

        // Don't clobber clips the user has customized in the Inspector.
        if (importer.clipAnimations != null && importer.clipAnimations.Length > 0)
            return;

        var defaults = importer.defaultClipAnimations; // one entry per baked take
        if (defaults == null || defaults.Length == 0)
            return;

        var clips = new List<ModelImporterClipAnimation>();

        foreach (var def in defaults)
        {
            // FBX take names carry the armature prefix (e.g. "Rat_Rig|RatIdle"),
            // so match on the part after the last '|'.
            string take = def.takeName;
            int bar = take.LastIndexOf('|');
            string shortTake = bar >= 0 ? take.Substring(bar + 1) : take;

            switch (shortTake)
            {
                case "RatIdle":
                {
                    var c = Clone(def);
                    c.name = "RatIdle";
                    c.loopTime = true;
                    clips.Add(c);
                    break;
                }
                case "RatSniff":
                {
                    var c = Clone(def);
                    c.name = "RatSniff";
                    c.loopTime = false;
                    clips.Add(c);
                    break;
                }
                case "RatScurry":
                {
                    // Bound + moderate-scurry source spans frames 1..44 in Blender.
                    // Split at Blender frame 20: opening bound -> looping scurry.
                    float start = def.firstFrame;
                    float end = def.lastFrame;
                    float split = start + 19f;          // frame 20 is 19 frames past frame 1
                    if (split <= start || split >= end) // safety if bake bounds differ
                        split = Mathf.Round((start + end) * 0.5f);

                    var runStart = Clone(def);
                    runStart.name = "RunStart";
                    runStart.firstFrame = start;
                    runStart.lastFrame = split;
                    runStart.loopTime = false;
                    clips.Add(runStart);

                    var runLoop = Clone(def);
                    runLoop.name = "RunLoop";
                    runLoop.firstFrame = split;
                    runLoop.lastFrame = end;
                    runLoop.loopTime = true;
                    clips.Add(runLoop);
                    break;
                }
                default:
                    clips.Add(def); // keep any unexpected takes untouched
                    break;
            }
        }

        importer.clipAnimations = clips.ToArray();
        Debug.Log($"[ClaudesRat] Configured rig (Generic) and {clips.Count} animation clips on import.");
    }

    static ModelImporterClipAnimation Clone(ModelImporterClipAnimation src)
    {
        return new ModelImporterClipAnimation
        {
            takeName = src.takeName,
            name = src.name,
            firstFrame = src.firstFrame,
            lastFrame = src.lastFrame,
            wrapMode = src.wrapMode,
            loop = src.loop,
            loopTime = src.loopTime,
            loopPose = src.loopPose,
        };
    }

    /// <summary>
    /// One-time self-heal: if the rat FBX is already imported but hasn't been run
    /// through this postprocessor yet (no RunLoop clip), force a reimport so the
    /// settings above get applied. The RunLoop guard prevents reimport loops.
    /// </summary>
    [InitializeOnLoadMethod]
    static void EnsureConfigured()
    {
        var importer = AssetImporter.GetAtPath(TargetPath) as ModelImporter;
        if (importer == null)
            return; // not imported yet; OnPreprocessModel handles the first import

        bool configured = importer.clipAnimations != null &&
                          importer.clipAnimations.Any(c => c.name == "RunLoop");
        if (!configured)
            AssetDatabase.ImportAsset(TargetPath, ImportAssetOptions.ForceUpdate);
    }

    [MenuItem("Tools/Claudes Rat/Reimport & Configure FBX")]
    static void ForceReconfigure()
    {
        var importer = AssetImporter.GetAtPath(TargetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[ClaudesRat] FBX not found at {TargetPath}");
            return;
        }
        importer.clipAnimations = new ModelImporterClipAnimation[0]; // reset to re-derive
        AssetDatabase.ImportAsset(TargetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[ClaudesRat] Forced reimport & reconfigure.");
    }
}
