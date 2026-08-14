using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Enlyn.Grass
{
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
        [Tooltip("该 LOD 完全显示的距离点。非首个 LOD 会在开始距离之前渐显。")]
        public float startDistance;

        [Min(0f)]
        [Tooltip("该 LOD 结束显示的距离。最后一个 LOD 的结束距离就是最大显示距离。0 表示无上限。")]
        public float endDistance = 20f;

        [Min(0f)]
        [Tooltip("点状剔除渐显/渐隐的距离范围。新 LOD 在开始距离之前渐显，旧 LOD 在结束距离之前渐隐。")]
        public float fadeDistance = 2f;

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

    [CreateAssetMenu(menuName = "AnimeGress/草类型配置", fileName = "AnimeGrassPrototype")]
    public sealed class AnimeGrassPrototype : ScriptableObject
    {
        [SerializeField]
        private AnimeGrassLod[] lods =
        {
            new AnimeGrassLod { startDistance = 0f, endDistance = 12f, fadeDistance = 2f },
            new AnimeGrassLod { startDistance = 12f, endDistance = 32f, fadeDistance = 2f },
            new AnimeGrassLod { startDistance = 32f, endDistance = 60f, fadeDistance = 3f }
        };

        [SerializeField, Min(0f)]
        private float windWeight = 1f;

        [SerializeField]
        private Color defaultInstanceColor = Color.white;

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
        public float WindWeight => windWeight;
        public Color DefaultInstanceColor => defaultInstanceColor;
        public Vector3 ModelPositionOffset => modelPositionOffset;
        public Quaternion ModelRotationOffset => Quaternion.Euler(modelRotationOffset);
        public Vector3 ModelScale => modelScale;
        public Matrix4x4 ModelCorrectionMatrix => Matrix4x4.TRS(
            modelPositionOffset,
            Quaternion.Euler(modelRotationOffset),
            modelScale);

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

        public float GetMaxRenderDistance()
        {
            float maxDistance = 0f;
            if (lods == null)
            {
                return maxDistance;
            }

            for (int i = 0; i < lods.Length; i++)
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

                maxDistance = Mathf.Max(maxDistance, lod.endDistance);
            }

            return maxDistance;
        }

        private void OnValidate()
        {
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

            for (int i = 0; i < lods.Length; i++)
            {
                AnimeGrassLod lod = lods[i];
                if (lod == null)
                {
                    continue;
                }

                lod.startDistance = Mathf.Max(0f, lod.startDistance);
                lod.subMeshIndex = Mathf.Max(0, lod.subMeshIndex);
                lod.fadeDistance = Mathf.Max(0f, lod.fadeDistance);
                if (lod.endDistance > 0f && lod.endDistance <= lod.startDistance)
                {
                    lod.endDistance = lod.startDistance + 1f;
                }
            }
        }

        private static float EnsureNonZeroScale(float value)
        {
            if (Mathf.Abs(value) >= 0.0001f)
            {
                return value;
            }

            return value < 0f ? -0.0001f : 0.0001f;
        }
    }
}
