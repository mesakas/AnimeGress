using Enlyn.Grass;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Enlyn.Grass.Editor
{
    [CustomEditor(typeof(AnimeGrassField))]
    public sealed class AnimeGrassFieldEditor : UnityEditor.Editor
    {
        private SerializedProperty prototypes;
        private SerializedProperty chunkSize;
        private SerializedProperty chunkBoundsPadding;
        private SerializedProperty frustumCulling;
        private SerializedProperty drawInEditMode;
        private SerializedProperty ignoreLodDistanceInEditMode;
        private SerializedProperty cameraOverride;
        private SerializedProperty renderingLayer;
        private SerializedProperty drawInstanceGizmos;
        private SerializedProperty gizmoDrawLimit;
        private SerializedProperty gizmoSize;
        private SerializedProperty gizmoColor;
        private ReorderableList prototypeList;

        private void OnEnable()
        {
            prototypes = serializedObject.FindProperty("prototypes");
            chunkSize = serializedObject.FindProperty("chunkSize");
            chunkBoundsPadding = serializedObject.FindProperty("chunkBoundsPadding");
            frustumCulling = serializedObject.FindProperty("frustumCulling");
            drawInEditMode = serializedObject.FindProperty("drawInEditMode");
            ignoreLodDistanceInEditMode = serializedObject.FindProperty("ignoreLodDistanceInEditMode");
            cameraOverride = serializedObject.FindProperty("cameraOverride");
            renderingLayer = serializedObject.FindProperty("renderingLayer");
            drawInstanceGizmos = serializedObject.FindProperty("drawInstanceGizmos");
            gizmoDrawLimit = serializedObject.FindProperty("gizmoDrawLimit");
            gizmoSize = serializedObject.FindProperty("gizmoSize");
            gizmoColor = serializedObject.FindProperty("gizmoColor");

            prototypeList = new ReorderableList(serializedObject, prototypes, true, true, true, true);
            prototypeList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "草类型列表");
            prototypeList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            prototypeList.drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty element = prototypes.GetArrayElementAtIndex(index);
                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, element, new GUIContent("草类型 " + index));
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            prototypeList.DoLayoutList();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("渲染配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(chunkSize, new GUIContent("分块大小", "草场按 XZ 平面分块。16-32 通常比较合适。"));
            EditorGUILayout.PropertyField(chunkBoundsPadding, new GUIContent("分块包围盒扩展", "草很高或风摆动幅度大时调大，避免视锥剔除过早。"));
            EditorGUILayout.PropertyField(frustumCulling, new GUIContent("启用视锥剔除", "关闭摄像机视野外的草块渲染。"));
            EditorGUILayout.PropertyField(drawInEditMode, new GUIContent("编辑模式显示", "在非运行状态下也渲染草，方便场景编辑。"));
            EditorGUILayout.PropertyField(ignoreLodDistanceInEditMode, new GUIContent("编辑模式忽略 LOD 距离", "只影响 Scene 视图编辑预览。开启后编辑时总是使用第一个可渲染 LOD，运行时仍按 LOD 距离显示。"));
            EditorGUILayout.PropertyField(
                cameraOverride,
                new GUIContent(
                    "游戏 LOD 参考摄像机",
                    "可选。为空时每个相机使用自身位置计算 LOD；指定后只作为游戏相机的 LOD 距离参考，不会限制哪些相机能够看到草。Scene 视图始终使用自己的摄像机。"));
            EditorGUILayout.PropertyField(renderingLayer, new GUIContent("渲染 Layer", "传给 Graphics.DrawMeshInstanced 的 layer。"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("定位与可视化", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(drawInstanceGizmos, new GUIContent("选中时显示草位置", "选中草场对象时，在 Scene 视图中用小圆点显示已铺设草的位置。"));
            EditorGUILayout.PropertyField(gizmoDrawLimit, new GUIContent("最多显示点数", "草很多时只抽样显示，避免 Scene 视图卡顿。"));
            EditorGUILayout.PropertyField(gizmoSize, new GUIContent("位置点大小"));
            EditorGUILayout.PropertyField(gizmoColor, new GUIContent("位置点颜色"));

            serializedObject.ApplyModifiedProperties();

            AnimeGrassField field = (AnimeGrassField)target;
            DrawPrototypeWarnings(field);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("运行时数据", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("已铺草实例数", field.InstanceCount.ToString());
            EditorGUILayout.LabelField("最近渲染相机", field.LastRenderCameraName);
            EditorGUILayout.LabelField("最近渲染帧", field.LastRenderFrame.ToString());
            EditorGUILayout.LabelField("进入视野分块数", field.LastVisibleChunkCount.ToString());
            EditorGUILayout.LabelField("检查实例数", field.LastEvaluatedInstanceCount.ToString());
            EditorGUILayout.LabelField("提交绘制实例数", field.LastQueuedInstanceCount.ToString());
            EditorGUILayout.LabelField("可渲染 LOD 检查数", field.LastRenderableLodCount.ToString());
            EditorGUILayout.LabelField("跳过：草类型无效", field.LastSkippedInvalidPrototypeCount.ToString());
            EditorGUILayout.LabelField("跳过：LOD/Mesh/材质缺失", field.LastSkippedMissingLodCount.ToString());
            EditorGUILayout.LabelField("跳过：距离淡出", field.LastSkippedDistanceCount.ToString());
            EditorGUILayout.LabelField("使用占位 Mesh 次数", field.LastFallbackMeshCount.ToString());
            if (field.TryGetInstancesBounds(out Bounds grassBounds))
            {
                EditorGUILayout.LabelField("草范围中心", grassBounds.center.ToString("F2"));
                EditorGUILayout.LabelField("草范围大小", grassBounds.size.ToString("F2"));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开铺草工具"))
                {
                    AnimeGrassPainterWindow.Open(field);
                }

                if (GUILayout.Button("聚焦全部草"))
                {
                    FocusGrassBounds(field);
                }

                if (GUILayout.Button("重建分块"))
                {
                    field.RebuildChunks();
                    EditorUtility.SetDirty(field);
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("打印诊断"))
                {
                    PrintDiagnostics(field);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加全局风场"))
                {
                    GameObject go = new GameObject("草全局风场");
                    Undo.RegisterCreatedObjectUndo(go, "创建草全局风场");
                    go.AddComponent<AnimeGrassWindZone>();
                    Selection.activeGameObject = go;
                }

                if (GUILayout.Button("清空草实例"))
                {
                    if (EditorUtility.DisplayDialog("清空草实例", "确定要删除这个草场里已经铺设的所有草吗？", "清空", "取消"))
                    {
                        Undo.RecordObject(field, "清空草实例");
                        field.ClearInstances();
                        EditorUtility.SetDirty(field);
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private static void DrawPrototypeWarnings(AnimeGrassField field)
        {
            if (field == null || field.Prototypes == null)
            {
                return;
            }

            for (int prototypeIndex = 0; prototypeIndex < field.Prototypes.Count; prototypeIndex++)
            {
                AnimeGrassPrototype prototype = field.Prototypes[prototypeIndex];
                if (prototype == null)
                {
                    EditorGUILayout.HelpBox("草类型 " + prototypeIndex + " 为空，这个类型的草不会显示。", MessageType.Warning);
                    continue;
                }

                AnimeGrassLod[] lods = prototype.Lods;
                if (lods == null || lods.Length == 0)
                {
                    EditorGUILayout.HelpBox(prototype.name + " 没有 LOD 配置，这个类型的草不会显示。", MessageType.Warning);
                    continue;
                }

                bool hasRenderableLod = false;
                for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                {
                    AnimeGrassLod lod = lods[lodIndex];
                    if (lod == null)
                    {
                        continue;
                    }

                    if (lod.mesh == null)
                    {
                        EditorGUILayout.HelpBox(prototype.name + " 的 LOD " + lodIndex + " 缺少 Mesh，草实例会被跳过。资源重新导入后请重新指定网格。", MessageType.Warning);
                    }

                    if (lod.material == null)
                    {
                        EditorGUILayout.HelpBox(prototype.name + " 的 LOD " + lodIndex + " 缺少材质，草实例会被跳过。", MessageType.Warning);
                    }

                    hasRenderableLod |= lod.IsRenderable;
                }

                if (!hasRenderableLod)
                {
                    EditorGUILayout.HelpBox(prototype.name + " 没有任何可渲染 LOD；草场里即使有实例也不会显示。", MessageType.Error);
                }
                else
                {
                    float maxRenderDistance = prototype.GetMaxRenderDistance();
                    if (maxRenderDistance > 0f && maxRenderDistance < 80f)
                    {
                        EditorGUILayout.HelpBox(prototype.name + " 的最大显示距离只有 " + maxRenderDistance.ToString("0.##") + "。Scene 视图相机位置可能比画面看起来更远，距离太小时会像没有草一样全部淡出。", MessageType.Info);
                    }
                }
            }
        }

        private static void FocusGrassBounds(AnimeGrassField field)
        {
            if (field == null || !field.TryGetInstancesBounds(out Bounds bounds))
            {
                EditorUtility.DisplayDialog("找不到草", "这个草场还没有已保存的草实例。", "确定");
                return;
            }

            Selection.activeGameObject = field.gameObject;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                EditorUtility.DisplayDialog("找不到 Scene 视图", "请先打开一个 Scene 视图。", "确定");
                return;
            }

            sceneView.Frame(bounds, false);
            sceneView.Repaint();
        }

        private static void PrintDiagnostics(AnimeGrassField field)
        {
            if (field == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[AnimeGress] 草场诊断: " + field.name);
            builder.AppendLine("实例数: " + field.InstanceCount);
            builder.AppendLine("最近渲染相机: " + field.LastRenderCameraName);
            builder.AppendLine("最近渲染帧: " + field.LastRenderFrame);
            builder.AppendLine("进入视野分块数: " + field.LastVisibleChunkCount);
            builder.AppendLine("检查实例数: " + field.LastEvaluatedInstanceCount);
            builder.AppendLine("提交绘制实例数: " + field.LastQueuedInstanceCount);
            builder.AppendLine("跳过无效草类型: " + field.LastSkippedInvalidPrototypeCount);
            builder.AppendLine("跳过缺失 LOD/Mesh/材质: " + field.LastSkippedMissingLodCount);
            builder.AppendLine("跳过距离淡出: " + field.LastSkippedDistanceCount);
            builder.AppendLine("使用占位 Mesh 次数: " + field.LastFallbackMeshCount);

            if (field.TryGetInstancesBounds(out Bounds bounds))
            {
                builder.AppendLine("草范围中心: " + bounds.center.ToString("F3"));
                builder.AppendLine("草范围大小: " + bounds.size.ToString("F3"));
            }

            for (int prototypeIndex = 0; prototypeIndex < field.Prototypes.Count; prototypeIndex++)
            {
                AnimeGrassPrototype prototype = field.Prototypes[prototypeIndex];
                builder.AppendLine("草类型 " + prototypeIndex + ": " + (prototype != null ? prototype.name : "空"));
                if (prototype == null)
                {
                    continue;
                }

                AnimeGrassLod[] lods = prototype.Lods;
                builder.AppendLine("  最大显示距离: " + prototype.GetMaxRenderDistance());
                if (lods == null)
                {
                    builder.AppendLine("  LOD: 空");
                    continue;
                }

                for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                {
                    AnimeGrassLod lod = lods[lodIndex];
                    if (lod == null)
                    {
                        builder.AppendLine("  LOD " + lodIndex + ": 空");
                        continue;
                    }

                    Mesh mesh = lod.mesh;
                    Material material = lod.material;
                    builder.AppendLine("  LOD " + lodIndex
                        + " Mesh=" + (mesh != null ? mesh.name + " v=" + mesh.vertexCount + " sub=" + mesh.subMeshCount : "空")
                        + " Material=" + (material != null ? material.name : "空")
                        + " Shader=" + (material != null && material.shader != null ? material.shader.name : "空")
                        + " Instancing=" + (material != null && material.enableInstancing)
                        + " Start=" + lod.startDistance
                        + " End=" + lod.endDistance
                        + " Fade=" + lod.fadeDistance);
                }
            }

            Debug.Log(builder.ToString(), field);
        }
    }

    public static class AnimeGrassEditorMenus
    {
        [MenuItem("GameObject/AnimeGress/二次元草场", false, 10)]
        private static void CreateGrassField(MenuCommand command)
        {
            GameObject go = new GameObject("二次元草场");
            Undo.RegisterCreatedObjectUndo(go, "创建二次元草场");
            go.AddComponent<AnimeGrassField>();

            if (command.context is GameObject parent)
            {
                go.transform.SetParent(parent.transform, false);
            }

            Selection.activeGameObject = go;
        }

        [MenuItem("Window/AnimeGress/草场铺设工具")]
        private static void OpenPainter()
        {
            AnimeGrassPainterWindow.Open(null);
        }
    }
}
