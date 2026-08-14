using UnityEngine;

namespace Enlyn.Grass
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AnimeGrassWindZone : MonoBehaviour
    {
        private static readonly int WindId = Shader.PropertyToID("_EnlynGrassWind");
        private static readonly int WindParamsId = Shader.PropertyToID("_EnlynGrassWindParams");
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
            Apply();
        }

        private void OnValidate()
        {
            strength = Mathf.Max(0f, strength);
            speed = Mathf.Max(0f, speed);
            waveScale = Mathf.Max(0.001f, waveScale);
            gustStrength = Mathf.Max(0f, gustStrength);
            gustScale = Mathf.Max(0.001f, gustScale);
            gustSpeed = Mathf.Max(0f, gustSpeed);
            windTintStrength = Mathf.Clamp01(windTintStrength);
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
            Shader.SetGlobalColor(WindTintId, windTint);
            Shader.SetGlobalFloat(WindTintStrengthId, windTintStrength);
        }
    }
}
