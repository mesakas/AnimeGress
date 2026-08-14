Shader "AnimeGress/Anime Grass Instanced"
{
    Properties
    {
        _BaseMap ("基础贴图", 2D) = "white" {}
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
        [HideInInspector] _BatchReceiveShadows ("Batch Receive Shadows", Float) = 1
        [HideInInspector] _InstanceColor ("Instance Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _InstanceNormal ("Instance Normal", Vector) = (0, 0, 0, 0)
        [HideInInspector] _InstanceWindWeight ("Instance Wind Weight", Float) = 1
        [HideInInspector] _InstanceFade ("Instance Fade", Float) = 1
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
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
                half _DebugView;
            CBUFFER_END

            float4 _EnlynGrassWind;
            float4 _EnlynGrassWindParams;
            half4 _EnlynGrassWindTint;
            half _EnlynGrassWindTintStrength;
            half _BatchReceiveShadows;

            UNITY_INSTANCING_BUFFER_START(EnlynGrassPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceNormal)
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
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half height01 : TEXCOORD3;
                half wind01 : TEXCOORD4;
                half fade : TEXCOORD5;
                half fogFactor : TEXCOORD6;
                float4 shadowCoord : TEXCOORD7;
            };

            float EnlynDither(float2 pixelPosition)
            {
                float2 p = floor(pixelPosition / max(1.0, _DitherScale));
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
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
                float3 grassUpWS = dot(instanceNormal, instanceNormal) > 0.0001
                    ? normalize(instanceNormal)
                    : objectUpWS;
                float3 objectOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 rootPositionWS = objectOriginWS + grassUpWS * _RootHeight;
                float height01 = saturate(dot(positionWS - rootPositionWS, grassUpWS) / max(0.001, _BladeHeight));

                float2 windDirection = _EnlynGrassWind.xy;
                windDirection = dot(windDirection, windDirection) > 0.0001 ? normalize(windDirection) : float2(1.0, 0.0);
                float wave = sin(dot(positionWS.xz, windDirection) * _EnlynGrassWindParams.x + _Time.y * _EnlynGrassWind.w);
                float gust = sin((positionWS.x + positionWS.z) * _EnlynGrassWindParams.z + _Time.y * _EnlynGrassWindParams.w);
                float bendMask = height01 * height01;
                float bend = (wave + gust * _EnlynGrassWindParams.y) * _EnlynGrassWind.z * _WindBend * windWeight * bendMask;

                positionWS.xz += windDirection * bend;

                if (dot(instanceNormal, instanceNormal) > 0.0001)
                {
                    normalWS = normalize(lerp(normalWS, instanceNormal, _NormalBlend));
                }

                half4 vertexColor = input.color;
                vertexColor.rgb = dot(vertexColor.rgb, vertexColor.rgb) > 0.0001h ? vertexColor.rgb : half3(1.0h, 1.0h, 1.0h);
                vertexColor.a = vertexColor.a > 0.0001h ? vertexColor.a : 1.0h;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalize(normalWS);
                output.color = vertexColor * instanceColor;
                output.height01 = height01;
                output.wind01 = saturate(wave * 0.5 + 0.5);
                output.fade = instanceFade > 0.0001 ? saturate(instanceFade) : 1.0;
                float3 shadowRootPositionWS = objectOriginWS + grassUpWS * max(0.001, _ShadowSampleOffset);
                float3 shadowSamplePositionWS = lerp(positionWS, shadowRootPositionWS, _GroundShadowSample);
                output.shadowCoord = TransformWorldToShadowCoord(shadowSamplePositionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.fade - EnlynDither(input.positionCS.xy) - 0.0001h);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = baseSample.a * _BaseColor.a * input.color.a;
                clip(alpha - _Cutoff);

                half3 fullGradient = lerp(_RootColor.rgb, _TipColor.rgb, input.height01);
                half3 stableGradient = lerp(_RootColor.rgb, _TipColor.rgb, 0.62h);
                half3 gradient = lerp(stableGradient, fullGradient, _GradientStrength);
                half3 albedo = baseSample.rgb * _BaseColor.rgb * input.color.rgb * gradient;

                Light mainLight = GetMainLight(input.shadowCoord);
                half lightAmount = abs(dot(normalize(input.normalWS), mainLight.direction));
                lightAmount = smoothstep(0.25h, 0.82h, lightAmount);
                half shadowAttenuation = mainLight.shadowAttenuation;
                shadowAttenuation = saturate((shadowAttenuation - _ShadowLeakReduction) / max(0.001h, 1.0h - _ShadowLeakReduction));
                half receiveShadowStrength = _ReceiveShadowStrength * _BatchReceiveShadows;
                half shadowVisibility = lerp(1.0h, shadowAttenuation, receiveShadowStrength);
                half lightVisibility = saturate(lightAmount * shadowVisibility * mainLight.distanceAttenuation);

                half windTintMask = lerp(1.0h, input.wind01, _WindTintSpatialVariation);
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
}
