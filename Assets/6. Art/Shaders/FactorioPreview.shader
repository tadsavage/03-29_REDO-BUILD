Shader "Custom/FactorioPreview"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,0.5)
        _LineColor ("Line Color", Color) = (0.5,0.5,0.5,1)
        _LineWidth ("Line Width", Range(0, 0.05)) = 0.01
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma target 4.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 barycentric : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _LineColor;
                float _LineWidth;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.barycentric = float3(0, 0, 0);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle Varyings input[3], inout TriangleStream<Varyings> stream)
            {
                float3 barycentrics[3] = {
                    float3(1, 0, 0),
                    float3(0, 1, 0),
                    float3(0, 0, 1)
                };

                for (int i = 0; i < 3; i++)
                {
                    Varyings o = input[i];
                    o.barycentric = barycentrics[i];
                    stream.Append(o);
                }
                stream.RestartStrip();
            }

            float4 frag (Varyings i) : SV_Target
            {
                // Simple edge detection based on barycentric coordinates
                float3 d = fwidth(i.barycentric);
                float3 a3 = smoothstep(float3(0,0,0), d * _LineWidth * 100.0, i.barycentric);
                float minBary = min(a3.x, min(a3.y, a3.z));
                
                // Inverse for line
                float lineFactor = 1.0 - minBary;

                float4 finalColor = lerp(_BaseColor, _LineColor, lineFactor);
                
                // Ensure we respect alpha
                finalColor.a = _BaseColor.a + (lineFactor * _LineColor.a);
                finalColor.a = saturate(finalColor.a);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
