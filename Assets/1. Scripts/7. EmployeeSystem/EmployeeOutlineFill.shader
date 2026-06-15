Shader "Hidden/EmployeeOutlineFill"
{
    // Pass 2 of the see-through employee outline (see EmployeeHighlighter.cs).
    // Inverted hull (Cull Front) extruded along normals, drawn ONLY where the mask did NOT
    // write the stencil (Comp NotEqual). That turns the expanded hull into a clean rim around
    // the silhouette instead of a solid blob. ZTest Always = visible through meshes.
    //
    // Colour + thickness are set per-material by EmployeeHighlighter. Its renderQueue is set
    // above the mask material's so the mask is guaranteed to have run first.
    Properties
    {
        // Default = UI accent blue #5C9BC4 (rgb 92,155,196).
        _OutlineColor ("Outline Color", Color) = (0.361, 0.608, 0.769, 1)
        _OutlineWidth ("Outline Width (world units)", Range(0, 0.2)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Fill"
            Tags { "LightMode"="UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest Always

            Stencil
            {
                Ref 1
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(IN.normalOS);
                posWS += nWS * _OutlineWidth;
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
