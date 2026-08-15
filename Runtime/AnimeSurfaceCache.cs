using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Enlyn.Grass
{
    public enum AnimeSurfaceCacheUpdateMode
    {
        OnChange,
        Interval,
        EveryFrame,
        Manual
    }

    [ExecuteAlways]
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class AnimeSurfaceCache : MonoBehaviour
    {
        private const string CaptureShaderResource = "AnimeGress/AnimeSurfaceCacheCapture";
        private const string CaptureShaderName = "Hidden/AnimeGress/Surface Cache Capture";

        private static readonly List<AnimeSurfaceCache> ActiveCacheList = new List<AnimeSurfaceCache>();
        private static readonly int CacheColorTextureId = Shader.PropertyToID("_AnimeSurfaceCacheColorTexture");
        private static readonly int CacheDataTextureId = Shader.PropertyToID("_AnimeSurfaceCacheDataTexture");
        private static readonly int CacheMaskTextureId = Shader.PropertyToID("_AnimeSurfaceCacheMaskTexture");
        private static readonly int CacheWorldToUvId = Shader.PropertyToID("_AnimeSurfaceCacheWorldToUV");
        private static readonly int CacheHeightParamsId = Shader.PropertyToID("_AnimeSurfaceCacheHeightParams");
        private static readonly int CacheTexelSizeId = Shader.PropertyToID("_AnimeSurfaceCacheTexelSize");
        private static readonly int CacheEnabledId = Shader.PropertyToID("_AnimeSurfaceCacheEnabled");

        private static readonly int SourceBaseMapId = Shader.PropertyToID("_AnimeSurfaceSourceBaseMap");
        private static readonly int SourceBaseColorId = Shader.PropertyToID("_AnimeSurfaceSourceBaseColor");
        private static readonly int SourceBaseMapStId = Shader.PropertyToID("_AnimeSurfaceSourceBaseMap_ST");
        private static readonly int SourceAlphaClipId = Shader.PropertyToID("_AnimeSurfaceSourceAlphaClip");
        private static readonly int SourceCutoffId = Shader.PropertyToID("_AnimeSurfaceSourceCutoff");
        private static readonly int SourceNormalFlattenId = Shader.PropertyToID("_AnimeSurfaceSourceNormalFlatten");
        private static readonly int SourceMaskId = Shader.PropertyToID("_AnimeSurfaceSourceMask");
        private static readonly int CaptureHeightParamsId = Shader.PropertyToID("_AnimeSurfaceCaptureHeightParams");
        private static readonly int DepthTestId = Shader.PropertyToID("_AnimeSurfaceDepthTest");

        private static readonly int StampMaskId = Shader.PropertyToID("_AnimeSurfaceStampMask");
        private static readonly int StampParamsId = Shader.PropertyToID("_AnimeSurfaceStampParams");
        private static readonly int StampWorldToLocalId = Shader.PropertyToID("_AnimeSurfaceStampWorldToLocal");
        private static readonly int StampDataTextureId = Shader.PropertyToID("_AnimeSurfaceStampDataTexture");
        private static readonly int StampColorTextureId = Shader.PropertyToID("_AnimeSurfaceStampColorTexture");

        private static readonly int TerrainHeightmapId = Shader.PropertyToID("_AnimeSurfaceTerrainHeightmap");
        private static readonly int TerrainControl0Id = Shader.PropertyToID("_AnimeSurfaceTerrainControl0");
        private static readonly int TerrainControl1Id = Shader.PropertyToID("_AnimeSurfaceTerrainControl1");
        private static readonly int TerrainPositionId = Shader.PropertyToID("_AnimeSurfaceTerrainPosition");
        private static readonly int TerrainSizeId = Shader.PropertyToID("_AnimeSurfaceTerrainSize");
        private static readonly int TerrainHeightmapTexelSizeId = Shader.PropertyToID("_AnimeSurfaceTerrainHeightmapTexelSize");
        private static readonly int TerrainLayerCountId = Shader.PropertyToID("_AnimeSurfaceTerrainLayerCount");
        private static readonly int TerrainColorMultiplierId = Shader.PropertyToID("_AnimeSurfaceTerrainColorMultiplier");
        private static readonly int TerrainNormalFlattenId = Shader.PropertyToID("_AnimeSurfaceTerrainNormalFlatten");
        private static readonly int TerrainMaskId = Shader.PropertyToID("_AnimeSurfaceTerrainMask");
        private static readonly int[] TerrainLayerMapIds = CreatePropertyIds("_AnimeSurfaceTerrainLayer", 8);
        private static readonly int[] TerrainLayerStIds = CreatePropertyIds("_AnimeSurfaceTerrainLayerST", 8);

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");

        [SerializeField]
        private Vector2 worldSize = new Vector2(128f, 128f);

        [SerializeField, Min(1f)]
        private float captureHeight = 80f;

        [SerializeField, Range(128, 4096)]
        private int resolution = 1024;

        [SerializeField]
        private LayerMask surfaceLayers = ~0;

        [SerializeField]
        private bool automaticRendererCollection = true;

        [SerializeField]
        private List<Renderer> explicitRenderers = new List<Renderer>();

        [SerializeField]
        private bool captureUnityTerrains = true;

        [SerializeField]
        private List<Terrain> explicitTerrains = new List<Terrain>();

        [SerializeField]
        private bool includeInactiveRenderers;

        [SerializeField]
        private AnimeSurfaceCacheUpdateMode updateMode = AnimeSurfaceCacheUpdateMode.OnChange;

        [SerializeField, Min(0.02f)]
        private float updateInterval = 0.25f;

        [SerializeField]
        private bool updateInEditMode = true;

        [SerializeField]
        private Transform followTarget;

        [SerializeField, Min(0f)]
        private float followSnapInTexels = 8f;

        [SerializeField]
        private int priority;

        [SerializeField]
        private Color emptySurfaceColor = new Color(1f, 1f, 1f, 0f);

        [SerializeField]
        private bool drawBounds = true;

        [SerializeField]
        private bool showBoundsWhenUnselected;

        [SerializeField]
        private Color boundsColor = new Color(0.2f, 0.95f, 0.75f, 0.8f);

        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly HashSet<int> rendererIds = new HashSet<int>();
        private readonly List<Terrain> terrains = new List<Terrain>();
        private readonly HashSet<int> terrainIds = new HashSet<int>();
        private MaterialPropertyBlock sourceProperties;

        private RenderTexture colorTexture;
        private RenderTexture dataTexture;
        private RenderTexture maskTexture;
        private RenderTexture depthTexture;
        private Material captureMaterial;
        private Mesh stampMesh;
        private bool dirty = true;
        private bool loggedMissingShader;
        private float nextUpdateTime;
        private Vector3 lastTransformPosition;
        private int lastRendererCount;
        private int lastTerrainCount;
        private int lastDrawCallCount;
        private int lastStampCount;
        private int lastUpdateFrame = -1;

        internal static IReadOnlyList<AnimeSurfaceCache> ActiveCaches => ActiveCacheList;
        public Vector2 WorldSize => worldSize;
        public float CaptureHeight => captureHeight;
        public int Resolution => resolution;
        public RenderTexture ColorTexture => colorTexture;
        public RenderTexture DataTexture => dataTexture;
        public RenderTexture MaskTexture => maskTexture;
        public bool IsDirty => dirty;
        public int LastRendererCount => lastRendererCount;
        public int LastTerrainCount => lastTerrainCount;
        public int LastDrawCallCount => lastDrawCallCount;
        public int LastStampCount => lastStampCount;
        public int LastUpdateFrame => lastUpdateFrame;
        public bool DrawBounds => drawBounds;
        public bool ShowBoundsWhenUnselected => showBoundsWhenUnselected;
        public Color BoundsColor => boundsColor;
        public Bounds WorldBounds => new Bounds(
            transform.position,
            new Vector3(worldSize.x, captureHeight, worldSize.y));

        private void OnEnable()
        {
            if (!ActiveCacheList.Contains(this))
            {
                ActiveCacheList.Add(this);
            }

            lastTransformPosition = transform.position;
            dirty = true;
            if (sourceProperties == null)
            {
                sourceProperties = new MaterialPropertyBlock();
            }

            EnsureResources();
            ApplyGlobalProperties(null);
        }

        private void OnDisable()
        {
            ActiveCacheList.Remove(this);
            ReleaseResources();
            BindBestAvailable(null);
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

        private void OnValidate()
        {
            worldSize.x = Mathf.Max(1f, worldSize.x);
            worldSize.y = Mathf.Max(1f, worldSize.y);
            captureHeight = Mathf.Max(1f, captureHeight);
            resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 128, 4096);
            updateInterval = Mathf.Max(0.02f, updateInterval);
            followSnapInTexels = Mathf.Max(0f, followSnapInTexels);
            dirty = true;

            if (isActiveAndEnabled)
            {
                EnsureResources();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying && !updateInEditMode)
            {
                return;
            }

            UpdateFollowPosition();

            if (transform.position != lastTransformPosition)
            {
                lastTransformPosition = transform.position;
                dirty = true;
            }

            TryRefreshIfRequired();
            ApplyGlobalProperties(null);
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        public void MarkDirty(Bounds changedBounds)
        {
            if (WorldBounds.Intersects(changedBounds))
            {
                dirty = true;
            }
        }

        [ContextMenu("Refresh Surface Cache")]
        public void RefreshNow()
        {
            dirty = true;
            RebuildCache();
            ApplyGlobalProperties(null);
        }

        public bool ContainsWorldPosition(Vector3 position)
        {
            Bounds bounds = WorldBounds;
            return position.x >= bounds.min.x && position.x <= bounds.max.x
                && position.z >= bounds.min.z && position.z <= bounds.max.z;
        }

        public static void RequestAllRefresh()
        {
            for (int i = 0; i < ActiveCacheList.Count; i++)
            {
                AnimeSurfaceCache cache = ActiveCacheList[i];
                if (cache != null)
                {
                    cache.dirty = true;
                }
            }
        }

        internal static void NotifyChanged(Bounds changedBounds)
        {
            for (int i = 0; i < ActiveCacheList.Count; i++)
            {
                AnimeSurfaceCache cache = ActiveCacheList[i];
                if (cache != null)
                {
                    cache.MarkDirty(changedBounds);
                }
            }
        }

        internal static void BindForCamera(Camera camera, CommandBuffer commandBuffer)
        {
            AnimeSurfaceCache cache = SelectCache(camera);
            if (cache == null)
            {
                SetGlobalFloat(commandBuffer, CacheEnabledId, 0f);
                return;
            }

            cache.ApplyGlobalProperties(commandBuffer);
        }

        private static void BindBestAvailable(CommandBuffer commandBuffer)
        {
            AnimeSurfaceCache cache = SelectCache(null);
            if (cache == null)
            {
                SetGlobalFloat(commandBuffer, CacheEnabledId, 0f);
                return;
            }

            cache.ApplyGlobalProperties(commandBuffer);
        }

        private static AnimeSurfaceCache SelectCache(Camera camera)
        {
            AnimeSurfaceCache best = null;
            bool bestContainsCamera = false;

            for (int i = 0; i < ActiveCacheList.Count; i++)
            {
                AnimeSurfaceCache candidate = ActiveCacheList[i];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.colorTexture == null)
                {
                    continue;
                }

                bool containsCamera = camera != null && candidate.ContainsWorldPosition(camera.transform.position);
                if (best == null
                    || containsCamera && !bestContainsCamera
                    || containsCamera == bestContainsCamera && candidate.priority > best.priority)
                {
                    best = candidate;
                    bestContainsCamera = containsCamera;
                }
            }

            return best;
        }

        private void TryRefreshIfRequired()
        {
            if (!isActiveAndEnabled || (!Application.isPlaying && !updateInEditMode))
            {
                return;
            }

            bool shouldRefresh = false;
            switch (updateMode)
            {
                case AnimeSurfaceCacheUpdateMode.OnChange:
                    shouldRefresh = dirty;
                    break;
                case AnimeSurfaceCacheUpdateMode.Interval:
                    shouldRefresh = dirty || Time.realtimeSinceStartup >= nextUpdateTime;
                    break;
                case AnimeSurfaceCacheUpdateMode.EveryFrame:
                    shouldRefresh = true;
                    break;
                case AnimeSurfaceCacheUpdateMode.Manual:
                    shouldRefresh = false;
                    break;
            }

            if (shouldRefresh)
            {
                RebuildCache();
                nextUpdateTime = Time.realtimeSinceStartup + updateInterval;
            }
        }

        private void UpdateFollowPosition()
        {
            if (followTarget == null)
            {
                return;
            }

            float texelWorldSize = Mathf.Max(worldSize.x, worldSize.y) / Mathf.Max(1, resolution);
            float snap = texelWorldSize * followSnapInTexels;
            Vector3 targetPosition = followTarget.position;
            if (snap > 0.0001f)
            {
                targetPosition.x = Mathf.Round(targetPosition.x / snap) * snap;
                targetPosition.z = Mathf.Round(targetPosition.z / snap) * snap;
            }

            Vector3 position = transform.position;
            position.x = targetPosition.x;
            position.z = targetPosition.z;
            if (position != transform.position)
            {
                transform.position = position;
            }
        }

        private void RebuildCache()
        {
            if (!EnsureResources())
            {
                return;
            }

            CollectSurfaces();
            Bounds cacheBounds = WorldBounds;
            float minHeight = cacheBounds.min.y;
            float maxHeight = cacheBounds.max.y;
            Vector3 cameraPosition = new Vector3(cacheBounds.center.x, maxHeight + 0.1f, cacheBounds.center.z);
            Matrix4x4 viewMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                * Matrix4x4.TRS(
                    cameraPosition,
                    Quaternion.Euler(90f, 0f, 0f),
                    Vector3.one).inverse;
            Matrix4x4 projectionMatrix = Matrix4x4.Ortho(
                -worldSize.x * 0.5f,
                worldSize.x * 0.5f,
                -worldSize.y * 0.5f,
                worldSize.y * 0.5f,
                0.01f,
                captureHeight + 0.2f);
            projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, true);

            CommandBuffer commandBuffer = CommandBufferPool.Get("AnimeGress Surface Cache");
            RenderTargetIdentifier colorTarget = colorTexture;
            RenderTargetIdentifier dataTarget = dataTexture;
            RenderTargetIdentifier maskTarget = maskTexture;
            RenderTargetIdentifier depthTarget = depthTexture;

            float clearDepth = SystemInfo.usesReversedZBuffer ? 0f : 1f;
            commandBuffer.SetRenderTarget(colorTarget, depthTarget);
            commandBuffer.ClearRenderTarget(true, true, emptySurfaceColor, clearDepth);
            commandBuffer.SetRenderTarget(dataTarget, depthTarget);
            commandBuffer.ClearRenderTarget(false, true, new Color(0.5f, 1f, 0.5f, 0f));
            commandBuffer.SetRenderTarget(maskTarget, depthTarget);
            commandBuffer.ClearRenderTarget(false, true, Color.clear);

            commandBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            commandBuffer.SetGlobalVector(
                CaptureHeightParamsId,
                new Vector4(minHeight, 1f / Mathf.Max(0.001f, maxHeight - minHeight), maxHeight, 0f));
            commandBuffer.SetRenderTarget(
                new[] { colorTarget, dataTarget, maskTarget },
                depthTarget);

            lastRendererCount = 0;
            lastTerrainCount = 0;
            lastDrawCallCount = 0;
            for (int rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                Renderer targetRenderer = renderers[rendererIndex];
                if (!IsRendererEligible(targetRenderer, cacheBounds))
                {
                    continue;
                }

                AnimeSurfaceCacheSource source = targetRenderer.GetComponentInParent<AnimeSurfaceCacheSource>();
                if (source != null && source.isActiveAndEnabled && source.ExcludeFromCache)
                {
                    continue;
                }

                Material[] materials = targetRenderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    continue;
                }

                bool rendererDrawn = false;
                int subMeshCount = GetSubMeshCount(targetRenderer, materials.Length);
                for (int materialIndex = 0; materialIndex < subMeshCount; materialIndex++)
                {
                    Material sourceMaterial = materials[materialIndex];
                    if (sourceMaterial == null)
                    {
                        continue;
                    }

                    ConfigureSource(commandBuffer, targetRenderer, materialIndex, sourceMaterial, source);
                    commandBuffer.DrawRenderer(targetRenderer, captureMaterial, materialIndex, 0);
                    lastDrawCallCount++;
                    rendererDrawn = true;
                }

                if (rendererDrawn)
                {
                    lastRendererCount++;
                }
            }

            DrawTerrains(commandBuffer, cacheBounds);

            DrawStamps(commandBuffer, viewMatrix, projectionMatrix, cacheBounds, maskTarget);
            commandBuffer.GenerateMips(colorTarget);
            commandBuffer.GenerateMips(dataTarget);
            commandBuffer.GenerateMips(maskTarget);
            commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

            Graphics.ExecuteCommandBuffer(commandBuffer);
            CommandBufferPool.Release(commandBuffer);
            dirty = false;
            lastUpdateFrame = Time.frameCount;
        }

        private void ConfigureSource(
            CommandBuffer commandBuffer,
            Renderer targetRenderer,
            int materialIndex,
            Material sourceMaterial,
            AnimeSurfaceCacheSource source)
        {
            if (sourceProperties == null)
            {
                sourceProperties = new MaterialPropertyBlock();
            }

            sourceProperties.Clear();
            targetRenderer.GetPropertyBlock(sourceProperties, materialIndex);

            Texture baseMap = GetTexture(sourceProperties, sourceMaterial, BaseMapId)
                ?? GetTexture(sourceProperties, sourceMaterial, MainTexId)
                ?? Texture2D.whiteTexture;
            int texturePropertyId = sourceMaterial.HasProperty(BaseMapId) ? BaseMapId : MainTexId;
            Vector2 textureScale = sourceMaterial.HasProperty(texturePropertyId)
                ? sourceMaterial.GetTextureScale(texturePropertyId)
                : Vector2.one;
            Vector2 textureOffset = sourceMaterial.HasProperty(texturePropertyId)
                ? sourceMaterial.GetTextureOffset(texturePropertyId)
                : Vector2.zero;

            Color baseColor = GetColor(sourceProperties, sourceMaterial, BaseColorId, Color.white);
            if (!sourceMaterial.HasProperty(BaseColorId))
            {
                baseColor = GetColor(sourceProperties, sourceMaterial, ColorId, Color.white);
            }

            bool alphaClip = sourceMaterial.IsKeywordEnabled("_ALPHATEST_ON")
                || sourceMaterial.HasProperty(AlphaClipId) && sourceMaterial.GetFloat(AlphaClipId) > 0.5f;
            float cutoff = sourceMaterial.HasProperty(CutoffId) ? sourceMaterial.GetFloat(CutoffId) : 0.5f;
            float normalFlatten = 0f;
            Vector4 surfaceMask = Vector4.zero;

            if (source != null && source.isActiveAndEnabled)
            {
                if (source.OverrideBaseMap)
                {
                    baseMap = source.BaseMap != null ? source.BaseMap : Texture2D.whiteTexture;
                    textureScale = source.BaseMapScale;
                    textureOffset = source.BaseMapOffset;
                }

                if (source.OverrideBaseColor)
                {
                    baseColor = source.BaseColor;
                }

                baseColor *= source.ColorMultiplier;
                normalFlatten = source.NormalFlattening;
                surfaceMask = source.SurfaceMask;
                if (source.OverrideAlphaClip)
                {
                    alphaClip = source.AlphaClip;
                    cutoff = source.AlphaCutoff;
                }
            }

            commandBuffer.SetGlobalTexture(SourceBaseMapId, baseMap);
            commandBuffer.SetGlobalColor(SourceBaseColorId, baseColor);
            commandBuffer.SetGlobalVector(
                SourceBaseMapStId,
                new Vector4(textureScale.x, textureScale.y, textureOffset.x, textureOffset.y));
            commandBuffer.SetGlobalFloat(SourceAlphaClipId, alphaClip ? 1f : 0f);
            commandBuffer.SetGlobalFloat(SourceCutoffId, cutoff);
            commandBuffer.SetGlobalFloat(SourceNormalFlattenId, normalFlatten);
            commandBuffer.SetGlobalVector(SourceMaskId, surfaceMask);
        }

        private void DrawStamps(
            CommandBuffer commandBuffer,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            Bounds cacheBounds,
            RenderTargetIdentifier maskTarget)
        {
            lastStampCount = 0;
            IReadOnlyList<GressVolume> volumes = GressVolume.ActiveVolumes;
            if (volumes.Count == 0)
            {
                return;
            }

            commandBuffer.SetRenderTarget(maskTarget);
            commandBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            commandBuffer.SetGlobalTexture(StampDataTextureId, dataTexture);
            commandBuffer.SetGlobalTexture(StampColorTextureId, colorTexture);
            for (int i = 0; i < volumes.Count; i++)
            {
                GressVolume volume = volumes[i];
                Vector4 stampMask = volume != null ? volume.SurfaceMask : Vector4.zero;
                if (volume == null
                    || !volume.isActiveAndEnabled
                    || !volume.ShouldRender
                    || stampMask.sqrMagnitude <= 0.000001f
                    || !cacheBounds.Intersects(volume.WorldBounds))
                {
                    continue;
                }

                commandBuffer.SetGlobalVector(StampMaskId, stampMask);
                commandBuffer.SetGlobalVector(
                    StampParamsId,
                    new Vector4(
                        volume.Shape == GressVolumeShape.Sphere ? 0f : 1f,
                        volume.Hardness,
                        0f,
                        0f));
                commandBuffer.SetGlobalMatrix(StampWorldToLocalId, volume.WorldToLocalMatrix);
                commandBuffer.DrawMesh(stampMesh, volume.StampDrawMatrix, captureMaterial, 0, 1);
                lastStampCount++;
            }
        }

        private void DrawTerrains(CommandBuffer commandBuffer, Bounds cacheBounds)
        {
            for (int terrainIndex = 0; terrainIndex < terrains.Count; terrainIndex++)
            {
                Terrain terrain = terrains[terrainIndex];
                if (!IsTerrainEligible(terrain, cacheBounds))
                {
                    continue;
                }

                TerrainData terrainData = terrain.terrainData;
                AnimeSurfaceCacheSource source = terrain.GetComponent<AnimeSurfaceCacheSource>();
                if (source != null && source.isActiveAndEnabled && source.ExcludeFromCache)
                {
                    continue;
                }

                ConfigureTerrain(commandBuffer, terrain, terrainData, source);
                Vector3 terrainPosition = terrain.transform.position;
                Vector3 terrainSize = terrainData.size;
                Matrix4x4 terrainMatrix = Matrix4x4.TRS(
                    terrainPosition + new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f),
                    Quaternion.identity,
                    new Vector3(terrainSize.x, 1f, terrainSize.z));
                commandBuffer.DrawMesh(stampMesh, terrainMatrix, captureMaterial, 0, 2);
                lastTerrainCount++;
                lastDrawCallCount++;
            }
        }

        private void ConfigureTerrain(
            CommandBuffer commandBuffer,
            Terrain terrain,
            TerrainData terrainData,
            AnimeSurfaceCacheSource source)
        {
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            Texture heightmap = terrainData.heightmapTexture != null
                ? terrainData.heightmapTexture
                : Texture2D.blackTexture;
            Texture[] controlTextures = terrainData.alphamapTextures;
            TerrainLayer[] layers = terrainData.terrainLayers;
            int layerCount = Mathf.Min(layers != null ? layers.Length : 0, 8);

            commandBuffer.SetGlobalTexture(TerrainHeightmapId, heightmap);
            commandBuffer.SetGlobalTexture(
                TerrainControl0Id,
                controlTextures != null && controlTextures.Length > 0
                    ? controlTextures[0]
                    : Texture2D.whiteTexture);
            commandBuffer.SetGlobalTexture(
                TerrainControl1Id,
                controlTextures != null && controlTextures.Length > 1
                    ? controlTextures[1]
                    : Texture2D.blackTexture);
            commandBuffer.SetGlobalVector(TerrainPositionId, terrainPosition);
            commandBuffer.SetGlobalVector(TerrainSizeId, terrainSize);
            commandBuffer.SetGlobalVector(
                TerrainHeightmapTexelSizeId,
                new Vector4(
                    1f / Mathf.Max(1, heightmap.width),
                    1f / Mathf.Max(1, heightmap.height),
                    heightmap.width,
                    heightmap.height));
            commandBuffer.SetGlobalFloat(TerrainLayerCountId, layerCount);
            commandBuffer.SetGlobalColor(
                TerrainColorMultiplierId,
                source != null && source.isActiveAndEnabled ? source.ColorMultiplier : Color.white);
            commandBuffer.SetGlobalFloat(
                TerrainNormalFlattenId,
                source != null && source.isActiveAndEnabled ? source.NormalFlattening : 0f);
            commandBuffer.SetGlobalVector(
                TerrainMaskId,
                source != null && source.isActiveAndEnabled ? source.SurfaceMask : Vector4.zero);

            for (int layerIndex = 0; layerIndex < 8; layerIndex++)
            {
                TerrainLayer layer = layerIndex < layerCount ? layers[layerIndex] : null;
                Texture diffuse = layer != null && layer.diffuseTexture != null
                    ? layer.diffuseTexture
                    : Texture2D.whiteTexture;
                Vector2 tileSize = layer != null ? layer.tileSize : new Vector2(terrainSize.x, terrainSize.z);
                Vector2 tileOffset = layer != null ? layer.tileOffset : Vector2.zero;
                commandBuffer.SetGlobalTexture(TerrainLayerMapIds[layerIndex], diffuse);
                commandBuffer.SetGlobalVector(
                    TerrainLayerStIds[layerIndex],
                    new Vector4(
                        1f / Mathf.Max(0.001f, tileSize.x),
                        1f / Mathf.Max(0.001f, tileSize.y),
                        tileOffset.x / Mathf.Max(0.001f, tileSize.x),
                        tileOffset.y / Mathf.Max(0.001f, tileSize.y)));
            }
        }

        private void CollectSurfaces()
        {
            renderers.Clear();
            rendererIds.Clear();
            terrains.Clear();
            terrainIds.Clear();

            if (automaticRendererCollection)
            {
                Renderer[] foundRenderers = FindObjectsByType<Renderer>(
                    includeInactiveRenderers ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (int i = 0; i < foundRenderers.Length; i++)
                {
                    AddRenderer(foundRenderers[i]);
                }
            }

            if (captureUnityTerrains)
            {
                Terrain[] foundTerrains = FindObjectsByType<Terrain>(
                    includeInactiveRenderers ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (int i = 0; i < foundTerrains.Length; i++)
                {
                    AddTerrain(foundTerrains[i]);
                }
            }

            if (explicitRenderers != null)
            {
                for (int i = 0; i < explicitRenderers.Count; i++)
                {
                    AddRenderer(explicitRenderers[i]);
                }
            }

            if (explicitTerrains != null)
            {
                for (int i = 0; i < explicitTerrains.Count; i++)
                {
                    AddTerrain(explicitTerrains[i]);
                }
            }
        }

        private void AddRenderer(Renderer targetRenderer)
        {
            if (targetRenderer == null || !rendererIds.Add(targetRenderer.GetInstanceID()))
            {
                return;
            }

            renderers.Add(targetRenderer);
        }

        private void AddTerrain(Terrain terrain)
        {
            if (terrain == null || !terrainIds.Add(terrain.GetInstanceID()))
            {
                return;
            }

            terrains.Add(terrain);
        }

        private bool IsRendererEligible(Renderer targetRenderer, Bounds cacheBounds)
        {
            if (targetRenderer == null
                || !targetRenderer.gameObject.scene.IsValid()
                || (!includeInactiveRenderers && (!targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy))
                || (surfaceLayers.value & (1 << targetRenderer.gameObject.layer)) == 0
                || !(targetRenderer is MeshRenderer || targetRenderer is SkinnedMeshRenderer)
                || targetRenderer.GetComponentInParent<AnimeGrassField>() != null
                || !cacheBounds.Intersects(targetRenderer.bounds))
            {
                return false;
            }

            HideFlags hideFlags = targetRenderer.gameObject.hideFlags;
            return (hideFlags & HideFlags.DontSave) == 0;
        }

        private static int GetSubMeshCount(Renderer targetRenderer, int materialCount)
        {
            Mesh mesh = null;
            if (targetRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                mesh = skinnedMeshRenderer.sharedMesh;
            }
            else if (targetRenderer is MeshRenderer)
            {
                MeshFilter meshFilter = targetRenderer.GetComponent<MeshFilter>();
                mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            }

            return mesh != null
                ? Mathf.Min(materialCount, mesh.subMeshCount)
                : materialCount;
        }

        private bool IsTerrainEligible(Terrain terrain, Bounds cacheBounds)
        {
            if (terrain == null
                || terrain.terrainData == null
                || !terrain.gameObject.scene.IsValid()
                || (!includeInactiveRenderers && !terrain.isActiveAndEnabled)
                || (surfaceLayers.value & (1 << terrain.gameObject.layer)) == 0)
            {
                return false;
            }

            Vector3 terrainSize = terrain.terrainData.size;
            Bounds terrainBounds = new Bounds(
                terrain.transform.position + terrainSize * 0.5f,
                terrainSize);
            return cacheBounds.Intersects(terrainBounds);
        }

        private bool EnsureResources()
        {
            int safeResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 128, 4096);
            bool texturesValid = colorTexture != null
                && colorTexture.IsCreated()
                && colorTexture.width == safeResolution;
            if (!texturesValid)
            {
                ReleaseTextures();
                colorTexture = CreateTexture(
                    "AnimeGress Surface Color",
                    safeResolution,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB,
                    0,
                    true);
                RenderTextureFormat dataFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                    ? RenderTextureFormat.ARGBHalf
                    : RenderTextureFormat.ARGB32;
                dataTexture = CreateTexture(
                    "AnimeGress Surface Normal Height",
                    safeResolution,
                    dataFormat,
                    RenderTextureReadWrite.Linear,
                    0,
                    true);
                maskTexture = CreateTexture(
                    "AnimeGress Surface Masks",
                    safeResolution,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear,
                    0,
                    true);
                depthTexture = CreateTexture(
                    "AnimeGress Surface Depth",
                    safeResolution,
                    RenderTextureFormat.Depth,
                    RenderTextureReadWrite.Linear,
                    24,
                    false);
                dirty = true;
                lastUpdateFrame = -1;
            }

            if (captureMaterial == null)
            {
                Shader captureShader = Resources.Load<Shader>(CaptureShaderResource);
                if (captureShader == null)
                {
                    captureShader = Shader.Find(CaptureShaderName);
                }

                if (captureShader == null)
                {
                    if (!loggedMissingShader)
                    {
                        Debug.LogError("[AnimeGress] Surface cache capture shader is missing.", this);
                        loggedMissingShader = true;
                    }

                    return false;
                }

                captureMaterial = new Material(captureShader)
                {
                    name = "AnimeGress Surface Cache Capture (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                captureMaterial.SetFloat(
                    DepthTestId,
                    SystemInfo.usesReversedZBuffer
                        ? (float)CompareFunction.GreaterEqual
                        : (float)CompareFunction.LessEqual);
                loggedMissingShader = false;
            }

            if (stampMesh == null)
            {
                stampMesh = CreateStampMesh();
            }

            return colorTexture != null && dataTexture != null && maskTexture != null && depthTexture != null;
        }

        private static RenderTexture CreateTexture(
            string textureName,
            int textureResolution,
            RenderTextureFormat format,
            RenderTextureReadWrite readWrite,
            int depthBits,
            bool useMipMap)
        {
            RenderTexture texture = new RenderTexture(
                textureResolution,
                textureResolution,
                depthBits,
                format,
                readWrite)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = useMipMap,
                autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }

        private static Mesh CreateStampMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "AnimeGress Surface Stamp Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, 0.5f),
                    new Vector3(-0.5f, 0f, 0.5f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ApplyGlobalProperties(CommandBuffer commandBuffer)
        {
            if (colorTexture == null
                || dataTexture == null
                || maskTexture == null
                || lastUpdateFrame < 0)
            {
                SetGlobalFloat(commandBuffer, CacheEnabledId, 0f);
                return;
            }

            Bounds bounds = WorldBounds;
            float cacheVScale = SystemInfo.graphicsUVStartsAtTop
                ? -1f / worldSize.y
                : 1f / worldSize.y;
            float cacheVOffset = SystemInfo.graphicsUVStartsAtTop
                ? 0.5f + bounds.center.z / worldSize.y
                : 0.5f - bounds.center.z / worldSize.y;
            Vector4 worldToUv = new Vector4(
                1f / worldSize.x,
                cacheVScale,
                0.5f - bounds.center.x / worldSize.x,
                cacheVOffset);
            Vector4 heightParams = new Vector4(
                bounds.min.y,
                1f / Mathf.Max(0.001f, bounds.size.y),
                bounds.max.y,
                0f);
            Vector4 texelSize = new Vector4(
                1f / colorTexture.width,
                1f / colorTexture.height,
                colorTexture.width,
                colorTexture.height);

            SetGlobalTexture(commandBuffer, CacheColorTextureId, colorTexture);
            SetGlobalTexture(commandBuffer, CacheDataTextureId, dataTexture);
            SetGlobalTexture(commandBuffer, CacheMaskTextureId, maskTexture);
            SetGlobalVector(commandBuffer, CacheWorldToUvId, worldToUv);
            SetGlobalVector(commandBuffer, CacheHeightParamsId, heightParams);
            SetGlobalVector(commandBuffer, CacheTexelSizeId, texelSize);
            SetGlobalFloat(commandBuffer, CacheEnabledId, 1f);
        }

        private static Texture GetTexture(
            MaterialPropertyBlock properties,
            Material material,
            int propertyId)
        {
            if (properties != null && properties.HasTexture(propertyId))
            {
                return properties.GetTexture(propertyId);
            }

            return material.HasProperty(propertyId) ? material.GetTexture(propertyId) : null;
        }

        private static int[] CreatePropertyIds(string prefix, int count)
        {
            int[] propertyIds = new int[count];
            for (int i = 0; i < count; i++)
            {
                propertyIds[i] = Shader.PropertyToID(prefix + i);
            }

            return propertyIds;
        }

        private static Color GetColor(
            MaterialPropertyBlock properties,
            Material material,
            int propertyId,
            Color fallback)
        {
            if (properties != null && properties.HasColor(propertyId))
            {
                return properties.GetColor(propertyId);
            }

            return material.HasProperty(propertyId) ? material.GetColor(propertyId) : fallback;
        }

        private static void SetGlobalTexture(CommandBuffer commandBuffer, int propertyId, Texture texture)
        {
            if (commandBuffer != null)
            {
                commandBuffer.SetGlobalTexture(propertyId, texture);
            }
            else
            {
                Shader.SetGlobalTexture(propertyId, texture);
            }
        }

        private static void SetGlobalVector(CommandBuffer commandBuffer, int propertyId, Vector4 value)
        {
            if (commandBuffer != null)
            {
                commandBuffer.SetGlobalVector(propertyId, value);
            }
            else
            {
                Shader.SetGlobalVector(propertyId, value);
            }
        }

        private static void SetGlobalFloat(CommandBuffer commandBuffer, int propertyId, float value)
        {
            if (commandBuffer != null)
            {
                commandBuffer.SetGlobalFloat(propertyId, value);
            }
            else
            {
                Shader.SetGlobalFloat(propertyId, value);
            }
        }

        private void ReleaseResources()
        {
            ReleaseTextures();
            DestroyUnityObject(captureMaterial);
            captureMaterial = null;
            DestroyUnityObject(stampMesh);
            stampMesh = null;
        }

        private void ReleaseTextures()
        {
            ReleaseTexture(ref colorTexture);
            ReleaseTexture(ref dataTexture);
            ReleaseTexture(ref maskTexture);
            ReleaseTexture(ref depthTexture);
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            if (texture.IsCreated())
            {
                texture.Release();
            }

            DestroyUnityObject(texture);
            texture = null;
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

    }
}
