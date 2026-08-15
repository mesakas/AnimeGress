Shader "AnimeGress/Anime Grass Instanced"
{
    Properties
    {
        _BaseMap ("基础贴图", 2D) = "white" {}
        _AlphaMap ("独立 Alpha 贴图", 2D) = "white" {}
        [Enum(R, 0, Alpha, 1)] _AlphaMapChannel ("Alpha 贴图通道", Float) = 0
        _BaseColor ("基础颜色", Color) = (1, 1, 1, 1)
        _RootColor ("根部颜色", Color) = (0.48, 0.78, 0.36, 1)
        _TipColor ("顶部颜色", Color) = (0.86, 1.0, 0.58, 1)
        _GradientStrength ("根顶渐变强度", Range(0, 1)) = 0.75
        _ShadowColor ("阴影颜色", Color) = (0.55, 0.65, 0.48, 1)
        _Cutoff ("透明裁剪阈值", Range(0, 1)) = 0.35
        _RootHeight ("根部高度", Float) = 0
        _BladeHeight ("草叶高度", Float) = 1
        _WindBend ("风弯曲强度", Range(0, 2)) = 0.75
        _WindTintResponse ("风色响应", Range(0, 1)) = 1
        _WindTintSpatialVariation ("风色空间变化", Range(0, 1)) = 0
        _NormalBlend ("表面法线混合", Range(0, 1)) = 0.35
        _DitherScale ("点状渐隐颗粒大小", Range(1, 8)) = 1
        _ReceiveShadowStrength ("接收阴影强度", Range(0, 1)) = 0.7
        _GroundShadowSample ("阴影贴地采样", Range(0, 1)) = 1
        _ShadowSampleOffset ("阴影采样高度偏移", Range(0.001, 0.25)) = 0.04
        _ShadowLeakReduction ("阴影漏光抑制", Range(0, 0.8)) = 0.05
        _DebugView ("调试显示模式", Range(0, 5)) = 0
        _SurfaceCacheColorInfluence ("地表颜色影响", Range(0, 1)) = 0
        _SurfaceCacheRootOnly ("仅影响草根", Range(0, 1)) = 0.75
        _SurfaceCacheNormalInfluence ("地表法线影响", Range(0, 1)) = 0
        [HideInInspector] _SurfaceCacheWetnessInfluence ("湿润遮罩影响", Range(0, 1)) = 0
        [HideInInspector] _SurfaceCacheSnowInfluence ("积雪遮罩影响", Range(0, 1)) = 0
        [HideInInspector] _SurfaceCacheBurnInfluence ("烧焦遮罩影响", Range(0, 1)) = 0
        _SurfaceCacheExclusionInfluence ("排除遮罩影响", Range(0, 1)) = 1
        _SurfaceCacheHeightTolerance ("高度匹配容差", Range(0.05, 10)) = 1.5
        [HideInInspector] _SurfaceCacheSnowColor ("积雪颜色", Color) = (0.9, 0.95, 1, 1)
        [HideInInspector] _SurfaceCacheBurnColor ("烧焦颜色", Color) = (0.12, 0.08, 0.04, 1)
        [HideInInspector] _BatchReceiveShadows ("Batch Receive Shadows", Float) = 1
        [HideInInspector] _InstanceColor ("Instance Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _InstanceNormal ("Instance Normal", Vector) = (0, 0, 0, 0)
        [HideInInspector] _InstanceBaseRotation ("Instance Base Rotation", Vector) = (0, 0, 0, 1)
        [HideInInspector] _InstanceWindWeight ("Instance Wind Weight", Float) = 1
        [HideInInspector] _InstanceFade ("Instance Fade", Float) = 1
        [HideInInspector] _EnlynGrassFaceTarget ("Grass Face Target", Vector) = (0, 0, 0, 0)
        [HideInInspector] _EnlynGrassFaceRotation ("Grass Face Rotation", Float) = 0
        [HideInInspector] _EnlynGrassInstanceRootOS ("Grass Instance Root OS", Vector) = (0, 0, 0, 1)
        [HideInInspector] _EnlynGrassViewPosition ("Grass View Position", Vector) = (0, 0, 0, 1)
        [HideInInspector] _EnlynGrassOverheadBend ("Grass Overhead Bend", Vector) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.ming.animegress/Shaders/AnimeSurfaceCache.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_AlphaMap);
            SAMPLER(sampler_AlphaMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _AlphaMap_ST;
                half _AlphaMapChannel;
                half4 _BaseColor;
                half4 _RootColor;
                half4 _TipColor;
                half _GradientStrength;
                half4 _ShadowColor;
                half _Cutoff;
                float _RootHeight;
                float _BladeHeight;
                half _WindBend;
                half _WindTintResponse;
                half _WindTintSpatialVariation;
                half _NormalBlend;
                float _DitherScale;
                half _ReceiveShadowStrength;
                half _GroundShadowSample;
                float _ShadowSampleOffset;
                half _ShadowLeakReduction;
                half _SurfaceCacheColorInfluence;
                half _SurfaceCacheRootOnly;
                half _SurfaceCacheNormalInfluence;
                half _SurfaceCacheWetnessInfluence;
                half _SurfaceCacheSnowInfluence;
                half _SurfaceCacheBurnInfluence;
                half _SurfaceCacheExclusionInfluence;
                float _SurfaceCacheHeightTolerance;
                half4 _SurfaceCacheSnowColor;
                half4 _SurfaceCacheBurnColor;
                half _DebugView;
            CBUFFER_END

            float4 _EnlynGrassWind;
            float4 _EnlynGrassWindParams;
            float4 _EnlynGrassWindColorParams;
            float4 _EnlynGrassWindColorGustParams;
            half4 _EnlynGrassWindTint;
            half _EnlynGrassWindTintStrength;
            half _BatchReceiveShadows;
            float4 _EnlynGrassFaceTarget;
            float _EnlynGrassFaceRotation;
            float4 _EnlynGrassInstanceRootOS;
            float4 _EnlynGrassViewPosition;
            float4 _EnlynGrassOverheadBend;

            #define ENLYN_GRASS_MAX_INTERACTION_VOLUMES 16
            float _EnlynGrassInteractionVolumeCount;
            float4 _EnlynGrassInteractionVolumeCenterShape[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
            float4 _EnlynGrassInteractionVolumeParams[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
            float4 _EnlynGrassInteractionVolumeExclusionParams[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
            float4 _EnlynGrassInteractionVolumeWorldToLocal0[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
            float4 _EnlynGrassInteractionVolumeWorldToLocal1[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];
            float4 _EnlynGrassInteractionVolumeWorldToLocal2[ENLYN_GRASS_MAX_INTERACTION_VOLUMES];

            UNITY_INSTANCING_BUFFER_START(EnlynGrassPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceNormal)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceBaseRotation)
                UNITY_DEFINE_INSTANCED_PROP(float, _InstanceWindWeight)
                UNITY_DEFINE_INSTANCED_PROP(float, _InstanceFade)
            UNITY_INSTANCING_BUFFER_END(EnlynGrassPerInstance)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half height01 : TEXCOORD3;
                half wind01 : TEXCOORD4;
                half fade : TEXCOORD5;
                half fogFactor : TEXCOORD6;
                float4 shadowCoord : TEXCOORD7;
                float4 surfaceCacheUvValidHeight : TEXCOORD8;
                half volumeExclusion : TEXCOORD9;
            };

            float EnlynDither(float2 pixelPosition)
            {
                float2 p = floor(pixelPosition / max(1.0, _DitherScale));
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
            }

            float3 EnlynRotateByQuaternion(float3 value, float4 rotation)
            {
                return value + 2.0 * cross(
                    rotation.xyz,
                    cross(rotation.xyz, value) + rotation.w * value);
            }

            float3 EnlynFaceTargetVector(
                float3 valueWS,
                float4 baseRotation,
                float3 targetRightWS,
                float3 targetUpWS,
                float3 targetForwardWS)
            {
                float rotationLengthSq = dot(baseRotation, baseRotation);
                float4 safeRotation = rotationLengthSq > 0.0001
                    ? baseRotation * rsqrt(rotationLengthSq)
                    : float4(0.0, 0.0, 0.0, 1.0);
                float3 unrotatedValue = EnlynRotateByQuaternion(
                    valueWS,
                    float4(-safeRotation.xyz, safeRotation.w));
                return targetRightWS * unrotatedValue.x
                    + targetUpWS * unrotatedValue.y
                    + targetForwardWS * unrotatedValue.z;
            }

            float3 EnlynRotateAroundAxis(float3 value, float3 axis, float angle)
            {
                float rotationSin;
                float rotationCos;
                sincos(angle, rotationSin, rotationCos);
                return value * rotationCos
                    + cross(axis, value) * rotationSin
                    + axis * dot(axis, value) * (1.0 - rotationCos);
            }

            float3 EnlynGrassVolumeToLocal(float3 positionWS, int volumeIndex)
            {
                float4 position = float4(positionWS, 1.0);
                return float3(
                    dot(_EnlynGrassInteractionVolumeWorldToLocal0[volumeIndex], position),
                    dot(_EnlynGrassInteractionVolumeWorldToLocal1[volumeIndex], position),
                    dot(_EnlynGrassInteractionVolumeWorldToLocal2[volumeIndex], position));
            }

            float3 EnlynGrassVolumeNormalToWorld(float3 normalLS, int volumeIndex)
            {
                float4 row0 = _EnlynGrassInteractionVolumeWorldToLocal0[volumeIndex];
                float4 row1 = _EnlynGrassInteractionVolumeWorldToLocal1[volumeIndex];
                float4 row2 = _EnlynGrassInteractionVolumeWorldToLocal2[volumeIndex];
                return float3(
                    dot(float3(row0.x, row1.x, row2.x), normalLS),
                    dot(float3(row0.y, row1.y, row2.y), normalLS),
                    dot(float3(row0.z, row1.z, row2.z), normalLS));
            }

            float3 EnlynGrassVolumeInteraction(
                float3 positionWS,
                float3 rootPositionWS,
                float height01)
            {
                float2 totalOffset = float2(0.0, 0.0);
                float totalExclusion = 0.0;
                int volumeCount = min(
                    (int)_EnlynGrassInteractionVolumeCount,
                    ENLYN_GRASS_MAX_INTERACTION_VOLUMES);

                [loop]
                for (int volumeIndex = 0; volumeIndex < volumeCount; volumeIndex++)
                {
                    float4 centerShape = _EnlynGrassInteractionVolumeCenterShape[volumeIndex];
                    float4 volumeParams = _EnlynGrassInteractionVolumeParams[volumeIndex];
                    float4 exclusionParams = _EnlynGrassInteractionVolumeExclusionParams[volumeIndex];
                    float3 normalizedPosition = EnlynGrassVolumeToLocal(
                        positionWS,
                        volumeIndex) * 2.0;
                    float normalizedDistance = centerShape.w < 0.5
                        ? length(normalizedPosition)
                        : max(
                            max(abs(normalizedPosition.x), abs(normalizedPosition.y)),
                            abs(normalizedPosition.z));
                    if (normalizedDistance < 1.0 && volumeParams.x > 0.0001)
                    {
                        float penetration = 1.0 - normalizedDistance;
                        float volumeWeight = smoothstep(
                            0.0,
                            max(0.001, volumeParams.y),
                            penetration);
                        float upperWeight = saturate(
                            (height01 - volumeParams.z)
                            / max(0.001, 1.0 - volumeParams.z));
                        upperWeight *= upperWeight;

                        float3 directionLS;
                        if (centerShape.w < 0.5)
                        {
                            directionLS = normalizedPosition;
                        }
                        else
                        {
                            float2 distanceToFace = 1.0 - abs(normalizedPosition.xz);
                            if (distanceToFace.x < distanceToFace.y)
                            {
                                directionLS = float3(normalizedPosition.x >= 0.0 ? 1.0 : -1.0, 0.0, 0.0);
                            }
                            else
                            {
                                directionLS = float3(0.0, 0.0, normalizedPosition.z >= 0.0 ? 1.0 : -1.0);
                            }
                        }

                        float3 directionWS3 = EnlynGrassVolumeNormalToWorld(directionLS, volumeIndex);
                        float2 directionWS = directionWS3.xz;
                        float directionLengthSq = dot(directionWS, directionWS);
                        directionWS = directionLengthSq > 0.0001
                            ? directionWS * rsqrt(directionLengthSq)
                            : float2(1.0, 0.0);
                        totalOffset += directionWS
                            * volumeParams.x
                            * volumeWeight
                            * upperWeight;
                    }

                    if (exclusionParams.x > 0.0001)
                    {
                        float3 rootNormalizedPosition = EnlynGrassVolumeToLocal(
                            rootPositionWS,
                            volumeIndex) * 2.0;
                        float rootDistance = centerShape.w < 0.5
                            ? length(rootNormalizedPosition)
                            : max(
                                max(abs(rootNormalizedPosition.x), abs(rootNormalizedPosition.y)),
                                abs(rootNormalizedPosition.z));
                        float exclusionWeight = 1.0 - smoothstep(
                            saturate(exclusionParams.y),
                            1.0,
                            rootDistance);
                        totalExclusion = max(
                            totalExclusion,
                            exclusionParams.x * exclusionWeight);
                    }
                }

                return float3(totalOffset, saturate(totalExclusion));
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(UNITY_INSTANCING_ENABLED)
                float windWeight = UNITY_ACCESS_INSTANCED_PROP(EnlynGrassPerInstance, _InstanceWindWeight);
                half4 instanceColor = UNITY_ACCESS_INSTANCED_PROP(EnlynGrassPerInstance, _InstanceColor);
                float3 instanceNormal = UNITY_ACCESS_INSTANCED_PROP(EnlynGrassPerInstance, _InstanceNormal).xyz;
                float instanceFade = UNITY_ACCESS_INSTANCED_PROP(EnlynGrassPerInstance, _InstanceFade);
                #else
                float windWeight = 1.0;
                half4 instanceColor = half4(1.0h, 1.0h, 1.0h, 1.0h);
                float3 instanceNormal = float3(0.0, 0.0, 0.0);
                float instanceFade = 1.0;
                #endif

                if (instanceColor.a <= 0.0001h)
                {
                    instanceColor = half4(1.0h, 1.0h, 1.0h, 1.0h);
                    windWeight = max(windWeight, 1.0);
                }

                float3 objectUpWS = normalize(mul((float3x3)unity_ObjectToWorld, float3(0.0, 1.0, 0.0)));
                float instanceNormalLengthSq = dot(instanceNormal, instanceNormal);
                float3 safeInstanceNormal = instanceNormal * rsqrt(max(0.0001, instanceNormalLengthSq));
                float3 grassUpWS = instanceNormalLengthSq > 0.0001
                    ? safeInstanceNormal
                    : objectUpWS;
                float3 objectOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 instanceRootPosition = TransformObjectToWorld(_EnlynGrassInstanceRootOS.xyz);
                [branch]
                if (_EnlynGrassFaceTarget.w > 0.5)
                {
                    #if defined(UNITY_INSTANCING_ENABLED)
                    float4 instanceBaseRotation = UNITY_ACCESS_INSTANCED_PROP(
                        EnlynGrassPerInstance,
                        _InstanceBaseRotation);
                    #else
                    float4 instanceBaseRotation = float4(0.0, 0.0, 0.0, 1.0);
                    #endif

                    float3 targetDirectionWS = _EnlynGrassFaceTarget.xyz - instanceRootPosition;
                    targetDirectionWS -= grassUpWS * dot(targetDirectionWS, grassUpWS);
                    float targetDirectionLengthSq = dot(targetDirectionWS, targetDirectionWS);
                    if (targetDirectionLengthSq > 0.000001)
                    {
                        float3 targetForwardWS = targetDirectionWS * rsqrt(targetDirectionLengthSq);
                        float rotationSin;
                        float rotationCos;
                        sincos(_EnlynGrassFaceRotation, rotationSin, rotationCos);
                        targetForwardWS = normalize(
                            targetForwardWS * rotationCos
                            + cross(grassUpWS, targetForwardWS) * rotationSin);
                        float3 targetRightWS = normalize(cross(grassUpWS, targetForwardWS));

                        positionWS = instanceRootPosition + EnlynFaceTargetVector(
                            positionWS - instanceRootPosition,
                            instanceBaseRotation,
                            targetRightWS,
                            grassUpWS,
                            targetForwardWS);
                        objectOriginWS = instanceRootPosition + EnlynFaceTargetVector(
                            objectOriginWS - instanceRootPosition,
                            instanceBaseRotation,
                            targetRightWS,
                            grassUpWS,
                            targetForwardWS);
                        normalWS = normalize(EnlynFaceTargetVector(
                            normalWS,
                            instanceBaseRotation,
                            targetRightWS,
                            grassUpWS,
                            targetForwardWS));
                        objectUpWS = normalize(EnlynFaceTargetVector(
                            objectUpWS,
                            instanceBaseRotation,
                            targetRightWS,
                            grassUpWS,
                            targetForwardWS));
                        if (instanceNormalLengthSq <= 0.0001)
                        {
                            grassUpWS = objectUpWS;
                        }
                    }
                }
                float3 rootPositionWS = objectOriginWS + grassUpWS * _RootHeight;
                float height01 = saturate(dot(positionWS - rootPositionWS, grassUpWS) / max(0.001, _BladeHeight));

                [branch]
                if (_EnlynGrassOverheadBend.x > 0.5
                    && _EnlynGrassOverheadBend.y > 0.0001
                    && height01 > 0.0001)
                {
                    float3 viewOffsetWS = _EnlynGrassViewPosition.xyz - instanceRootPosition;
                    float viewDistanceSq = dot(viewOffsetWS, viewOffsetWS);
                    if (viewDistanceSq > 0.000001)
                    {
                        float3 viewDirectionWS = viewOffsetWS * rsqrt(viewDistanceSq);
                        float viewElevation = saturate(dot(viewDirectionWS, grassUpWS));
                        float overheadWeight = smoothstep(
                            _EnlynGrassOverheadBend.z,
                            _EnlynGrassOverheadBend.w,
                            viewElevation);
                        float bendAngle = _EnlynGrassOverheadBend.y * overheadWeight;
                        float vertexBendAngle = bendAngle * height01;
                        if (vertexBendAngle > 0.0001)
                        {
                            float3 bendDirectionWS = viewDirectionWS
                                - grassUpWS * dot(viewDirectionWS, grassUpWS);
                            float bendDirectionLengthSq = dot(bendDirectionWS, bendDirectionWS);
                            if (bendDirectionLengthSq <= 0.000001)
                            {
                                bendDirectionWS = normalWS
                                    - grassUpWS * dot(normalWS, grassUpWS);
                                bendDirectionLengthSq = dot(bendDirectionWS, bendDirectionWS);
                            }
                            if (bendDirectionLengthSq <= 0.000001)
                            {
                                float3 referenceAxis = abs(grassUpWS.y) < 0.99
                                    ? float3(0.0, 1.0, 0.0)
                                    : float3(1.0, 0.0, 0.0);
                                bendDirectionWS = cross(referenceAxis, grassUpWS);
                                bendDirectionLengthSq = dot(bendDirectionWS, bendDirectionWS);
                            }

                            bendDirectionWS *= rsqrt(max(0.000001, bendDirectionLengthSq));
                            float sourceHeight = max(
                                0.0,
                                dot(positionWS - rootPositionWS, grassUpWS));
                            float3 lateralOffsetWS = positionWS
                                - rootPositionWS
                                - grassUpWS * sourceHeight;
                            float bendSin;
                            float bendCos;
                            sincos(vertexBendAngle, bendSin, bendCos);
                            float arcScale = sourceHeight / vertexBendAngle;
                            float bentHeight = bendSin * arcScale;
                            float bentOffset = (1.0 - bendCos) * arcScale;
                            positionWS = rootPositionWS
                                + lateralOffsetWS
                                + grassUpWS * bentHeight
                                + bendDirectionWS * bentOffset;

                            float3 bendAxisWS = normalize(cross(grassUpWS, bendDirectionWS));
                            normalWS = normalize(EnlynRotateAroundAxis(
                                normalWS,
                                bendAxisWS,
                                vertexBendAngle));
                        }
                    }
                }

                float2 windDirection = _EnlynGrassWind.xy;
                windDirection = dot(windDirection, windDirection) > 0.0001 ? normalize(windDirection) : float2(1.0, 0.0);
                float wave = sin(dot(positionWS.xz, windDirection) * _EnlynGrassWindParams.x + _Time.y * _EnlynGrassWind.w);
                float gust = sin((positionWS.x + positionWS.z) * _EnlynGrassWindParams.z + _Time.y * _EnlynGrassWindParams.w);
                float windTintWave = sin(
                    dot(positionWS.xz, windDirection) * _EnlynGrassWindColorParams.x
                    + _Time.y * _EnlynGrassWindColorParams.y);
                float windTintGust = sin(
                    (positionWS.x + positionWS.z) * _EnlynGrassWindColorGustParams.x
                    + _Time.y * _EnlynGrassWindColorGustParams.y);
                float windTintSignal = saturate(
                    (windTintWave + windTintGust * _EnlynGrassWindColorParams.z) * 0.5 + 0.5);
                float bendMask = height01 * height01;
                float bend = (wave + gust * _EnlynGrassWindParams.y) * _EnlynGrassWind.z * _WindBend * windWeight * bendMask;

                positionWS.xz += windDirection * bend;
                float3 volumeInteraction = EnlynGrassVolumeInteraction(
                    positionWS,
                    objectOriginWS,
                    height01);
                positionWS.xz += volumeInteraction.xy;

                if (dot(instanceNormal, instanceNormal) > 0.0001)
                {
                    normalWS = normalize(lerp(normalWS, instanceNormal, _NormalBlend));
                }

                half4 vertexColor = input.color;
                vertexColor.rgb = dot(vertexColor.rgb, vertexColor.rgb) > 0.0001h ? vertexColor.rgb : half3(1.0h, 1.0h, 1.0h);
                vertexColor.a = vertexColor.a > 0.0001h ? vertexColor.a : 1.0h;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv.xy = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uv.zw = TRANSFORM_TEX(input.uv, _AlphaMap);
                output.normalWS = normalize(normalWS);
                output.color = vertexColor * instanceColor;
                output.height01 = height01;
                output.wind01 = windTintSignal;
                output.fade = abs(instanceFade) > 0.0001
                    ? clamp(instanceFade, -1.0, 1.0)
                    : 1.0;
                float3 shadowRootPositionWS = objectOriginWS + grassUpWS * max(0.001, _ShadowSampleOffset);
                float3 shadowSamplePositionWS = lerp(positionWS, shadowRootPositionWS, _GroundShadowSample);
                output.shadowCoord = TransformWorldToShadowCoord(shadowSamplePositionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                float2 surfaceCacheUv = AnimeSurfaceCacheWorldToUV(objectOriginWS);
                output.surfaceCacheUvValidHeight = float4(
                    surfaceCacheUv,
                    AnimeSurfaceCacheContainsUV(surfaceCacheUv),
                    objectOriginWS.y);
                output.volumeExclusion = volumeInteraction.z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                AnimeSurfaceCacheSample surface = SampleAnimeSurfaceCache(
                    input.surfaceCacheUvValidHeight.xy,
                    input.surfaceCacheUvValidHeight.z);
                float cachedSurfaceHeight = _AnimeSurfaceCacheHeightParams.x
                    + surface.height01 / max(0.0001, _AnimeSurfaceCacheHeightParams.y);
                half heightMatch = 1.0h - smoothstep(
                    _SurfaceCacheHeightTolerance * 0.5,
                    _SurfaceCacheHeightTolerance,
                    abs(cachedSurfaceHeight - input.surfaceCacheUvValidHeight.w));
                surface.valid *= heightMatch;
                half surfaceExclusion = surface.masks.a
                    * surface.valid
                    * _SurfaceCacheExclusionInfluence;
                surfaceExclusion = max(surfaceExclusion, input.volumeExclusion);
                half visibleFade = abs(input.fade) * saturate(1.0h - surfaceExclusion);
                float ditherThreshold = EnlynDither(input.positionCS.xy);
                ditherThreshold = input.fade < 0.0h
                    ? 1.0 - ditherThreshold
                    : ditherThreshold;
                clip(visibleFade - ditherThreshold - 0.0001h);

                half4 baseSample = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    input.uv.xy);
                half4 alphaSample = SAMPLE_TEXTURE2D(
                    _AlphaMap,
                    sampler_AlphaMap,
                    input.uv.zw);
                half alphaMask = lerp(
                    alphaSample.r,
                    alphaSample.a,
                    step(0.5h, _AlphaMapChannel));
                half alpha = baseSample.a
                    * alphaMask
                    * _BaseColor.a
                    * input.color.a;
                clip(alpha - _Cutoff);

                half3 fullGradient = lerp(_RootColor.rgb, _TipColor.rgb, input.height01);
                half3 stableGradient = lerp(_RootColor.rgb, _TipColor.rgb, 0.62h);
                half3 gradient = lerp(stableGradient, fullGradient, _GradientStrength);
                half3 albedo = baseSample.rgb * _BaseColor.rgb * input.color.rgb * gradient;

                half rootWeight = lerp(1.0h, 1.0h - input.height01, _SurfaceCacheRootOnly);
                half surfaceWeight = surface.valid * rootWeight;
                albedo = lerp(
                    albedo,
                    surface.color,
                    saturate(surfaceWeight * _SurfaceCacheColorInfluence));
                albedo *= 1.0h - surface.masks.r * surfaceWeight * _SurfaceCacheWetnessInfluence * 0.4h;
                albedo = lerp(
                    albedo,
                    _SurfaceCacheSnowColor.rgb,
                    surface.masks.g * surfaceWeight * _SurfaceCacheSnowInfluence);
                albedo = lerp(
                    albedo,
                    _SurfaceCacheBurnColor.rgb,
                    surface.masks.b * surfaceWeight * _SurfaceCacheBurnInfluence);

                Light mainLight = GetMainLight(input.shadowCoord);
                half3 lightingNormalWS = normalize(lerp(
                    input.normalWS,
                    surface.normalWS,
                    surfaceWeight * _SurfaceCacheNormalInfluence));
                half lightAmount = abs(dot(lightingNormalWS, mainLight.direction));
                lightAmount = smoothstep(0.25h, 0.82h, lightAmount);
                half shadowAttenuation = mainLight.shadowAttenuation;
                shadowAttenuation = saturate((shadowAttenuation - _ShadowLeakReduction) / max(0.001h, 1.0h - _ShadowLeakReduction));
                half receiveShadowStrength = _ReceiveShadowStrength * _BatchReceiveShadows;
                half shadowVisibility = lerp(1.0h, shadowAttenuation, receiveShadowStrength);
                half lightVisibility = saturate(lightAmount * shadowVisibility * mainLight.distanceAttenuation);

                half windTintMask = lerp(
                    1.0h,
                    input.wind01,
                    _WindTintSpatialVariation * saturate(_EnlynGrassWindColorParams.w));
                half debugView = round(_DebugView);
                if (debugView == 1.0h)
                {
                    return half4(albedo, alpha);
                }
                if (debugView == 2.0h)
                {
                    return half4(input.height01.xxx, alpha);
                }
                if (debugView == 3.0h)
                {
                    return half4(shadowAttenuation.xxx, alpha);
                }
                if (debugView == 4.0h)
                {
                    return half4(lightAmount.xxx, alpha);
                }
                if (debugView == 5.0h)
                {
                    return half4(windTintMask.xxx, alpha);
                }

                half3 litColor = albedo * lerp(_ShadowColor.rgb, mainLight.color.rgb, lightVisibility);

                half windTintAmount = saturate(windTintMask * _EnlynGrassWindTintStrength * _WindTintResponse);
                litColor = lerp(litColor, litColor * _EnlynGrassWindTint.rgb, windTintAmount);
                litColor = MixFog(litColor, input.fogFactor);

                return half4(litColor, alpha);
            }
            ENDHLSL
        }
    }

    CustomEditor "Enlyn.Grass.Editor.AnimeGrassShaderGUI"
}
