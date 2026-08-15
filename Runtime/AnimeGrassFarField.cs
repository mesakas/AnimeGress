using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Enlyn.Grass
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AnimeGrassField))]
    [AddComponentMenu("AnimeGress/远景草覆盖")]
    public sealed class AnimeGrassFarField : MonoBehaviour
    {
        private const string OverlayShaderResource = "AnimeGress/AnimeGrassFarFieldOverlay";
        private const string OverlayShaderName = "Hidden/AnimeGress/Far Field Overlay";
        private static readonly List<AnimeGrassFarField> ActiveFarFields = new List<AnimeGrassFarField>();
        private static readonly int CoverageTextureId = Shader.PropertyToID("_AnimeGrassFarCoverageTexture");
        private static readonly int HeightTextureId = Shader.PropertyToID("_AnimeGrassFarHeightTexture");
        private static readonly int WorldToUvId = Shader.PropertyToID("_AnimeGrassFarWorldToUV");
        private static readonly int HeightParamsId = Shader.PropertyToID("_AnimeGrassFarHeightParams");
        private static readonly int DistanceParamsId = Shader.PropertyToID("_AnimeGrassFarDistanceParams");
        private static readonly int AppearanceParamsId = Shader.PropertyToID("_AnimeGrassFarAppearanceParams");
        private static readonly int DisturbanceParamsId = Shader.PropertyToID("_AnimeGrassFarDisturbanceParams");

        [SerializeField]
        private bool farFieldEnabled = true;

        [SerializeField, Min(0f)]
        private float transitionStartDistance = 57f;

        [SerializeField, Min(0.01f)]
        private float transitionEndDistance = 60f;

        [SerializeField, Range(64, 1024)]
        private int cacheResolution = 256;

        [SerializeField, Min(0.05f)]
        private float coverageRadius = 0.6f;

        [SerializeField, Range(0f, 0.95f)]
        private float coverageHardness = 0.55f;

        [SerializeField]
        private Color colorMultiplier = Color.white;

        [SerializeField, Range(0f, 1f)]
        private float colorInfluence = 0.7f;

        [SerializeField, Range(0f, 1f)]
        private float pseudoShadowStrength = 0.18f;

        [SerializeField, Range(0f, 1f)]
        private float pseudoShadowDisturbance = 0.55f;

        [SerializeField, Min(0.25f)]
        private float pseudoShadowPatchSize = 6f;

        [SerializeField, Min(0f)]
        private float pseudoShadowDriftSpeed = 0.6f;

        [SerializeField, Range(0f, 2f)]
        private float windTintResponse = 1f;

        [SerializeField, Min(0.05f)]
        private float surfaceHeightTolerance = 0.8f;

        [SerializeField, Range(0f, 1f)]
        private float minimumUpwardNormal = 0.35f;

        [SerializeField]
        private bool previewInEditMode = true;

        private AnimeGrassField field;
        private Texture2D coverageTexture;
        private Texture2D heightTexture;
        private Material overlayMaterial;
        private MaterialPropertyBlock properties;
        private Vector4 worldToUv;
        private Vector4 heightParams;
        private bool dirty = true;
        private int cachedInstanceCount;
        private int cachedColorSignature;

        private struct FarInstanceSample
        {
            public Vector3 position;
            public Color color;
        }

        public bool FarFieldEnabled => farFieldEnabled;
        public bool PreviewInEditMode => previewInEditMode;
        public bool IsDirty => dirty;
        public int CachedInstanceCount => cachedInstanceCount;
        public Texture2D CoverageTexture => coverageTexture;
        public Texture2D HeightTexture => heightTexture;
        internal static bool HasActiveFarFields => ActiveFarFields.Count > 0;

        private void OnEnable()
        {
            field = GetComponent<AnimeGrassField>();
            if (!ActiveFarFields.Contains(this))
            {
                ActiveFarFields.Add(this);
            }

            dirty = true;
        }

        private void OnDisable()
        {
            ActiveFarFields.Remove(this);
            ReleaseResources();
        }

        private void OnValidate()
        {
            transitionStartDistance = Mathf.Max(0f, transitionStartDistance);
            transitionEndDistance = Mathf.Max(transitionStartDistance + 0.01f, transitionEndDistance);
            cacheResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(cacheResolution), 64, 1024);
            coverageRadius = Mathf.Max(0.05f, coverageRadius);
            coverageHardness = Mathf.Clamp(coverageHardness, 0f, 0.95f);
            colorInfluence = Mathf.Clamp01(colorInfluence);
            pseudoShadowStrength = Mathf.Clamp01(pseudoShadowStrength);
            pseudoShadowDisturbance = Mathf.Clamp01(pseudoShadowDisturbance);
            pseudoShadowPatchSize = Mathf.Max(0.25f, pseudoShadowPatchSize);
            pseudoShadowDriftSpeed = Mathf.Max(0f, pseudoShadowDriftSpeed);
            windTintResponse = Mathf.Max(0f, windTintResponse);
            surfaceHeightTolerance = Mathf.Max(0.05f, surfaceHeightTolerance);
            minimumUpwardNormal = Mathf.Clamp01(minimumUpwardNormal);
            dirty = true;
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        public void RebuildNow()
        {
            dirty = true;
            EnsureCoverageCache();
        }

        public bool MatchTransitionToLastLod()
        {
            AnimeGrassField targetField = GetField();
            if (targetField == null)
            {
                return false;
            }

            float farthestEndDistance = 0f;
            float matchingFadeDistance = 0f;
            IReadOnlyList<AnimeGrassPrototype> prototypes = targetField.Prototypes;
            for (int prototypeIndex = 0; prototypeIndex < prototypes.Count; prototypeIndex++)
            {
                AnimeGrassPrototype prototype = prototypes[prototypeIndex];
                AnimeGrassLod[] lods = prototype != null ? prototype.Lods : null;
                if (lods == null)
                {
                    continue;
                }

                for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                {
                    AnimeGrassLod lod = lods[lodIndex];
                    if (lod != null && lod.material != null && lod.endDistance > farthestEndDistance)
                    {
                        farthestEndDistance = lod.endDistance;
                        matchingFadeDistance = lod.fadeDistance;
                    }
                }
            }

            if (farthestEndDistance <= 0f)
            {
                return false;
            }

            transitionEndDistance = farthestEndDistance;
            transitionStartDistance = Mathf.Max(
                0f,
                farthestEndDistance - Mathf.Max(0.5f, matchingFadeDistance));
            return true;
        }

        internal static void RenderAll(Camera camera, CommandBuffer commandBuffer)
        {
            for (int i = 0; i < ActiveFarFields.Count; i++)
            {
                AnimeGrassFarField farField = ActiveFarFields[i];
                if (farField != null && farField.isActiveAndEnabled)
                {
                    farField.Render(camera, commandBuffer);
                }
            }
        }

        internal static bool ShouldRenderAny(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            for (int i = 0; i < ActiveFarFields.Count; i++)
            {
                AnimeGrassFarField farField = ActiveFarFields[i];
                if (farField == null || !farField.isActiveAndEnabled || !farField.farFieldEnabled)
                {
                    continue;
                }

                if (!Application.isPlaying && !farField.previewInEditMode)
                {
                    continue;
                }

                AnimeGrassField targetField = farField.GetField();
                if (targetField != null && (camera.cullingMask & (1 << targetField.RenderingLayer)) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void Render(Camera camera, CommandBuffer commandBuffer)
        {
            if (!farFieldEnabled
                || commandBuffer == null
                || camera == null
                || (!Application.isPlaying && !previewInEditMode))
            {
                return;
            }

            AnimeGrassField targetField = GetField();
            if (targetField == null || (camera.cullingMask & (1 << targetField.RenderingLayer)) == 0)
            {
                return;
            }

            if (!EnsureCoverageCache() || !EnsureMaterial())
            {
                return;
            }

            if (properties == null)
            {
                properties = new MaterialPropertyBlock();
            }

            properties.Clear();
            properties.SetTexture(CoverageTextureId, coverageTexture);
            properties.SetTexture(HeightTextureId, heightTexture);
            properties.SetVector(WorldToUvId, worldToUv);
            properties.SetVector(HeightParamsId, heightParams);
            properties.SetVector(
                DistanceParamsId,
                new Vector4(
                    transitionStartDistance,
                    transitionEndDistance,
                    1f / Mathf.Max(0.01f, transitionEndDistance - transitionStartDistance),
                    0f));
            properties.SetVector(
                AppearanceParamsId,
                new Vector4(colorInfluence, pseudoShadowStrength, windTintResponse, minimumUpwardNormal));
            properties.SetVector(
                DisturbanceParamsId,
                new Vector4(
                    pseudoShadowDisturbance,
                    1f / Mathf.Max(0.25f, pseudoShadowPatchSize),
                    pseudoShadowDriftSpeed,
                    0f));
            commandBuffer.DrawProcedural(
                Matrix4x4.identity,
                overlayMaterial,
                0,
                MeshTopology.Triangles,
                3,
                1,
                properties);
        }

        private AnimeGrassField GetField()
        {
            if (field == null)
            {
                field = GetComponent<AnimeGrassField>();
            }

            return field;
        }

        private bool EnsureCoverageCache()
        {
            AnimeGrassField targetField = GetField();
            if (targetField == null)
            {
                return false;
            }

            int colorSignature = ComputeColorSignature(targetField);
            if (!dirty
                && coverageTexture != null
                && heightTexture != null
                && colorSignature == cachedColorSignature)
            {
                return cachedInstanceCount > 0;
            }

            IReadOnlyList<AnimeGrassInstance> instances = targetField.Instances;
            if (instances == null || instances.Count == 0)
            {
                cachedInstanceCount = 0;
                cachedColorSignature = colorSignature;
                dirty = false;
                return false;
            }

            List<FarInstanceSample> samples = new List<FarInstanceSample>(instances.Count);
            for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
            {
                AnimeGrassInstance instance = instances[instanceIndex];
                if (TryResolveInstanceColor(targetField, instance, out Color instanceFarColor))
                {
                    samples.Add(new FarInstanceSample
                    {
                        position = instance.position,
                        color = instanceFarColor
                    });
                }
            }

            if (samples.Count == 0)
            {
                cachedInstanceCount = 0;
                cachedColorSignature = colorSignature;
                dirty = false;
                return false;
            }

            int resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(cacheResolution), 64, 1024);
            EnsureTextures(resolution);

            Vector3 minimum = samples[0].position;
            Vector3 maximum = samples[0].position;
            for (int i = 1; i < samples.Count; i++)
            {
                minimum = Vector3.Min(minimum, samples[i].position);
                maximum = Vector3.Max(maximum, samples[i].position);
            }

            float margin = coverageRadius + 0.5f;
            minimum -= new Vector3(margin, 1f, margin);
            maximum += new Vector3(margin, 1f, margin);
            float worldWidth = Mathf.Max(1f, maximum.x - minimum.x);
            float worldDepth = Mathf.Max(1f, maximum.z - minimum.z);
            float worldHeight = Mathf.Max(1f, maximum.y - minimum.y);
            worldToUv = new Vector4(
                1f / worldWidth,
                1f / worldDepth,
                -minimum.x / worldWidth,
                -minimum.z / worldDepth);
            heightParams = new Vector4(minimum.y, worldHeight, surfaceHeightTolerance, 0f);

            int pixelCount = resolution * resolution;
            float[] density = new float[pixelCount];
            float[] colorWeight = new float[pixelCount];
            float[] heightWeight = new float[pixelCount];
            Vector3[] colorSum = new Vector3[pixelCount];
            float[] rootHeight = new float[pixelCount];
            float pixelWorldWidth = worldWidth / resolution;
            float pixelWorldDepth = worldDepth / resolution;
            int radiusX = Mathf.Max(1, Mathf.CeilToInt(coverageRadius / pixelWorldWidth));
            int radiusY = Mathf.Max(1, Mathf.CeilToInt(coverageRadius / pixelWorldDepth));

            for (int instanceIndex = 0; instanceIndex < samples.Count; instanceIndex++)
            {
                FarInstanceSample sample = samples[instanceIndex];
                float centerX = (sample.position.x - minimum.x) / worldWidth * resolution - 0.5f;
                float centerY = (sample.position.z - minimum.z) / worldDepth * resolution - 0.5f;
                int minPixelX = Mathf.Max(0, Mathf.FloorToInt(centerX) - radiusX);
                int maxPixelX = Mathf.Min(resolution - 1, Mathf.CeilToInt(centerX) + radiusX);
                int minPixelY = Mathf.Max(0, Mathf.FloorToInt(centerY) - radiusY);
                int maxPixelY = Mathf.Min(resolution - 1, Mathf.CeilToInt(centerY) + radiusY);

                for (int pixelY = minPixelY; pixelY <= maxPixelY; pixelY++)
                {
                    float worldZ = minimum.z + (pixelY + 0.5f) * pixelWorldDepth;
                    for (int pixelX = minPixelX; pixelX <= maxPixelX; pixelX++)
                    {
                        float worldX = minimum.x + (pixelX + 0.5f) * pixelWorldWidth;
                        float normalizedDistance = Vector2.Distance(
                            new Vector2(worldX, worldZ),
                            new Vector2(sample.position.x, sample.position.z)) / coverageRadius;
                        if (normalizedDistance >= 1f)
                        {
                            continue;
                        }

                        float edgeT = Mathf.InverseLerp(coverageHardness, 1f, normalizedDistance);
                        float contribution = 1f - edgeT * edgeT * (3f - 2f * edgeT);
                        int pixelIndex = pixelY * resolution + pixelX;
                        density[pixelIndex] = 1f - (1f - density[pixelIndex]) * (1f - contribution);
                        colorSum[pixelIndex] += new Vector3(
                            sample.color.r,
                            sample.color.g,
                            sample.color.b) * contribution;
                        colorWeight[pixelIndex] += contribution;
                        if (contribution > heightWeight[pixelIndex])
                        {
                            heightWeight[pixelIndex] = contribution;
                            rootHeight[pixelIndex] = sample.position.y;
                        }
                    }
                }
            }

            Color[] coveragePixels = new Color[pixelCount];
            Color[] heightPixels = new Color[pixelCount];
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                Vector3 color = colorWeight[pixelIndex] > 0.0001f
                    ? colorSum[pixelIndex] / colorWeight[pixelIndex]
                    : Vector3.zero;
                float pixelDensity = density[pixelIndex];
                coveragePixels[pixelIndex] = new Color(
                    color.x * pixelDensity,
                    color.y * pixelDensity,
                    color.z * pixelDensity,
                    pixelDensity);
                float normalizedHeight = heightWeight[pixelIndex] > 0f
                    ? Mathf.Clamp01((rootHeight[pixelIndex] - minimum.y) / worldHeight)
                    : 0f;
                heightPixels[pixelIndex] = new Color(normalizedHeight * pixelDensity, 0f, 0f, 1f);
            }

            coverageTexture.SetPixels(coveragePixels);
            coverageTexture.Apply(true, false);
            heightTexture.SetPixels(heightPixels);
            heightTexture.Apply(true, false);
            cachedInstanceCount = samples.Count;
            cachedColorSignature = colorSignature;
            dirty = false;
            return true;
        }

        private static int ComputeColorSignature(AnimeGrassField targetField)
        {
            unchecked
            {
                int hash = 17;
                IReadOnlyList<AnimeGrassPrototype> prototypes = targetField.Prototypes;
                hash = hash * 31 + prototypes.Count;
                for (int prototypeIndex = 0; prototypeIndex < prototypes.Count; prototypeIndex++)
                {
                    AnimeGrassPrototype prototype = prototypes[prototypeIndex];
                    hash = hash * 31 + (prototype != null ? prototype.GetInstanceID() : 0);
                    if (prototype == null)
                    {
                        continue;
                    }

                    hash = hash * 31 + prototype.DefaultInstanceColor.GetHashCode();
                    Material material = FindFirstMaterial(prototype);
                    hash = hash * 31 + (material != null ? material.GetInstanceID() : 0);
                    if (material == null)
                    {
                        continue;
                    }

                    if (material.HasProperty("_RootColor"))
                    {
                        hash = hash * 31 + material.GetColor("_RootColor").GetHashCode();
                    }
                    if (material.HasProperty("_TipColor"))
                    {
                        hash = hash * 31 + material.GetColor("_TipColor").GetHashCode();
                    }
                    if (material.HasProperty("_BaseColor"))
                    {
                        hash = hash * 31 + material.GetColor("_BaseColor").GetHashCode();
                    }
                }

                return hash;
            }
        }

        private bool TryResolveInstanceColor(
            AnimeGrassField targetField,
            AnimeGrassInstance instance,
            out Color grassColor)
        {
            grassColor = Color.white;
            if (instance.prototypeIndex < 0 || instance.prototypeIndex >= targetField.Prototypes.Count)
            {
                return false;
            }

            AnimeGrassPrototype prototype = targetField.Prototypes[instance.prototypeIndex];
            Material material = prototype != null ? FindFirstMaterial(prototype) : null;
            if (prototype == null || material == null)
            {
                return false;
            }

            Color rootColor = material.HasProperty("_RootColor")
                ? material.GetColor("_RootColor")
                : Color.white;
            Color tipColor = material.HasProperty("_TipColor")
                ? material.GetColor("_TipColor")
                : rootColor;
            grassColor = Color.Lerp(rootColor, tipColor, 0.62f);
            if (material.HasProperty("_BaseColor"))
            {
                grassColor *= material.GetColor("_BaseColor");
            }

            Color instanceTint = instance.color.a > 0.0001f
                ? instance.color
                : prototype.DefaultInstanceColor;
            grassColor *= instanceTint;
            grassColor *= colorMultiplier;
            grassColor.a = 1f;
            return true;
        }

        private static Material FindFirstMaterial(AnimeGrassPrototype prototype)
        {
            AnimeGrassLod[] lods = prototype.Lods;
            if (lods == null)
            {
                return null;
            }

            for (int i = 0; i < lods.Length; i++)
            {
                if (lods[i] != null && lods[i].material != null)
                {
                    return lods[i].material;
                }
            }

            return null;
        }

        private void EnsureTextures(int resolution)
        {
            if (coverageTexture != null && coverageTexture.width == resolution)
            {
                return;
            }

            ReleaseTexture(ref coverageTexture);
            ReleaseTexture(ref heightTexture);
            coverageTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
            {
                name = name + " Far Grass Coverage",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            TextureFormat heightFormat = SystemInfo.SupportsTextureFormat(TextureFormat.RFloat)
                ? TextureFormat.RFloat
                : TextureFormat.RGBAFloat;
            heightTexture = new Texture2D(resolution, resolution, heightFormat, true, true)
            {
                name = name + " Far Grass Height",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private bool EnsureMaterial()
        {
            if (overlayMaterial != null)
            {
                return true;
            }

            Shader shader = Resources.Load<Shader>(OverlayShaderResource);
            if (shader == null)
            {
                shader = Shader.Find(OverlayShaderName);
            }
            if (shader == null)
            {
                Debug.LogError("[AnimeGress] 找不到远景草覆盖 Shader。", this);
                return false;
            }

            overlayMaterial = new Material(shader)
            {
                name = "AnimeGress Far Field Overlay (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            return true;
        }

        private void ReleaseResources()
        {
            ReleaseTexture(ref coverageTexture);
            ReleaseTexture(ref heightTexture);
            if (overlayMaterial != null)
            {
                DestroyRuntimeObject(overlayMaterial);
                overlayMaterial = null;
            }
        }

        private static void ReleaseTexture(ref Texture2D texture)
        {
            if (texture != null)
            {
                DestroyRuntimeObject(texture);
                texture = null;
            }
        }

        private static void DestroyRuntimeObject(Object target)
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
