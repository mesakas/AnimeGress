Shader "Hidden/AnimeGrass/Surface Cache Capture"
{
    Properties
    {
        [HideInInspector] _AnimeSurfaceDepthTest ("Depth Test", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Capture"

            Cull Off
            ZWrite On
            ZTest [_AnimeSurfaceDepthTest]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CaptureVert
            #pragma fragment CaptureFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_AnimeSurfaceSourceBaseMap);
            SAMPLER(sampler_AnimeSurfaceSourceBaseMap);

            float4 _AnimeSurfaceSourceBaseMap_ST;
            half4 _AnimeSurfaceSourceBaseColor;
            half4 _AnimeSurfaceSourceMask;
            half _AnimeSurfaceSourceAlphaClip;
            half _AnimeSurfaceSourceCutoff;
            half _AnimeSurfaceSourceNormalFlatten;
            float4 _AnimeSurfaceCaptureHeightParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            struct CaptureOutput
            {
                half4 color : SV_Target0;
                half4 normalHeight : SV_Target1;
                half4 masks : SV_Target2;
            };

            Varyings CaptureVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _AnimeSurfaceSourceBaseMap_ST.xy + _AnimeSurfaceSourceBaseMap_ST.zw;
                return output;
            }

            CaptureOutput CaptureFrag(Varyings input)
            {
                CaptureOutput output;
                half4 baseSample = SAMPLE_TEXTURE2D(
                    _AnimeSurfaceSourceBaseMap,
                    sampler_AnimeSurfaceSourceBaseMap,
                    input.uv) * _AnimeSurfaceSourceBaseColor;
                if (_AnimeSurfaceSourceAlphaClip > 0.5h)
                {
                    clip(baseSample.a - _AnimeSurfaceSourceCutoff);
                }

                half3 normalWS = normalize(lerp(input.normalWS, half3(0.0h, 1.0h, 0.0h), _AnimeSurfaceSourceNormalFlatten));
                half normalizedHeight = saturate(
                    (input.positionWS.y - _AnimeSurfaceCaptureHeightParams.x)
                    * _AnimeSurfaceCaptureHeightParams.y);

                output.color = half4(baseSample.rgb, 1.0h);
                output.normalHeight = half4(normalWS * 0.5h + 0.5h, normalizedHeight);
                output.masks = saturate(_AnimeSurfaceSourceMask);
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Stamp"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One One
            BlendOp Max

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StampVert
            #pragma fragment StampFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_AnimeSurfaceStampDataTexture);
            TEXTURE2D(_AnimeSurfaceStampColorTexture);

            half4 _AnimeSurfaceStampMask;
            float4 _AnimeSurfaceStampParams;
            float4x4 _AnimeSurfaceStampWorldToLocal;
            float4 _AnimeSurfaceCaptureHeightParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings StampVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 StampFrag(Varyings input) : SV_Target
            {
                int2 cachePixel = int2(input.positionCS.xy);
                half surfaceValid = LOAD_TEXTURE2D(_AnimeSurfaceStampColorTexture, cachePixel).a;
                half normalizedHeight = LOAD_TEXTURE2D(_AnimeSurfaceStampDataTexture, cachePixel).a;
                float surfaceHeight = _AnimeSurfaceCaptureHeightParams.x
                    + normalizedHeight / max(0.0001, _AnimeSurfaceCaptureHeightParams.y);
                float3 positionWS = float3(input.positionWS.x, surfaceHeight, input.positionWS.z);
                float3 normalizedPosition = abs(
                    mul(_AnimeSurfaceStampWorldToLocal, float4(positionWS, 1.0)).xyz * 2.0);
                float distanceToCenter = _AnimeSurfaceStampParams.x < 0.5
                    ? length(normalizedPosition)
                    : max(max(normalizedPosition.x, normalizedPosition.y), normalizedPosition.z);
                half weight = 1.0h - smoothstep(
                    saturate(_AnimeSurfaceStampParams.y),
                    1.0,
                    distanceToCenter);
                return saturate(_AnimeSurfaceStampMask) * weight * surfaceValid;
            }
            ENDHLSL
        }

        Pass
        {
            Name "TerrainCapture"

            Cull Off
            ZWrite On
            ZTest [_AnimeSurfaceDepthTest]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainVert
            #pragma fragment TerrainFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_AnimeSurfaceTerrainHeightmap);
            SAMPLER(sampler_AnimeSurfaceTerrainHeightmap);
            TEXTURE2D(_AnimeSurfaceTerrainControl0);
            SAMPLER(sampler_AnimeSurfaceTerrainControl0);
            TEXTURE2D(_AnimeSurfaceTerrainControl1);
            SAMPLER(sampler_AnimeSurfaceTerrainControl1);
            TEXTURE2D(_AnimeSurfaceTerrainLayer0);
            SAMPLER(sampler_AnimeSurfaceTerrainLayer0);
            TEXTURE2D(_AnimeSurfaceTerrainLayer1);
            SAMPLER(sampler_AnimeSurfaceTerrainLayer1);
            TEXTURE2D(_AnimeSurfaceTerrainLayer2);
            SAMPLER(sampler_AnimeSurfaceTerrainLayer2);
            TEXTURE2D(_AnimeSurfaceTerrainLayer3);
            SAMPLER(sampler_AnimeSurfaceTerrainLayer3);
            TEXTURE2D(_AnimeSurfaceTerrainLayer4);
            SAMPLER(sampler_AnimeSurfaceTerrainLayer4);
            TEXTURE2D(_AnimeSurfaceTerrainLayer5);
            SAMPLER(sampler_AnimeSurfaceTerrainLayer5);
            TEXTURE2D(_AnimeSurfaceTerrainLayer6);
            SAMPLER(sampler_AnimeSurfaceTerrainLayer6);
            TEXTURE2D(_AnimeSurfaceTerrainLayer7);
            SAMPLER(sampler_AnimeSurfaceTerrainLayer7);

            float4 _AnimeSurfaceTerrainPosition;
            float4 _AnimeSurfaceTerrainSize;
            float4 _AnimeSurfaceTerrainHeightmapTexelSize;
            float4 _AnimeSurfaceCaptureHeightParams;
            float4 _AnimeSurfaceTerrainLayerST0;
            float4 _AnimeSurfaceTerrainLayerST1;
            float4 _AnimeSurfaceTerrainLayerST2;
            float4 _AnimeSurfaceTerrainLayerST3;
            float4 _AnimeSurfaceTerrainLayerST4;
            float4 _AnimeSurfaceTerrainLayerST5;
            float4 _AnimeSurfaceTerrainLayerST6;
            float4 _AnimeSurfaceTerrainLayerST7;
            half4 _AnimeSurfaceTerrainColorMultiplier;
            half4 _AnimeSurfaceTerrainMask;
            half _AnimeSurfaceTerrainLayerCount;
            half _AnimeSurfaceTerrainNormalFlatten;

            struct TerrainAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct TerrainVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 terrainUv : TEXCOORD0;
                float2 localXZ : TEXCOORD1;
            };

            struct TerrainOutput
            {
                half4 color : SV_Target0;
                half4 normalHeight : SV_Target1;
                half4 masks : SV_Target2;
                float depth : SV_Depth;
            };

            float SampleTerrainHeight(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(
                    _AnimeSurfaceTerrainHeightmap,
                    sampler_AnimeSurfaceTerrainHeightmap,
                    saturate(uv),
                    0).r;
            }

            TerrainVaryings TerrainVert(TerrainAttributes input)
            {
                TerrainVaryings output;
                float2 terrainUv = input.uv;
                float3 positionWS = float3(
                    _AnimeSurfaceTerrainPosition.x + terrainUv.x * _AnimeSurfaceTerrainSize.x,
                    _AnimeSurfaceTerrainPosition.y,
                    _AnimeSurfaceTerrainPosition.z + terrainUv.y * _AnimeSurfaceTerrainSize.z);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.terrainUv = terrainUv;
                output.localXZ = terrainUv * _AnimeSurfaceTerrainSize.xz;
                return output;
            }

            TerrainOutput TerrainFrag(TerrainVaryings input)
            {
                TerrainOutput output;
                float height01 = SampleTerrainHeight(input.terrainUv);
                float3 positionWS = float3(
                    _AnimeSurfaceTerrainPosition.x + input.terrainUv.x * _AnimeSurfaceTerrainSize.x,
                    _AnimeSurfaceTerrainPosition.y + height01 * _AnimeSurfaceTerrainSize.y,
                    _AnimeSurfaceTerrainPosition.z + input.terrainUv.y * _AnimeSurfaceTerrainSize.z);
                float2 texel = _AnimeSurfaceTerrainHeightmapTexelSize.xy;
                float heightLeft = SampleTerrainHeight(input.terrainUv - float2(texel.x, 0.0));
                float heightRight = SampleTerrainHeight(input.terrainUv + float2(texel.x, 0.0));
                float heightDown = SampleTerrainHeight(input.terrainUv - float2(0.0, texel.y));
                float heightUp = SampleTerrainHeight(input.terrainUv + float2(0.0, texel.y));
                float3 tangentX = float3(
                    2.0 * texel.x * _AnimeSurfaceTerrainSize.x,
                    (heightRight - heightLeft) * _AnimeSurfaceTerrainSize.y,
                    0.0);
                float3 tangentZ = float3(
                    0.0,
                    (heightUp - heightDown) * _AnimeSurfaceTerrainSize.y,
                    2.0 * texel.y * _AnimeSurfaceTerrainSize.z);
                half3 normalWS = normalize(cross(tangentZ, tangentX));
                normalWS = normalize(lerp(
                    normalWS,
                    half3(0.0h, 1.0h, 0.0h),
                    _AnimeSurfaceTerrainNormalFlatten));
                half4 control0 = SAMPLE_TEXTURE2D(
                    _AnimeSurfaceTerrainControl0,
                    sampler_AnimeSurfaceTerrainControl0,
                    input.terrainUv);
                half4 control1 = SAMPLE_TEXTURE2D(
                    _AnimeSurfaceTerrainControl1,
                    sampler_AnimeSurfaceTerrainControl1,
                    input.terrainUv);
                if (_AnimeSurfaceTerrainLayerCount < 0.5h)
                {
                    control0 = half4(1.0h, 0.0h, 0.0h, 0.0h);
                    control1 = 0.0h;
                }

                half weights[8] =
                {
                    control0.r, control0.g, control0.b, control0.a,
                    control1.r, control1.g, control1.b, control1.a
                };
                half3 layerColors[8] =
                {
                    SAMPLE_TEXTURE2D(_AnimeSurfaceTerrainLayer0, sampler_AnimeSurfaceTerrainLayer0, input.localXZ * _AnimeSurfaceTerrainLayerST0.xy + _AnimeSurfaceTerrainLayerST0.zw).rgb,
                    SAMPLE_TEXTURE2D(_AnimeSurfaceTerrainLayer1, sampler_AnimeSurfaceTerrainLayer1, input.localXZ * _AnimeSurfaceTerrainLayerST1.xy + _AnimeSurfaceTerrainLayerST1.zw).rgb,
                    SAMPLE_TEXTURE2D(_AnimeSurfaceTerrainLayer2, sampler_AnimeSurfaceTerrainLayer2, input.localXZ * _AnimeSurfaceTerrainLayerST2.xy + _AnimeSurfaceTerrainLayerST2.zw).rgb,
                    SAMPLE_TEXTURE2D(_AnimeSurfaceTerrainLayer3, sampler_AnimeSurfaceTerrainLayer3, input.localXZ * _AnimeSurfaceTerrainLayerST3.xy + _AnimeSurfaceTerrainLayerST3.zw).rgb,
                    SAMPLE_TEXTURE2D(_AnimeSurfaceTerrainLayer4, sampler_AnimeSurfaceTerrainLayer4, input.localXZ * _AnimeSurfaceTerrainLayerST4.xy + _AnimeSurfaceTerrainLayerST4.zw).rgb,
                    SAMPLE_TEXTURE2D(_AnimeSurfaceTerrainLayer5, sampler_AnimeSurfaceTerrainLayer5, input.localXZ * _AnimeSurfaceTerrainLayerST5.xy + _AnimeSurfaceTerrainLayerST5.zw).rgb,
                    SAMPLE_TEXTURE2D(_AnimeSurfaceTerrainLayer6, sampler_AnimeSurfaceTerrainLayer6, input.localXZ * _AnimeSurfaceTerrainLayerST6.xy + _AnimeSurfaceTerrainLayerST6.zw).rgb,
                    SAMPLE_TEXTURE2D(_AnimeSurfaceTerrainLayer7, sampler_AnimeSurfaceTerrainLayer7, input.localXZ * _AnimeSurfaceTerrainLayerST7.xy + _AnimeSurfaceTerrainLayerST7.zw).rgb
                };

                half3 terrainColor = 0.0h;
                half totalWeight = 0.0h;
                [unroll]
                for (int layerIndex = 0; layerIndex < 8; layerIndex++)
                {
                    half active = step((half)layerIndex + 0.5h, _AnimeSurfaceTerrainLayerCount);
                    half weight = weights[layerIndex] * active;
                    terrainColor += layerColors[layerIndex] * weight;
                    totalWeight += weight;
                }

                terrainColor = totalWeight > 0.0001h
                    ? terrainColor / totalWeight
                    : layerColors[0];
                half normalizedHeight = saturate(
                    (positionWS.y - _AnimeSurfaceCaptureHeightParams.x)
                    * _AnimeSurfaceCaptureHeightParams.y);
                float4 terrainPositionCS = TransformWorldToHClip(positionWS);

                output.color = half4(terrainColor * _AnimeSurfaceTerrainColorMultiplier.rgb, 1.0h);
                output.normalHeight = half4(normalWS * 0.5h + 0.5h, normalizedHeight);
                output.masks = saturate(_AnimeSurfaceTerrainMask);
                output.depth = saturate(terrainPositionCS.z / terrainPositionCS.w);
                return output;
            }
            ENDHLSL
        }
    }
}
