using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Enlyn.Grass
{
    public enum AnimeSurfaceCacheStampShape
    {
        Sphere,
        Box
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AnimeSurfaceCacheStamp : MonoBehaviour
    {
        public const int MaxGrassInteractionVolumes = 16;
        private static readonly List<AnimeSurfaceCacheStamp> ActiveStampList = new List<AnimeSurfaceCacheStamp>();
        private static readonly int InteractionVolumeCountId = Shader.PropertyToID("_EnlynGrassInteractionVolumeCount");
        private static readonly int InteractionVolumeCenterShapeId = Shader.PropertyToID("_EnlynGrassInteractionVolumeCenterShape");
        private static readonly int InteractionVolumeHalfSizeStrengthId = Shader.PropertyToID("_EnlynGrassInteractionVolumeHalfSizeStrength");
        private static readonly int InteractionVolumeRotationParamsId = Shader.PropertyToID("_EnlynGrassInteractionVolumeRotationParams");
        private static readonly Vector4[] InteractionVolumeCenterShape = new Vector4[MaxGrassInteractionVolumes];
        private static readonly Vector4[] InteractionVolumeHalfSizeStrength = new Vector4[MaxGrassInteractionVolumes];
        private static readonly Vector4[] InteractionVolumeRotationParams = new Vector4[MaxGrassInteractionVolumes];

        [SerializeField]
        private AnimeSurfaceCacheStampShape shape = AnimeSurfaceCacheStampShape.Sphere;

        [SerializeField]
        private Vector2 size = new Vector2(2f, 2f);

        [SerializeField, Min(0.01f)]
        private float height = 2f;

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
        private Vector2 lastSize;
        private float lastHeight;
        private AnimeSurfaceCacheStampShape lastShape;
        private float lastHardness;
        private Vector4 lastGrassInteraction;

        internal static IReadOnlyList<AnimeSurfaceCacheStamp> ActiveStamps => ActiveStampList;
        public AnimeSurfaceCacheStampShape Shape => shape;
        public Vector2 Size => size;
        public float Height => height;
        public Vector3 VolumeSize => new Vector3(size.x, height, size.y);
        public float Hardness => hardness;
        public Vector4 SurfaceMask => new Vector4(0f, 0f, 0f, exclusion);
        public bool RepelGrass => repelGrass;
        public float GrassRepulsionStrength => grassRepulsionStrength;
        public float GrassRepulsionFalloff => grassRepulsionFalloff;
        public float GrassRepulsionHeightStart => grassRepulsionHeightStart;
        public float Exclusion => exclusion;
        public bool ShowWhenUnselected => showWhenUnselected;
        public bool ShouldRender => Application.isPlaying || renderInEditMode;
        public Matrix4x4 LocalToWorldMatrix => Matrix4x4.TRS(
            transform.position,
            Quaternion.Euler(0f, transform.eulerAngles.y, 0f),
            new Vector3(size.x, 1f, size.y));
        public Bounds WorldBounds
        {
            get
            {
                Vector3 halfSize = VolumeSize * 0.5f;
                Quaternion yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                Vector3 right = yaw * Vector3.right;
                Vector3 forward = yaw * Vector3.forward;
                Vector3 extents = new Vector3(
                    Mathf.Abs(right.x) * halfSize.x + Mathf.Abs(forward.x) * halfSize.z,
                    halfSize.y,
                    Mathf.Abs(right.z) * halfSize.x + Mathf.Abs(forward.z) * halfSize.z);
                return new Bounds(transform.position, extents * 2f);
            }
        }

        private void OnEnable()
        {
            if (!ActiveStampList.Contains(this))
            {
                ActiveStampList.Add(this);
            }

            StoreState();
            if (exclusion > 0.0001f)
            {
                AnimeSurfaceCache.NotifyChanged(WorldBounds);
            }
            ApplyGrassInteractionGlobals();
        }

        private void OnDisable()
        {
            ActiveStampList.Remove(this);
            if (exclusion > 0.0001f || lastMask.w > 0.0001f)
            {
                AnimeSurfaceCache.RequestAllRefresh();
            }
            ApplyGrassInteractionGlobals();
        }

        private void OnValidate()
        {
            bool previouslyAffectedCache = lastMask.w > 0.0001f;
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);
            height = Mathf.Max(0.01f, height);
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
            bool volumeChanged = currentLocalToWorld != lastLocalToWorld
                || size != lastSize
                || !Mathf.Approximately(height, lastHeight)
                || shape != lastShape
                || !Mathf.Approximately(hardness, lastHardness);
            bool maskChanged = currentMask != lastMask;
            bool interactionChanged = currentGrassInteraction != lastGrassInteraction;
            if (!volumeChanged && !maskChanged && !interactionChanged)
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
            lastSize = size;
            lastHeight = height;
            lastShape = shape;
            lastHardness = hardness;
            lastGrassInteraction = GetGrassInteractionState();
        }

        internal static void ApplyGrassInteractionGlobals(CommandBuffer commandBuffer = null)
        {
            int volumeCount = 0;
            for (int stampIndex = 0;
                 stampIndex < ActiveStampList.Count && volumeCount < MaxGrassInteractionVolumes;
                 stampIndex++)
            {
                AnimeSurfaceCacheStamp stamp = ActiveStampList[stampIndex];
                if (stamp == null
                    || !stamp.isActiveAndEnabled
                    || !stamp.ShouldRender
                    || !stamp.repelGrass
                    || stamp.grassRepulsionStrength <= 0f)
                {
                    continue;
                }

                Vector3 center = stamp.transform.position;
                Vector3 halfSize = stamp.VolumeSize * 0.5f;
                float yawRadians = stamp.transform.eulerAngles.y * Mathf.Deg2Rad;
                InteractionVolumeCenterShape[volumeCount] = new Vector4(
                    center.x,
                    center.y,
                    center.z,
                    stamp.shape == AnimeSurfaceCacheStampShape.Sphere ? 0f : 1f);
                InteractionVolumeHalfSizeStrength[volumeCount] = new Vector4(
                    Mathf.Max(0.005f, halfSize.x),
                    Mathf.Max(0.005f, halfSize.y),
                    Mathf.Max(0.005f, halfSize.z),
                    stamp.grassRepulsionStrength);
                InteractionVolumeRotationParams[volumeCount] = new Vector4(
                    Mathf.Cos(yawRadians),
                    Mathf.Sin(yawRadians),
                    stamp.grassRepulsionFalloff,
                    stamp.grassRepulsionHeightStart);
                volumeCount++;
            }

            if (commandBuffer != null)
            {
                commandBuffer.SetGlobalFloat(InteractionVolumeCountId, volumeCount);
                commandBuffer.SetGlobalVectorArray(InteractionVolumeCenterShapeId, InteractionVolumeCenterShape);
                commandBuffer.SetGlobalVectorArray(InteractionVolumeHalfSizeStrengthId, InteractionVolumeHalfSizeStrength);
                commandBuffer.SetGlobalVectorArray(InteractionVolumeRotationParamsId, InteractionVolumeRotationParams);
            }
            else
            {
                Shader.SetGlobalFloat(InteractionVolumeCountId, volumeCount);
                Shader.SetGlobalVectorArray(InteractionVolumeCenterShapeId, InteractionVolumeCenterShape);
                Shader.SetGlobalVectorArray(InteractionVolumeHalfSizeStrengthId, InteractionVolumeHalfSizeStrength);
                Shader.SetGlobalVectorArray(InteractionVolumeRotationParamsId, InteractionVolumeRotationParams);
            }
        }

        private Vector4 GetGrassInteractionState()
        {
            return new Vector4(
                repelGrass ? 1f : 0f,
                grassRepulsionStrength,
                grassRepulsionFalloff,
                grassRepulsionHeightStart);
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
            return Matrix4x4.TRS(
                transform.position,
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f),
                VolumeSize * scale);
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
            if (shape == AnimeSurfaceCacheStampShape.Sphere)
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
