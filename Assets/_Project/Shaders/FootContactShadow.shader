// Soft, round "contact shadow" blob drawn on a flat quad lying on the ground under a character's foot/body.
// The falloff is procedural (no texture), so one tiny material serves every blob; per-blob strength comes from
// the _Alpha property (set by FootContactShadow through a MaterialPropertyBlock, so it fades as a foot lifts).
Shader "Hidden/_Project/FootContactShadow"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0, 0, 0, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.5
        _Softness ("Edge Softness (higher = tighter core)", Range(0.5, 6)) = 2.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ContactShadow"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -2, -2   // sit just above the floor without z-fighting

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
                half _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;
                float d = saturate(1.0 - dot(p, p));          // 1 at the centre, 0 at the edge of the circle
                float a = pow(d, _Softness) * _Alpha;          // soft round falloff
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
