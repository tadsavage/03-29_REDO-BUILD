Shader "Custom/FactorioPreview"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,0.5)
        _LineColor ("Line Color", Color) = (0.4,0.4,0.4,1)
        _LineWidth ("Line Width", Range(0, 0.1)) = 0.02
        _Chalkiness ("Chalkiness", Range(0, 1)) = 0.5
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
                float3 localPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _LineColor;
                float _LineWidth;
                float _Chalkiness;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.localPos = v.positionOS.xyz;
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

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + .1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float4 frag (Varyings i) : SV_Target
            {
                // Use local position for the noise to keep it static relative to the object
                float noise = hash(i.localPos * 20.0 + i.barycentric * 10.0);
                
                // Modulate line width with noise
                float jitteredWidth = _LineWidth * (1.0 + (noise - 0.5) * _Chalkiness * 2.0);
                
                // Simple edge detection based on barycentric coordinates
                float3 d = fwidth(i.barycentric);
                
                // Sharper transition but influenced by jitter
                float3 a3 = smoothstep(float3(0,0,0), d * jitteredWidth * 50.0, i.barycentric);
                float minBary = min(a3.x, min(a3.y, a3.z));
                
                // Inverse for line
                float lineFactor = 1.0 - minBary;
                
                // Add "breakup" to the line alpha based on chalkiness
                float breakup = saturate(1.0 - (noise * _Chalkiness * 0.8));
                lineFactor *= breakup;

                // Boost for visibility
                lineFactor = pow(lineFactor, 0.8);

                float4 finalColor = lerp(_BaseColor, _LineColor, lineFactor);
                
                // Final alpha calculation
                finalColor.a = _BaseColor.a + (lineFactor * _LineColor.a * 0.7);
                finalColor.a = saturate(finalColor.a);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
