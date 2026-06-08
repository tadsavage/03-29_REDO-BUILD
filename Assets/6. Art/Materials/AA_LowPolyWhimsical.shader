Shader "Custom/LowPolyWhimsical"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Shared Texture Palette", 2D) = "white" {}
        _Tint ("Color Tint", Color) = (1,1,1,1)
        
        [NoScaleOffset] _OcclusionMap ("Occlusion Map G", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0.0, 1.0)) = 1.0
        [Toggle] _UseOcclusionMap ("Use Occlusion Map", Float) = 0.0

        // --- URP/Lit compatibility shims (hidden, preserve values when switching shaders) ---
        [HideInInspector] _BaseColor ("Base Color", Color) = (1,1,1,1)
        [HideInInspector] _MetallicGlossMap ("Metallic Gloss Map", 2D) = "white" {}
        [HideInInspector] _SpecGlossMap ("Spec Gloss Map", 2D) = "white" {}
        [HideInInspector] _BumpMap ("Normal Map", 2D) = "bump" {}
        [HideInInspector] _EmissionMap ("Emission Map", 2D) = "white" {}
        [HideInInspector] _DetailMask ("Detail Mask", 2D) = "white" {}
        [HideInInspector] _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0

        _ShadowColor ("Shadow Tint Color", Color) = (0.15, 0.15, 0.3, 1)
        _ShadowStep ("Toon Shadow Cutoff", Range(0.0, 1.0)) = 0.4
        _ShadowFeather ("Toon Shadow Smoothness", Range(0.0, 1.0)) = 0.05
        
        _EdgeIntensity ("Edge Darkening Force", Range(0.0, 2.0)) = 0.8
        _EdgePower ("Edge Sharpness", Range(0.5, 8.0)) = 2.0
        
        [Toggle] _ShinyEdges ("Enable Shiny Edges", Float) = 1.0
        _SpecularColor ("Shiny Edge Color", Color) = (1,1,1,1)
        _SpecularSize ("Shiny Edge Size", Range(0.01, 1.0)) = 0.1
        _SpecularGloss ("Shiny Edge Sharpness", Range(0.01, 0.5)) = 0.02

        [Toggle] _UseRim ("Enable Rim Light", Float) = 1.0
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Sharpness", Range(0.1, 8.0)) = 3.0
        _RimCutoff ("Rim Cutoff Threshold", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                float2 uv1          : TEXCOORD1; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float2 uvAO         : TEXCOORD3;
                float3 viewDirWS    : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _ShadowColor;
                float4 _SpecularColor;
                float4 _RimColor;
                float _OcclusionStrength;
                float _UseOcclusionMap;
                float _ShadowStep;
                float _ShadowFeather;
                float _EdgeIntensity;
                float _EdgePower;
                float _ShinyEdges;
                float _SpecularSize;
                float _SpecularGloss;
                float _UseRim;
                float _RimPower;
                float _RimCutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.uvAO = input.uv1;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _Tint;
                float aoFactor;
                if (_UseOcclusionMap > 0.5)
                {
                    float rawAO = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uvAO).g;
                    aoFactor = lerp(1.0, rawAO, _OcclusionStrength);
                }
                else
                {
                    aoFactor = 1.0;
                }

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);

                float dNL = dot(normalWS, lightDir);
                float shadowFactor = smoothstep(_ShadowStep, _ShadowStep + _ShadowFeather, dNL);
                
                float3 ambient = SampleSH(normalWS) * 0.25; 
                float3 dynamicShadow = _ShadowColor.rgb * (1.0 - shadowFactor);
                float3 dynamicLit = mainLight.color * shadowFactor;
                float3 totalLighting = saturate(dynamicLit + (dynamicShadow * aoFactor) + (ambient * aoFactor));

                float dNV = saturate(dot(normalWS, viewDirWS));
                float edgeFresnel = pow(1.0 - dNV, _EdgePower) * _EdgeIntensity;
                
                float3 enrichedColor = baseColor.rgb * (1.2 - (edgeFresnel * 0.3));
                float3 finalColor = enrichedColor * totalLighting;
                finalColor -= (edgeFresnel * 0.4 * baseColor.rgb * aoFactor);

                if (_ShinyEdges > 0.5 && shadowFactor > 0.1)
                {
                    float3 halfDir = normalize(lightDir + viewDirWS);
                    float dNH = saturate(dot(normalWS, halfDir));
                    
                    float specularCutoff = 1.0 - _SpecularSize;
                    float specHighlight = smoothstep(specularCutoff, specularCutoff + _SpecularGloss, dNH);
                    
                    finalColor += _SpecularColor.rgb * specHighlight * mainLight.color * aoFactor;
                }

                if (_UseRim > 0.5)
                {
                    float rimDot = 1.0 - dNV;
                    float rimIntensity = pow(rimDot, _RimPower);
                    rimIntensity *= smoothstep(_RimCutoff - 0.05, _RimCutoff + 0.05, dNL);
                    
                    finalColor += _RimColor.rgb * rimIntensity * mainLight.color * aoFactor;
                }

                return half4(saturate(finalColor), baseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
