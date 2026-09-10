using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-off setup for the yard backdrop's occlusion fade + depth-of-field, mirroring
/// GridResizeTool's "run once from the menu, then save the scene" pattern.
/// </summary>
public static class YardBackdropSetupTool
{
    private const string ProfilePath    = "Assets/_Project/Settings/VP_YardBackdrop.asset";
    private const string VolumeObjectName = "YardBackdropVolume";

    [MenuItem("Tools/Yard Backdrop/Setup Fade + Depth Of Field")]
    private static void Setup()
    {
        var backdrop = GameObject.Find("YardBackdrop");
        if (backdrop == null)
        {
            EditorUtility.DisplayDialog("Yard Backdrop Setup",
                "No 'YardBackdrop' GameObject found in the active scene.", "OK");
            return;
        }

        // ── 1. Occlusion fade ────────────────────────────────────────────────
        var fade = backdrop.GetComponent<YardBackdropFade>();
        if (fade == null)
        {
            fade = Undo.AddComponent<YardBackdropFade>(backdrop);
        }

        // ── 2. Depth of field volume profile ─────────────────────────────────
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        bool createdProfile = false;
        if (profile == null)
        {
            string dir = Path.GetDirectoryName(ProfilePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            createdProfile = true;
        }

        if (!profile.TryGet(out DepthOfField dof))
        {
            // VolumeProfile.Add<T>() is a runtime-side API — it only appends the component to the
            // in-memory `components` list, it never persists it. Without an explicit
            // AddObjectToAsset call the new DepthOfField has no serialized identity, so the
            // profile saves with a dangling null (fileID: 0) in its components list and the
            // override is silently lost. Confirmed 2026-09-09: first run of this tool produced
            // exactly that.
            dof = profile.Add<DepthOfField>(true);
            dof.name = nameof(DepthOfField);
            dof.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(dof, profile);
        }

        dof.active = true;
        dof.mode.overrideState = true;
        dof.mode.value = DepthOfFieldMode.Gaussian;
        // Tuned to this project's 100x100 grid @ 1.325 cell size (~66-unit playfield radius, see
        // the scene's PlacementGrid): gameplay stays sharp out to just past the yard edge, the
        // backdrop beyond it goes soft — a "toy town beyond the fence" read, like Two Point
        // Hospital's blurred neighbourhood, not a fully blurred vista.
        dof.gaussianStart.overrideState = true;
        dof.gaussianStart.value = 60f;
        dof.gaussianEnd.overrideState = true;
        dof.gaussianEnd.value = 110f;
        dof.gaussianMaxRadius.overrideState = true;
        dof.gaussianMaxRadius.value = 1f;
        dof.highQualitySampling.overrideState = true;
        dof.highQualitySampling.value = false;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // ── 3. Global volume in the scene ───────────────────────────────────
        var volumeGO = GameObject.Find(VolumeObjectName);
        if (volumeGO == null)
        {
            volumeGO = new GameObject(VolumeObjectName);
            Undo.RegisterCreatedObjectUndo(volumeGO, "Create Yard Backdrop Volume");
        }

        var volume = volumeGO.GetComponent<Volume>();
        if (volume == null) volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0;
        volume.weight = 1f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volumeGO);

        // ── 4. Make sure the player camera actually runs post-processing ────
        var cam = Camera.main;
        if (cam != null)
        {
            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null && !camData.renderPostProcessing)
            {
                camData.renderPostProcessing = true;
                EditorUtility.SetDirty(cam);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string msg = $"YardBackdropFade on '{backdrop.name}'.\n" +
                     $"{(createdProfile ? "Created" : "Reused")} DoF profile at {ProfilePath}.\n" +
                     $"Volume '{VolumeObjectName}' in scene, camera post-processing enabled.";

        Debug.Log($"[YardBackdropSetup] {msg.Replace("\n", " ")}\n⚠ Save the scene now (Ctrl+S).");
        EditorUtility.DisplayDialog("Yard Backdrop Setup Done", msg + "\n\n⚠ Save the scene now (Ctrl+S).", "Got it");
    }
}
