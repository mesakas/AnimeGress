Shader "Hidden/AnimeGrass/Far Field Overlay"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "FarFieldOverlay"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ ENLYN_GRASS_DISABLE_INTERACTION
            #pragma multi_compile _ ENLYN_GRASS_DISABLE_SHADOWS
            #pragma multi_compile _ ENLYN_GRASS_DISABLE_FAR_PATTERN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_AnimeGrassFarCoverageTexture);
            SAMPLER(sampler_AnimeGrassFarCoverageTexture);
            TEXTURE2D(_AnimeGrassFarHeightTexture);
            SAMPLER(sampler_AnimeGrassFarHeightTexture);

            float4 _AnimeGrassFarWorldToUV;
            float4 _AnimeGrassFarHeightParams;
            float4 _AnimeGrassFarDistanceParams;
            float _AnimeGrassFarDistanceMode;
            half4 _AnimeGrassFarAppearanceParams;
            float4 _AnimeGrassFarPatternParams;
            half4 _AnimeGrassFarPatternTint;
            half4 _AnimeGrassFarPatternShadowColor;
            float4 _AnimeGrassFarDisturbanceParams;
            float4 _AnimeGrassFarRippleParams;
            half4 _AnimeGrassFarShadowColor;
            half4 _AnimeGrassFarLightingParams;

            #if !defined(ENLYN_GRASS_DISABLE_INTERACTION)
                #define ENLYN_GRASS_MAX_INTERACTION_VOLUMES 16
                float _EnlynGrassInteractionVolumeCount;
                float4 _EnlynGrassInteractionVolumeCenterShape[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
                float4 _EnlynGrassInteractionVolumeParams[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
                float4 _EnlynGrassInteractionVolumeExclusionParams[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
                float4 _EnlynGrassInteractionVolumeWorldToLocal0[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
                float4 _EnlynGrassInteractionVolumeWorldToLocal1[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
                float4 _EnlynGrassInteractionVolumeWorldToLocal2[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
            #endif

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half IsValidSceneDepth(float depth)
            {
                #if UNITY_REVERSED_Z
                return step(0.00001, depth);
                #else
                return step(depth, 0.99999);
                #endif
            }

            #if !defined(ENLYN_GRASS_DISABLE_FAR_PATTERN)
            float EnlynPatternHash(float2 position)
            {
                float3 value = frac(float3(position.xyx) * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float EnlynPatternNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 local = frac(position);
                local = local * local * (3.0 - 2.0 * local);
                float bottom = lerp(
                    EnlynPatternHash(cell),
                    EnlynPatternHash(cell + float2(1.0, 0.0)),
                    local.x);
                float top = lerp(
                    EnlynPatternHash(cell + float2(0.0, 1.0)),
                    EnlynPatternHash(cell + float2(1.0, 1.0)),
                    local.x);
                return lerp(bottom, top, local.y);
            }
            #endif

            float EnlynFarFieldDither(float2 pixelPosition)
            {
                float2 p = floor(pixelPosition);
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
            }

            #if !defined(ENLYN_GRASS_DISABLE_FAR_PATTERN)
            float EnlynPatternFbm(float2 position)
            {
                float value = EnlynPatternNoise(position) * 0.5;
                position = mul(
                    float2x2(0.8, -0.6, 0.6, 0.8),
                    position) * 2.03 + float2(7.1, 3.7);
                value += EnlynPatternNoise(position) * 0.3;
                position = mul(
                    float2x2(0.6, 0.8, -0.8, 0.6),
                    position) * 2.01 + float2(-2.4, 8.3);
                value += EnlynPatternNoise(position) * 0.2;
                return value;
            }
            #endif

            #if !defined(ENLYN_GRASS_DISABLE_INTERACTION)
            half EnlynFarFieldVolumeExclusion(float3 positionWS)
            {
                half totalExclusion = 0.0h;
                int volumeCount = min(
                    (int)_EnlynGrassInteractionVolumeCount,
                    ENLYN_GRASS_MAX_INTERACTION_VOLUMES);

                [loop]
                for (int volumeIndex = 0; volumeIndex < volumeCount; volumeIndex++)
                {
                    float4 exclusionParams = _EnlynGrassInteractionVolumeExclusionParams[volumeIndex];
                    if (exclusionParams.x <= 0.0001)
                    {
                        continue;
                    }

                    float4 centerShape = _EnlynGrassInteractionVolumeCenterShape[volumeIndex];
                    float4 position = float4(positionWS, 1.0);
                    float3 normalizedPosition = float3(
                        dot(_EnlynGrassInteractionVolumeWorldToLocal0[volumeIndex], position),
                        dot(_EnlynGrassInteractionVolumeWorldToLocal1[volumeIndex], position),
                        dot(_EnlynGrassInteractionVolumeWorldToLocal2[volumeIndex], position)) * 2.0;
                    float normalizedDistance = centerShape.w < 0.5
                        ? length(normalizedPosition)
                        : max(
                            max(abs(normalizedPosition.x), abs(normalizedPosition.y)),
                            abs(normalizedPosition.z));
                    half exclusionWeight = 1.0h - smoothstep(
                        saturate(exclusionParams.y),
                        1.0,
                        normalizedDistance);
                    totalExclusion = max(
                        totalExclusion,
                        exclusionParams.x * exclusionWeight);
                }

                return saturate(totalExclusion);
            }
            #endif

            half4 Frag(Varyings input) : SV_Target
            {
                float rawDepth = SampleSceneDepth(input.uv);
                half validDepth = IsValidSceneDepth(rawDepth);
                float3 positionWS = ComputeWorldSpacePosition(input.uv, rawDepth, UNITY_MATRIX_I_VP);
                float2 coverageUv = positionWS.xz * _AnimeGrassFarWorldToUV.xy
                    + _AnimeGrassFarWorldToUV.zw;
                half2 insideMin = step(0.0, coverageUv);
                half2 insideMax = step(coverageUv, 1.0);
                half inside = insideMin.x * insideMin.y * insideMax.x * insideMax.y;

                half4 coverage = SAMPLE_TEXTURE2D(
                    _AnimeGrassFarCoverageTexture,
                    sampler_AnimeGrassFarCoverageTexture,
                    coverageUv);
                float height01 = SAMPLE_TEXTURE2D(
                    _AnimeGrassFarHeightTexture,
                    sampler_AnimeGrassFarHeightTexture,
                    coverageUv).r / max(coverage.a, 0.0001h);
                float rootHeight = _AnimeGrassFarHeightParams.x
                    + height01 * _AnimeGrassFarHeightParams.y;
                float heightTolerance = max(0.001, _AnimeGrassFarHeightParams.z);
                float surfaceEdgeSoftness = saturate(_AnimeGrassFarHeightParams.w);
                float heightFeather = max(
                    0.001,
                    heightTolerance * surfaceEdgeSoftness);
                float heightDelta = positionWS.y - rootHeight;
                half heightMatch = 1.0h - smoothstep(
                    heightTolerance - heightFeather,
                    heightTolerance,
                    abs(heightDelta));

                // A symmetric height tolerance is useful on uneven ground, but it
                // also lets a projected grass patch bleed down the face below it.
                // Fade that underside over a much shorter, pixel-stable distance.
                float undersideFadeDistance = max(
                    0.05,
                    heightTolerance * lerp(
                        0.08,
                        0.25,
                        surfaceEdgeSoftness));
                float undersideScreenFeather = min(
                    fwidth(heightDelta) * 1.5,
                    undersideFadeDistance * 0.75);
                half undersideMatch = 1.0h - smoothstep(
                    max(0.0, undersideFadeDistance - undersideScreenFeather),
                    undersideFadeDistance + undersideScreenFeather,
                    max(0.0, -heightDelta));
                heightMatch *= undersideMatch;

                float3 coveragePositionWS = float3(positionWS.x, rootHeight, positionWS.z);
                float3 coverageDx = ddx(coveragePositionWS);
                float3 coverageDy = ddy(coveragePositionWS);
                half3 coverageSurfaceNormal = normalize(cross(coverageDy, coverageDx));
                half3 surfaceNormal = normalize(SampleSceneNormals(input.uv));
                half slopeStart = clamp(
                    _AnimeGrassFarAppearanceParams.w,
                    0.5h,
                    0.999h);
                half slopeFeather = max(
                    0.001h,
                    (1.0h - slopeStart) * surfaceEdgeSoftness);
                half upwardAmount = min(
                    abs(surfaceNormal.y),
                    abs(coverageSurfaceNormal.y));
                half upward = smoothstep(
                    slopeStart,
                    min(1.0h, slopeStart + slopeFeather),
                    upwardAmount);
                float3 cameraOffset = _WorldSpaceCameraPos - positionWS;
                float cameraDistance = length(cameraOffset);
                if (_AnimeGrassFarDistanceMode > 1.5)
                {
                    cameraDistance = length(cameraOffset.xz);
                }
                else if (_AnimeGrassFarDistanceMode > 0.5)
                {
                    cameraDistance = length(cameraOffset.xy);
                }
                half farFadeInLinear = saturate(
                    (cameraDistance - _AnimeGrassFarDistanceParams.x)
                    * _AnimeGrassFarDistanceParams.z);
                // Use a stable dithered distance fade instead of alpha blending
                // the near edge. Accepted pixels stay opaque, so the transition
                // can soften without forming a continuous bright terrain band.
                half farFadeIn = step(
                    1.0h - farFadeInLinear,
                    (half)EnlynFarFieldDither(input.positionCS.xy));
                half farFadeOut = saturate(
                    (_AnimeGrassFarDistanceParams.y - cameraDistance)
                    * _AnimeGrassFarDistanceParams.w);

                half patternSignal = 1.0h;
                half breakupMask = 1.0h;
                float warpB = 0.5;
                half patternEnabled = 0.0h;
                half irregularity = 0.0h;
                #if !defined(ENLYN_GRASS_DISABLE_FAR_PATTERN)
                    float2 patternDirection = _AnimeGrassFarPatternParams.xy;
                    patternDirection = dot(patternDirection, patternDirection) > 0.0001
                        ? normalize(patternDirection)
                        : float2(1.0, 0.0);
                    patternEnabled = saturate(_AnimeGrassFarPatternParams.z);
                    float patternTravel = _Time.y * max(0.0, _AnimeGrassFarPatternParams.w);
                    irregularity = saturate(_AnimeGrassFarDisturbanceParams.x);
                    [branch]
                    if (patternEnabled > 0.0001h)
                    {
                        float2 safeWorldToUvScale = max(
                            abs(_AnimeGrassFarWorldToUV.xy),
                            float2(0.000001, 0.000001));
                        float2 coverageCenter = (0.5 - _AnimeGrassFarWorldToUV.zw)
                            / safeWorldToUvScale;
                        float2 localPatternPosition = positionWS.xz - coverageCenter;
                        float2 perpendicularDirection = float2(-patternDirection.y, patternDirection.x);
                        float alongPattern = dot(localPatternPosition, patternDirection);
                        float acrossPattern = dot(localPatternPosition, perpendicularDirection);

                        float noiseScale = max(0.0001, _AnimeGrassFarDisturbanceParams.y);
                        float2 noiseDrift = patternDirection * patternTravel;
                        float2 noisePosition = (localPatternPosition + noiseDrift) * noiseScale;
                        float warpA = EnlynPatternFbm(noisePosition + float2(11.7, -4.3));
                        warpB = EnlynPatternFbm(
                            noisePosition * 0.67
                            + float2(warpA * 2.1, -warpA * 1.6)
                            + float2(-7.2, 13.4));
                        float breakupNoise = EnlynPatternFbm(
                            noisePosition * 0.43
                            - noiseDrift * noiseScale * 0.31
                            + float2(23.1, 5.8));

                        float waveFrequency = max(0.0001, _AnimeGrassFarRippleParams.y);
                        float waveSpacing = 6.28318530718 / waveFrequency;
                        float curveScale = max(1.0, _AnimeGrassFarRippleParams.z);
                        float curveCoordinate = acrossPattern / curveScale;
                        float curveOffset = (
                            sin(curveCoordinate * 1.37 + warpB * 3.1 + 0.4) * 0.42
                            + (warpA - 0.5) * 1.65 * irregularity)
                            * waveSpacing
                            * saturate(_AnimeGrassFarRippleParams.x);
                        float warpedDistance = alongPattern
                            + curveOffset
                            + (warpB - 0.5) * waveSpacing * 1.55 * irregularity;
                        float primaryPhase = (warpedDistance + patternTravel) * waveFrequency;
                        half primaryWave = sin(primaryPhase);

                        float diagonalDistance = alongPattern * 0.56 + acrossPattern * 0.24;
                        float secondaryPhase = (
                            diagonalDistance
                            + patternTravel * 0.63
                            + (warpA - 0.5) * waveSpacing * 1.8)
                            * waveFrequency * 0.72 + 2.1;
                        half secondaryWave = sin(secondaryPhase);
                        half combinedWave = lerp(
                            primaryWave,
                            primaryWave * 0.55h + secondaryWave * 0.45h,
                            irregularity * 0.82h);
                        patternSignal = saturate(combinedWave * 0.5h + 0.5h);
                        patternSignal = saturate(
                            patternSignal
                            + (warpA * 0.55 + warpB * 0.45 - 0.5)
                            * irregularity
                            * 0.9h);
                        patternSignal = smoothstep(0.16h, 0.84h, patternSignal);
                        breakupMask = smoothstep(0.16h, 0.8h, breakupNoise);
                        patternSignal *= lerp(
                            1.0h,
                            lerp(0.3h, 1.15h, breakupMask),
                            irregularity * 0.78h);
                        patternSignal = saturate(patternSignal);
                    }
                #endif

                half3 coverageColor = coverage.rgb / max(coverage.a, 0.0001h);
                half3 farColor = coverageColor;

                #if defined(ENLYN_GRASS_DISABLE_SHADOWS)
                    Light mainLight = GetMainLight();
                    half shadowVisibility = 1.0h;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(
                        positionWS + surfaceNormal * 0.04);
                    Light mainLight = GetMainLight(shadowCoord);
                    half shadowVisibility = lerp(
                        1.0h,
                        mainLight.shadowAttenuation,
                        saturate(_AnimeGrassFarLightingParams.y));
                #endif
                half lightAmount = abs(dot(surfaceNormal, mainLight.direction));
                lightAmount = smoothstep(0.25h, 0.82h, lightAmount);
                half lightVisibility = saturate(
                    lightAmount
                    * shadowVisibility
                    * mainLight.distanceAttenuation);
                half3 matchedLighting = lerp(
                    _AnimeGrassFarShadowColor.rgb,
                    mainLight.color.rgb,
                    lightVisibility);
                farColor *= lerp(
                    half3(1.0h, 1.0h, 1.0h),
                    matchedLighting,
                    saturate(_AnimeGrassFarLightingParams.x));
                farColor = MixFog(
                    farColor,
                    ComputeFogFactor(TransformWorldToHClip(positionWS).z));

                half shadowSignal = saturate(
                    1.0h - patternSignal
                    + (warpB - 0.5h) * irregularity * 0.32h);
                half shadowBand = smoothstep(0.32h, 0.88h, shadowSignal);
                shadowBand *= lerp(
                    1.0h,
                    lerp(0.42h, 1.0h, breakupMask),
                    irregularity * 0.72h);
                half patternColorStrength = saturate(
                    _AnimeGrassFarPatternTint.a
                    * _AnimeGrassFarAppearanceParams.z
                    * patternEnabled);
                half patternShadowStrength = saturate(
                    _AnimeGrassFarAppearanceParams.y
                    * patternEnabled);
                half3 patternColor = lerp(
                    farColor,
                    _AnimeGrassFarPatternTint.rgb,
                    patternColorStrength);
                half3 patternShadowColor = lerp(
                    farColor,
                    _AnimeGrassFarPatternShadowColor.rgb,
                    patternShadowStrength);
                farColor = lerp(
                    patternColor,
                    patternShadowColor,
                    saturate(shadowBand));

                half volumeExclusion = 0.0h;
                #if !defined(ENLYN_GRASS_DISABLE_INTERACTION)
                    volumeExclusion = EnlynFarFieldVolumeExclusion(positionWS);
                #endif

                half visibility = saturate(coverage.a
                    * _AnimeGrassFarAppearanceParams.x
                    * farFadeIn
                    * farFadeOut
                    * heightMatch
                    * upward
                    * (1.0h - volumeExclusion)
                    * inside
                    * validDepth);
                return half4(farColor, visibility);
            }
            ENDHLSL
        }
    }
}
