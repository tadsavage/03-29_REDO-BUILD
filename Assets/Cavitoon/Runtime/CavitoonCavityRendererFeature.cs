using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Cavitoon
{
    /// <summary>
    /// Applies the Cavitoon Lite world-space cavity multiplier as a full-screen URP Render Graph pass.
    /// </summary>
    public sealed class CavitoonCavityRendererFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "Hidden/Cavitoon/Cavity";

        public enum InjectionPoint
        {
            BeforeRenderingTransparents = (int)RenderPassEvent.BeforeRenderingTransparents,
            BeforeRenderingPostProcessing = (int)RenderPassEvent.BeforeRenderingPostProcessing,
            AfterRenderingPostProcessing = (int)RenderPassEvent.AfterRenderingPostProcessing
        }

        public enum WorldSampleCount
        {
            Four = 4,
            Eight = 8,
            Sixteen = 16
        }

        [Serializable]
        public sealed class Settings
        {
            [Min(0.001f)]
            [Tooltip("Sampling radius in world units.")]
            public float worldRadius = 0.25f;

            [Range(0.0f, 5.0f)]
            [Tooltip("Brightness multiplier applied to convex world-space ridges.")]
            public float worldRidge = 2.5f;

            [Range(0.0f, 5.0f)]
            [Tooltip("Darkening multiplier applied to concave world-space valleys.")]
            public float worldValley = 1.0f;

            [Min(0.0f)]
            [Tooltip("Reduces world-space cavity as samples move away from the center pixel.")]
            public float worldAttenuation = 1.0f;

            [Tooltip("Number of radial samples used by the world-space cavity filter.")]
            public WorldSampleCount worldSampleCount = WorldSampleCount.Sixteen;

            [Min(0.0f)]
            [Tooltip("Upper screen-space limit for the projected world radius.")]
            public float maxWorldPixelRadius = 24.0f;

            [Tooltip("Point in the URP frame at which the cavity multiplier is applied.")]
            public InjectionPoint injectionPoint = InjectionPoint.BeforeRenderingTransparents;

            [Tooltip("Also render the effect in Unity's Scene view.")]
            public bool showInSceneView = true;
        }

        [SerializeField]
        private Settings m_Settings = new Settings();

        [SerializeField]
        [Tooltip("Shader used by the full-screen cavity pass. Keeping this reference prevents build stripping.")]
        private Shader m_Shader;

        private Material m_Material;
        private CavityRenderPass m_Pass;
        private bool m_MissingShaderWarningIssued;

        public Settings settings => m_Settings;

        public override void Create()
        {
            if (m_Settings == null)
                m_Settings = new Settings();

            if (m_Pass == null)
                m_Pass = new CavityRenderPass();

            if (m_Shader == null)
                m_Shader = Shader.Find(ShaderName);

            if (m_Material != null && m_Material.shader != m_Shader)
            {
                CoreUtils.Destroy(m_Material);
                m_Material = null;
            }

            TryCreateMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType cameraType = renderingData.cameraData.cameraType;
            bool isGameCamera = cameraType == CameraType.Game;
            bool isEnabledSceneView = m_Settings.showInSceneView && cameraType == CameraType.SceneView;

            if (!isGameCamera && !isEnabledSceneView)
                return;

            if (!HasVisibleEffect(m_Settings))
                return;

            if (!TryCreateMaterial() || m_Material.passCount == 0)
                return;

            m_Pass.Setup(m_Material, m_Settings);
            renderer.EnqueuePass(m_Pass);
        }

        private static bool HasVisibleEffect(Settings settings)
        {
            return settings.worldRadius > 0.0f &&
                   (settings.worldRidge > 0.0f || settings.worldValley > 0.0f);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass = null;
            CoreUtils.Destroy(m_Material);
            m_Material = null;
            m_MissingShaderWarningIssued = false;
        }

        private bool TryCreateMaterial()
        {
            if (m_Material != null)
                return true;

            Shader shader = m_Shader;
            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
                m_Shader = shader;
            }

            if (shader == null || !shader.isSupported)
            {
                if (!m_MissingShaderWarningIssued)
                {
                    Debug.LogWarning(
                        $"{nameof(CavitoonCavityRendererFeature)} could not find a supported shader named " +
                        $"\"{ShaderName}\". The cavity pass will be skipped.",
                        this);
                    m_MissingShaderWarningIssued = true;
                }

                return false;
            }

            m_Material = CoreUtils.CreateEngineMaterial(shader);
            m_MissingShaderWarningIssued = false;
            return m_Material != null;
        }

        private sealed class CavityRenderPass : ScriptableRenderPass
        {
            private const string MainPassName = "Cavitoon Cavity";
            private const string CopyPassName = "Cavitoon Copy Color";
            private const string ColorCopyName = "_CavitoonColorCopy";

            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitTextureTexelSizeId = Shader.PropertyToID("_BlitTexture_TexelSize");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int WorldRadiusId = Shader.PropertyToID("_WorldRadius");
            private static readonly int WorldRidgeId = Shader.PropertyToID("_WorldRidge");
            private static readonly int WorldValleyId = Shader.PropertyToID("_WorldValley");
            private static readonly int WorldAttenuationId = Shader.PropertyToID("_WorldAttenuation");
            private static readonly int WorldSampleCountId = Shader.PropertyToID("_WorldSampleCount");
            private static readonly int MaxWorldPixelRadiusId = Shader.PropertyToID("_MaxWorldPixelRadius");

            private static readonly Vector4 BlitScaleBias = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
            private static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();

            private Material m_Material;
            private ShaderParameters m_Parameters;
            private bool m_BackBufferWarningIssued;
            private bool m_MissingInputsWarningIssued;

            public CavityRenderPass()
            {
                profilingSampler = new ProfilingSampler(MainPassName);
                requiresIntermediateTexture = true;
            }

            public void Setup(Material material, Settings settings)
            {
                m_Material = material;
                m_Parameters = ShaderParameters.From(settings);
                renderPassEvent = (RenderPassEvent)settings.injectionPoint;
                requiresIntermediateTexture = true;

                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_Material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                if (resourceData.isActiveTargetBackBuffer)
                {
                    if (!m_BackBufferWarningIssued)
                    {
                        Debug.LogWarning(
                            $"{nameof(CavitoonCavityRendererFeature)} requires an intermediate camera color " +
                            "texture and cannot read directly from the back buffer. The pass was skipped.");
                        m_BackBufferWarningIssued = true;
                    }

                    return;
                }

                TextureHandle cameraColor = resourceData.activeColorTexture;
                TextureHandle cameraDepth = resourceData.cameraDepthTexture;
                TextureHandle cameraNormals = resourceData.cameraNormalsTexture;

                if (!cameraColor.IsValid() || !cameraDepth.IsValid() || !cameraNormals.IsValid())
                {
                    if (!m_MissingInputsWarningIssued)
                    {
                        Debug.LogWarning(
                            $"{nameof(CavitoonCavityRendererFeature)} requires valid camera color, depth, " +
                            "and normals textures. The pass was skipped.");
                        m_MissingInputsWarningIssued = true;
                    }

                    return;
                }

                m_BackBufferWarningIssued = false;
                m_MissingInputsWarningIssued = false;

                TextureDesc copyDescriptor = renderGraph.GetTextureDesc(cameraColor);
                copyDescriptor.name = ColorCopyName;
                copyDescriptor.clearBuffer = false;

                TextureHandle colorCopy = renderGraph.CreateTexture(copyDescriptor);
                renderGraph.AddBlitPass(
                    cameraColor,
                    colorCopy,
                    Vector2.one,
                    Vector2.zero,
                    passName: CopyPassName);

                using (IRasterRenderGraphBuilder builder =
                       renderGraph.AddRasterRenderPass<PassData>(MainPassName, out PassData passData, profilingSampler))
                {
                    passData.material = m_Material;
                    passData.source = colorCopy;
                    passData.parameters = m_Parameters;

                    builder.UseTexture(colorCopy, AccessFlags.Read);
                    builder.UseTexture(cameraDepth, AccessFlags.Read);
                    builder.UseTexture(cameraNormals, AccessFlags.Read);
                    builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        ExecuteMainPass(
                            context.cmd,
                            data.source,
                            data.material,
                            data.parameters);
                    });
                }
            }

            private static void ExecuteMainPass(
                RasterCommandBuffer commandBuffer,
                RTHandle source,
                Material material,
                ShaderParameters parameters)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTextureId, source);
                SetTextureSize(SharedPropertyBlock, BlitTextureTexelSizeId, source);
                SharedPropertyBlock.SetVector(BlitScaleBiasId, BlitScaleBias);
                SharedPropertyBlock.SetFloat(WorldRadiusId, parameters.worldRadius);
                SharedPropertyBlock.SetFloat(WorldRidgeId, parameters.worldRidge);
                SharedPropertyBlock.SetFloat(WorldValleyId, parameters.worldValley);
                SharedPropertyBlock.SetFloat(WorldAttenuationId, parameters.worldAttenuation);
                SharedPropertyBlock.SetInteger(WorldSampleCountId, parameters.worldSampleCount);
                SharedPropertyBlock.SetFloat(MaxWorldPixelRadiusId, parameters.maxWorldPixelRadius);

                commandBuffer.DrawProcedural(
                    Matrix4x4.identity,
                    material,
                    0,
                    MeshTopology.Triangles,
                    3,
                    1,
                    SharedPropertyBlock);
            }

            private static void SetTextureSize(
                MaterialPropertyBlock propertyBlock,
                int propertyId,
                RTHandle textureHandle)
            {
                RenderTexture texture = textureHandle.rt;
                if (texture == null)
                    return;

                propertyBlock.SetVector(
                    propertyId,
                    new Vector4(
                        1.0f / texture.width,
                        1.0f / texture.height,
                        texture.width,
                        texture.height));
            }

            private sealed class PassData
            {
                public Material material;
                public TextureHandle source;
                public ShaderParameters parameters;
            }

            private struct ShaderParameters
            {
                public float worldRadius;
                public float worldRidge;
                public float worldValley;
                public float worldAttenuation;
                public int worldSampleCount;
                public float maxWorldPixelRadius;

                public static ShaderParameters From(Settings settings)
                {
                    return new ShaderParameters
                    {
                        worldRadius = Mathf.Max(0.001f, settings.worldRadius),
                        worldRidge = Mathf.Max(0.0f, settings.worldRidge),
                        worldValley = Mathf.Max(0.0f, settings.worldValley),
                        worldAttenuation = Mathf.Max(0.0f, settings.worldAttenuation),
                        worldSampleCount = (int)settings.worldSampleCount,
                        maxWorldPixelRadius = Mathf.Max(0.0f, settings.maxWorldPixelRadius)
                    };
                }
            }
        }
    }
}
