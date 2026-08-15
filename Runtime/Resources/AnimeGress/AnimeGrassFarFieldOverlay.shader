Shader "Hidden/AnimeGress/Far Field Overlay"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "FarFieldOverlay"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_AnimeGrassFarCoverageTexture);
            SAMPLER(sampler_AnimeGrassFarCoverageTexture);
            TEXTURE2D(_AnimeGrassFarHeightTexture);
            SAMPLER(sampler_AnimeGrassFarHeightTexture);

            float4 _AnimeGrassFarWorldToUV;
            float4 _AnimeGrassFarHeightParams;
            float4 _AnimeGrassFarDistanceParams;
            half4 _AnimeGrassFarAppearanceParams;
            float4 _AnimeGrassFarDisturbanceParams;
            float4 _EnlynGrassWind;
            float4 _EnlynGrassWindParams;
            half4 _EnlynGrassWindTint;
            half _EnlynGrassWindTintStrength;

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
                half heightMatch = 1.0h - smoothstep(
                    _AnimeGrassFarHeightParams.z * 0.5,
                    _AnimeGrassFarHeightParams.z,
                    abs(positionWS.y - rootHeight));

                float3 worldDx = ddx(positionWS);
                float3 worldDy = ddy(positionWS);
                half3 surfaceNormal = normalize(cross(worldDy, worldDx));
                half normalRange = max(
                    0.001h,
                    min(0.2h, 1.0h - _AnimeGrassFarAppearanceParams.w));
                half upward = saturate(
                    (abs(surfaceNormal.y) - _AnimeGrassFarAppearanceParams.w)
                    / normalRange);
                float cameraDistance = distance(_WorldSpaceCameraPos, positionWS);
                half farFade = saturate(
                    (cameraDistance - _AnimeGrassFarDistanceParams.x)
                    * _AnimeGrassFarDistanceParams.z);

                float2 windDirection = _EnlynGrassWind.xy;
                windDirection = dot(windDirection, windDirection) > 0.0001
                    ? normalize(windDirection)
                    : float2(1.0, 0.0);
                float wave = sin(
                    dot(positionWS.xz, windDirection) * _EnlynGrassWindParams.x
                    + _Time.y * _EnlynGrassWind.w);
                float gust = sin(
                    (positionWS.x + positionWS.z) * _EnlynGrassWindParams.z
                    + _Time.y * _EnlynGrassWindParams.w);
                half wind01 = saturate((wave + gust * _EnlynGrassWindParams.y) * 0.5 + 0.5);
                half windTintAmount = saturate(
                    wind01
                    * _EnlynGrassWindTintStrength
                    * _AnimeGrassFarAppearanceParams.z);
                half3 coverageColor = coverage.rgb / max(coverage.a, 0.0001h);
                half3 farColor = lerp(
                    coverageColor,
                    coverageColor * _EnlynGrassWindTint.rgb,
                    windTintAmount);
                float2 disturbancePosition = positionWS.xz * _AnimeGrassFarDisturbanceParams.y
                    + windDirection
                    * (_Time.y * _AnimeGrassFarDisturbanceParams.z * _AnimeGrassFarDisturbanceParams.y);
                half disturbance = sin(dot(disturbancePosition, float2(2.17, 1.13)) + 0.7);
                disturbance += sin(dot(disturbancePosition, float2(-1.41, 2.73)) - 1.9) * 0.5h;
                disturbance += sin(dot(disturbancePosition, float2(3.87, 0.61)) + 2.4) * 0.25h;
                disturbance *= 0.5714286h;
                half disturbanceStrength = _AnimeGrassFarDisturbanceParams.x;
                half shadowSignal = saturate(
                    1.0h - wind01 + disturbance * disturbanceStrength * 0.35h);
                half shadowBand = smoothstep(0.35h, 0.9h, shadowSignal);
                half shadowBreakup = lerp(
                    1.0h,
                    saturate(0.78h + disturbance * 0.32h),
                    disturbanceStrength);
                shadowBand *= shadowBreakup;
                farColor *= 1.0h - shadowBand * _AnimeGrassFarAppearanceParams.y;

                half alpha = coverage.a
                    * _AnimeGrassFarAppearanceParams.x
                    * farFade
                    * heightMatch
                    * upward
                    * inside
                    * validDepth;
                return half4(farColor, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
