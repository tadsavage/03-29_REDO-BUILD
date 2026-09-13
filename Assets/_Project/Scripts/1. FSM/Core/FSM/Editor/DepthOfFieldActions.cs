using System;
using Bezi;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Bezi actions for tuning the Gaussian Depth of Field override on a VolumeProfile asset.
/// The Bezi action reflection layer only exposes VolumeComponent.active for DepthOfField, not
/// its individual VolumeParameter fields (mode, gaussianStart, etc.), so this action edits them
/// directly through the Editor VolumeProfile API, mirroring YardBackdropSetupTool's approach.
/// </summary>
public static class DepthOfFieldActions
{
    /// <summary>
    /// Sets whether the DepthOfField override on the given VolumeProfile asset is active, without
    /// touching any of its other settings.
    /// </summary>
    [BeziAction("Enables or disables the DepthOfField override on a VolumeProfile asset without changing its other settings.")]
    public static string SetDepthOfFieldActive(string profileAssetPath, bool active)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profileAssetPath);
        if (profile == null)
            throw new Exception($"No VolumeProfile found at '{profileAssetPath}'.");

        if (!profile.TryGet(out DepthOfField dof))
            throw new Exception($"VolumeProfile at '{profileAssetPath}' has no DepthOfField override.");

        dof.active = active;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        return $"DepthOfField on '{profileAssetPath}' active = {active}.";
    }

    /// <summary>
    /// Sets the Gaussian Depth of Field override on the given VolumeProfile asset. Forces mode to
    /// Gaussian. gaussianStart/gaussianEnd are world-space distances from the camera; blur ramps
    /// from 0 at gaussianStart to gaussianMaxRadius at gaussianEnd. gaussianMaxRadius is clamped
    /// to URP's supported range (0.5-1.5).
    /// </summary>
    [BeziAction(
        "Sets the Gaussian Depth of Field override (start distance, end distance, max blur radius, high quality sampling) on a VolumeProfile asset. Use to tune tilt-shift/miniature style blur."
    )]
    public static string SetGaussianDepthOfField(
        string profileAssetPath,
        float gaussianStart,
        float gaussianEnd,
        float gaussianMaxRadius,
        bool highQualitySampling)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profileAssetPath);
        if (profile == null)
            throw new Exception($"No VolumeProfile found at '{profileAssetPath}'.");

        if (!profile.TryGet(out DepthOfField dof))
            throw new Exception($"VolumeProfile at '{profileAssetPath}' has no DepthOfField override.");

        dof.active = true;

        dof.mode.overrideState = true;
        dof.mode.value = DepthOfFieldMode.Gaussian;

        dof.gaussianStart.overrideState = true;
        dof.gaussianStart.value = gaussianStart;

        dof.gaussianEnd.overrideState = true;
        dof.gaussianEnd.value = gaussianEnd;

        dof.gaussianMaxRadius.overrideState = true;
        dof.gaussianMaxRadius.value = gaussianMaxRadius;

        dof.highQualitySampling.overrideState = true;
        dof.highQualitySampling.value = highQualitySampling;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        return $"DepthOfField on '{profileAssetPath}' set to Gaussian: start={gaussianStart}, end={gaussianEnd}, maxRadius={dof.gaussianMaxRadius.value}, highQualitySampling={highQualitySampling}.";
    }

    /// <summary>
    /// Sets the physically-based Bokeh Depth of Field override on the given VolumeProfile asset.
    /// Forces mode to Bokeh. focusDistance is the world-space distance from the camera that stays
    /// sharp; aperture is the f-stop (lower = shallower depth of field / stronger blur); focalLength
    /// is in millimeters (higher = stronger blur). bladeCount/bladeCurvature/bladeRotation shape the
    /// bokeh highlights and are cosmetic only.
    /// </summary>
    [BeziAction(
        "Sets the physically-based Bokeh Depth of Field override (focus distance, aperture, focal length, blade count/curvature/rotation) on a VolumeProfile asset. Use for a stronger, more filmic blur than Gaussian mode allows."
    )]
    public static string SetBokehDepthOfField(
        string profileAssetPath,
        float focusDistance,
        float aperture,
        float focalLength,
        int bladeCount,
        float bladeCurvature,
        float bladeRotation)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profileAssetPath);
        if (profile == null)
            throw new Exception($"No VolumeProfile found at '{profileAssetPath}'.");

        if (!profile.TryGet(out DepthOfField dof))
            throw new Exception($"VolumeProfile at '{profileAssetPath}' has no DepthOfField override.");

        dof.active = true;

        dof.mode.overrideState = true;
        dof.mode.value = DepthOfFieldMode.Bokeh;

        dof.focusDistance.overrideState = true;
        dof.focusDistance.value = focusDistance;

        dof.aperture.overrideState = true;
        dof.aperture.value = aperture;

        dof.focalLength.overrideState = true;
        dof.focalLength.value = focalLength;

        dof.bladeCount.overrideState = true;
        dof.bladeCount.value = bladeCount;

        dof.bladeCurvature.overrideState = true;
        dof.bladeCurvature.value = bladeCurvature;

        dof.bladeRotation.overrideState = true;
        dof.bladeRotation.value = bladeRotation;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        return $"DepthOfField on '{profileAssetPath}' set to Bokeh: focusDistance={focusDistance}, aperture={dof.aperture.value}, focalLength={dof.focalLength.value}, bladeCount={bladeCount}, bladeCurvature={bladeCurvature}, bladeRotation={bladeRotation}.";
    }
}
