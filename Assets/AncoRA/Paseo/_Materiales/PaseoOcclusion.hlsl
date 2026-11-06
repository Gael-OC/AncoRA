// Only measured, confident foreground pixels can cut the model. No dilation or hole filling.
float _PaseoOcclusionOn;
TEXTURE2D(_PaseoOcclusionDepth);
SAMPLER(sampler_PaseoOcclusionDepth);
float4x4 _PaseoOcclusionDisplay;

void PaseoClipForeground(float4 screenPosition, float eyeDepth)
{
    if (_PaseoOcclusionOn < 0.5)
        return;
    float2 screenUV = screenPosition.xy / screenPosition.w;
    // ARCore supplies a row-major display transform, as used by its camera background.
    float2 uv = mul(float4(screenUV, 1.0, 0.0), _PaseoOcclusionDisplay).xy;
    if (any(uv < 0.0) || any(uv > 1.0))
        return;
    float realDepth = SAMPLE_TEXTURE2D_LOD(_PaseoOcclusionDepth, sampler_PaseoOcclusionDepth, uv, 0).r;
    if (realDepth > 0.0 && realDepth + 0.25 < eyeDepth)
        discard;
}
