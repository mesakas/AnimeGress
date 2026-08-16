using Enlyn.Grass;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Enlyn.Grass.Editor
{
    [CustomEditor(typeof(AnimeGrassPrototype))]
    public sealed class AnimeGrassPrototypeEditor : UnityEditor.Editor
    {
        private const int LodElementBaseRowCount = 15;
        private const int SeparateAxisRowCount = 3;
        private const float LodElementPadding = 8f;
        private static readonly string[] LodDistanceModeNames =
        {
            "三维距离（仅距离控制）",
            "XY 距离 + Z 轴距离",
            "仅 XY 距离（忽略 Z）",
            "仅水平 XZ 距离（忽略高度 Y）"
        };

        private SerializedProperty lods;
        private SerializedProperty lodDistanceMode;
        private SerializedProperty replaceDistantLodsWithFarField;
        private SerializedProperty lastMeshLodIndex;
        private SerializedProperty windWeight;
        private SerializedProperty defaultInstanceColor;
        private SerializedProperty distanceDensityEnabled;
        private SerializedProperty nearDistanceDensity;
        private SerializedProperty farDistanceDensity;
        private SerializedProperty densityTransitionStartDistance;
        private SerializedProperty densityTransitionEndDistance;
        private SerializedProperty densityTransitionSoftness;
        private SerializedProperty densityRandomSeed;
        private SerializedProperty modelPositionOffset;
        private SerializedProperty modelRotationOffset;
        private SerializedProperty modelScale;
        private ReorderableList lodList;

        private void OnEnable()
        {
            lods = serializedObject.FindProperty("lods");
            lodDistanceMode = serializedObject.FindProperty("lodDistanceMode");
            replaceDistantLodsWithFarField = serializedObject.FindProperty("replaceDistantLodsWithFarField");
            lastMeshLodIndex = serializedObject.FindProperty("lastMeshLodIndex");
            windWeight = serializedObject.FindProperty("windWeight");
            defaultInstanceColor = serializedObject.FindProperty("defaultInstanceColor");
            distanceDensityEnabled = serializedObject.FindProperty("distanceDensityEnabled");
            nearDistanceDensity = serializedObject.FindProperty("nearDistanceDensity");
            farDistanceDensity = serializedObject.FindProperty("farDistanceDensity");
            densityTransitionStartDistance = serializedObject.FindProperty("densityTransitionStartDistance");
            densityTransitionEndDistance = serializedObject.FindProperty("densityTransitionEndDistance");
            densityTransitionSoftness = serializedObject.FindProperty("densityTransitionSoftness");
            densityRandomSeed = serializedObject.FindProperty("densityRandomSeed");
            modelPositionOffset = serializedObject.FindProperty("modelPositionOffset");
            modelRotationOffset = serializedObject.FindProperty("modelRotationOffset");
            modelScale = serializedObject.FindProperty("modelScale");

            lodList = new ReorderableList(serializedObject, lods, true, true, true, true);
            lodList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "LOD 配置");
            lodList.elementHeight = GetLodElementHeight();
            lodList.elementHeightCallback = _ => GetLodElementHeight();
            lodList.drawElementCallback = DrawLodElement;
        }

        private float GetLodElementHeight()
        {
            int rowCount = LodElementBaseRowCount
                + (UsesSeparateAxisDistances() ? SeparateAxisRowCount : 0);
            float rowsHeight = EditorGUIUtility.singleLineHeight * rowCount;
            float spacingHeight = EditorGUIUtility.standardVerticalSpacing * (rowCount - 1);
            return rowsHeight + spacingHeight + LodElementPadding * 2f;
        }

        private bool UsesSeparateAxisDistances()
        {
            return lodDistanceMode != null
                && lodDistanceMode.enumValueIndex
                == (int)AnimeGrassLodDistanceMode.SeparateXYAndZ;
        }

        private bool UsesXyDistance()
        {
            if (lodDistanceMode == null)
            {
                return false;
            }

            int mode = lodDistanceMode.enumValueIndex;
            return mode == (int)AnimeGrassLodDistanceMode.SeparateXYAndZ
                || mode == (int)AnimeGrassLodDistanceMode.XYDistanceOnly;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("草类型基础配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(defaultInstanceColor, new GUIContent("默认实例颜色", "铺设时会和笔刷颜色相乘。"));
            EditorGUILayout.PropertyField(windWeight, new GUIContent("整体受风权重", "这个草类型整体受风影响的倍率。0 表示不受风。"));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("模型校正", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(modelPositionOffset, new GUIContent("位置偏移", "模型网格相对每个铺设点的位置偏移。"));
            EditorGUILayout.PropertyField(modelRotationOffset, new GUIContent("旋转偏移", "用于修正 FBX 或模型自身的局部轴向。"));
            EditorGUILayout.PropertyField(modelScale, new GUIContent("基础缩放", "用于修正模型单位和草类型的基础尺寸，不影响笔刷随机缩放。"));
            if (GUILayout.Button("从 FBX 自动读取校正"))
            {
                ApplyImportedModelCorrection();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("距离密度", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                distanceDensityEnabled,
                new GUIContent("启用距离密度", "在 LOD 之外，根据相机距离确定性地减少实际提交绘制的草实例数量。"));
            if (distanceDensityEnabled.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    nearDistanceDensity,
                    new GUIContent("近处草数量比例", "密度过渡开始距离以内保留的草实例比例。1 表示全部显示。"));
                EditorGUILayout.PropertyField(
                    farDistanceDensity,
                    new GUIContent("远处草数量比例", "密度过渡结束距离以外保留的草实例比例。0.35 表示约保留 35%。"));
                EditorGUILayout.PropertyField(
                    densityTransitionStartDistance,
                    new GUIContent("密度过渡开始距离", "从该相机距离开始由近处数量逐渐过渡到远处数量。"));
                EditorGUILayout.PropertyField(
                    densityTransitionEndDistance,
                    new GUIContent("密度过渡结束距离", "到达该距离时使用完整的远处草数量比例。"));
                EditorGUILayout.PropertyField(
                    densityTransitionSoftness,
                    new GUIContent("单株切换柔和度", "控制每株草跨过随机密度阈值时的点状渐隐宽度。通常使用 0.05-0.12。"));
                EditorGUILayout.PropertyField(
                    densityRandomSeed,
                    new GUIContent("密度随机种子", "改变被保留草株的确定性随机分布，不会修改铺设数据。"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("LOD 距离", EditorStyles.boldLabel);
            lodDistanceMode.enumValueIndex = EditorGUILayout.Popup(
                new GUIContent(
                    "距离计算模式",
                    "三维距离使用相机到草株的直线距离；XY 与 Z 分离模式由两组距离中更远的一组决定 LOD；仅 XY 忽略 Z；仅水平 XZ 忽略 Unity 的 Y 高度。"),
                lodDistanceMode.enumValueIndex,
                LodDistanceModeNames);
            string distanceModeHelp;
            if (UsesSeparateAxisDistances())
            {
                distanceModeHelp = "当前使用字面世界 XY 平面距离和世界 Z 轴距离分别控制 LOD：XY = sqrt(dx² + dy²)，Z = abs(dz)。任意一组进入更远级别时，都会切换到对应的低细节 LOD。";
            }
            else if (lodDistanceMode.enumValueIndex == (int)AnimeGrassLodDistanceMode.XYDistanceOnly)
            {
                distanceModeHelp = "当前仅使用世界 XY 平面距离控制 LOD：XY = sqrt(dx² + dy²)。相机沿 Z 轴升高不会切换到面片 LOD，也不会因为高度而隐藏真实草。";
            }
            else if (lodDistanceMode.enumValueIndex == (int)AnimeGrassLodDistanceMode.XZDistanceOnly)
            {
                distanceModeHelp = "当前仅使用 Unity 水平 XZ 平面距离控制 LOD：XZ = sqrt(dx² + dz²)。相机沿 Y 轴升高不会切换到面片 LOD，适合当前这种高空俯视场景。";
            }
            else
            {
                distanceModeHelp = "当前仅使用相机到草株的三维直线距离控制 LOD。";
            }
            EditorGUILayout.HelpBox(
                distanceModeHelp,
                MessageType.None);

            EditorGUILayout.PropertyField(
                replaceDistantLodsWithFarField,
                new GUIContent(
                    "远景草替换后续 LOD",
                    "启用后，只渲染到指定的最后实体 LOD；后续面片 LOD 不再提交，最后实体 LOD 会在自身结束距离内渐隐，由远景草覆盖接管。"));
            if (replaceDistantLodsWithFarField.boolValue)
            {
                int maxLodIndex = Mathf.Max(0, lods.arraySize - 1);
                lastMeshLodIndex.intValue = EditorGUILayout.IntSlider(
                    new GUIContent(
                        "最后实体 LOD",
                        "该索引之后的 LOD 保留配置但不参与渲染。选择 LOD 0 可跳过后续面片草。"),
                    Mathf.Clamp(lastMeshLodIndex.intValue, 0, maxLodIndex),
                    0,
                    maxLodIndex);
                EditorGUILayout.HelpBox(
                    "请在远景草覆盖组件中使用相同的距离模式，并点击“匹配最后一级 LOD 渐隐距离”，避免真实草与远景覆盖之间出现空带。",
                    MessageType.Info);
            }

            EditorGUILayout.Space(8f);
            lodList.DoLayoutList();
            DrawLodWarnings();

            EditorGUILayout.HelpBox("相邻 LOD 会自动连接距离，并在“渐隐距离”内同时提交前后两级，使用互补的随机点状剔除完成平滑替换。距离密度单独控制实际显示的草株数量。", MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLodWarnings()
        {
            if (lods == null || lods.arraySize == 0)
            {
                EditorGUILayout.HelpBox("没有 LOD 配置，这个草类型不会显示。", MessageType.Error);
                return;
            }

            bool hasRenderableLod = false;
            int lastActiveLodIndex = replaceDistantLodsWithFarField.boolValue
                ? Mathf.Clamp(lastMeshLodIndex.intValue, 0, lods.arraySize - 1)
                : lods.arraySize - 1;
            for (int i = 0; i < lods.arraySize; i++)
            {
                SerializedProperty lod = lods.GetArrayElementAtIndex(i);
                SerializedProperty mesh = lod.FindPropertyRelative("mesh");
                SerializedProperty material = lod.FindPropertyRelative("material");
                SerializedProperty endDistance = lod.FindPropertyRelative("endDistance");

                bool isActiveLod = i <= lastActiveLodIndex;
                if (isActiveLod && mesh.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("LOD " + i + " 缺少 Mesh。资源删除或重新导入后，需要重新指定 FBX 里的 Mesh 子资源。", MessageType.Warning);
                }

                if (isActiveLod && material.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("LOD " + i + " 缺少材质。", MessageType.Warning);
                }

                if (isActiveLod && i < lastActiveLodIndex && endDistance.floatValue <= 0f)
                {
                    EditorGUILayout.HelpBox(
                        "LOD " + i + " 的结束距离为 0，后续 LOD 无法获得有效的自动切换距离。",
                        MessageType.Warning);
                }

                hasRenderableLod |= isActiveLod
                    && mesh.objectReferenceValue != null
                    && material.objectReferenceValue != null;
            }

            if (!hasRenderableLod)
            {
                EditorGUILayout.HelpBox("没有任何可渲染 LOD；草场里即使有实例也不会显示。", MessageType.Error);
            }
        }

        private void DrawLodElement(Rect rect, int index, bool active, bool focused)
        {
            SerializedProperty lod = lods.GetArrayElementAtIndex(index);
            SerializedProperty mesh = lod.FindPropertyRelative("mesh");
            SerializedProperty material = lod.FindPropertyRelative("material");
            SerializedProperty subMeshIndex = lod.FindPropertyRelative("subMeshIndex");
            SerializedProperty startDistance = lod.FindPropertyRelative("startDistance");
            SerializedProperty endDistance = lod.FindPropertyRelative("endDistance");
            SerializedProperty fadeDistance = lod.FindPropertyRelative("fadeDistance");
            SerializedProperty zStartDistance = lod.FindPropertyRelative("zStartDistance");
            SerializedProperty zEndDistance = lod.FindPropertyRelative("zEndDistance");
            SerializedProperty zFadeDistance = lod.FindPropertyRelative("zFadeDistance");
            SerializedProperty faceTarget = lod.FindPropertyRelative("faceTarget");
            SerializedProperty faceTargetRotationOffset = lod.FindPropertyRelative("faceTargetRotationOffset");
            SerializedProperty overheadBend = lod.FindPropertyRelative("overheadBend");
            SerializedProperty overheadBendAngle = lod.FindPropertyRelative("overheadBendAngle");
            SerializedProperty overheadBendStartAngle = lod.FindPropertyRelative("overheadBendStartAngle");
            SerializedProperty overheadBendEndAngle = lod.FindPropertyRelative("overheadBendEndAngle");
            SerializedProperty shadowCasting = lod.FindPropertyRelative("shadowCasting");
            SerializedProperty receiveShadows = lod.FindPropertyRelative("receiveShadows");

            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            bool separateAxisDistances = UsesSeparateAxisDistances();
            bool xyDistance = UsesXyDistance();
            bool xzDistance = lodDistanceMode.enumValueIndex
                == (int)AnimeGrassLodDistanceMode.XZDistanceOnly;
            bool replacedByFarField = replaceDistantLodsWithFarField.boolValue
                && index > lastMeshLodIndex.intValue;
            float y = rect.y + LodElementPadding;
            Rect lineRect = new Rect(rect.x, y, rect.width, line);
            EditorGUI.LabelField(
                lineRect,
                "LOD " + index + (replacedByFarField ? "（由远景草替换）" : string.Empty),
                EditorStyles.boldLabel);

            y += line + spacing;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, mesh, new GUIContent("网格 Mesh", "可以是单面片、交叉面片，也可以是完整模型草。"));

            y += line + spacing;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, material, new GUIContent("材质", "建议使用支持实例化和 _InstanceFade 的草材质。"));

            y += line + spacing;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, subMeshIndex, new GUIContent("子网格索引"));

            y += line + spacing;
            lineRect.y = y;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(
                    lineRect,
                    startDistance,
                    new GUIContent(
                        xyDistance || xzDistance
                            ? (index > 0
                                ? (xzDistance ? "XZ 开始距离（自动）" : "XY 开始距离（自动）")
                                : (xzDistance ? "XZ 开始距离（固定）" : "XY 开始距离（固定）"))
                            : (index > 0 ? "开始距离（自动）" : "开始距离（固定）"),
                        index > 0
                            ? "自动等于上一级 LOD 的结束距离，以保证两级的点状过渡连续。"
                            : "第一级 LOD 固定从 0 距离开始。"));
            }

            y += line + spacing;
            lineRect.y = y;
            EditorGUI.PropertyField(
                lineRect,
                endDistance,
                new GUIContent(
                    xyDistance || xzDistance
                        ? (xzDistance ? "XZ 结束距离" : "XY 结束距离")
                        : "结束距离",
                    xyDistance || xzDistance
                        ? (xzDistance
                            ? "该 LOD 在 Unity 水平 XZ 平面距离上的结束值。0 表示无上限。"
                            : "该 LOD 在世界 XY 平面距离上的结束值。0 表示无上限。")
                        : "该 LOD 结束显示的三维直线距离。最后一个 LOD 的结束距离就是最大显示距离。0 表示无上限。"));

            y += line + spacing;
            lineRect.y = y;
            EditorGUI.PropertyField(
                lineRect,
                fadeDistance,
                new GUIContent(
                    xyDistance || xzDistance
                        ? (xzDistance ? "XZ 渐隐距离" : "XY 渐隐距离")
                        : "渐隐距离",
                    "控制从该 LOD 到下一级的互补随机点状过渡；最后一级也用该距离渐隐到最大显示距离。"));

            if (separateAxisDistances)
            {
                y += line + spacing;
                lineRect.y = y;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.PropertyField(
                        lineRect,
                        zStartDistance,
                        new GUIContent(
                            index > 0 ? "Z 开始距离（自动）" : "Z 开始距离（固定）",
                            "使用相机与草株在世界 Z 轴上的绝对距离，自动连接上一级的 Z 结束距离。"));
                }

                y += line + spacing;
                lineRect.y = y;
                EditorGUI.PropertyField(
                    lineRect,
                    zEndDistance,
                    new GUIContent("Z 结束距离", "该 LOD 在世界 Z 轴绝对距离上的结束值。0 表示无上限。"));

                y += line + spacing;
                lineRect.y = y;
                EditorGUI.PropertyField(
                    lineRect,
                    zFadeDistance,
                    new GUIContent("Z 渐隐距离", "沿世界 Z 轴切换到下一级 LOD 时使用的互补点状过渡距离。"));
            }

            y += line + spacing;
            lineRect.y = y;
            EditorGUI.PropertyField(
                lineRect,
                faceTarget,
                new GUIContent(
                    "始终面向观察目标",
                    "绕草根法线朝向角色目标或当前摄像机。适合远距离面片草，不建议对完整模型草开启。"));

            y += line + spacing;
            lineRect.y = y;
            using (new EditorGUI.DisabledScope(!faceTarget.boolValue))
            {
                EditorGUI.PropertyField(
                    lineRect,
                    faceTargetRotationOffset,
                    new GUIContent(
                        "面向旋转偏移",
                        "绕草根法线追加的角度。面片方向反了可先尝试 180，侧向错误可尝试 90 或 -90。"));
            }

            y += line + spacing;
            lineRect.y = y;
            EditorGUI.PropertyField(
                lineRect,
                overheadBend,
                new GUIContent(
                    "启用俯视弯曲",
                    "相机升到草场上方时弯曲草叶上部，减少远处面片草的硬侧边。"));

            y += line + spacing;
            lineRect.y = y;
            using (new EditorGUI.DisabledScope(!overheadBend.boolValue))
            {
                EditorGUI.PropertyField(
                    lineRect,
                    overheadBendAngle,
                    new GUIContent("最大弯曲角", "俯视达到完全生效角度时，草叶顶部弯曲的角度。"));
            }

            y += line + spacing;
            lineRect.y = y;
            using (new EditorGUI.DisabledScope(!overheadBend.boolValue))
            {
                EditorGUI.PropertyField(
                    lineRect,
                    overheadBendStartAngle,
                    new GUIContent("开始俯视角", "相机仰角达到该值后开始弯曲。"));
            }

            y += line + spacing;
            lineRect.y = y;
            using (new EditorGUI.DisabledScope(!overheadBend.boolValue))
            {
                EditorGUI.PropertyField(
                    lineRect,
                    overheadBendEndAngle,
                    new GUIContent("完全生效角", "相机仰角达到该值时使用完整弯曲角度。"));
            }

            y += line + spacing;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, shadowCasting, new GUIContent("投射阴影"));

            y += line + spacing;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, receiveShadows, new GUIContent("接收阴影"));
        }

        private void ApplyImportedModelCorrection()
        {
            AnimeGrassPrototype prototype = (AnimeGrassPrototype)target;
            AnimeGrassLod[] prototypeLods = prototype.Lods;
            if (prototypeLods == null)
            {
                EditorUtility.DisplayDialog("无法读取模型校正", "草类型没有 LOD 配置。", "确定");
                return;
            }

            for (int lodIndex = 0; lodIndex < prototypeLods.Length; lodIndex++)
            {
                Mesh mesh = prototypeLods[lodIndex]?.mesh;
                if (mesh == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(mesh);
                GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (modelRoot == null)
                {
                    continue;
                }

                MeshFilter[] filters = modelRoot.GetComponentsInChildren<MeshFilter>(true);
                for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
                {
                    MeshFilter filter = filters[filterIndex];
                    if (filter.sharedMesh != mesh)
                    {
                        continue;
                    }

                    Matrix4x4 importedMatrix = filter.transform.localToWorldMatrix;
                    Undo.RecordObject(prototype, "读取草模型校正");
                    modelPositionOffset.vector3Value = importedMatrix.GetColumn(3);
                    modelRotationOffset.vector3Value = importedMatrix.rotation.eulerAngles;
                    modelScale.vector3Value = importedMatrix.lossyScale;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prototype);
                    return;
                }
            }

            EditorUtility.DisplayDialog(
                "无法读取模型校正",
                "LOD Mesh 不是模型文件中的 Mesh 子资源，或没有找到对应的 MeshFilter。可手动填写位置、旋转和基础缩放。",
                "确定");
        }
    }
}
