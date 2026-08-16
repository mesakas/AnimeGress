using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Enlyn.Grass
{
    public enum AnimeGrassQualityProfile
    {
        [InspectorName("自动（按构建平台）")]
        Automatic,
        [InspectorName("桌面端完整效果")]
        Desktop,
        [InspectorName("移动端优化")]
        Mobile
    }

    [System.Flags]
    public enum AnimeGrassMobileFeature
    {
        [InspectorName("无")]
        None = 0,
        [InspectorName("实体草地表缓存响应")]
        SurfaceCache = 1 << 0,
        [InspectorName("实时 GrassVolume 交互")]
        RealtimeInteraction = 1 << 1,
        [InspectorName("主光源实时阴影")]
        MainLightShadows = 1 << 2,
        [InspectorName("远景草覆盖")]
        FarField = 1 << 3,
        [InspectorName("远景动态明暗图案")]
        FarFieldPattern = 1 << 4
    }

    public sealed class AnimeGrassRendererFeature : ScriptableRendererFeature
    {
        private const string DisableSurfaceCacheKeyword = "ENLYN_GRASS_DISABLE_SURFACE_CACHE";
        private const string DisableInteractionKeyword = "ENLYN_GRASS_DISABLE_INTERACTION";
        private const string DisableShadowsKeyword = "ENLYN_GRASS_DISABLE_SHADOWS";
        private const string DisableFarPatternKeyword = "ENLYN_GRASS_DISABLE_FAR_PATTERN";

        [SerializeField]
        private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [SerializeField]
        private AnimeGrassQualityProfile qualityProfile = AnimeGrassQualityProfile.Automatic;

        [SerializeField]
        private AnimeGrassMobileFeature mobileFeatures = AnimeGrassMobileFeature.FarField;

        private AnimeGrassRenderPass renderPass;

        public override void Create()
        {
            renderPass = new AnimeGrassRenderPass
            {
                renderPassEvent = renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera renderCamera = renderingData.cameraData.camera;
            bool useMobileProfile = ResolveMobileProfile();
            bool useSurfaceCache = IsFeatureEnabled(
                useMobileProfile,
                AnimeGrassMobileFeature.SurfaceCache);
            bool useRealtimeInteraction = IsFeatureEnabled(
                useMobileProfile,
                AnimeGrassMobileFeature.RealtimeInteraction);
            bool useMainLightShadows = IsFeatureEnabled(
                useMobileProfile,
                AnimeGrassMobileFeature.MainLightShadows);
            bool useFarFieldPattern = IsFeatureEnabled(
                useMobileProfile,
                AnimeGrassMobileFeature.FarFieldPattern);
            bool allowFarFields = IsFeatureEnabled(
                useMobileProfile,
                AnimeGrassMobileFeature.FarField);
            bool renderFarFields = allowFarFields && AnimeGrassFarField.ShouldRenderAny(renderCamera);
            bool renderGrassFields = AnimeGrassField.ShouldRenderAny(renderCamera);
            if (renderPass == null
                || !AnimeGrassField.ShouldRenderCamera(renderCamera)
                || (!renderGrassFields && !renderFarFields))
            {
                return;
            }

            renderPass.renderPassEvent = renderPassEvent;
            renderPass.Setup(
                useSurfaceCache,
                useRealtimeInteraction,
                useMainLightShadows,
                useFarFieldPattern,
                renderFarFields);
            renderPass.ConfigureInput(renderFarFields
                ? ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal
                : ScriptableRenderPassInput.None);
            renderer.EnqueuePass(renderPass);
        }

        private bool IsFeatureEnabled(
            bool useMobileProfile,
            AnimeGrassMobileFeature feature)
        {
            return !useMobileProfile || (mobileFeatures & feature) != 0;
        }

        private bool ResolveMobileProfile()
        {
            if (qualityProfile == AnimeGrassQualityProfile.Desktop)
            {
                return false;
            }

            if (qualityProfile == AnimeGrassQualityProfile.Mobile)
            {
                return true;
            }

#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            return Application.isMobilePlatform;
#endif
        }

        private sealed class AnimeGrassRenderPass : ScriptableRenderPass
        {
            private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler("Enlyn Anime Grass");
            private bool useSurfaceCache;
            private bool useRealtimeInteraction;
            private bool useMainLightShadows;
            private bool useFarFieldPattern;
            private bool renderFarFields;

            public void Setup(
                bool surfaceCache,
                bool realtimeInteraction,
                bool mainLightShadows,
                bool farFieldPattern,
                bool farFields)
            {
                useSurfaceCache = surfaceCache;
                useRealtimeInteraction = realtimeInteraction;
                useMainLightShadows = mainLightShadows;
                useFarFieldPattern = farFieldPattern;
                renderFarFields = farFields;
            }

            [System.Obsolete("Compatibility Mode render pass used by this project.")]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Camera renderCamera = renderingData.cameraData.camera;
                if (!AnimeGrassField.ShouldRenderCamera(renderCamera))
                {
                    return;
                }

                CommandBuffer commandBuffer = CommandBufferPool.Get();
                using (new ProfilingScope(commandBuffer, ProfilingSampler))
                {
                    CoreUtils.SetKeyword(commandBuffer, DisableSurfaceCacheKeyword, !useSurfaceCache);
                    CoreUtils.SetKeyword(commandBuffer, DisableInteractionKeyword, !useRealtimeInteraction);
                    CoreUtils.SetKeyword(commandBuffer, DisableShadowsKeyword, !useMainLightShadows);
                    CoreUtils.SetKeyword(commandBuffer, DisableFarPatternKeyword, !useFarFieldPattern);
                    if (useSurfaceCache)
                    {
                        AnimeSurfaceCache.BindForCamera(renderCamera, commandBuffer);
                    }

                    if (useRealtimeInteraction)
                    {
                        GrassVolume.ApplyGrassInteractionGlobals(commandBuffer);
                    }
                    else
                    {
                        GrassVolume.DisableGrassInteractionGlobals(commandBuffer);
                    }

                    if (renderFarFields)
                    {
                        AnimeGrassFarField.RenderAll(renderCamera, commandBuffer);
                    }

                    AnimeGrassField.MarkRenderedByRendererFeature(renderCamera);
                    var fields = AnimeGrassField.ActiveFields;
                    for (int i = 0; i < fields.Count; i++)
                    {
                        AnimeGrassField field = fields[i];
                        if (field != null && field.isActiveAndEnabled)
                        {
                            field.RenderForCamera(renderCamera, commandBuffer, false);
                        }
                    }
                }

                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }
        }
    }
}
