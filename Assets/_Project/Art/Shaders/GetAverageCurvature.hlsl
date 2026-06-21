float2 SampleSceneNormalBuffer(float2 uv, float3x3 viewMatrix)
{
    float3 normalViewSpace = float3(0.0, 0.0, 0.0);
    float3 normalWorldSpace = SHADERGRAPH_SAMPLE_SCENE_NORMAL(uv);
    normalViewSpace = mul(viewMatrix, normalWorldSpace);
    return normalViewSpace.xy;
}

float CalculateCurvature(float2 left, float2 right, float2 down, float2 up, float exponent, float multiplier)
{
    float resultX = left.x - right.x;
    float resultY = up.y - down.y;
    float totalResult = resultX + resultY;

    float curvature = 0.5 + sign(totalResult) * pow(abs(totalResult * multiplier), exponent);
    return clamp(curvature, 0.0, 1.0);
}

float GetCurvatureAtPoint(float2 uv, float exponent, float multiplier, float3x3 viewMatrix)
{
    float2 leftRight = float2(1.0 / _ScreenParams.x, 0.0);
    float2 upDown = float2(0.0, 1.0 / _ScreenParams.y);

    float2 left = SampleSceneNormalBuffer(uv + leftRight, viewMatrix);
    float2 right = SampleSceneNormalBuffer(uv - leftRight, viewMatrix);
    float2 down = SampleSceneNormalBuffer(uv - upDown, viewMatrix);
    float2 up = SampleSceneNormalBuffer(uv + upDown, viewMatrix);

    return CalculateCurvature(left, right, down, up, exponent, multiplier);
}

// Reverted to clean original 6-parameter template to match your graph inputs completely
void GetAverageCurvature_float(float2 screenPosition, float radius, float exponent, float multiplier, float sharpness, out float curvature)
{
    // Force initialize immediately to appease the D3D11 back-end pipeline compiler
    curvature = 0.0;

    float3x3 viewMatrix = (float3x3) UNITY_MATRIX_V;
    float totalWeight = 0.0;
    int r = (int) radius;

    sharpness = clamp(1.0 - sharpness, 0.0001, 1.0);

    for (int i = -r; i <= r; i++)
    {
        for (int j = -r; j <= r; j++)
        {
            float2 pixelOffset = float2((float) i, (float) j);
            float2 uvOffset = pixelOffset / _ScreenParams.xy;
            float weight = 1.0 / (dot(pixelOffset, pixelOffset) + sharpness);
            totalWeight += weight;
            curvature += weight * GetCurvatureAtPoint(screenPosition + uvOffset, exponent, multiplier, viewMatrix);
        }
    }

    if (totalWeight > 0.0)
    {
        curvature /= totalWeight;
    }

    // Remap 0.5 neutral to 1.0 (Multiply-neutral)
    // 0.0 (concave) -> 0.0 (darkens)
    // 0.5 (flat)    -> 1.0 (no change)
    // 1.0 (convex)  -> 2.0 (brightens)
    curvature *= 2.0;
}
