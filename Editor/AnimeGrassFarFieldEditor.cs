using Enlyn.Grass;
using UnityEditor;
using UnityEngine;

namespace Enlyn.Grass.Editor
{
    [CustomEditor(typeof(AnimeGrassFarField))]
    public sealed class AnimeGrassFarFieldEditor : UnityEditor.Editor
    {
        private SerializedProperty farFieldEnabled;
        private SerializedProperty transitionStartDistance;
        private SerializedProperty transitionEndDistance;
        private SerializedProperty cacheResolution;
        private SerializedProperty coverageRadius;
        private SerializedProperty coverageHardness;
        private SerializedProperty colorMultiplier;
        private SerializedProperty colorInfluence;
        private SerializedProperty pseudoShadowStrength;
        private SerializedProperty pseudoShadowDisturbance;
        private SerializedProperty pseudoShadowPatchSize;
        private SerializedProperty pseudoShadowDriftSpeed;
        private SerializedProperty windTintResponse;
        private SerializedProperty surfaceHeightTolerance;
        private SerializedProperty minimumUpwardNormal;
        private SerializedProperty previewInEditMode;

        private void OnEnable()
        {
            farFieldEnabled = serializedObject.FindProperty("farFieldEnabled");
            transitionStartDistance = serializedObject.FindProperty("transitionStartDistance");
            transitionEndDistance = serializedObject.FindProperty("transitionEndDistance");
            cacheResolution = serializedObject.FindProperty("cacheResolution");
            coverageRadius = serializedObject.FindProperty("coverageRadius");
            coverageHardness = serializedObject.FindProperty("coverageHardness");
            colorMultiplier = serializedObject.FindProperty("colorMultiplier");
            colorInfluence = serializedObject.FindProperty("colorInfluence");
            pseudoShadowStrength = serializedObject.FindProperty("pseudoShadowStrength");
            pseudoShadowDisturbance = serializedObject.FindProperty("pseudoShadowDisturbance");
            pseudoShadowPatchSize = serializedObject.FindProperty("pseudoShadowPatchSize");
            pseudoShadowDriftSpeed = serializedObject.FindProperty("pseudoShadowDriftSpeed");
            windTintResponse = serializedObject.FindProperty("windTintResponse");
            surfaceHeightTolerance = serializedObject.FindProperty("surfaceHeightTolerance");
            minimumUpwardNormal = serializedObject.FindProperty("minimumUpwardNormal");
            previewInEditMode = serializedObject.FindProperty("previewInEditMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(
                farFieldEnabled,
                new GUIContent("启用远景覆盖", "关闭后不会生成缓存，也不会向地表叠加远景草效果。"));

            using (new EditorGUI.DisabledScope(!farFieldEnabled.boolValue))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("过渡距离", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    transitionStartDistance,
                    new GUIContent("覆盖开始距离", "从这个相机距离开始逐渐显示地表覆盖，通常应略早于最后一级 LOD 的结束距离。"));
                EditorGUILayout.PropertyField(
                    transitionEndDistance,
                    new GUIContent("覆盖完全显示距离", "到达这个相机距离时覆盖达到完整强度。"));
                if (GUILayout.Button("匹配最后一级 LOD 渐隐距离"))
                {
                    Undo.RecordObject(target, "匹配远景草覆盖距离");
                    if (!((AnimeGrassFarField)target).MatchTransitionToLastLod())
                    {
                        Debug.LogWarning("[AnimeGress] 没有找到结束距离大于 0 的草 LOD。", target);
                    }
                    serializedObject.Update();
                    EditorUtility.SetDirty(target);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("覆盖缓存", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    cacheResolution,
                    new GUIContent("缓存分辨率", "覆盖整个草场的方形纹理分辨率。草场较大或边界需要更清晰时提高。"));
                EditorGUILayout.PropertyField(
                    coverageRadius,
                    new GUIContent("单株覆盖半径", "每株草写入地表覆盖的世界空间半径。一般设置为草间距的 0.5-0.8 倍。"));
                EditorGUILayout.PropertyField(
                    coverageHardness,
                    new GUIContent("覆盖边缘硬度", "数值越高，每株草的覆盖边缘越清晰。"));

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("颜色与风场", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    colorMultiplier,
                    new GUIContent("覆盖颜色倍率", "对从草材质和实例颜色计算出的远景颜色进行整体调整。"));
                EditorGUILayout.PropertyField(
                    colorInfluence,
                    new GUIContent("覆盖颜色强度", "控制远景颜色覆盖地表的透明度。"));
                EditorGUILayout.PropertyField(
                    pseudoShadowStrength,
                    new GUIContent("伪阴影强度", "用随全局风场移动的暗色变化模拟远景草影，不生成真实阴影。"));
                EditorGUILayout.PropertyField(
                    pseudoShadowDisturbance,
                    new GUIContent("伪阴影扰动", "打散规则风带的边缘和明暗强度。0 表示完全规则，1 表示扰动最明显。"));
                EditorGUILayout.PropertyField(
                    pseudoShadowPatchSize,
                    new GUIContent("扰动斑块尺寸", "扰动图案在世界空间中的大致尺寸。数值越大，明暗斑块越宽。"));
                EditorGUILayout.PropertyField(
                    pseudoShadowDriftSpeed,
                    new GUIContent("扰动移动速度", "扰动斑块沿全局风向移动的世界空间速度。设为 0 时图案固定。"));
                EditorGUILayout.PropertyField(
                    windTintResponse,
                    new GUIContent("风色响应", "控制全局风场颜色变化对远景覆盖的影响比例。"));

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("地表限制", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    surfaceHeightTolerance,
                    new GUIContent("地表高度容差", "只覆盖与草根缓存高度接近的可见表面，避免影响上方遮挡物或下层地面。"));
                EditorGUILayout.PropertyField(
                    minimumUpwardNormal,
                    new GUIContent("最低向上法线", "过滤墙面和陡峭侧面。0 表示不过滤，1 表示只接受完全朝上的表面。"));
                EditorGUILayout.PropertyField(
                    previewInEditMode,
                    new GUIContent("编辑模式预览", "在 Scene 视图中预览远景覆盖。"));
            }

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            AnimeGrassFarField farField = (AnimeGrassFarField)target;
            if (changed)
            {
                farField.MarkDirty();
                EditorUtility.SetDirty(farField);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "该效果在真实草之前绘制，只保留远景颜色、风色和伪阴影，不会生成远距离草几何体或真实投影。需要在 URP Renderer 中启用 Anime Grass Renderer Feature。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("立即重建覆盖缓存"))
                {
                    farField.RebuildNow();
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("选择所属草场"))
                {
                    Selection.activeObject = farField.GetComponent<AnimeGrassField>();
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("缓存状态", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("状态", farField.IsDirty ? "等待重建" : "已就绪");
            EditorGUILayout.LabelField("已缓存实例数", farField.CachedInstanceCount.ToString());
            DrawTexturePreview("颜色与覆盖率", farField.CoverageTexture);
            DrawTexturePreview("草根高度", farField.HeightTexture);
        }

        private static void DrawTexturePreview(string label, Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            EditorGUILayout.LabelField(label);
            Rect previewRect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(180f));
            EditorGUI.DrawPreviewTexture(previewRect, texture, null, ScaleMode.ScaleToFit);
        }
    }
}
