Shader "Hidden/EmployeeOutlineMask"
{
    // Pass 1 of the see-through employee outline (see EmployeeHighlighter.cs).
    // Stamps the body footprint into the stencil buffer only (ColorMask 0). ZTest Always so
    // the silhouette is marked even when the employee is hidden behind walls/racks.
    //
    // Ordering vs. the fill is guaranteed by RENDER QUEUE, not pass order: the highlighter
    // sets this material's renderQueue below the fill material's, so the mask always draws
    // first within the transparent phase. (Two passes in one material would render in an
    // order URP does not guarantee, which is why this is split into two shaders.)
    Properties
    {
        // Fragments below this world height are discarded, so geometry under the floor (e.g. the reach truck's mast) never shows through it.
        _OutlineClipY ("Clip below world Y", Float) = -10000
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Mask"
            Tags { "LightMode"="UniversalForward" }

            Cull Off          // both faces: gaps the body can be seen through (parts hidden by a garment) still count as silhouette, so no rim lines appear inside it
            ZWrite Off
            ZTest Always
            ColorMask 0

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _OutlineClipY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                clip(IN.positionWS.y - _OutlineClipY);
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
