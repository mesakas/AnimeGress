using System;
using UnityEngine;

namespace Enlyn.Grass
{
    [Serializable]
    public struct AnimeGrassInstance
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector3 normal;
        public int prototypeIndex;
        public Color color;
        public float windWeight;

        public static AnimeGrassInstance Create(
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Vector3 normal,
            int prototypeIndex,
            Color color,
            float windWeight)
        {
            return new AnimeGrassInstance
            {
                position = position,
                rotation = rotation,
                scale = scale,
                normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up,
                prototypeIndex = prototypeIndex,
                color = color,
                windWeight = windWeight
            };
        }

        public Matrix4x4 ToMatrix()
        {
            return Matrix4x4.TRS(position, rotation, scale);
        }
    }
}
