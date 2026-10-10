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
        // > 0 switches to a CONSTANT SCREEN-SPACE width in pixels (tight, and the same thickness at any zoom). 0 = the world-unit width above.
        _OutlinePixels ("Outline Width (pixels, 0 = use world width)", Range(0, 12)) = 0
        // Fragments below this world height are discarded (geometry under the floor must not show through it). Default = off.
        _OutlineClipY ("Clip below world Y", Float) = -10000
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
                float _OutlinePixels;
                float _OutlineClipY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; float worldY : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS   = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.worldY = posWS.y;
                if (_OutlinePixels > 0.001)
                {
                    // Push the vertex outward by a fixed number of PIXELS: find which way the normal points on screen, then offset in clip space.
                    float4 clip  = TransformWorldToHClip(posWS);
                    float4 clipN = TransformWorldToHClip(posWS + nWS * 0.02);
                    float2 dirPx = (clipN.xy / clipN.w - clip.xy / clip.w) * _ScreenParams.xy;
                    dirPx = dirPx / max(length(dirPx), 1e-5);
                    clip.xy += dirPx * (_OutlinePixels * 2.0 / _ScreenParams.xy) * clip.w;
                    OUT.positionCS = clip;
                    return OUT;
                }
                posWS += nWS * _OutlineWidth;
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                clip(IN.worldY - _OutlineClipY);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
