using UnityEngine;

namespace Enlyn.Grass
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AnimeSurfaceCacheSource : MonoBehaviour
    {
        [SerializeField]
        private bool excludeFromCache;

        [SerializeField]
        private Color colorMultiplier = Color.white;

        [SerializeField]
        private bool overrideBaseMap;

        [SerializeField]
        private Texture baseMap;

        [SerializeField]
        private Vector2 baseMapScale = Vector2.one;

        [SerializeField]
        private Vector2 baseMapOffset;

        [SerializeField]
        private bool overrideBaseColor;

        [SerializeField]
        private Color baseColor = Color.white;

        [SerializeField, Range(0f, 1f)]
        private float normalFlattening;

        [SerializeField, Range(0f, 1f)]
        private float wetness;

        [SerializeField, Range(0f, 1f)]
        private float snow;

        [SerializeField, Range(0f, 1f)]
        private float burn;

        [SerializeField, Range(0f, 1f)]
        private float exclusion;

        [SerializeField]
        private bool overrideAlphaClip;

        [SerializeField]
        private bool alphaClip;

        [SerializeField, Range(0f, 1f)]
        private float alphaCutoff = 0.5f;

        private Matrix4x4 lastLocalToWorld;

        public bool ExcludeFromCache => excludeFromCache;
        public Color ColorMultiplier => colorMultiplier;
        public bool OverrideBaseMap => overrideBaseMap;
        public Texture BaseMap => baseMap;
        public Vector2 BaseMapScale => baseMapScale;
        public Vector2 BaseMapOffset => baseMapOffset;
        public bool OverrideBaseColor => overrideBaseColor;
        public Color BaseColor => baseColor;
        public float NormalFlattening => normalFlattening;
        public Vector4 SurfaceMask => new Vector4(0f, 0f, 0f, exclusion);
        public bool OverrideAlphaClip => overrideAlphaClip;
        public bool AlphaClip => alphaClip;
        public float AlphaCutoff => alphaCutoff;

        private void OnEnable()
        {
            lastLocalToWorld = transform.localToWorldMatrix;
            NotifyChanged();
        }

        private void OnDisable()
        {
            NotifyChanged();
        }

        private void OnValidate()
        {
            normalFlattening = Mathf.Clamp01(normalFlattening);
            wetness = Mathf.Clamp01(wetness);
            snow = Mathf.Clamp01(snow);
            burn = Mathf.Clamp01(burn);
            exclusion = Mathf.Clamp01(exclusion);
            alphaCutoff = Mathf.Clamp01(alphaCutoff);
            NotifyChanged();
        }

        private void Update()
        {
            Matrix4x4 currentLocalToWorld = transform.localToWorldMatrix;
            if (currentLocalToWorld == lastLocalToWorld)
            {
                return;
            }

            lastLocalToWorld = currentLocalToWorld;
            NotifyChanged();
        }

        public void NotifyChanged()
        {
            AnimeSurfaceCache.RequestAllRefresh();
        }
    }
}
