using Enlyn.Grass;
using UnityEditor;
using UnityEngine;

namespace Enlyn.Grass.Editor
{
    [CustomEditor(typeof(AnimeGrassWindZone))]
    public sealed class AnimeGrassWindZoneEditor : UnityEditor.Editor
    {
        private SerializedProperty direction;
        private SerializedProperty strength;
        private SerializedProperty speed;
        private SerializedProperty waveScale;
        private SerializedProperty gustStrength;
        private SerializedProperty gustScale;
        private SerializedProperty gustSpeed;
        private SerializedProperty windTint;
        private SerializedProperty windTintStrength;

        private void OnEnable()
        {
            direction = serializedObject.FindProperty("direction");
            strength = serializedObject.FindProperty("strength");
            speed = serializedObject.FindProperty("speed");
            waveScale = serializedObject.FindProperty("waveScale");
            gustStrength = serializedObject.FindProperty("gustStrength");
            gustScale = serializedObject.FindProperty("gustScale");
            gustSpeed = serializedObject.FindProperty("gustSpeed");
            windTint = serializedObject.FindProperty("windTint");
            windTintStrength = serializedObject.FindProperty("windTintStrength");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("全局风场", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(direction, new GUIContent("风向", "XZ 平面的风向。会自动归一化。"));
            EditorGUILayout.PropertyField(strength, new GUIContent("风力强度"));
            EditorGUILayout.PropertyField(speed, new GUIContent("风速"));
            EditorGUILayout.PropertyField(waveScale, new GUIContent("主风波纹密度", "值越大，风浪变化越密。"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("阵风", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gustStrength, new GUIContent("阵风强度"));
            EditorGUILayout.PropertyField(gustScale, new GUIContent("阵风波纹密度"));
            EditorGUILayout.PropertyField(gustSpeed, new GUIContent("阵风速度"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("风色变化", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(windTint, new GUIContent("风色"));
            EditorGUILayout.PropertyField(windTintStrength, new GUIContent("风色强度", "草随风摆动时叠加的颜色强度。"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
