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
        private SerializedProperty fadeOutStartDistance;
        private SerializedProperty maximumDisplayDistance;
        private SerializedProperty cacheResolution;
        private SerializedProperty coverageRadius;
        private SerializedProperty coverageHardness;
        private SerializedProperty coverageHoleFillPixels;
        private SerializedProperty colorMultiplier;
        private SerializedProperty matchNearGrassColor;
        private SerializedProperty colorInfluence;
        private SerializedProperty nearGrassLightingInfluence;
        private SerializedProperty surfacePatternEnabled;
        private SerializedProperty surfacePatternDirection;
        private SerializedProperty surfacePatternTint;
        private SerializedProperty surfacePatternTintStrength;
        private SerializedProperty pseudoShadowStrength;
        private SerializedProperty surfacePatternShadowColor;
        private SerializedProperty pseudoShadowDisturbance;
        private SerializedProperty pseudoShadowPatchSize;
        private SerializedProperty pseudoShadowDriftSpeed;
        private SerializedProperty pseudoShadowWaveCurvature;
        private SerializedProperty pseudoShadowWaveSpacing;
        private SerializedProperty pseudoShadowCurveScale;
        private SerializedProperty windTintResponse;
        private SerializedProperty surfaceHeightTolerance;
        private SerializedProperty minimumUpwardNormal;
        private SerializedProperty surfaceFilterEdgeSoftness;
        private SerializedProperty previewInEditMode;

        private void OnEnable()
        {
            farFieldEnabled = serializedObject.FindProperty("farFieldEnabled");
            transitionStartDistance = serializedObject.FindProperty("transitionStartDistance");
            transitionEndDistance = serializedObject.FindProperty("transitionEndDistance");
            fadeOutStartDistance = serializedObject.FindProperty("fadeOutStartDistance");
            maximumDisplayDistance = serializedObject.FindProperty("maximumDisplayDistance");
            cacheResolution = serializedObject.FindProperty("cacheResolution");
            coverageRadius = serializedObject.FindProperty("coverageRadius");
            coverageHardness = serializedObject.FindProperty("coverageHardness");
            coverageHoleFillPixels = serializedObject.FindProperty("coverageHoleFillPixels");
            colorMultiplier = serializedObject.FindProperty("colorMultiplier");
            matchNearGrassColor = serializedObject.FindProperty("matchNearGrassColor");
            colorInfluence = serializedObject.FindProperty("colorInfluence");
            nearGrassLightingInfluence = serializedObject.FindProperty("nearGrassLightingInfluence");
            surfacePatternEnabled = serializedObject.FindProperty("surfacePatternEnabled");
            surfacePatternDirection = serializedObject.FindProperty("surfacePatternDirection");
            surfacePatternTint = serializedObject.FindProperty("surfacePatternTint");
            surfacePatternTintStrength = serializedObject.FindProperty("surfacePatternTintStrength");
            pseudoShadowStrength = serializedObject.FindProperty("pseudoShadowStrength");
            surfacePatternShadowColor = serializedObject.FindProperty("surfacePatternShadowColor");
            pseudoShadowDisturbance = serializedObject.FindProperty("pseudoShadowDisturbance");
            pseudoShadowPatchSize = serializedObject.FindProperty("pseudoShadowPatchSize");
            pseudoShadowDriftSpeed = serializedObject.FindProperty("pseudoShadowDriftSpeed");
            pseudoShadowWaveCurvature = serializedObject.FindProperty("pseudoShadowWaveCurvature");
            pseudoShadowWaveSpacing = serializedObject.FindProperty("pseudoShadowWaveSpacing");
            pseudoShadowCurveScale = serializedObject.FindProperty("pseudoShadowCurveScale");
            windTintResponse = serializedObject.FindProperty("windTintResponse");
            surfaceHeightTolerance = serializedObject.FindProperty("surfaceHeightTolerance");
            minimumUpwardNormal = serializedObject.FindProperty("minimumUpwardNormal");
            surfaceFilterEdgeSoftness = serializedObject.FindProperty("surfaceFilterEdgeSoftness");
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
                EditorGUILayout.PropertyField(
                    fadeOutStartDistance,
                    new GUIContent("最远渐隐开始距离", "超过该距离后，远景覆盖开始使用透明度平滑隐出。"));
                EditorGUILayout.PropertyField(
                    maximumDisplayDistance,
                    new GUIContent("最大显示距离", "到达该距离时远景颜色和地表纹理完全消失，不会无限绘制。"));
                EditorGUILayout.HelpBox(
                    "覆盖开始距离到覆盖完全显示距离之间会形成近景边缘渐变。想让前沿更柔和，就拉大这两个距离的间隔；想更干脆，就把它们调近。",
                    MessageType.Info);
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
                float edgeSoftness = 1f - coverageHardness.floatValue;
                float updatedEdgeSoftness = EditorGUILayout.Slider(
                    new GUIContent(
                        "覆盖轮廓柔和度",
                        "控制草场覆盖缓存外轮廓和株间孔洞边缘的渐隐宽度。数值越大，透明度过渡越宽；修改后需要重建覆盖缓存。"),
                    edgeSoftness,
                    0.05f,
                    1f);
                if (!Mathf.Approximately(edgeSoftness, updatedEdgeSoftness))
                {
                    coverageHardness.floatValue = 1f - updatedEdgeSoftness;
                }
                EditorGUILayout.PropertyField(
                    coverageHoleFillPixels,
                    new GUIContent(
                        "内部孔洞填充",
                        "对覆盖缓存进行闭合处理，填充草丛内部由株间距产生的小孔。0 关闭，通常使用 2-4；不会主动填满大面积道路或未铺草区域。"));
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("远景基础颜色", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    matchNearGrassColor,
                    new GUIContent(
                        "匹配近景草颜色",
                        "使用草材质的代表色、阴影色和接收阴影强度；覆盖边缘和 LOD 交接区域使用平滑透明度过渡。"));
                if (matchNearGrassColor.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        nearGrassLightingInfluence,
                        new GUIContent(
                            "近景光照匹配",
                            "控制远景覆盖应用近景草主光源与阴影颜色模型的比例。1 表示完整匹配，地形法线与草叶法线差异仍可能产生轻微偏差。"));
                    EditorGUILayout.HelpBox(
                        "匹配模式会自动使用完整基础颜色强度，并忽略手动覆盖颜色倍率。地表纹理仍使用下方的独立颜色配置。基础贴图包含复杂颜色图案时只能匹配代表色。",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.PropertyField(
                        colorMultiplier,
                        new GUIContent("覆盖颜色倍率", "对从草材质和实例颜色计算出的远景颜色进行整体调整。"));
                    EditorGUILayout.PropertyField(
                        colorInfluence,
                        new GUIContent("覆盖颜色强度", "控制远景颜色覆盖地表的透明度。低于 1 时会平滑露出更多地面。"));
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("地表纹理显示", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    surfacePatternEnabled,
                    new GUIContent("显示地表纹理", "单独控制远景覆盖上的颜色与明暗纹理，不影响真实草叶的风吹摆动。"));
                using (new EditorGUI.DisabledScope(!surfacePatternEnabled.boolValue))
                {
                    EditorGUILayout.PropertyField(
                        surfacePatternDirection,
                        new GUIContent("纹理移动方向", "只控制地表纹理的朝向和移动方向，不修改全局草场风向。"));
                    EditorGUILayout.PropertyField(
                        pseudoShadowDriftSpeed,
                        new GUIContent("纹理移动速度", "只控制地表纹理的移动速度。设为 0 时纹理固定。"));
                    EditorGUILayout.PropertyField(
                        surfacePatternTint,
                        new GUIContent("纹理亮部颜色", "地表纹理亮部使用的绝对目标颜色，不再与基础草色相乘。"));
                    EditorGUILayout.PropertyField(
                        surfacePatternTintStrength,
                        new GUIContent("纹理亮部颜色强度", "控制远景覆盖向纹理亮部目标颜色混合的比例。设为 1 时亮部可完整达到指定颜色。"));
                    EditorGUILayout.PropertyField(
                        windTintResponse,
                        new GUIContent("纹理颜色响应", "对纹理颜色强度进行额外缩放，不再读取真实草材质的风色响应。"));
                    EditorGUILayout.PropertyField(
                        pseudoShadowStrength,
                        new GUIContent("纹理阴影强度", "控制纹理暗部向指定阴影颜色混合的比例，不生成真实投影。设为 1 时暗部可完整达到指定颜色。"));
                    EditorGUILayout.PropertyField(
                        surfacePatternShadowColor,
                        new GUIContent(
                            "纹理阴影颜色",
                            "纹理暗部的目标颜色。该颜色不再与基础草色相乘；阴影强度为 1 时可直接得到指定颜色。"));
                    EditorGUILayout.PropertyField(
                        pseudoShadowWaveSpacing,
                        new GUIContent("纹理主尺度", "控制主要明暗变化之间的大致世界空间距离。"));
                    EditorGUILayout.PropertyField(
                        pseudoShadowDisturbance,
                        new GUIContent("纹理不规则度", "打散规则条带并形成断续、宽窄不同的纹理斑块。"));
                    EditorGUILayout.PropertyField(
                        pseudoShadowPatchSize,
                        new GUIContent("不规则斑块尺寸", "控制噪声斑块在世界空间中的大致尺寸。"));
                    EditorGUILayout.PropertyField(
                        pseudoShadowWaveCurvature,
                        new GUIContent("纹理弯曲强度", "控制主纹理沿横向发生弯曲和偏移的程度。"));
                    EditorGUILayout.PropertyField(
                        pseudoShadowCurveScale,
                        new GUIContent("纹理弯曲尺度", "控制弯曲变化横向延展的世界空间尺度。"));
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("地表限制", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    surfaceHeightTolerance,
                    new GUIContent("地表高度容差", "只覆盖与草根缓存高度接近的可见表面，避免影响上方遮挡物或下层地面。"));
                EditorGUILayout.PropertyField(
                    minimumUpwardNormal,
                    new GUIContent("最低向上法线", "过滤墙面和陡峭侧面。0.5 可排除约 60 度以上的陡面，数值越高，允许覆盖的坡面越平缓。"));
                EditorGUILayout.PropertyField(
                    surfaceFilterEdgeSoftness,
                    new GUIContent(
                        "地表过滤边缘柔和度",
                        "控制高度容差边界和斜坡法线边界的透明度过渡宽度。数值越大，边缘越柔和。"));
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
                "该效果在真实草之前绘制，只保留远景基础颜色和独立地表纹理，不会生成远距离草几何体或真实投影。需要在 URP Renderer 中启用 Anime Grass Renderer Feature。",
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
