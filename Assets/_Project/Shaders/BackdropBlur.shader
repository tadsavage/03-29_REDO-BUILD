// Full-screen depth-based blur used to soften the distant mountain backdrop far beyond
// URP's built-in Depth of Field radius caps (Gaussian: 2px scaled, Bokeh: hardcoded 14px).
// Driven by a FullScreenPassRendererFeature so it runs as a single extra blit pass.
Shader "Hidden/_Project/BackdropBlur"
{
    Properties
    {
        // World-space distance (from camera) where the blur starts ramping in.
        _BlurStart ("Blur Start Distance", Float) = 68
        // World-space distance (from camera) where the blur reaches full strength.
        _BlurEnd ("Blur End Distance", Float) = 78
        // Maximum blur radius in screen pixels at full strength. Not clamped by URP internals,
        // so this can go far beyond the ~14px ceiling built into URP's Bokeh/Gaussian DoF passes.
        _MaxBlurRadiusPixels ("Max Blur Radius (Pixels)", Range(0, 400)) = 140
    }

    HLSLINCLUDE

        #pragma target 3.5

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _BlurStart;
        float _BlurEnd;
        float _MaxBlurRadiusPixels;

        // Vogel (golden-angle) disk sampling pattern tap count. Higher = smoother blur at
        // higher cost; this runs once per pixel in a single full-screen pass.
        #define BACKDROP_BLUR_SAMPLES 24
        #define GOLDEN_ANGLE 2.39996323

        half4 FragBackdropBlur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

            half4 baseColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

            float rawDepth = SampleSceneDepth(uv);
            float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

            half t = saturate((eyeDepth - _BlurStart) / max(_BlurEnd - _BlurStart, 1e-4));

            UNITY_BRANCH
            if (t <= 0.0)
                return baseColor;

            float radiusPixels = t * _MaxBlurRadiusPixels;
            float2 texelSize = _BlitTexture_TexelSize.xy;

            half4 acc = 0.0;

            UNITY_UNROLL
            for (int i = 0; i < BACKDROP_BLUR_SAMPLES; i++)
            {
                float fi = (float)i;
                float theta = fi * GOLDEN_ANGLE;
                float r = sqrt((fi + 0.5) / BACKDROP_BLUR_SAMPLES);
                float2 dir = float2(cos(theta), sin(theta));
                float2 sampleUV = uv + dir * r * radiusPixels * texelSize;
                acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
            }
            acc *= (1.0 / BACKDROP_BLUR_SAMPLES);

            return lerp(baseColor, acc, t);
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Backdrop Blur"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBackdropBlur
            ENDHLSL
        }
    }
}
