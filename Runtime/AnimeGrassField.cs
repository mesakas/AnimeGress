using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Enlyn.Grass
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AnimeGrassField : MonoBehaviour
    {
        private const int MaxBatchSize = 1023;
        private const int CurrentSettingsVersion = 2;
        private static readonly List<AnimeGrassField> ActiveFieldList = new List<AnimeGrassField>();
        private static readonly Dictionary<int, int> RendererFeatureRenderedFrames = new Dictionary<int, int>();
#if UNITY_EDITOR
        private const double EditorPreviewInterval = 1.0 / 30.0;
        private double nextEditorPreviewTime;
        private bool editorPreviewDirty = true;
        private GameObject editorPreviewRoot;
        private readonly List<Material> editorPreviewMaterials = new List<Material>();
        private readonly Dictionary<Material, Material> editorPreviewMaterialCache = new Dictionary<Material, Material>();
        private readonly List<EditorPreviewInstance> editorPreviewInstances = new List<EditorPreviewInstance>();
#endif

        private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");
        private static readonly int InstanceNormalId = Shader.PropertyToID("_InstanceNormal");
        private static readonly int InstanceWindWeightId = Shader.PropertyToID("_InstanceWindWeight");
        private static readonly int InstanceFadeId = Shader.PropertyToID("_InstanceFade");
        private static readonly int BatchReceiveShadowsId = Shader.PropertyToID("_BatchReceiveShadows");
        private static Mesh fallbackBladeMesh;

        [SerializeField]
        private List<AnimeGrassPrototype> prototypes = new List<AnimeGrassPrototype>();

        [SerializeField, HideInInspector]
        private List<AnimeGrassInstance> instances = new List<AnimeGrassInstance>();

        [SerializeField, Min(1f)]
        private float chunkSize = 24f;

        [SerializeField, Min(0f)]
        private float chunkBoundsPadding = 3f;

        [SerializeField]
        private bool frustumCulling = true;

        [SerializeField]
        private bool drawInEditMode = true;

        [SerializeField]
        private bool ignoreLodDistanceInEditMode;

        [SerializeField, HideInInspector]
        private int settingsVersion;

        [SerializeField]
        private Camera cameraOverride;

        [SerializeField]
        private int renderingLayer;

        [SerializeField]
        private bool drawInstanceGizmos = true;

        [SerializeField, Min(1)]
        private int gizmoDrawLimit = 2000;

        [SerializeField, Min(0.01f)]
        private float gizmoSize = 0.12f;

        [SerializeField]
        private Color gizmoColor = new Color(0.25f, 1f, 0.45f, 0.85f);

        private readonly Dictionary<Vector2Int, RuntimeChunk> chunks = new Dictionary<Vector2Int, RuntimeChunk>();
        private readonly Dictionary<int, RuntimeBatch> batches = new Dictionary<int, RuntimeBatch>();
        private Camera[] fallbackRenderCameras = new Camera[4];
        private Plane[] frustumPlanes;
        private bool chunksDirty = true;
        private string lastRenderCameraName = "未渲染";
        private int lastRenderFrame = -1;
        private int lastVisibleChunkCount;
        private int lastEvaluatedInstanceCount;
        private int lastQueuedInstanceCount;
        private int lastRenderableLodCount;
        private int lastSkippedInvalidPrototypeCount;
        private int lastSkippedMissingLodCount;
        private int lastSkippedDistanceCount;
        private int lastFallbackMeshCount;

        public List<AnimeGrassPrototype> Prototypes => prototypes;
        public IReadOnlyList<AnimeGrassInstance> Instances => instances;
        public int InstanceCount => instances.Count;
        public string LastRenderCameraName => lastRenderCameraName;
        public int LastRenderFrame => lastRenderFrame;
        public int LastVisibleChunkCount => lastVisibleChunkCount;
        public int LastEvaluatedInstanceCount => lastEvaluatedInstanceCount;
        public int LastQueuedInstanceCount => lastQueuedInstanceCount;
        public int LastRenderableLodCount => lastRenderableLodCount;
        public int LastSkippedInvalidPrototypeCount => lastSkippedInvalidPrototypeCount;
        public int LastSkippedMissingLodCount => lastSkippedMissingLodCount;
        public int LastSkippedDistanceCount => lastSkippedDistanceCount;
        public int LastFallbackMeshCount => lastFallbackMeshCount;
        public int RenderingLayer => renderingLayer;
        internal static IReadOnlyList<AnimeGrassField> ActiveFields => ActiveFieldList;
        internal static bool HasActiveFields => ActiveFieldList.Count > 0;

        public bool DrawInEditMode
        {
            get => drawInEditMode;
            set
            {
                drawInEditMode = value;
#if UNITY_EDITOR
                editorPreviewDirty = true;
#endif
            }
        }

        public void AddPrototype(AnimeGrassPrototype prototype)
        {
            if (prototype == null || prototypes.Contains(prototype))
            {
                return;
            }

            prototypes.Add(prototype);
            MarkDirty();
        }

        public void AddInstance(AnimeGrassInstance instance)
        {
            instances.Add(instance);
            MarkDirty();
        }

        public void AddInstances(List<AnimeGrassInstance> newInstances)
        {
            if (newInstances == null || newInstances.Count == 0)
            {
                return;
            }

            instances.AddRange(newInstances);
            MarkDirty();
        }

        public bool IsValidInstanceIndex(int index)
        {
            return instances != null && index >= 0 && index < instances.Count;
        }

        public AnimeGrassInstance GetInstance(int index)
        {
            return instances[index];
        }

        public void SetInstance(int index, AnimeGrassInstance instance)
        {
            if (!IsValidInstanceIndex(index))
            {
                return;
            }

            instances[index] = instance;
            MarkDirty();
        }

        public int FindClosestInstanceToRay(Ray ray, float maxDistanceFromRay, int prototypeFilter = -1)
        {
            if (instances == null || instances.Count == 0)
            {
                return -1;
            }

            float maxDistanceSqr = maxDistanceFromRay * maxDistanceFromRay;
            float bestDistanceSqr = maxDistanceSqr;
            float bestRayDistance = float.PositiveInfinity;
            int bestIndex = -1;

            Vector3 rayDirection = ray.direction.normalized;
            for (int i = 0; i < instances.Count; i++)
            {
                AnimeGrassInstance instance = instances[i];
                if (prototypeFilter >= 0 && instance.prototypeIndex != prototypeFilter)
                {
                    continue;
                }

                Vector3 toInstance = instance.position - ray.origin;
                float rayDistance = Vector3.Dot(toInstance, rayDirection);
                if (rayDistance < 0f)
                {
                    continue;
                }

                Vector3 closestPoint = ray.origin + rayDirection * rayDistance;
                float distanceSqr = (instance.position - closestPoint).sqrMagnitude;
                if (distanceSqr > bestDistanceSqr)
                {
                    continue;
                }

                if (distanceSqr < bestDistanceSqr || rayDistance < bestRayDistance)
                {
                    bestDistanceSqr = distanceSqr;
                    bestRayDistance = rayDistance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public int RemoveInstancesInSphere(Vector3 center, float radius, int prototypeFilter = -1)
        {
            float radiusSqr = radius * radius;
            int removed = 0;

            for (int i = instances.Count - 1; i >= 0; i--)
            {
                AnimeGrassInstance instance = instances[i];
                if (prototypeFilter >= 0 && instance.prototypeIndex != prototypeFilter)
                {
                    continue;
                }

                if ((instance.position - center).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                instances.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
            {
                MarkDirty();
            }

            return removed;
        }

        public void ClearInstances()
        {
            if (instances.Count == 0)
            {
                return;
            }

            instances.Clear();
            MarkDirty();
        }

        public void MarkDirty()
        {
            chunksDirty = true;
            if (TryGetComponent(out AnimeGrassFarField farField))
            {
                farField.MarkDirty();
            }
#if UNITY_EDITOR
            editorPreviewDirty = true;
#endif
        }

        public void RebuildChunks()
        {
            chunks.Clear();

            if (chunkSize <= 0.01f)
            {
                chunkSize = 1f;
            }

            for (int i = 0; i < instances.Count; i++)
            {
                AnimeGrassInstance instance = instances[i];
                Vector2Int key = GetChunkKey(instance.position);
                if (!chunks.TryGetValue(key, out RuntimeChunk chunk))
                {
                    chunk = new RuntimeChunk(key);
                    chunks.Add(key, chunk);
                }

                chunk.Add(i, instance.position);
            }

            foreach (RuntimeChunk chunk in chunks.Values)
            {
                chunk.FinishBounds(chunkBoundsPadding);
            }

            chunksDirty = false;
        }

        public bool TryGetInstancesBounds(out Bounds bounds)
        {
            bounds = default;
            if (instances == null || instances.Count == 0)
            {
                return false;
            }

            bounds = new Bounds(instances[0].position, Vector3.one);
            for (int i = 1; i < instances.Count; i++)
            {
                bounds.Encapsulate(instances[i].position);
            }

            bounds.Expand(Mathf.Max(1f, chunkBoundsPadding) * 2f);
            return true;
        }

        private void OnEnable()
        {
            if (!ActiveFieldList.Contains(this))
            {
                ActiveFieldList.Add(this);
            }

            chunksDirty = true;
#if UNITY_EDITOR
            editorPreviewDirty = true;
            EditorApplication.update -= RefreshEditorPreview;
            EditorApplication.update += RefreshEditorPreview;
            EditorApplication.projectChanged -= MarkEditorPreviewDirty;
            EditorApplication.projectChanged += MarkEditorPreviewDirty;
#endif
        }

        private void OnDisable()
        {
            ActiveFieldList.Remove(this);
            batches.Clear();
#if UNITY_EDITOR
            EditorApplication.update -= RefreshEditorPreview;
            EditorApplication.projectChanged -= MarkEditorPreviewDirty;
            ClearEditorPreview();
#endif
        }

        private void OnValidate()
        {
            if (settingsVersion < CurrentSettingsVersion)
            {
                ignoreLodDistanceInEditMode = false;
                settingsVersion = CurrentSettingsVersion;
            }

            chunkSize = Mathf.Max(1f, chunkSize);
            chunkBoundsPadding = Mathf.Max(0f, chunkBoundsPadding);
            gizmoDrawLimit = Mathf.Max(1, gizmoDrawLimit);
            gizmoSize = Mathf.Max(0.01f, gizmoSize);
            chunksDirty = true;
            batches.Clear();
            if (TryGetComponent(out AnimeGrassFarField farField))
            {
                farField.MarkDirty();
            }
#if UNITY_EDITOR
            editorPreviewDirty = true;
#endif
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            int requiredCameraCount = Camera.allCamerasCount;
            if (fallbackRenderCameras.Length < requiredCameraCount)
            {
                Array.Resize(ref fallbackRenderCameras, Mathf.NextPowerOfTwo(requiredCameraCount));
            }

            int cameraCount = Camera.GetAllCameras(fallbackRenderCameras);
            for (int i = 0; i < cameraCount; i++)
            {
                RenderFallbackCamera(fallbackRenderCameras[i]);
            }

#if UNITY_EDITOR
            Camera sceneCamera = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.camera
                : null;
            bool sceneCameraAlreadyIncluded = false;
            for (int i = 0; i < cameraCount; i++)
            {
                if (fallbackRenderCameras[i] == sceneCamera)
                {
                    sceneCameraAlreadyIncluded = true;
                    break;
                }
            }

            if (!sceneCameraAlreadyIncluded)
            {
                RenderFallbackCamera(sceneCamera);
            }
#endif
        }

#if UNITY_EDITOR
        private void MarkEditorPreviewDirty()
        {
            editorPreviewDirty = true;
        }

        private void RefreshEditorPreview()
        {
            if (Application.isPlaying)
            {
                if (editorPreviewRoot != null)
                {
                    ClearEditorPreview();
                }

                return;
            }

            if (!drawInEditMode || !isActiveAndEnabled)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < nextEditorPreviewTime)
            {
                return;
            }

            nextEditorPreviewTime = now + EditorPreviewInterval;
            if (editorPreviewDirty)
            {
                RebuildEditorPreview();
            }

            UpdateEditorPreview(SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.camera
                : null);
            SceneView.RepaintAll();
        }

        private void RebuildEditorPreview()
        {
            ClearEditorPreview();
            editorPreviewDirty = false;

            if (!drawInEditMode || instances == null || prototypes == null || instances.Count == 0)
            {
                return;
            }

            editorPreviewRoot = new GameObject(name + " [Grass Preview]")
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                layer = renderingLayer
            };

            for (int i = 0; i < instances.Count; i++)
            {
                AnimeGrassInstance instance = instances[i];
                if (instance.prototypeIndex < 0 || instance.prototypeIndex >= prototypes.Count)
                {
                    continue;
                }

                AnimeGrassPrototype prototype = prototypes[instance.prototypeIndex];
                if (prototype == null || !TryGetEditorPreviewLod(prototype, out AnimeGrassLod lod, out Mesh mesh))
                {
                    continue;
                }

                GameObject previewObject = new GameObject("Grass " + i)
                {
                    hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                    layer = renderingLayer
                };
                previewObject.transform.SetParent(editorPreviewRoot.transform, false);
                Vector3 correctedPosition = instance.position
                    + instance.rotation * Vector3.Scale(instance.scale, prototype.ModelPositionOffset);
                Quaternion correctedRotation = instance.rotation * prototype.ModelRotationOffset;
                previewObject.transform.SetPositionAndRotation(correctedPosition, correctedRotation);
                previewObject.transform.localScale = Vector3.Scale(instance.scale, prototype.ModelScale);

                MeshFilter meshFilter = previewObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;

                MeshRenderer meshRenderer = previewObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = GetEditorPreviewMaterial(lod.material);
                meshRenderer.shadowCastingMode = lod.shadowCasting;
                meshRenderer.receiveShadows = lod.receiveShadows;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;

                EditorPreviewInstance previewInstance = new EditorPreviewInstance(
                    instance,
                    prototype,
                    meshFilter,
                    meshRenderer);
                editorPreviewInstances.Add(previewInstance);
                ApplyEditorPreview(previewInstance, lod, mesh, 1f);
            }
        }

        private void UpdateEditorPreview(Camera sceneCamera)
        {
            SyncEditorPreviewMaterials();

            for (int i = 0; i < editorPreviewInstances.Count; i++)
            {
                EditorPreviewInstance preview = editorPreviewInstances[i];
                float distance = sceneCamera != null
                    ? Vector3.Distance(sceneCamera.transform.position, preview.instance.position)
                    : 0f;
                bool ignoreDistance = ignoreLodDistanceInEditMode || sceneCamera == null;
                if (!TryGetEditorPreviewLod(
                    preview.prototype,
                    distance,
                    ignoreDistance,
                    out AnimeGrassLod lod,
                    out Mesh mesh,
                    out float fade))
                {
                    preview.meshRenderer.enabled = false;
                    continue;
                }

                ApplyEditorPreview(preview, lod, mesh, fade);
            }
        }

        private void ApplyEditorPreview(
            EditorPreviewInstance preview,
            AnimeGrassLod lod,
            Mesh mesh,
            float fade)
        {
            preview.meshRenderer.enabled = fade > 0.001f;
            if (!preview.meshRenderer.enabled)
            {
                return;
            }

            if (preview.lod != lod || preview.meshFilter.sharedMesh != mesh)
            {
                preview.lod = lod;
                preview.meshFilter.sharedMesh = mesh;
                preview.meshRenderer.sharedMaterial = GetEditorPreviewMaterial(lod.material);
                preview.meshRenderer.shadowCastingMode = lod.shadowCasting;
                preview.meshRenderer.receiveShadows = lod.receiveShadows;
            }

            Color instanceColor = preview.instance.color.a > 0.0001f
                ? preview.instance.color
                : preview.prototype.DefaultInstanceColor;
            preview.properties.Clear();
            preview.properties.SetColor(InstanceColorId, instanceColor);
            preview.properties.SetVector(
                InstanceNormalId,
                new Vector4(
                    preview.instance.normal.x,
                    preview.instance.normal.y,
                    preview.instance.normal.z,
                    0f));
            preview.properties.SetFloat(
                InstanceWindWeightId,
                Mathf.Max(0f, preview.instance.windWeight * preview.prototype.WindWeight));
            preview.properties.SetFloat(InstanceFadeId, Mathf.Clamp01(fade));
            preview.properties.SetFloat(BatchReceiveShadowsId, lod.receiveShadows ? 1f : 0f);
            preview.meshRenderer.SetPropertyBlock(preview.properties);
        }

        private Material GetEditorPreviewMaterial(Material source)
        {
            if (editorPreviewMaterialCache.TryGetValue(source, out Material previewMaterial))
            {
                return previewMaterial;
            }

            previewMaterial = new Material(source)
            {
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = true
            };
            editorPreviewMaterialCache.Add(source, previewMaterial);
            editorPreviewMaterials.Add(previewMaterial);
            return previewMaterial;
        }

        private void SyncEditorPreviewMaterials()
        {
            foreach (KeyValuePair<Material, Material> pair in editorPreviewMaterialCache)
            {
                Material source = pair.Key;
                Material preview = pair.Value;
                if (source == null || preview == null || source.shader == null)
                {
                    continue;
                }

                if (preview.shader != source.shader)
                {
                    preview.shader = source.shader;
                }

                preview.CopyPropertiesFromMaterial(source);
                preview.renderQueue = source.renderQueue;
                preview.doubleSidedGI = source.doubleSidedGI;
                preview.enableInstancing = true;
            }
        }

        private static bool TryGetEditorPreviewLod(
            AnimeGrassPrototype prototype,
            out AnimeGrassLod previewLod,
            out Mesh previewMesh)
        {
            return TryGetEditorPreviewLod(
                prototype,
                0f,
                true,
                out previewLod,
                out previewMesh,
                out _);
        }

        private static bool TryGetEditorPreviewLod(
            AnimeGrassPrototype prototype,
            float distance,
            bool ignoreDistance,
            out AnimeGrassLod previewLod,
            out Mesh previewMesh,
            out float previewFade)
        {
            previewLod = null;
            previewMesh = null;
            previewFade = 0f;
            AnimeGrassLod[] lods = prototype.Lods;
            if (lods == null)
            {
                return false;
            }

            for (int i = 0; i < lods.Length; i++)
            {
                AnimeGrassLod lod = lods[i];
                if (lod == null || !IsMaterialUsable(lod.material))
                {
                    continue;
                }

                Mesh mesh = IsMeshUsable(lod.mesh) ? lod.mesh : GetFallbackBladeMesh();
                if (!IsMeshUsable(mesh))
                {
                    continue;
                }

                float fade = ignoreDistance ? 1f : lod.EvaluateFade(distance);
                if (fade <= previewFade)
                {
                    continue;
                }

                previewLod = lod;
                previewMesh = mesh;
                previewFade = fade;
                if (ignoreDistance)
                {
                    return true;
                }
            }

            return previewLod != null;
        }

        private void ClearEditorPreview()
        {
            if (editorPreviewRoot != null)
            {
                DestroyImmediate(editorPreviewRoot);
                editorPreviewRoot = null;
            }

            for (int i = 0; i < editorPreviewMaterials.Count; i++)
            {
                if (editorPreviewMaterials[i] != null)
                {
                    DestroyImmediate(editorPreviewMaterials[i]);
                }
            }

            editorPreviewMaterials.Clear();
            editorPreviewMaterialCache.Clear();
            editorPreviewInstances.Clear();
        }

        private sealed class EditorPreviewInstance
        {
            public EditorPreviewInstance(
                AnimeGrassInstance instance,
                AnimeGrassPrototype prototype,
                MeshFilter meshFilter,
                MeshRenderer meshRenderer)
            {
                this.instance = instance;
                this.prototype = prototype;
                this.meshFilter = meshFilter;
                this.meshRenderer = meshRenderer;
                properties = new MaterialPropertyBlock();
            }

            public readonly AnimeGrassInstance instance;
            public readonly AnimeGrassPrototype prototype;
            public readonly MeshFilter meshFilter;
            public readonly MeshRenderer meshRenderer;
            public readonly MaterialPropertyBlock properties;
            public AnimeGrassLod lod;
        }
#endif

        internal static bool ShouldRenderCamera(Camera renderCamera)
        {
            return renderCamera != null
                && (renderCamera.cameraType == CameraType.Game
                || renderCamera.cameraType == CameraType.SceneView);
        }

        internal static void MarkRenderedByRendererFeature(Camera renderCamera)
        {
            if (renderCamera == null)
            {
                return;
            }

            RendererFeatureRenderedFrames[renderCamera.GetInstanceID()] = Time.frameCount;
        }

        private static bool WasRenderedByRendererFeature(Camera renderCamera)
        {
            if (renderCamera == null
                || !RendererFeatureRenderedFrames.TryGetValue(renderCamera.GetInstanceID(), out int renderedFrame))
            {
                return false;
            }

            return renderedFrame >= Time.frameCount - 1;
        }

        internal void RenderForCamera(Camera renderCamera, CommandBuffer commandBuffer)
        {
            if (renderCamera == null)
            {
                return;
            }

            if (!Application.isPlaying && !drawInEditMode)
            {
                return;
            }

            if ((renderCamera.cullingMask & (1 << renderingLayer)) == 0)
            {
                return;
            }

            AnimeSurfaceCache.BindForCamera(renderCamera, commandBuffer);
            AnimeSurfaceCacheStamp.ApplyGrassInteractionGlobals(commandBuffer);
            RenderGrass(renderCamera, commandBuffer);
        }

        private void RenderGrass(Camera renderCamera, CommandBuffer commandBuffer)
        {
            ResetRenderStats(renderCamera);

            if (renderCamera == null || prototypes == null || instances == null || instances.Count == 0)
            {
                return;
            }

            if (chunksDirty)
            {
                RebuildChunks();
            }

            ResetBatches();

            Camera lodCamera = ResolveLodReferenceCamera(renderCamera);
            Vector3 cameraPosition = lodCamera.transform.position;
            bool useFrustumCulling = frustumCulling && Application.isPlaying;
            if (useFrustumCulling)
            {
                frustumPlanes = GeometryUtility.CalculateFrustumPlanes(renderCamera);
            }

            foreach (RuntimeChunk chunk in chunks.Values)
            {
                if (useFrustumCulling && !GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.Bounds))
                {
                    continue;
                }

                lastVisibleChunkCount++;
                RenderChunk(chunk, cameraPosition, renderCamera, commandBuffer);
            }

            FlushBatches(renderCamera, commandBuffer);
        }

        private void RenderChunk(RuntimeChunk chunk, Vector3 cameraPosition, Camera renderCamera, CommandBuffer commandBuffer)
        {
            List<int> indices = chunk.InstanceIndices;
            for (int i = 0; i < indices.Count; i++)
            {
                lastEvaluatedInstanceCount++;
                AnimeGrassInstance instance = instances[indices[i]];
                if (instance.prototypeIndex < 0 || instance.prototypeIndex >= prototypes.Count)
                {
                    lastSkippedInvalidPrototypeCount++;
                    continue;
                }

                AnimeGrassPrototype prototype = prototypes[instance.prototypeIndex];
                if (prototype == null)
                {
                    lastSkippedInvalidPrototypeCount++;
                    continue;
                }

                float distance = Vector3.Distance(cameraPosition, instance.position);
                AnimeGrassLod[] lods = prototype.Lods;
                if (lods == null)
                {
                    lastSkippedMissingLodCount++;
                    continue;
                }

                bool ignoreDistance = !Application.isPlaying && ignoreLodDistanceInEditMode;
                bool drewEditPreviewLod = false;
                for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                {
                    AnimeGrassLod lod = lods[lodIndex];
                    if (lod == null || !IsMaterialUsable(lod.material))
                    {
                        lastSkippedMissingLodCount++;
                        continue;
                    }

                    Mesh renderMesh = IsMeshUsable(lod.mesh) ? lod.mesh : GetFallbackBladeMesh();
                    if (!IsMeshUsable(renderMesh))
                    {
                        lastSkippedMissingLodCount++;
                        continue;
                    }

                    if (!IsMeshUsable(lod.mesh))
                    {
                        lastFallbackMeshCount++;
                    }

                    lastRenderableLodCount++;
                    if (ignoreDistance && drewEditPreviewLod)
                    {
                        continue;
                    }

                    float fade = ignoreDistance ? 1f : lod.EvaluateFade(distance);
                    if (fade <= 0.001f)
                    {
                        lastSkippedDistanceCount++;
                        continue;
                    }

                    RuntimeBatch batch = GetBatch(instance.prototypeIndex, lodIndex, prototype, lod, renderMesh);
                    batch.Add(instance, fade);
                    lastQueuedInstanceCount++;
                    drewEditPreviewLod = true;
                    if (batch.Count == MaxBatchSize)
                    {
                        batch.Flush(renderingLayer, renderCamera, commandBuffer);
                    }
                }
            }
        }

        private RuntimeBatch GetBatch(int prototypeIndex, int lodIndex, AnimeGrassPrototype prototype, AnimeGrassLod lod, Mesh renderMesh)
        {
            int key = (prototypeIndex << 8) | lodIndex;
            if (!batches.TryGetValue(key, out RuntimeBatch batch) || !batch.Matches(lod, renderMesh))
            {
                batch = new RuntimeBatch(prototype, lod, renderMesh);
                batches[key] = batch;
            }

            return batch;
        }

        private void ResetBatches()
        {
            foreach (RuntimeBatch batch in batches.Values)
            {
                batch.Reset();
            }
        }

        private void ResetRenderStats(Camera renderCamera)
        {
            lastRenderCameraName = renderCamera != null ? renderCamera.name + " (" + renderCamera.cameraType + ")" : "无相机";
            lastRenderFrame = Time.frameCount;
            lastVisibleChunkCount = 0;
            lastEvaluatedInstanceCount = 0;
            lastQueuedInstanceCount = 0;
            lastRenderableLodCount = 0;
            lastSkippedInvalidPrototypeCount = 0;
            lastSkippedMissingLodCount = 0;
            lastSkippedDistanceCount = 0;
            lastFallbackMeshCount = 0;
        }

        private void FlushBatches(Camera renderCamera, CommandBuffer commandBuffer)
        {
            foreach (RuntimeBatch batch in batches.Values)
            {
                batch.Flush(renderingLayer, renderCamera, commandBuffer);
            }
        }

        private Vector2Int GetChunkKey(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / chunkSize),
                Mathf.FloorToInt(position.z / chunkSize));
        }

        private void RenderFallbackCamera(Camera renderCamera)
        {
            if (!ShouldRenderCamera(renderCamera)
                || !renderCamera.isActiveAndEnabled
                || WasRenderedByRendererFeature(renderCamera))
            {
                return;
            }

            RenderForCamera(renderCamera, null);
        }

        private Camera ResolveLodReferenceCamera(Camera renderCamera)
        {
#if UNITY_EDITOR
            if (renderCamera.cameraType == CameraType.SceneView)
            {
                return renderCamera;
            }
#endif
            return cameraOverride != null ? cameraOverride : renderCamera;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawInstanceGizmos || instances == null || instances.Count == 0)
            {
                return;
            }

            Gizmos.color = gizmoColor;
            int step = Mathf.Max(1, Mathf.CeilToInt(instances.Count / (float)Mathf.Max(1, gizmoDrawLimit)));
            for (int i = 0; i < instances.Count; i += step)
            {
                Gizmos.DrawWireSphere(instances[i].position, gizmoSize);
            }

            if (TryGetInstancesBounds(out Bounds bounds))
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.35f);
                Gizmos.DrawCube(bounds.center, bounds.size);
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }

        private static Mesh GetFallbackBladeMesh()
        {
            if (IsMeshUsable(fallbackBladeMesh))
            {
                return fallbackBladeMesh;
            }

            fallbackBladeMesh = new Mesh
            {
                name = "Enlyn Grass Missing Mesh Fallback",
                hideFlags = HideFlags.HideAndDontSave
            };

            Vector3[] vertices =
            {
                new Vector3(-0.18f, 0f, 0f),
                new Vector3(0.18f, 0f, 0f),
                new Vector3(-0.06f, 0.85f, 0f),
                new Vector3(0.06f, 0.85f, 0f),
                new Vector3(0f, 0f, -0.18f),
                new Vector3(0f, 0f, 0.18f),
                new Vector3(0f, 0.85f, -0.06f),
                new Vector3(0f, 0.85f, 0.06f)
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            Color[] colors =
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white,
                Color.white,
                Color.white,
                Color.white,
                Color.white
            };

            fallbackBladeMesh.SetVertices(vertices);
            fallbackBladeMesh.SetUVs(0, uvs);
            fallbackBladeMesh.SetColors(colors);
            fallbackBladeMesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 }, 0);
            fallbackBladeMesh.RecalculateNormals();
            fallbackBladeMesh.RecalculateBounds();
            return fallbackBladeMesh;
        }

        private static bool IsMeshUsable(Mesh mesh)
        {
            if (mesh == null)
            {
                return false;
            }

            try
            {
                return mesh.vertexCount > 0 && mesh.subMeshCount > 0;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool IsMaterialUsable(Material material)
        {
            if (material == null)
            {
                return false;
            }

            try
            {
                return material.shader != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private sealed class RuntimeChunk
        {
            private bool hasBounds;
            private Bounds bounds;

            public RuntimeChunk(Vector2Int key)
            {
                Key = key;
            }

            public Vector2Int Key { get; }
            public Bounds Bounds => bounds;
            public List<int> InstanceIndices { get; } = new List<int>();

            public void Add(int instanceIndex, Vector3 position)
            {
                InstanceIndices.Add(instanceIndex);

                if (!hasBounds)
                {
                    bounds = new Bounds(position, Vector3.one * 0.25f);
                    hasBounds = true;
                    return;
                }

                bounds.Encapsulate(position);
            }

            public void FinishBounds(float padding)
            {
                if (!hasBounds)
                {
                    bounds = new Bounds(Vector3.zero, Vector3.one);
                    return;
                }

                bounds.Expand(Mathf.Max(0.1f, padding) * 2f);
            }
        }

        private sealed class RuntimeBatch
        {
            private readonly AnimeGrassPrototype prototype;
            private readonly AnimeGrassLod lod;
            private readonly Mesh mesh;
            private readonly Matrix4x4[] matrices = new Matrix4x4[MaxBatchSize];
            private readonly Vector4[] colors = new Vector4[MaxBatchSize];
            private readonly Vector4[] normals = new Vector4[MaxBatchSize];
            private readonly float[] windWeights = new float[MaxBatchSize];
            private readonly float[] fades = new float[MaxBatchSize];
            private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

            public RuntimeBatch(AnimeGrassPrototype prototype, AnimeGrassLod lod, Mesh mesh)
            {
                this.prototype = prototype;
                this.lod = lod;
                this.mesh = mesh;
            }

            public int Count { get; private set; }

            public bool Matches(AnimeGrassLod otherLod, Mesh otherMesh)
            {
                return ReferenceEquals(lod, otherLod) && ReferenceEquals(mesh, otherMesh);
            }

            public void Add(AnimeGrassInstance instance, float fade)
            {
                matrices[Count] = instance.ToMatrix() * prototype.ModelCorrectionMatrix;
                colors[Count] = new Vector4(instance.color.r, instance.color.g, instance.color.b, instance.color.a);
                normals[Count] = new Vector4(instance.normal.x, instance.normal.y, instance.normal.z, 0f);
                windWeights[Count] = Mathf.Max(0f, instance.windWeight * prototype.WindWeight);
                fades[Count] = Mathf.Clamp01(fade);
                Count++;
            }

            public void Reset()
            {
                Count = 0;
            }

            public void Flush(int layer, Camera camera, CommandBuffer commandBuffer)
            {
                if (Count == 0 || lod == null || !IsMeshUsable(mesh))
                {
                    Count = 0;
                    return;
                }

                Material material = lod.material;
                if (!IsMaterialUsable(material))
                {
                    Count = 0;
                    return;
                }

                try
                {
                    if (!material.enableInstancing)
                    {
                        material.enableInstancing = true;
                    }
                }
                catch (MissingReferenceException)
                {
                    Count = 0;
                    return;
                }
                catch (NullReferenceException)
                {
                    Count = 0;
                    return;
                }
                catch (ArgumentException)
                {
                    Count = 0;
                    return;
                }

                propertyBlock.Clear();
                propertyBlock.SetVectorArray(InstanceColorId, colors);
                propertyBlock.SetVectorArray(InstanceNormalId, normals);
                propertyBlock.SetFloatArray(InstanceWindWeightId, windWeights);
                propertyBlock.SetFloatArray(InstanceFadeId, fades);
                propertyBlock.SetFloat(BatchReceiveShadowsId, lod.receiveShadows ? 1f : 0f);

                int subMeshIndex;
                try
                {
                    subMeshIndex = Mathf.Clamp(lod.subMeshIndex, 0, Mathf.Max(0, mesh.subMeshCount - 1));
                }
                catch (MissingReferenceException)
                {
                    Count = 0;
                    return;
                }

                try
                {
                    if (commandBuffer != null)
                    {
                        commandBuffer.DrawMeshInstanced(
                            mesh,
                            subMeshIndex,
                            material,
                            0,
                            matrices,
                            Count,
                            propertyBlock);
                    }
                    else
                    {
                        Graphics.DrawMeshInstanced(
                            mesh,
                            subMeshIndex,
                            material,
                            matrices,
                            Count,
                            propertyBlock,
                            lod.shadowCasting,
                            lod.receiveShadows,
                            layer,
                            camera,
                            LightProbeUsage.Off,
                            null);
                    }
                }
                catch (MissingReferenceException)
                {
                    Count = 0;
                    return;
                }
                catch (ArgumentException)
                {
                    Count = 0;
                    return;
                }

                Count = 0;
            }
        }
    }
}
