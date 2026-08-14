using Enlyn.Grass;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Enlyn.Grass.Editor
{
    [CustomEditor(typeof(AnimeGrassPrototype))]
    public sealed class AnimeGrassPrototypeEditor : UnityEditor.Editor
    {
        private SerializedProperty lods;
        private SerializedProperty windWeight;
        private SerializedProperty defaultInstanceColor;
        private SerializedProperty modelPositionOffset;
        private SerializedProperty modelRotationOffset;
        private SerializedProperty modelScale;
        private ReorderableList lodList;

        private void OnEnable()
        {
            lods = serializedObject.FindProperty("lods");
            windWeight = serializedObject.FindProperty("windWeight");
            defaultInstanceColor = serializedObject.FindProperty("defaultInstanceColor");
            modelPositionOffset = serializedObject.FindProperty("modelPositionOffset");
            modelRotationOffset = serializedObject.FindProperty("modelRotationOffset");
            modelScale = serializedObject.FindProperty("modelScale");

            lodList = new ReorderableList(serializedObject, lods, true, true, true, true);
            lodList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "LOD 配置");
            lodList.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 8f + 26f;
            lodList.drawElementCallback = DrawLodElement;
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
            lodList.DoLayoutList();
            DrawLodWarnings();

            EditorGUILayout.HelpBox("显示距离由每个 LOD 的“开始距离 / 结束距离”控制；“渐隐距离”会使用点状剔除做平滑显隐，不使用透明混合。", MessageType.None);

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
            for (int i = 0; i < lods.arraySize; i++)
            {
                SerializedProperty lod = lods.GetArrayElementAtIndex(i);
                SerializedProperty mesh = lod.FindPropertyRelative("mesh");
                SerializedProperty material = lod.FindPropertyRelative("material");

                if (mesh.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("LOD " + i + " 缺少 Mesh。资源删除或重新导入后，需要重新指定 FBX 里的 Mesh 子资源。", MessageType.Warning);
                }

                if (material.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("LOD " + i + " 缺少材质。", MessageType.Warning);
                }

                hasRenderableLod |= mesh.objectReferenceValue != null && material.objectReferenceValue != null;
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
            SerializedProperty shadowCasting = lod.FindPropertyRelative("shadowCasting");
            SerializedProperty receiveShadows = lod.FindPropertyRelative("receiveShadows");

            float line = EditorGUIUtility.singleLineHeight;
            float y = rect.y + 4f;
            Rect lineRect = new Rect(rect.x, y, rect.width, line);
            EditorGUI.LabelField(lineRect, "LOD " + index, EditorStyles.boldLabel);

            y += line + 2f;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, mesh, new GUIContent("网格 Mesh", "可以是单面片、交叉面片，也可以是完整模型草。"));

            y += line + 2f;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, material, new GUIContent("材质", "建议使用支持实例化和 _InstanceFade 的草材质。"));

            y += line + 2f;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, subMeshIndex, new GUIContent("子网格索引"));

            y += line + 2f;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, startDistance, new GUIContent("开始距离", "该 LOD 完全显示的距离点。非首个 LOD 会在开始距离之前渐显。"));

            y += line + 2f;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, endDistance, new GUIContent("结束距离", "该 LOD 结束显示的距离。最后一个 LOD 的结束距离就是最大显示距离。0 表示无上限。"));

            y += line + 2f;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, fadeDistance, new GUIContent("渐隐距离", "在 LOD 切换和最大显示距离处使用点状剔除渐变。"));

            y += line + 2f;
            lineRect.y = y;
            EditorGUI.PropertyField(lineRect, shadowCasting, new GUIContent("投射阴影"));

            y += line + 2f;
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
