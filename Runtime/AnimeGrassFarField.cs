using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Enlyn.Grass
{
    public enum AnimeGrassFarFieldDistanceMode
    {
        SpatialDistance = 0,
        XYDistanceOnly = 1,
        XZDistanceOnly = 2
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AnimeGrassField))]
    [AddComponentMenu("AnimeGrass/远景草覆盖")]
    public sealed class AnimeGrassFarField : MonoBehaviour
    {
        private const string OverlayShaderResource = "AnimeGrass/AnimeGrassFarFieldOverlay";
        private const string OverlayShaderName = "Hidden/AnimeGrass/Far Field Overlay";
        private static readonly List<AnimeGrassFarField> ActiveFarFields = new List<AnimeGrassFarField>();
        private static readonly int CoverageTextureId = Shader.PropertyToID("_AnimeGrassFarCoverageTexture");
        private static readonly int HeightTextureId = Shader.PropertyToID("_AnimeGrassFarHeightTexture");
        private static readonly int WorldToUvId = Shader.PropertyToID("_AnimeGrassFarWorldToUV");
        private static readonly int HeightParamsId = Shader.PropertyToID("_AnimeGrassFarHeightParams");
        private static readonly int DistanceParamsId = Shader.PropertyToID("_AnimeGrassFarDistanceParams");
        private static readonly int DistanceModeId = Shader.PropertyToID("_AnimeGrassFarDistanceMode");
        private static readonly int AppearanceParamsId = Shader.PropertyToID("_AnimeGrassFarAppearanceParams");
        private static readonly int PatternParamsId = Shader.PropertyToID("_AnimeGrassFarPatternParams");
        private static readonly int PatternTintId = Shader.PropertyToID("_AnimeGrassFarPatternTint");
        private static readonly int PatternShadowColorId = Shader.PropertyToID("_AnimeGrassFarPatternShadowColor");
        private static readonly int DisturbanceParamsId = Shader.PropertyToID("_AnimeGrassFarDisturbanceParams");
        private static readonly int RippleParamsId = Shader.PropertyToID("_AnimeGrassFarRippleParams");
        private static readonly int ShadowColorId = Shader.PropertyToID("_AnimeGrassFarShadowColor");
        private static readonly int LightingParamsId = Shader.PropertyToID("_AnimeGrassFarLightingParams");

        [SerializeField]
        private bool farFieldEnabled = true;

        [SerializeField]
        private AnimeGrassFarFieldDistanceMode distanceMode;

        [SerializeField, Min(0f)]
        private float transitionStartDistance = 57f;

        [SerializeField, Min(0.01f)]
        private float transitionEndDistance = 60f;

        [SerializeField, Min(0f)]
        private float fadeOutStartDistance = 180f;

        [SerializeField, Min(0.01f)]
        private float maximumDisplayDistance = 220f;

        [SerializeField, Range(64, 1024)]
        private int cacheResolution = 256;

        [SerializeField, Min(0.05f)]
        private float coverageRadius = 0.6f;

        [SerializeField, Range(0f, 0.95f)]
        private float coverageHardness = 0.55f;

        [SerializeField, Range(0, 8)]
        private int coverageHoleFillPixels = 3;

        [SerializeField]
        private Color colorMultiplier = Color.white;

        [SerializeField]
        private bool matchNearGrassColor = true;

        [SerializeField, Range(0f, 1f)]
        private float colorInfluence = 0.7f;

        [SerializeField, Range(0f, 1f)]
        private float nearGrassLightingInfluence = 1f;

        [SerializeField]
        private bool surfacePatternEnabled = true;

        [SerializeField]
        private Vector2 surfacePatternDirection = new Vector2(1f, 0.2f);

        [SerializeField]
        private Color surfacePatternTint = new Color(0.72f, 0.95f, 1f, 1f);

        [SerializeField, Range(0f, 1f)]
        private float surfacePatternTintStrength = 0.18f;

        [SerializeField, Range(0f, 1f)]
        private float pseudoShadowStrength = 0.18f;

        [SerializeField, ColorUsage(false, false)]
        private Color surfacePatternShadowColor = Color.black;

        [SerializeField, Range(0f, 1f)]
        private float pseudoShadowDisturbance = 0.55f;

        [SerializeField, Min(0.25f)]
        private float pseudoShadowPatchSize = 6f;

        [SerializeField, Min(0f)]
        private float pseudoShadowDriftSpeed = 0.6f;

        [SerializeField, Range(0f, 1f)]
        private float pseudoShadowWaveCurvature = 0.8f;

        [SerializeField, Min(0.5f)]
        private float pseudoShadowWaveSpacing = 9f;

        [FormerlySerializedAs("pseudoShadowArcRadius")]
        [SerializeField, Min(1f)]
        private float pseudoShadowCurveScale = 20f;

        [SerializeField, Range(0f, 2f)]
        private float windTintResponse = 1f;

        [SerializeField, Min(0.05f)]
        private float surfaceHeightTolerance = 0.8f;

        [SerializeField, Range(0.5f, 1f)]
        private float minimumUpwardNormal = 0.5f;

        [SerializeField, Range(0.05f, 1f)]
        private float surfaceFilterEdgeSoftness = 0.65f;

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
        private Color representativeShadowColor = new Color(0.55f, 0.65f, 0.48f, 1f);
        private float representativeReceiveShadowStrength = 0.7f;

        private struct FarInstanceSample
        {
            public Vector3 position;
            public Color color;
            public Color shadowColor;
            public float receiveShadowStrength;
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
            fadeOutStartDistance = Mathf.Max(transitionEndDistance, fadeOutStartDistance);
            maximumDisplayDistance = Mathf.Max(
                fadeOutStartDistance + 0.01f,
                maximumDisplayDistance);
            cacheResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(cacheResolution), 64, 1024);
            coverageRadius = Mathf.Max(0.05f, coverageRadius);
            coverageHardness = Mathf.Clamp(coverageHardness, 0f, 0.95f);
            coverageHoleFillPixels = Mathf.Clamp(coverageHoleFillPixels, 0, 8);
            colorInfluence = Mathf.Clamp01(colorInfluence);
            nearGrassLightingInfluence = Mathf.Clamp01(nearGrassLightingInfluence);
            if (surfacePatternDirection.sqrMagnitude <= 0.0001f)
            {
                surfacePatternDirection = Vector2.right;
            }
            surfacePatternTintStrength = Mathf.Clamp01(surfacePatternTintStrength);
            pseudoShadowStrength = Mathf.Clamp01(pseudoShadowStrength);
            surfacePatternShadowColor.a = 1f;
            pseudoShadowDisturbance = Mathf.Clamp01(pseudoShadowDisturbance);
            pseudoShadowPatchSize = Mathf.Max(0.25f, pseudoShadowPatchSize);
            pseudoShadowDriftSpeed = Mathf.Max(0f, pseudoShadowDriftSpeed);
            pseudoShadowWaveCurvature = Mathf.Clamp01(pseudoShadowWaveCurvature);
            pseudoShadowWaveSpacing = Mathf.Max(0.5f, pseudoShadowWaveSpacing);
            pseudoShadowCurveScale = Mathf.Max(1f, pseudoShadowCurveScale);
            windTintResponse = Mathf.Max(0f, windTintResponse);
            surfaceHeightTolerance = Mathf.Max(0.05f, surfaceHeightTolerance);
            minimumUpwardNormal = Mathf.Clamp(minimumUpwardNormal, 0.5f, 1f);
            surfaceFilterEdgeSoftness = Mathf.Clamp(surfaceFilterEdgeSoftness, 0.05f, 1f);
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
                    if (prototype.IsLodActive(lodIndex)
                        && lod != null
                        && lod.material != null
                        && lod.endDistance > farthestEndDistance)
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
            fadeOutStartDistance = Mathf.Max(transitionEndDistance, fadeOutStartDistance);
            maximumDisplayDistance = Mathf.Max(
                fadeOutStartDistance + 0.01f,
                maximumDisplayDistance);
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
                    maximumDisplayDistance,
                    1f / Mathf.Max(0.01f, transitionEndDistance - transitionStartDistance),
                    1f / Mathf.Max(0.01f, maximumDisplayDistance - fadeOutStartDistance)));
            properties.SetFloat(DistanceModeId, (float)distanceMode);
            properties.SetVector(
                AppearanceParamsId,
                new Vector4(
                    matchNearGrassColor ? 1f : colorInfluence,
                    surfacePatternEnabled ? pseudoShadowStrength : 0f,
                    windTintResponse,
                    minimumUpwardNormal));
            Vector2 safePatternDirection = surfacePatternDirection.sqrMagnitude > 0.0001f
                ? surfacePatternDirection.normalized
                : Vector2.right;
            properties.SetVector(
                PatternParamsId,
                new Vector4(
                    safePatternDirection.x,
                    safePatternDirection.y,
                    surfacePatternEnabled ? 1f : 0f,
                    pseudoShadowDriftSpeed));
            Color patternTint = surfacePatternTint;
            patternTint.a = surfacePatternEnabled ? surfacePatternTintStrength : 0f;
            properties.SetColor(PatternTintId, patternTint);
            properties.SetColor(PatternShadowColorId, surfacePatternShadowColor);
            properties.SetVector(
                DisturbanceParamsId,
                new Vector4(
                    pseudoShadowDisturbance,
                    1f / Mathf.Max(0.25f, pseudoShadowPatchSize),
                    0f,
                    0f));
            properties.SetVector(
                RippleParamsId,
                new Vector4(
                    pseudoShadowWaveCurvature,
                    2f * Mathf.PI / Mathf.Max(0.5f, pseudoShadowWaveSpacing),
                    pseudoShadowCurveScale,
                    0f));
            properties.SetColor(ShadowColorId, representativeShadowColor);
            properties.SetVector(
                LightingParamsId,
                new Vector4(
                    matchNearGrassColor ? nearGrassLightingInfluence : 0f,
                    representativeReceiveShadowStrength,
                    0f,
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
                if (TryResolveInstanceAppearance(
                    targetField,
                    instance,
                    out Color instanceFarColor,
                    out Color instanceShadowColor,
                    out float instanceReceiveShadowStrength))
                {
                    samples.Add(new FarInstanceSample
                    {
                        position = instance.position,
                        color = instanceFarColor,
                        shadowColor = instanceShadowColor,
                        receiveShadowStrength = instanceReceiveShadowStrength
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

            Color shadowColorSum = Color.clear;
            float receiveShadowStrengthSum = 0f;
            for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                shadowColorSum += samples[sampleIndex].shadowColor;
                receiveShadowStrengthSum += samples[sampleIndex].receiveShadowStrength;
            }
            float inverseSampleCount = 1f / samples.Count;
            representativeShadowColor = shadowColorSum * inverseSampleCount;
            representativeShadowColor.a = 1f;
            representativeReceiveShadowStrength = Mathf.Clamp01(
                receiveShadowStrengthSum * inverseSampleCount);

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
            heightParams = new Vector4(
                minimum.y,
                worldHeight,
                surfaceHeightTolerance,
                surfaceFilterEdgeSoftness);

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

            int[] coverageSourcePixels = null;
            if (coverageHoleFillPixels > 0)
            {
                density = CloseCoverageHoles(
                    density,
                    resolution,
                    coverageHoleFillPixels,
                    out coverageSourcePixels);
            }

            Color[] coveragePixels = new Color[pixelCount];
            Color[] heightPixels = new Color[pixelCount];
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                int attributePixelIndex = pixelIndex;
                if (colorWeight[attributePixelIndex] <= 0.0001f
                    && coverageSourcePixels != null
                    && coverageSourcePixels[pixelIndex] >= 0)
                {
                    attributePixelIndex = coverageSourcePixels[pixelIndex];
                }

                Vector3 color = colorWeight[attributePixelIndex] > 0.0001f
                    ? colorSum[attributePixelIndex] / colorWeight[attributePixelIndex]
                    : Vector3.zero;
                float pixelDensity = density[pixelIndex];
                coveragePixels[pixelIndex] = new Color(
                    color.x * pixelDensity,
                    color.y * pixelDensity,
                    color.z * pixelDensity,
                    pixelDensity);
                float normalizedHeight = heightWeight[attributePixelIndex] > 0f
                    ? Mathf.Clamp01((rootHeight[attributePixelIndex] - minimum.y) / worldHeight)
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

        private static float[] CloseCoverageHoles(
            float[] source,
            int resolution,
            int radius,
            out int[] sourcePixels)
        {
            int pixelCount = source.Length;
            float[] horizontalDilated = new float[pixelCount];
            int[] horizontalSources = new int[pixelCount];
            float[] dilated = new float[pixelCount];
            int[] dilatedSources = new int[pixelCount];

            for (int y = 0; y < resolution; y++)
            {
                int row = y * resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float best = 0f;
                    int bestIndex = -1;
                    int minX = Mathf.Max(0, x - radius);
                    int maxX = Mathf.Min(resolution - 1, x + radius);
                    for (int sampleX = minX; sampleX <= maxX; sampleX++)
                    {
                        int sampleIndex = row + sampleX;
                        if (source[sampleIndex] > best)
                        {
                            best = source[sampleIndex];
                            bestIndex = sampleIndex;
                        }
                    }

                    int pixelIndex = row + x;
                    horizontalDilated[pixelIndex] = best;
                    horizontalSources[pixelIndex] = bestIndex;
                }
            }

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float best = 0f;
                    int bestIndex = -1;
                    int minY = Mathf.Max(0, y - radius);
                    int maxY = Mathf.Min(resolution - 1, y + radius);
                    for (int sampleY = minY; sampleY <= maxY; sampleY++)
                    {
                        int sampleIndex = sampleY * resolution + x;
                        if (horizontalDilated[sampleIndex] > best)
                        {
                            best = horizontalDilated[sampleIndex];
                            bestIndex = horizontalSources[sampleIndex];
                        }
                    }

                    int pixelIndex = y * resolution + x;
                    dilated[pixelIndex] = best;
                    dilatedSources[pixelIndex] = bestIndex;
                }
            }

            float[] horizontalEroded = new float[pixelCount];
            for (int y = 0; y < resolution; y++)
            {
                int row = y * resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float minimum = 1f;
                    int minX = Mathf.Max(0, x - radius);
                    int maxX = Mathf.Min(resolution - 1, x + radius);
                    for (int sampleX = minX; sampleX <= maxX; sampleX++)
                    {
                        minimum = Mathf.Min(minimum, dilated[row + sampleX]);
                    }
                    horizontalEroded[row + x] = minimum;
                }
            }

            float[] closed = new float[pixelCount];
            sourcePixels = new int[pixelCount];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float minimum = 1f;
                    int minY = Mathf.Max(0, y - radius);
                    int maxY = Mathf.Min(resolution - 1, y + radius);
                    for (int sampleY = minY; sampleY <= maxY; sampleY++)
                    {
                        minimum = Mathf.Min(
                            minimum,
                            horizontalEroded[sampleY * resolution + x]);
                    }

                    int pixelIndex = y * resolution + x;
                    closed[pixelIndex] = Mathf.Max(source[pixelIndex], minimum);
                    sourcePixels[pixelIndex] = source[pixelIndex] > 0.0001f
                        ? pixelIndex
                        : dilatedSources[pixelIndex];
                }
            }

            return closed;
        }

        private int ComputeColorSignature(AnimeGrassField targetField)
        {
            unchecked
            {
                int hash = 17;
                IReadOnlyList<AnimeGrassPrototype> prototypes = targetField.Prototypes;
                hash = hash * 31 + prototypes.Count;
                hash = hash * 31 + matchNearGrassColor.GetHashCode();
                hash = hash * 31 + colorMultiplier.GetHashCode();
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
                    if (material.HasProperty("_ShadowColor"))
                    {
                        hash = hash * 31 + material.GetColor("_ShadowColor").GetHashCode();
                    }
                    if (material.HasProperty("_ReceiveShadowStrength"))
                    {
                        hash = hash * 31 + material.GetFloat("_ReceiveShadowStrength").GetHashCode();
                    }
                    if (material.HasProperty("_BaseMap"))
                    {
                        Texture baseMap = material.GetTexture("_BaseMap");
                        hash = hash * 31 + (baseMap != null ? baseMap.GetInstanceID() : 0);
                    }
                }

                return hash;
            }
        }

        private bool TryResolveInstanceAppearance(
            AnimeGrassField targetField,
            AnimeGrassInstance instance,
            out Color grassColor,
            out Color shadowColor,
            out float receiveShadowStrength)
        {
            grassColor = Color.white;
            shadowColor = new Color(0.55f, 0.65f, 0.48f, 1f);
            receiveShadowStrength = 0.7f;
            if (!targetField.IsPrototypeVisible(instance.prototypeIndex))
            {
                return false;
            }

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
            if (!matchNearGrassColor)
            {
                grassColor *= colorMultiplier;
            }
            grassColor.a = 1f;
            if (material.HasProperty("_ShadowColor"))
            {
                shadowColor = material.GetColor("_ShadowColor");
                shadowColor.a = 1f;
            }
            if (material.HasProperty("_ReceiveShadowStrength"))
            {
                receiveShadowStrength = Mathf.Clamp01(material.GetFloat("_ReceiveShadowStrength"));
            }
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
                Debug.LogError("[AnimeGrass] 找不到远景草覆盖 Shader。", this);
                return false;
            }

            overlayMaterial = new Material(shader)
            {
                name = "AnimeGrass Far Field Overlay (Runtime)",
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
