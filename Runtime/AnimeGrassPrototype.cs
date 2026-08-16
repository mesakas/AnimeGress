using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Enlyn.Grass
{
    public enum AnimeGrassLodDistanceMode
    {
        SpatialDistance = 0,
        SeparateXYAndZ = 1,
        XYDistanceOnly = 2,
        XZDistanceOnly = 3
    }

    [Serializable]
    public sealed class AnimeGrassLod
    {
        [Tooltip("该距离段使用的网格。可以是单面片、交叉面片，也可以是完整模型草。")]
        public Mesh mesh;

        [Tooltip("该 LOD 使用的材质。需要实例颜色、风和渐隐时，建议使用支持实例化的草 shader。")]
        public Material material;

        [Min(0)]
        [Tooltip("渲染该 LOD 网格的哪个子网格。")]
        public int subMeshIndex;

        [Min(0f)]
        [Tooltip("第一级固定为 0；后续 LOD 自动等于上一级的结束距离。")]
        public float startDistance;

        [Min(0f)]
        [Tooltip("该 LOD 结束显示的距离。最后一个 LOD 的结束距离就是最大显示距离。0 表示无上限。")]
        public float endDistance = 20f;

        [Min(0f)]
        [Tooltip("从该 LOD 切换到下一级时的互补点状过渡距离。最后一级用它渐隐到最大显示距离。")]
        public float fadeDistance = 2f;

        [Min(0f)]
        [Tooltip("XY 与 Z 分离模式下，该级 Z 轴距离的起点。第一级固定为 0，后续级自动连接。")]
        public float zStartDistance;

        [Min(0f)]
        [Tooltip("XY 与 Z 分离模式下，该 LOD 在世界 Z 轴方向结束显示的绝对距离。0 表示无上限。")]
        public float zEndDistance = 20f;

        [Min(0f)]
        [Tooltip("XY 与 Z 分离模式下，从该 LOD 沿世界 Z 轴切换到下一级时的点状渐隐距离。")]
        public float zFadeDistance = 2f;

        [Tooltip("启用后，该 LOD 会绕草根法线旋转并始终朝向草场的观察目标。适合远距离面片草。")]
        public bool faceTarget;

        [Range(-180f, 180f)]
        [Tooltip("始终面向观察目标时，绕草根法线追加的旋转角度。用于校正面片模型的正面轴向。")]
        public float faceTargetRotationOffset;

        [Tooltip("相机升到草场上方时，将该 LOD 的草叶上部弯向观察方向，减弱面片草的侧边轮廓。")]
        public bool overheadBend;

        [Range(0f, 90f)]
        [Tooltip("俯视时草叶顶部的最大弯曲角度。")]
        public float overheadBendAngle = 55f;

        [Range(0f, 89f)]
        [Tooltip("相机仰角达到该角度后开始弯曲。0 表示相机与地面齐平时开始。")]
        public float overheadBendStartAngle = 35f;

        [Range(1f, 90f)]
        [Tooltip("相机仰角达到该角度时使用完整弯曲角度。")]
        public float overheadBendEndAngle = 75f;

        public ShadowCastingMode shadowCasting = ShadowCastingMode.Off;
        public bool receiveShadows = true;

        public bool IsRenderable => mesh != null && material != null;

        public bool ContainsDistance(float distance)
        {
            if (distance < startDistance)
            {
                return false;
            }

            return endDistance <= 0f || distance < endDistance;
        }

        public float EvaluateFade(float distance)
        {
            float safeFadeDistance = Mathf.Max(0f, fadeDistance);
            float fade = 1f;

            if (distance < startDistance)
            {
                if (safeFadeDistance <= 0f || startDistance <= 0f)
                {
                    return 0f;
                }

                float startFadeBegin = Mathf.Max(0f, startDistance - safeFadeDistance);
                fade = Mathf.InverseLerp(startFadeBegin, startDistance, distance);
            }

            if (endDistance > 0f)
            {
                if (distance >= endDistance)
                {
                    return 0f;
                }

                if (safeFadeDistance > 0f)
                {
                    float endFadeBegin = Mathf.Max(startDistance, endDistance - safeFadeDistance);
                    if (distance > endFadeBegin)
                    {
                        fade = Mathf.Min(fade, Mathf.InverseLerp(endDistance, endFadeBegin, distance));
                    }
                }
            }

            return Mathf.Clamp01(fade);
        }

    }

    [CreateAssetMenu(menuName = "AnimeGrass/草类型配置", fileName = "AnimeGrassPrototype")]
    public sealed class AnimeGrassPrototype : ScriptableObject
    {
        [SerializeField]
        private AnimeGrassLod[] lods =
        {
            new AnimeGrassLod { startDistance = 0f, endDistance = 12f, fadeDistance = 2f },
            new AnimeGrassLod { startDistance = 12f, endDistance = 32f, fadeDistance = 2f },
            new AnimeGrassLod { startDistance = 32f, endDistance = 60f, fadeDistance = 3f }
        };

        [SerializeField]
        private AnimeGrassLodDistanceMode lodDistanceMode;

        [SerializeField]
        private bool replaceDistantLodsWithFarField;

        [SerializeField, Min(0)]
        private int lastMeshLodIndex;

        [SerializeField, HideInInspector]
        private bool separateAxisDistancesInitialized;

        [SerializeField, Min(0f)]
        private float windWeight = 1f;

        [SerializeField]
        private Color defaultInstanceColor = Color.white;

        [SerializeField]
        private bool distanceDensityEnabled;

        [SerializeField, Range(0f, 1f)]
        private float nearDistanceDensity = 1f;

        [SerializeField, Range(0f, 1f)]
        private float farDistanceDensity = 0.35f;

        [SerializeField, Min(0f)]
        private float densityTransitionStartDistance = 20f;

        [SerializeField, Min(0.01f)]
        private float densityTransitionEndDistance = 60f;

        [SerializeField, Range(0.001f, 0.25f)]
        private float densityTransitionSoftness = 0.08f;

        [SerializeField]
        private int densityRandomSeed = 1979;

        [SerializeField]
        [Tooltip("模型网格相对铺设点的位置校正。通常保持为零。")]
        private Vector3 modelPositionOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("模型网格相对铺设方向的欧拉角校正。用于处理模型局部轴向不一致。")]
        private Vector3 modelRotationOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("模型网格的基础缩放。用于统一校正 FBX 单位或不同草类型的尺寸。")]
        private Vector3 modelScale = Vector3.one;

        public AnimeGrassLod[] Lods => lods;
        public AnimeGrassLodDistanceMode LodDistanceMode => lodDistanceMode;
        public bool ReplaceDistantLodsWithFarField => replaceDistantLodsWithFarField;
        public int LastMeshLodIndex => GetLastActiveLodIndex();
        public float WindWeight => windWeight;
        public Color DefaultInstanceColor => defaultInstanceColor;
        public Vector3 ModelPositionOffset => modelPositionOffset;
        public Quaternion ModelRotationOffset => Quaternion.Euler(modelRotationOffset);
        public Vector3 ModelScale => modelScale;
        public Matrix4x4 ModelCorrectionMatrix => Matrix4x4.TRS(
            modelPositionOffset,
            Quaternion.Euler(modelRotationOffset),
            modelScale);

        public float EvaluateDistanceDensityFade(AnimeGrassInstance instance, float distance)
        {
            if (!distanceDensityEnabled)
            {
                return 1f;
            }

            float transition = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    densityTransitionStartDistance,
                    densityTransitionEndDistance,
                    distance));
            float targetDensity = Mathf.Lerp(
                nearDistanceDensity,
                farDistanceDensity,
                transition);
            if (targetDensity <= 0.0001f)
            {
                return 0f;
            }
            if (targetDensity >= 0.9999f)
            {
                return 1f;
            }

            float selectionValue = GetStableDensitySelection(instance, densityRandomSeed);
            float halfSoftness = densityTransitionSoftness * 0.5f;
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    selectionValue - halfSoftness,
                    selectionValue + halfSoftness,
                    targetDensity));
        }

        public int GetLodIndex(float distance)
        {
            if (lods == null)
            {
                return -1;
            }

            for (int i = 0; i < lods.Length; i++)
            {
                AnimeGrassLod lod = lods[i];
                if (lod != null && lod.IsRenderable && lod.ContainsDistance(distance))
                {
                    return i;
                }
            }

            return -1;
        }

        public float EvaluateLodDitherFade(int lodIndex, float distance)
        {
            return EvaluateLodDitherFadeFromProgress(
                lodIndex,
                EvaluateAxisLodProgress(Mathf.Max(0f, distance), false));
        }

        public float EvaluateLodDitherFade(int lodIndex, Vector3 cameraOffset)
        {
            if (lodDistanceMode == AnimeGrassLodDistanceMode.SpatialDistance)
            {
                return EvaluateLodDitherFade(lodIndex, cameraOffset.magnitude);
            }

            float xyDistance = Mathf.Sqrt(
                cameraOffset.x * cameraOffset.x
                + cameraOffset.y * cameraOffset.y);
            if (lodDistanceMode == AnimeGrassLodDistanceMode.XYDistanceOnly)
            {
                return EvaluateLodDitherFade(lodIndex, xyDistance);
            }

            if (lodDistanceMode == AnimeGrassLodDistanceMode.XZDistanceOnly)
            {
                float xzDistance = Mathf.Sqrt(
                    cameraOffset.x * cameraOffset.x
                    + cameraOffset.z * cameraOffset.z);
                return EvaluateLodDitherFade(lodIndex, xzDistance);
            }

            float zDistance = Mathf.Abs(cameraOffset.z);
            float lodProgress = Mathf.Max(
                EvaluateAxisLodProgress(xyDistance, false),
                EvaluateAxisLodProgress(zDistance, true));
            return EvaluateLodDitherFadeFromProgress(lodIndex, lodProgress);
        }

        private float EvaluateAxisLodProgress(float distance, bool useZAxis)
        {
            if (lods == null || lods.Length == 0)
            {
                return 0f;
            }

            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                AnimeGrassLod lod = lods[lodIndex];
                if (lod == null)
                {
                    continue;
                }

                float startDistance = useZAxis
                    ? lod.zStartDistance
                    : lod.startDistance;
                float endDistance = useZAxis
                    ? lod.zEndDistance
                    : lod.endDistance;
                float fadeDistance = useZAxis
                    ? lod.zFadeDistance
                    : lod.fadeDistance;
                if (endDistance <= 0f)
                {
                    return lodIndex;
                }

                float fadeBegin = Mathf.Max(
                    startDistance,
                    endDistance - Mathf.Max(0f, fadeDistance));
                if (fadeBegin < endDistance && distance <= fadeBegin)
                {
                    return lodIndex;
                }

                if (distance < endDistance)
                {
                    return lodIndex + Mathf.InverseLerp(
                        fadeBegin,
                        endDistance,
                        distance);
                }
            }

            return lods.Length;
        }

        private float EvaluateLodDitherFadeFromProgress(int lodIndex, float lodProgress)
        {
            if (lods == null || lodIndex < 0 || lodIndex >= lods.Length)
            {
                return 0f;
            }

            if (!IsLodActive(lodIndex))
            {
                return 0f;
            }

            int activeLodIndex = Mathf.FloorToInt(lodProgress);
            if (activeLodIndex < 0 || activeLodIndex >= lods.Length)
            {
                return 0f;
            }

            float transition = Mathf.Clamp01(lodProgress - activeLodIndex);
            if (lodIndex == activeLodIndex)
            {
                return 1f - transition;
            }

            if (transition > 0f && lodIndex == activeLodIndex + 1)
            {
                return -transition;
            }

            return 0f;
        }

        public float GetMaxRenderDistance()
        {
            float maxDistance = 0f;
            if (lods == null)
            {
                return maxDistance;
            }

            int lastActiveLodIndex = GetLastActiveLodIndex();
            for (int i = 0; i <= lastActiveLodIndex; i++)
            {
                AnimeGrassLod lod = lods[i];
                if (lod == null || !lod.IsRenderable)
                {
                    continue;
                }

                if (lod.endDistance <= 0f)
                {
                    return 0f;
                }

                if (lodDistanceMode == AnimeGrassLodDistanceMode.SeparateXYAndZ
                    && lod.zEndDistance <= 0f)
                {
                    return 0f;
                }

                maxDistance = Mathf.Max(maxDistance, lod.endDistance);
                if (lodDistanceMode == AnimeGrassLodDistanceMode.SeparateXYAndZ)
                {
                    maxDistance = Mathf.Max(maxDistance, lod.zEndDistance);
                }
            }

            return maxDistance;
        }

        public bool IsLodActive(int lodIndex)
        {
            return lods != null
                && lodIndex >= 0
                && lodIndex < lods.Length
                && (!replaceDistantLodsWithFarField || lodIndex <= GetLastActiveLodIndex());
        }

        private int GetLastActiveLodIndex()
        {
            if (lods == null || lods.Length == 0)
            {
                return -1;
            }

            return replaceDistantLodsWithFarField
                ? Mathf.Clamp(lastMeshLodIndex, 0, lods.Length - 1)
                : lods.Length - 1;
        }

        private void OnValidate()
        {
            nearDistanceDensity = Mathf.Clamp01(nearDistanceDensity);
            farDistanceDensity = Mathf.Clamp01(farDistanceDensity);
            densityTransitionStartDistance = Mathf.Max(0f, densityTransitionStartDistance);
            densityTransitionEndDistance = Mathf.Max(
                densityTransitionStartDistance + 0.01f,
                densityTransitionEndDistance);
            densityTransitionSoftness = Mathf.Clamp(
                densityTransitionSoftness,
                0.001f,
                0.25f);

            if (modelScale.sqrMagnitude < 0.000001f)
            {
                modelScale = Vector3.one;
            }

            modelScale.x = EnsureNonZeroScale(modelScale.x);
            modelScale.y = EnsureNonZeroScale(modelScale.y);
            modelScale.z = EnsureNonZeroScale(modelScale.z);

            if (lods == null)
            {
                return;
            }

            lastMeshLodIndex = lods.Length > 0
                ? Mathf.Clamp(lastMeshLodIndex, 0, lods.Length - 1)
                : 0;

            bool initializeSeparateAxisDistances = !separateAxisDistancesInitialized;
            AnimeGrassLod previousLod = null;
            for (int i = 0; i < lods.Length; i++)
            {
                AnimeGrassLod lod = lods[i];
                if (lod == null)
                {
                    continue;
                }

                if (previousLod == null)
                {
                    lod.startDistance = 0f;
                }
                else if (previousLod.endDistance > 0f)
                {
                    lod.startDistance = previousLod.endDistance;
                }
                else
                {
                    lod.startDistance = Mathf.Max(0f, lod.startDistance);
                }

                lod.subMeshIndex = Mathf.Max(0, lod.subMeshIndex);
                lod.fadeDistance = Mathf.Max(0f, lod.fadeDistance);
                if (initializeSeparateAxisDistances)
                {
                    lod.zEndDistance = lod.endDistance;
                    lod.zFadeDistance = lod.fadeDistance;
                }

                if (previousLod == null)
                {
                    lod.zStartDistance = 0f;
                }
                else if (previousLod.zEndDistance > 0f)
                {
                    lod.zStartDistance = previousLod.zEndDistance;
                }
                else
                {
                    lod.zStartDistance = Mathf.Max(0f, lod.zStartDistance);
                }

                lod.zFadeDistance = Mathf.Max(0f, lod.zFadeDistance);
                if (lod.zEndDistance > 0f && lod.zEndDistance <= lod.zStartDistance)
                {
                    lod.zEndDistance = lod.zStartDistance + 1f;
                }
                lod.overheadBendAngle = Mathf.Clamp(lod.overheadBendAngle, 0f, 90f);
                lod.overheadBendStartAngle = Mathf.Clamp(lod.overheadBendStartAngle, 0f, 89f);
                lod.overheadBendEndAngle = Mathf.Clamp(
                    lod.overheadBendEndAngle,
                    lod.overheadBendStartAngle + 1f,
                    90f);
                if (lod.endDistance > 0f && lod.endDistance <= lod.startDistance)
                {
                    lod.endDistance = lod.startDistance + 1f;
                }

                previousLod = lod;
            }

            separateAxisDistancesInitialized = true;
        }

        private static float EnsureNonZeroScale(float value)
        {
            if (Mathf.Abs(value) >= 0.0001f)
            {
                return value;
            }

            return value < 0f ? -0.0001f : 0.0001f;
        }

        private AnimeGrassLod GetPreviousLod(int lodIndex)
        {
            for (int i = lodIndex - 1; i >= 0; i--)
            {
                if (lods[i] != null)
                {
                    return lods[i];
                }
            }

            return null;
        }

        private static float GetStableDensitySelection(AnimeGrassInstance instance, int seed)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)Mathf.RoundToInt(instance.position.x * 1000f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(instance.position.y * 1000f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(instance.position.z * 1000f)) * 16777619u;
                hash = (hash ^ (uint)instance.prototypeIndex) * 16777619u;
                hash = (hash ^ (uint)seed) * 16777619u;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 16777216f;
            }
        }
    }
}
