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
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Mask"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
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

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target { return half4(0, 0, 0, 0); }
            ENDHLSL
        }
    }
}
