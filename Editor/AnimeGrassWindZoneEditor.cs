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
        private SerializedProperty windTintVariation;
        private SerializedProperty windTintSpeed;
        private SerializedProperty windTintWaveScale;
        private SerializedProperty windTintGustStrength;
        private SerializedProperty windTintGustScale;
        private SerializedProperty windTintGustSpeed;

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
            windTintVariation = serializedObject.FindProperty("windTintVariation");
            windTintSpeed = serializedObject.FindProperty("windTintSpeed");
            windTintWaveScale = serializedObject.FindProperty("windTintWaveScale");
            windTintGustStrength = serializedObject.FindProperty("windTintGustStrength");
            windTintGustScale = serializedObject.FindProperty("windTintGustScale");
            windTintGustSpeed = serializedObject.FindProperty("windTintGustSpeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("全局风向", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(direction, new GUIContent("风向", "XZ 平面的风向。会自动归一化。"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("草叶摆动", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(strength, new GUIContent("风力强度"));
            EditorGUILayout.PropertyField(speed, new GUIContent("摆动速度"));
            EditorGUILayout.PropertyField(waveScale, new GUIContent("摆动波纹密度", "只控制草叶弯曲波纹；值越大，同一范围内的摆动变化越密。"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("草叶阵风", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gustStrength, new GUIContent("阵风强度"));
            EditorGUILayout.PropertyField(gustScale, new GUIContent("阵风波纹密度"));
            EditorGUILayout.PropertyField(gustSpeed, new GUIContent("阵风速度"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("风色变化", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(windTint, new GUIContent("风色"));
            EditorGUILayout.PropertyField(windTintStrength, new GUIContent("颜色强度", "独立控制风色叠加量，不影响草叶摆动。"));
            EditorGUILayout.PropertyField(windTintVariation, new GUIContent("颜色波纹变化", "0 为整片草场使用均匀风色，1 为完整显示颜色波纹。"));
            EditorGUILayout.PropertyField(windTintSpeed, new GUIContent("颜色变化速度"));
            EditorGUILayout.PropertyField(windTintWaveScale, new GUIContent("颜色波纹密度", "只控制颜色变化的空间密度，不影响草叶摆动。"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("风色阵风", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(windTintGustStrength, new GUIContent("颜色阵风强度"));
            EditorGUILayout.PropertyField(windTintGustScale, new GUIContent("颜色阵风密度"));
            EditorGUILayout.PropertyField(windTintGustSpeed, new GUIContent("颜色阵风速度"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
