using System;
using System.Linq;
using Bezi;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Bezi actions for wiring a FullScreenPassRendererFeature into a UniversalRendererData asset
/// (e.g. PC_Renderer.asset). The Bezi action reflection layer does not expose
/// UniversalRendererData.m_RendererFeatures, so this action mirrors Unity's own
/// ScriptableRendererDataEditor.AddComponent implementation: create the feature as a sub-asset,
/// then grow the m_RendererFeatures/m_RendererFeatureMap serialized lists to reference it.
/// </summary>
public static class RendererFeatureActions
{
    /// <summary>
    /// Forces Unity to reimport (and thus recompile) the asset at the given path, bypassing any
    /// stale import/shader-compile cache.
    /// </summary>
    [BeziAction("Forces a full reimport of the asset at the given path (e.g. a .shader file), bypassing any stale import cache.")]
    public static string ForceReimportAsset(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        return $"Reimported '{assetPath}'.";
    }

    /// <summary>
    /// Adds a FullScreenPassRendererFeature to the given UniversalRendererData asset, or updates
    /// an existing one with the same featureName in place (so re-running this action is safe).
    /// </summary>
    [BeziAction(
        "Adds a FullScreenPassRendererFeature (single full-screen blit pass driven by a Material) to a UniversalRendererData asset. If a feature with the given featureName already exists on the renderer, updates it in place instead of creating a duplicate."
    )]
    public static string AddOrUpdateFullScreenPassRendererFeature(
        string rendererDataAssetPath,
        string featureName,
        string passMaterialAssetPath,
        string injectionPoint,
        bool fetchColorBuffer,
        bool requiresDepth,
        int passIndex)
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererDataAssetPath);
        if (rendererData == null)
            throw new Exception($"No UniversalRendererData found at '{rendererDataAssetPath}'.");

        var passMaterial = AssetDatabase.LoadAssetAtPath<Material>(passMaterialAssetPath);
        if (passMaterial == null)
            throw new Exception($"No Material found at '{passMaterialAssetPath}'.");

        if (!Enum.TryParse(injectionPoint, out FullScreenPassRendererFeature.InjectionPoint parsedInjectionPoint))
            throw new Exception($"Invalid injectionPoint '{injectionPoint}'. Expected BeforeRenderingTransparents, BeforeRenderingPostProcessing, or AfterRenderingPostProcessing.");

        var requirements = requiresDepth ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None;

        var existing = rendererData.rendererFeatures
            .OfType<FullScreenPassRendererFeature>()
            .FirstOrDefault(f => f.name == featureName);

        if (existing != null)
        {
            existing.injectionPoint = parsedInjectionPoint;
            existing.fetchColorBuffer = fetchColorBuffer;
            existing.requirements = requirements;
            existing.passMaterial = passMaterial;
            existing.passIndex = passIndex;

            EditorUtility.SetDirty(existing);
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            return $"Updated existing FullScreenPassRendererFeature '{featureName}' on '{rendererDataAssetPath}'.";
        }

        var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = featureName;
        feature.hideFlags |= HideFlags.HideInHierarchy;
        feature.injectionPoint = parsedInjectionPoint;
        feature.fetchColorBuffer = fetchColorBuffer;
        feature.requirements = requirements;
        feature.passMaterial = passMaterial;
        feature.passIndex = passIndex;

        AssetDatabase.AddObjectToAsset(feature, rendererData);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

        var serializedRendererData = new SerializedObject(rendererData);
        var featuresProp = serializedRendererData.FindProperty("m_RendererFeatures");
        var mapProp = serializedRendererData.FindProperty("m_RendererFeatureMap");

        if (featuresProp == null || mapProp == null)
            throw new Exception("Could not find m_RendererFeatures/m_RendererFeatureMap on the UniversalRendererData serialized object.");

        featuresProp.arraySize++;
        featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;

        mapProp.arraySize++;
        mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = localId;

        serializedRendererData.ApplyModifiedProperties();

        rendererData.SetDirty();
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return $"Added FullScreenPassRendererFeature '{featureName}' to '{rendererDataAssetPath}' using material '{passMaterialAssetPath}'.";
    }
}
