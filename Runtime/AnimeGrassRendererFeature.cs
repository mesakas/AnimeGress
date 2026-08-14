using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Enlyn.Grass
{
    public sealed class AnimeGrassRendererFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

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
            if (!Application.isPlaying
                || renderPass == null
                || !AnimeGrassField.HasActiveFields
                || !AnimeGrassField.ShouldRenderCamera(renderCamera))
            {
                return;
            }

            renderPass.renderPassEvent = renderPassEvent;
            renderer.EnqueuePass(renderPass);
        }

        private sealed class AnimeGrassRenderPass : ScriptableRenderPass
        {
            private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler("Enlyn Anime Grass");

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
                    AnimeGrassField.MarkRenderedByRendererFeature(renderCamera);
                    var fields = AnimeGrassField.ActiveFields;
                    for (int i = 0; i < fields.Count; i++)
                    {
                        AnimeGrassField field = fields[i];
                        if (field != null && field.isActiveAndEnabled)
                        {
                            field.RenderForCamera(renderCamera, commandBuffer);
                        }
                    }
                }

                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }
        }
    }
}
