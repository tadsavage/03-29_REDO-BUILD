// Cavitoon Lite: world-space cavity only.
Shader "Hidden/Cavitoon/Cavity"
{
    Properties
    {
        [Header(World Space)]
        [Min(0.0)] _WorldRadius("Radius", Float) = 0.25
        [Range(0.0, 5.0)] _WorldRidge("Ridge", Float) = 2.5
        [Range(0.0, 5.0)] _WorldValley("Valley", Float) = 1.0
        [Min(0.0)] _WorldAttenuation("Attenuation", Float) = 1.0
        [Enum(Four, 4, Eight, 8, Sixteen, 16)] _WorldSampleCount("Samples", Int) = 16
        [Min(0.0)] _MaxWorldPixelRadius("Maximum Pixel Radius", Float) = 24.0
        [HideInInspector] _ReferenceResolutionHeight("Reference Resolution Height", Float) = 1080.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Cavity"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _WorldRadius;
                float _WorldRidge;
                float _WorldValley;
                float _WorldAttenuation;
                int _WorldSampleCount;
                float _MaxWorldPixelRadius;
                float _ReferenceResolutionHeight;
            CBUFFER_END

            bool IsOutsideViewport(float2 uv)
            {
                return any(uv < 0.0) || any(uv > 1.0);
            }

            bool IsBackgroundDepth(float rawDepth)
            {
                // Point-sampled clear depth is exact. An epsilon would incorrectly
                // classify distant geometry as sky on reversed-Z platforms.
                return rawDepth == UNITY_RAW_FAR_CLIP_VALUE;
            }

            float DeviceDepthFromRaw(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth;
                #else
                    return lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
            }

            float3 ReconstructViewPosition(float2 uv, float rawDepth)
            {
                float deviceDepth = DeviceDepthFromRaw(rawDepth);
                float4 positionCS = ComputeClipSpacePosition(uv, deviceDepth);
                float4 positionVS = mul(UNITY_MATRIX_I_P, positionCS);
                // Raw inverse projection stays in the same view-space convention
                // as normals transformed by UNITY_MATRIX_V.
                return positionVS.xyz / positionVS.w;
            }

            float3 SampleViewNormal(float2 uv)
            {
                float3 normalWS = SampleSceneNormals(uv);
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                float lengthSquared = dot(normalVS, normalVS);

                // A custom shader without a valid DepthNormals pass can leave the
                // normals target cleared. Returning zero avoids normalize(0) NaNs.
                if (lengthSquared < 1e-6)
                    return 0.0;

                return normalVS * rsqrt(lengthSquared);
            }

            float2 Rotate(float2 value, float2 complexRotation)
            {
                return float2(
                    value.x * complexRotation.x - value.y * complexRotation.y,
                    value.x * complexRotation.y + value.y * complexRotation.x);
            }

            float2 GetWorldKernelStartDirection(int sampleCount)
            {
                if (sampleCount < 6)
                    return float2(0.707106781, 0.707106781); // 45 degrees.
                if (sampleCount < 12)
                    return float2(0.923879533, 0.382683432); // 22.5 degrees.
                return float2(0.980785280, 0.195090322); // 11.25 degrees.
            }

            float2 GetWorldKernelCrossRotation(int sampleCount)
            {
                if (sampleCount < 6)
                    return float2(1.0, 0.0);
                if (sampleCount < 12)
                    return float2(0.707106781, 0.707106781); // 45 degrees.
                return float2(0.923879533, 0.382683432); // 22.5 degrees.
            }

            float GetWorldKernelRadiusNormalization(int sampleCount)
            {
                if (sampleCount < 6)
                    return 0.577350269; // 1 / sqrt(3).
                if (sampleCount < 12)
                    return 0.258198890; // 1 / sqrt(15).
                return 0.125988158; // 1 / sqrt(63).
            }

            float ResolutionScaleFromTexelSize(float4 texelSize)
            {
                float currentHeight = max(abs(texelSize.w), 1.0);
                float referenceHeight = max(_ReferenceResolutionHeight, 1.0);
                return currentHeight / referenceHeight;
            }

            int GetWorldSampleCount()
            {
                if (_WorldSampleCount < 6)
                    return 4;
                if (_WorldSampleCount < 12)
                    return 8;
                return 16;
            }

            float2 EvaluateWorldCavity(
                float2 uv,
                float centerRawDepth,
                float3 centerPositionVS,
                float3 centerNormalVS,
                out float resolutionFade)
            {
                resolutionFade = 0.0;
                if (_WorldRadius <= 0.0 || (_WorldRidge <= 0.0 && _WorldValley <= 0.0))
                    return 0.0;

                float perspectiveScale = rcp(max(abs(centerPositionVS.z), 1e-4));
                float depthScale = lerp(perspectiveScale, 1.0, unity_OrthoParams.w);
                float2 projectionScale = 0.5 * abs(float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11));
                float2 radiusUV = _WorldRadius * projectionScale * depthScale;

                // Scale both the projected and world radius when limiting close-up kernels,
                // so depth reconstruction and attenuation remain self-consistent.
                float2 texelSize = max(abs(_CameraDepthTexture_TexelSize.xy), 1e-7);
                float projectedPixelRadius = max(
                    radiusUV.x / texelSize.x,
                    radiusUV.y / texelSize.y);
                float radiusScale = 1.0;
                if (_MaxWorldPixelRadius > 0.0)
                {
                    float maxWorldPixelRadius =
                        _MaxWorldPixelRadius * ResolutionScaleFromTexelSize(_CameraDepthTexture_TexelSize);
                    radiusScale = min(1.0, maxWorldPixelRadius / max(projectedPixelRadius, 1e-4));
                }

                radiusUV *= radiusScale;
                float effectiveWorldRadius = _WorldRadius * radiusScale;
                float effectiveProjectedPixelRadius = projectedPixelRadius * radiusScale;
                resolutionFade = smoothstep(0.75, 2.0, effectiveProjectedPixelRadius);
                if (resolutionFade <= 0.0)
                    return 0.0;

                int sampleCount = GetWorldSampleCount();
                // Every ring is a rotated four-arm cross (+d, +perp, -d, -perp).
                // Its zero centroid and isotropic footprint reduce camera-motion crawl.
                float2 crossDirection = GetWorldKernelStartDirection(sampleCount);
                float2 crossRotation = GetWorldKernelCrossRotation(sampleCount);
                float radiusNormalization = GetWorldKernelRadiusNormalization(sampleCount);

                float valleyAccumulator = 0.0;
                float ridgeAccumulator = 0.0;
                float sampleWeightAccumulator = 0.0;

                [loop]
                for (int sampleIndex = 0; sampleIndex < 16; ++sampleIndex)
                {
                    if (sampleIndex >= sampleCount)
                        break;

                    int crossIndex = sampleIndex >> 2;
                    int crossArm = sampleIndex & 3;
                    float diskRadius =
                        ((float)(crossIndex * 2) + 1.0) * radiusNormalization;

                    float2 armDirection = crossDirection;
                    if (crossArm == 1)
                        armDirection = float2(-crossDirection.y, crossDirection.x);
                    else if (crossArm == 2)
                        armDirection = -crossDirection;
                    else if (crossArm == 3)
                        armDirection = float2(crossDirection.y, -crossDirection.x);

                    float2 sampleOffsetUV = armDirection * diskRadius * radiusUV;
                    float2 sampleOffsetPixels = abs(sampleOffsetUV) / texelSize;
                    float tapReachPixels = length(sampleOffsetPixels);

                    // A point-depth tap below half a pixel contains no new spatial
                    // information. Fade it in up to one pixel to avoid binary texel jumps.
                    float tapWeight = smoothstep(0.5, 1.0, tapReachPixels);
                    sampleWeightAccumulator += tapWeight;

                    float2 sampleUV = uv + sampleOffsetUV;

                    if (tapWeight > 0.0 && !IsOutsideViewport(sampleUV))
                    {
                        float sampleRawDepth = SampleSceneDepth(sampleUV);
                        float3 samplePositionVS;

                        if (IsBackgroundDepth(sampleRawDepth))
                        {
                            // Retain a bright world-space ridge at visible silhouettes.
                            samplePositionVS = ReconstructViewPosition(sampleUV, centerRawDepth);
                            samplePositionVS.z -= effectiveWorldRadius;
                        }
                        else
                        {
                            samplePositionVS = ReconstructViewPosition(sampleUV, sampleRawDepth);
                        }

                        float3 directionVS = samplePositionVS - centerPositionVS;
                        float distanceToSample = max(length(directionVS), 1e-4);
                        float signedPlaneDistance = dot(directionVS, centerNormalVS);
                        float bias = 0.05 * distanceToSample + 1e-4;
                        float attenuation = rcp(
                            distanceToSample *
                            (1.0 + distanceToSample * distanceToSample * max(_WorldAttenuation, 0.0)) +
                            1e-4);

                        if (signedPlaneDistance > -bias)
                            valleyAccumulator += signedPlaneDistance * attenuation * tapWeight;

                        float ridgeDistance = -signedPlaneDistance;
                        if (ridgeDistance > bias)
                            ridgeAccumulator += ridgeDistance * attenuation * tapWeight;
                    }

                    if (crossArm == 3)
                        crossDirection = Rotate(crossDirection, crossRotation);
                }

                float inverseSampleWeight = rcp(max(sampleWeightAccumulator, 1e-4));
                float valley = saturate(
                    valleyAccumulator * inverseSampleWeight * max(_WorldValley, 0.0));
                float ridge = max(
                    ridgeAccumulator * inverseSampleWeight * max(_WorldRidge, 0.0),
                    0.0);
                return float2(ridge, valley);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 sourceUV = ClampAndScaleUVForBilinear(
                    UnityStereoTransformScreenSpaceTex(uv),
                    _BlitTexture_TexelSize.xy);
                half4 sourceColor = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture, sampler_LinearClamp, sourceUV, 0.0);
                float centerRawDepth = SampleSceneDepth(uv);

                if (IsBackgroundDepth(centerRawDepth))
                    return sourceColor;

                float3 centerPositionVS = ReconstructViewPosition(uv, centerRawDepth);
                float3 centerNormalVS = SampleViewNormal(uv);
                if (dot(centerNormalVS, centerNormalVS) < 0.5)
                    return sourceColor;

                float resolutionFade;
                float2 worldResponse = EvaluateWorldCavity(
                    uv,
                    centerRawDepth,
                    centerPositionVS,
                    centerNormalVS,
                    resolutionFade);

                // Lite uses the classic monochrome Multiply response:
                // Valley darkens and Ridge brightens the original camera color.
                float worldMultiplier = clamp(
                    (1.0 - worldResponse.y) * (1.0 + worldResponse.x),
                    0.0,
                    4.0);
                float cavityMultiplier = lerp(1.0, worldMultiplier, resolutionFade);

                return half4(sourceColor.rgb * cavityMultiplier, sourceColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
