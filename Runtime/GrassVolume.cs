using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Enlyn.Grass
{
    [MovedFrom(true, sourceNamespace: "Enlyn.Grass", sourceAssembly: "Ming.AnimeGrass.Runtime", sourceClassName: "AnimeSurfaceCacheStampShape")]
    public enum GrassVolumeShape
    {
        Sphere,
        Box
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("AnimeGrass/GrassVolume")]
    [MovedFrom(true, sourceNamespace: "Enlyn.Grass", sourceAssembly: "Ming.AnimeGrass.Runtime", sourceClassName: "AnimeSurfaceCacheStamp")]
    public sealed class GrassVolume : MonoBehaviour
    {
        private const int CurrentSerializationVersion = 1;
        public const int MaxGrassInteractionVolumes = 16;
        private static readonly List<GrassVolume> ActiveVolumeList = new List<GrassVolume>();
        private static readonly int InteractionVolumeCountId = Shader.PropertyToID("_EnlynGrassInteractionVolumeCount");
        private static readonly int InteractionVolumeCenterShapeId = Shader.PropertyToID("_EnlynGrassInteractionVolumeCenterShape");
        private static readonly int InteractionVolumeParamsId = Shader.PropertyToID("_EnlynGrassInteractionVolumeParams");
        private static readonly int InteractionVolumeExclusionParamsId = Shader.PropertyToID("_EnlynGrassInteractionVolumeExclusionParams");
        private static readonly int InteractionVolumeWorldToLocal0Id = Shader.PropertyToID("_EnlynGrassInteractionVolumeWorldToLocal0");
        private static readonly int InteractionVolumeWorldToLocal1Id = Shader.PropertyToID("_EnlynGrassInteractionVolumeWorldToLocal1");
        private static readonly int InteractionVolumeWorldToLocal2Id = Shader.PropertyToID("_EnlynGrassInteractionVolumeWorldToLocal2");
        private static readonly Vector4[] InteractionVolumeCenterShape = new Vector4[MaxGrassInteractionVolumes];
        private static readonly Vector4[] InteractionVolumeParams = new Vector4[MaxGrassInteractionVolumes];
        private static readonly Vector4[] InteractionVolumeExclusionParams = new Vector4[MaxGrassInteractionVolumes];
        private static readonly Vector4[] InteractionVolumeWorldToLocal0 = new Vector4[MaxGrassInteractionVolumes];
        private static readonly Vector4[] InteractionVolumeWorldToLocal1 = new Vector4[MaxGrassInteractionVolumes];
        private static readonly Vector4[] InteractionVolumeWorldToLocal2 = new Vector4[MaxGrassInteractionVolumes];

        [SerializeField]
        private GrassVolumeShape shape = GrassVolumeShape.Sphere;

        [SerializeField, HideInInspector]
        private Vector2 size = new Vector2(2f, 2f);

        [SerializeField, HideInInspector]
        private float height = 2f;

        [SerializeField, HideInInspector]
        private int serializationVersion;

        [SerializeField, Range(0f, 1f)]
        private float hardness = 0.7f;

        [SerializeField, Range(0f, 1f)]
        private float wetness;

        [SerializeField, Range(0f, 1f)]
        private float snow;

        [SerializeField, Range(0f, 1f)]
        private float burn;

        [SerializeField, Range(0f, 1f)]
        private float exclusion;

        [SerializeField]
        private bool realtimeExclusion;

        [SerializeField]
        private bool repelGrass = true;

        [SerializeField, Min(0f)]
        private float grassRepulsionStrength = 0.8f;

        [SerializeField, Range(0.01f, 1f)]
        private float grassRepulsionFalloff = 0.4f;

        [SerializeField, Range(0f, 0.95f)]
        private float grassRepulsionHeightStart = 0.15f;

        [SerializeField]
        private bool renderInEditMode = true;

        [FormerlySerializedAs("alwaysShowVolume")]
        [SerializeField]
        private bool showWhenUnselected;

        [SerializeField]
        private bool showSelectedFill = true;

        private Matrix4x4 lastLocalToWorld;
        private Vector4 lastMask;
        private GrassVolumeShape lastShape;
        private float lastHardness;
        private Vector4 lastGrassInteraction;
        private Vector4 lastExclusionInteraction;

        internal static IReadOnlyList<GrassVolume> ActiveVolumes => ActiveVolumeList;
        public GrassVolumeShape Shape => shape;
        public float Hardness => hardness;
        public Vector4 SurfaceMask => new Vector4(
            0f,
            0f,
            0f,
            UsesRealtimeExclusion ? 0f : exclusion);
        public bool RepelGrass => repelGrass;
        public float GrassRepulsionStrength => grassRepulsionStrength;
        public float GrassRepulsionFalloff => grassRepulsionFalloff;
        public float GrassRepulsionHeightStart => grassRepulsionHeightStart;
        public float Exclusion => exclusion;
        public bool RealtimeExclusion => realtimeExclusion;
        public bool UsesRealtimeExclusion => realtimeExclusion || GetComponentInParent<Camera>() != null;
        public bool ShowWhenUnselected => showWhenUnselected;
        public bool ShouldRender => Application.isPlaying || renderInEditMode;
        public Matrix4x4 WorldToLocalMatrix => transform.worldToLocalMatrix;
        public Matrix4x4 StampDrawMatrix
        {
            get
            {
                Bounds bounds = WorldBounds;
                return Matrix4x4.TRS(
                    bounds.center,
                    Quaternion.identity,
                    new Vector3(bounds.size.x, 1f, bounds.size.z));
            }
        }
        public Bounds WorldBounds
        {
            get
            {
                Matrix4x4 matrix = transform.localToWorldMatrix;
                Vector3 extents = new Vector3(
                    (Mathf.Abs(matrix.m00) + Mathf.Abs(matrix.m01) + Mathf.Abs(matrix.m02)) * 0.5f,
                    (Mathf.Abs(matrix.m10) + Mathf.Abs(matrix.m11) + Mathf.Abs(matrix.m12)) * 0.5f,
                    (Mathf.Abs(matrix.m20) + Mathf.Abs(matrix.m21) + Mathf.Abs(matrix.m22)) * 0.5f);
                return new Bounds(matrix.MultiplyPoint3x4(Vector3.zero), extents * 2f);
            }
        }

        private void Reset()
        {
            serializationVersion = CurrentSerializationVersion;
        }

        private void OnEnable()
        {
            UpgradeSerializedData();
            EnsureValidTransformScale();
            if (!ActiveVolumeList.Contains(this))
            {
                ActiveVolumeList.Add(this);
            }

            StoreState();
            if (SurfaceMask.w > 0.0001f)
            {
                AnimeSurfaceCache.NotifyChanged(WorldBounds);
            }
            ApplyGrassInteractionGlobals();
        }

        private void OnDisable()
        {
            ActiveVolumeList.Remove(this);
            if (exclusion > 0.0001f || lastMask.w > 0.0001f)
            {
                AnimeSurfaceCache.RequestAllRefresh();
            }
            ApplyGrassInteractionGlobals();
        }

        private void OnValidate()
        {
            bool previouslyAffectedCache = lastMask.w > 0.0001f;
            UpgradeSerializedData();
            EnsureValidTransformScale();
            hardness = Mathf.Clamp01(hardness);
            wetness = Mathf.Clamp01(wetness);
            snow = Mathf.Clamp01(snow);
            burn = Mathf.Clamp01(burn);
            exclusion = Mathf.Clamp01(exclusion);
            grassRepulsionStrength = Mathf.Max(0f, grassRepulsionStrength);
            grassRepulsionFalloff = Mathf.Clamp(grassRepulsionFalloff, 0.01f, 1f);
            grassRepulsionHeightStart = Mathf.Clamp(grassRepulsionHeightStart, 0f, 0.95f);
            if (previouslyAffectedCache || exclusion > 0.0001f)
            {
                AnimeSurfaceCache.RequestAllRefresh();
            }
            StoreState();
            ApplyGrassInteractionGlobals();
        }

        private void Update()
        {
            Matrix4x4 currentLocalToWorld = transform.localToWorldMatrix;
            Vector4 currentMask = SurfaceMask;
            Vector4 currentGrassInteraction = GetGrassInteractionState();
            Vector4 currentExclusionInteraction = GetGrassExclusionState();
            bool volumeChanged = currentLocalToWorld != lastLocalToWorld
                || shape != lastShape
                || !Mathf.Approximately(hardness, lastHardness);
            bool maskChanged = currentMask != lastMask;
            bool interactionChanged = currentGrassInteraction != lastGrassInteraction;
            bool exclusionInteractionChanged = currentExclusionInteraction != lastExclusionInteraction;
            if (!volumeChanged
                && !maskChanged
                && !interactionChanged
                && !exclusionInteractionChanged)
            {
                return;
            }

            if (maskChanged
                || volumeChanged && (currentMask.w > 0.0001f || lastMask.w > 0.0001f))
            {
                AnimeSurfaceCache.RequestAllRefresh();
            }
            StoreState();
            ApplyGrassInteractionGlobals();
        }

        private void StoreState()
        {
            lastLocalToWorld = transform.localToWorldMatrix;
            lastMask = SurfaceMask;
            lastShape = shape;
            lastHardness = hardness;
            lastGrassInteraction = GetGrassInteractionState();
            lastExclusionInteraction = GetGrassExclusionState();
        }

        internal static void ApplyGrassInteractionGlobals(CommandBuffer commandBuffer = null)
        {
            int volumeCount = 0;
            for (int volumeIndex = 0;
                 volumeIndex < ActiveVolumeList.Count && volumeCount < MaxGrassInteractionVolumes;
                 volumeIndex++)
            {
                GrassVolume volume = ActiveVolumeList[volumeIndex];
                bool usesRealtimeExclusion = volume != null && volume.UsesRealtimeExclusion;
                if (volume == null
                    || !volume.isActiveAndEnabled
                    || !volume.ShouldRender
                    || (!volume.repelGrass || volume.grassRepulsionStrength <= 0f)
                    && (!usesRealtimeExclusion || volume.exclusion <= 0f))
                {
                    continue;
                }

                Vector3 center = volume.transform.position;
                Matrix4x4 worldToLocal = volume.transform.worldToLocalMatrix;
                InteractionVolumeCenterShape[volumeCount] = new Vector4(
                    center.x,
                    center.y,
                    center.z,
                    volume.shape == GrassVolumeShape.Sphere ? 0f : 1f);
                InteractionVolumeParams[volumeCount] = new Vector4(
                    volume.repelGrass ? volume.grassRepulsionStrength : 0f,
                    volume.grassRepulsionFalloff,
                    volume.grassRepulsionHeightStart,
                    0f);
                InteractionVolumeExclusionParams[volumeCount] = new Vector4(
                    usesRealtimeExclusion ? volume.exclusion : 0f,
                    volume.hardness,
                    0f,
                    0f);
                InteractionVolumeWorldToLocal0[volumeCount] = worldToLocal.GetRow(0);
                InteractionVolumeWorldToLocal1[volumeCount] = worldToLocal.GetRow(1);
                InteractionVolumeWorldToLocal2[volumeCount] = worldToLocal.GetRow(2);
                volumeCount++;
            }

            if (commandBuffer != null)
            {
                commandBuffer.SetGlobalFloat(InteractionVolumeCountId, volumeCount);
                commandBuffer.SetGlobalVectorArray(InteractionVolumeCenterShapeId, InteractionVolumeCenterShape);
                commandBuffer.SetGlobalVectorArray(InteractionVolumeParamsId, InteractionVolumeParams);
                commandBuffer.SetGlobalVectorArray(InteractionVolumeExclusionParamsId, InteractionVolumeExclusionParams);
                commandBuffer.SetGlobalVectorArray(InteractionVolumeWorldToLocal0Id, InteractionVolumeWorldToLocal0);
                commandBuffer.SetGlobalVectorArray(InteractionVolumeWorldToLocal1Id, InteractionVolumeWorldToLocal1);
                commandBuffer.SetGlobalVectorArray(InteractionVolumeWorldToLocal2Id, InteractionVolumeWorldToLocal2);
            }
            else
            {
                Shader.SetGlobalFloat(InteractionVolumeCountId, volumeCount);
                Shader.SetGlobalVectorArray(InteractionVolumeCenterShapeId, InteractionVolumeCenterShape);
                Shader.SetGlobalVectorArray(InteractionVolumeParamsId, InteractionVolumeParams);
                Shader.SetGlobalVectorArray(InteractionVolumeExclusionParamsId, InteractionVolumeExclusionParams);
                Shader.SetGlobalVectorArray(InteractionVolumeWorldToLocal0Id, InteractionVolumeWorldToLocal0);
                Shader.SetGlobalVectorArray(InteractionVolumeWorldToLocal1Id, InteractionVolumeWorldToLocal1);
                Shader.SetGlobalVectorArray(InteractionVolumeWorldToLocal2Id, InteractionVolumeWorldToLocal2);
            }
        }

        private void UpgradeSerializedData()
        {
            if (serializationVersion >= CurrentSerializationVersion)
            {
                return;
            }

            Vector3 parentScale = transform.parent != null
                ? Abs(transform.parent.lossyScale)
                : Vector3.one;
            transform.localScale = new Vector3(
                Mathf.Max(0.01f, size.x) / Mathf.Max(0.0001f, parentScale.x),
                Mathf.Max(0.01f, height) / Mathf.Max(0.0001f, parentScale.y),
                Mathf.Max(0.01f, size.y) / Mathf.Max(0.0001f, parentScale.z));
            serializationVersion = CurrentSerializationVersion;
        }

        private void EnsureValidTransformScale()
        {
            Vector3 localScale = transform.localScale;
            Vector3 validScale = new Vector3(
                ClampScale(localScale.x),
                ClampScale(localScale.y),
                ClampScale(localScale.z));
            if (localScale != validScale)
            {
                transform.localScale = validScale;
            }
        }

        private static float ClampScale(float value)
        {
            if (Mathf.Abs(value) >= 0.01f)
            {
                return value;
            }

            return value < 0f ? -0.01f : 0.01f;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private Vector4 GetGrassInteractionState()
        {
            return new Vector4(
                repelGrass ? 1f : 0f,
                grassRepulsionStrength,
                grassRepulsionFalloff,
                grassRepulsionHeightStart);
        }

        private Vector4 GetGrassExclusionState()
        {
            return new Vector4(
                UsesRealtimeExclusion ? 1f : 0f,
                exclusion,
                hardness,
                0f);
        }

        private void OnDrawGizmos()
        {
            if (showWhenUnselected)
            {
                DrawVolumeWireframes();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showWhenUnselected)
            {
                DrawVolumeWireframes();
            }

            if (!showSelectedFill)
            {
                return;
            }

            Color fillColor = GetOuterGizmoColor();
            fillColor.a = 0.07f;
            Gizmos.color = fillColor;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = GetVolumeGizmoMatrix(1f);
            DrawVolume(false);
            Gizmos.matrix = previousMatrix;
        }

        private void DrawVolumeWireframes()
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = GetVolumeGizmoMatrix(1f);
            Gizmos.color = GetOuterGizmoColor();
            DrawVolume(true);

            if (exclusion > 0.0001f && hardness > 0.0001f)
            {
                Gizmos.matrix = GetVolumeGizmoMatrix(Mathf.Clamp01(hardness));
                Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.75f);
                DrawVolume(true);
            }

            if (repelGrass && grassRepulsionStrength > 0.0001f)
            {
                float fullStrengthScale = Mathf.Clamp01(1f - grassRepulsionFalloff);
                if (fullStrengthScale > 0.0001f)
                {
                    Gizmos.matrix = GetVolumeGizmoMatrix(fullStrengthScale);
                    Gizmos.color = new Color(0.2f, 0.95f, 0.9f, 0.8f);
                    DrawVolume(true);
                }
            }

            Gizmos.matrix = previousMatrix;
        }

        private Matrix4x4 GetVolumeGizmoMatrix(float scale)
        {
            return transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * scale);
        }

        private Color GetOuterGizmoColor()
        {
            bool hasExclusion = exclusion > 0.0001f;
            bool hasRepulsion = repelGrass && grassRepulsionStrength > 0.0001f;
            if (hasExclusion && hasRepulsion)
            {
                return new Color(1f, 0.75f, 0.2f, 0.9f);
            }
            if (hasExclusion)
            {
                return new Color(1f, 0.3f, 0.2f, 0.9f);
            }
            if (hasRepulsion)
            {
                return new Color(0.2f, 0.95f, 0.9f, 0.9f);
            }

            return new Color(0.65f, 0.65f, 0.65f, 0.75f);
        }

        private void DrawVolume(bool wireframe)
        {
            if (shape == GrassVolumeShape.Sphere)
            {
                if (wireframe)
                {
                    Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
                }
                else
                {
                    Gizmos.DrawSphere(Vector3.zero, 0.5f);
                }
            }
            else
            {
                if (wireframe)
                {
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                }
                else
                {
                    Gizmos.DrawCube(Vector3.zero, Vector3.one);
                }
            }
        }
    }
}
