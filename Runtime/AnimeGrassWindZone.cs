using UnityEngine;

namespace Enlyn.Grass
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AnimeGrassWindZone : MonoBehaviour
    {
        private const int CurrentSerializationVersion = 1;
        private static readonly int WindId = Shader.PropertyToID("_EnlynGrassWind");
        private static readonly int WindParamsId = Shader.PropertyToID("_EnlynGrassWindParams");
        private static readonly int WindColorParamsId = Shader.PropertyToID("_EnlynGrassWindColorParams");
        private static readonly int WindColorGustParamsId = Shader.PropertyToID("_EnlynGrassWindColorGustParams");
        private static readonly int WindTintId = Shader.PropertyToID("_EnlynGrassWindTint");
        private static readonly int WindTintStrengthId = Shader.PropertyToID("_EnlynGrassWindTintStrength");

        [SerializeField]
        private Vector2 direction = new Vector2(1f, 0.2f);

        [SerializeField, Min(0f)]
        private float strength = 0.35f;

        [SerializeField, Min(0f)]
        private float speed = 1.1f;

        [SerializeField, Min(0.001f)]
        private float waveScale = 0.12f;

        [SerializeField, Min(0f)]
        private float gustStrength = 0.35f;

        [SerializeField, Min(0.001f)]
        private float gustScale = 0.045f;

        [SerializeField, Min(0f)]
        private float gustSpeed = 0.55f;

        [SerializeField]
        private Color windTint = new Color(0.72f, 0.95f, 1f, 1f);

        [SerializeField, Range(0f, 1f)]
        private float windTintStrength = 0.18f;

        [SerializeField, Range(0f, 1f)]
        private float windTintVariation = 1f;

        [SerializeField, Min(0f)]
        private float windTintSpeed = 1.1f;

        [SerializeField, Min(0.001f)]
        private float windTintWaveScale = 0.12f;

        [SerializeField, Min(0f)]
        private float windTintGustStrength = 0.35f;

        [SerializeField, Min(0.001f)]
        private float windTintGustScale = 0.045f;

        [SerializeField, Min(0f)]
        private float windTintGustSpeed = 0.55f;

        [SerializeField, HideInInspector]
        private int serializationVersion;

        public Vector2 Direction
        {
            get => direction;
            set
            {
                direction = value;
                Apply();
            }
        }

        public float Strength
        {
            get => strength;
            set
            {
                strength = Mathf.Max(0f, value);
                Apply();
            }
        }

        public Color WindTint
        {
            get => windTint;
            set
            {
                windTint = value;
                Apply();
            }
        }

        private void OnEnable()
        {
            UpgradeSerializedData();
            Apply();
        }

        private void OnValidate()
        {
            UpgradeSerializedData();
            strength = Mathf.Max(0f, strength);
            speed = Mathf.Max(0f, speed);
            waveScale = Mathf.Max(0.001f, waveScale);
            gustStrength = Mathf.Max(0f, gustStrength);
            gustScale = Mathf.Max(0.001f, gustScale);
            gustSpeed = Mathf.Max(0f, gustSpeed);
            windTintStrength = Mathf.Clamp01(windTintStrength);
            windTintVariation = Mathf.Clamp01(windTintVariation);
            windTintSpeed = Mathf.Max(0f, windTintSpeed);
            windTintWaveScale = Mathf.Max(0.001f, windTintWaveScale);
            windTintGustStrength = Mathf.Max(0f, windTintGustStrength);
            windTintGustScale = Mathf.Max(0.001f, windTintGustScale);
            windTintGustSpeed = Mathf.Max(0f, windTintGustSpeed);
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        public void Apply()
        {
            Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Shader.SetGlobalVector(WindId, new Vector4(safeDirection.x, safeDirection.y, strength, speed));
            Shader.SetGlobalVector(WindParamsId, new Vector4(waveScale, gustStrength, gustScale, gustSpeed));
            Shader.SetGlobalVector(
                WindColorParamsId,
                new Vector4(windTintWaveScale, windTintSpeed, windTintGustStrength, windTintVariation));
            Shader.SetGlobalVector(
                WindColorGustParamsId,
                new Vector4(windTintGustScale, windTintGustSpeed, 0f, 0f));
            Shader.SetGlobalColor(WindTintId, windTint);
            Shader.SetGlobalFloat(WindTintStrengthId, windTintStrength);
        }

        private void Reset()
        {
            serializationVersion = CurrentSerializationVersion;
        }

        private void UpgradeSerializedData()
        {
            if (serializationVersion >= CurrentSerializationVersion)
            {
                return;
            }

            windTintSpeed = speed;
            windTintWaveScale = waveScale;
            windTintGustStrength = gustStrength;
            windTintGustScale = gustScale;
            windTintGustSpeed = gustSpeed;
            windTintVariation = 1f;
            serializationVersion = CurrentSerializationVersion;
        }
    }
}
