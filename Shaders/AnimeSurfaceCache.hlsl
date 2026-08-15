#ifndef ANIMEGRESS_SURFACE_CACHE_INCLUDED
#define ANIMEGRESS_SURFACE_CACHE_INCLUDED

TEXTURE2D(_AnimeSurfaceCacheColorTexture);
SAMPLER(sampler_AnimeSurfaceCacheColorTexture);
TEXTURE2D(_AnimeSurfaceCacheDataTexture);
SAMPLER(sampler_AnimeSurfaceCacheDataTexture);
TEXTURE2D(_AnimeSurfaceCacheMaskTexture);
SAMPLER(sampler_AnimeSurfaceCacheMaskTexture);

float4 _AnimeSurfaceCacheWorldToUV;
float4 _AnimeSurfaceCacheHeightParams;
float4 _AnimeSurfaceCacheTexelSize;
float _AnimeSurfaceCacheEnabled;

struct AnimeSurfaceCacheSample
{
    half3 color;
    half3 normalWS;
    half height01;
    half4 masks;
    half valid;
};

float2 AnimeSurfaceCacheWorldToUV(float3 positionWS)
{
    return positionWS.xz * _AnimeSurfaceCacheWorldToUV.xy + _AnimeSurfaceCacheWorldToUV.zw;
}

half AnimeSurfaceCacheContainsUV(float2 uv)
{
    half2 insideMin = step(0.0, uv);
    half2 insideMax = step(uv, 1.0);
    return insideMin.x * insideMin.y * insideMax.x * insideMax.y;
}

AnimeSurfaceCacheSample SampleAnimeSurfaceCache(float2 uv, half inside)
{
    AnimeSurfaceCacheSample surface;
    half4 colorSample = SAMPLE_TEXTURE2D(
        _AnimeSurfaceCacheColorTexture,
        sampler_AnimeSurfaceCacheColorTexture,
        uv);
    half4 dataSample = SAMPLE_TEXTURE2D(
        _AnimeSurfaceCacheDataTexture,
        sampler_AnimeSurfaceCacheDataTexture,
        uv);
    surface.color = colorSample.rgb;
    surface.normalWS = normalize(dataSample.rgb * 2.0h - 1.0h);
    surface.height01 = dataSample.a;
    surface.masks = SAMPLE_TEXTURE2D(
        _AnimeSurfaceCacheMaskTexture,
        sampler_AnimeSurfaceCacheMaskTexture,
        uv);
    surface.valid = saturate(inside * colorSample.a * _AnimeSurfaceCacheEnabled);
    return surface;
}

#endif
