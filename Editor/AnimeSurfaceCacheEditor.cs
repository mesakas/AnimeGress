using Enlyn.Grass;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Enlyn.Grass.Editor
{
    [InitializeOnLoad]
    internal static class AnimeSurfaceCacheEditorRefresh
    {
        static AnimeSurfaceCacheEditorRefresh()
        {
            EditorApplication.projectChanged -= AnimeSurfaceCache.RequestAllRefresh;
            EditorApplication.projectChanged += AnimeSurfaceCache.RequestAllRefresh;
            EditorApplication.hierarchyChanged -= AnimeSurfaceCache.RequestAllRefresh;
            EditorApplication.hierarchyChanged += AnimeSurfaceCache.RequestAllRefresh;
        }
    }

    [InitializeOnLoad]
    internal static class AnimeSurfaceCacheSceneOverlay
    {
        static AnimeSurfaceCacheSceneOverlay()
        {
            SceneView.duringSceneGui -= DrawVisibleCacheBounds;
            SceneView.duringSceneGui += DrawVisibleCacheBounds;
        }

        private static void DrawVisibleCacheBounds(SceneView sceneView)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var caches = AnimeSurfaceCache.ActiveCaches;
            for (int cacheIndex = 0; cacheIndex < caches.Count; cacheIndex++)
            {
                AnimeSurfaceCache cache = caches[cacheIndex];
                bool isSelected = cache != null && Selection.Contains(cache.gameObject);
                if (cache == null
                    || !cache.isActiveAndEnabled
                    || !cache.DrawBounds
                    || (!isSelected && !cache.ShowBoundsWhenUnselected))
                {
                    continue;
                }

                DrawCacheBounds(cache, isSelected);
            }
        }

        private static void DrawCacheBounds(AnimeSurfaceCache cache, bool isSelected)
        {
            Bounds bounds = cache.WorldBounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] bottom =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z)
            };
            Vector3[] top =
            {
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z)
            };
            Vector3[] footprint =
            {
                new Vector3(min.x, bounds.center.y, min.z),
                new Vector3(max.x, bounds.center.y, min.z),
                new Vector3(max.x, bounds.center.y, max.z),
                new Vector3(min.x, bounds.center.y, max.z)
            };

            Color color = cache.BoundsColor;
            color.a = Mathf.Max(0.35f, color.a);
            Color fillColor = color;
            fillColor.a = isSelected ? 0.045f : 0f;

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.DrawSolidRectangleWithOutline(footprint, fillColor, color);
            Handles.color = color;
            DrawLoop(bottom, isSelected ? 3f : 2f);
            DrawLoop(top, isSelected ? 3f : 2f);
            for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
            {
                Handles.DrawAAPolyLine(
                    isSelected ? 3f : 2f,
                    bottom[cornerIndex],
                    top[cornerIndex]);
            }

            if (isSelected)
            {
                float texelX = cache.WorldSize.x / Mathf.Max(1, cache.Resolution);
                float texelZ = cache.WorldSize.y / Mathf.Max(1, cache.Resolution);
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                labelStyle.normal.textColor = color;
                Handles.Label(
                    top[0],
                    $"地表缓存  {cache.WorldSize.x:0.#} x {cache.WorldSize.y:0.#} m  "
                    + $"Y {bounds.min.y:0.#} 至 {bounds.max.y:0.#} m  "
                    + $"{texelX:0.###} / {texelZ:0.###} m/像素",
                    labelStyle);
            }

            Handles.zTest = previousZTest;
        }

        private static void DrawLoop(Vector3[] corners, float width)
        {
            Handles.DrawAAPolyLine(
                width,
                corners[0],
                corners[1],
                corners[2],
                corners[3],
                corners[0]);
        }
    }

    [CustomEditor(typeof(AnimeSurfaceCache))]
    public sealed class AnimeSurfaceCacheEditor : UnityEditor.Editor
    {
        private static readonly string[] UpdateModeNames =
        {
            "变化时更新",
            "定时更新",
            "每帧更新",
            "仅手动更新"
        };

        private SerializedProperty worldSize;
        private SerializedProperty captureHeight;
        private SerializedProperty resolution;
        private SerializedProperty surfaceLayers;
        private SerializedProperty automaticRendererCollection;
        private SerializedProperty explicitRenderers;
        private SerializedProperty captureUnityTerrains;
        private SerializedProperty explicitTerrains;
        private SerializedProperty includeInactiveRenderers;
        private SerializedProperty updateMode;
        private SerializedProperty updateInterval;
        private SerializedProperty updateInEditMode;
        private SerializedProperty followTarget;
        private SerializedProperty followSnapInTexels;
        private SerializedProperty priority;
        private SerializedProperty emptySurfaceColor;
        private SerializedProperty drawBounds;
        private SerializedProperty showBoundsWhenUnselected;
        private SerializedProperty boundsColor;
        private bool showTextures;

        private void OnEnable()
        {
            worldSize = serializedObject.FindProperty("worldSize");
            captureHeight = serializedObject.FindProperty("captureHeight");
            resolution = serializedObject.FindProperty("resolution");
            surfaceLayers = serializedObject.FindProperty("surfaceLayers");
            automaticRendererCollection = serializedObject.FindProperty("automaticRendererCollection");
            explicitRenderers = serializedObject.FindProperty("explicitRenderers");
            captureUnityTerrains = serializedObject.FindProperty("captureUnityTerrains");
            explicitTerrains = serializedObject.FindProperty("explicitTerrains");
            includeInactiveRenderers = serializedObject.FindProperty("includeInactiveRenderers");
            updateMode = serializedObject.FindProperty("updateMode");
            updateInterval = serializedObject.FindProperty("updateInterval");
            updateInEditMode = serializedObject.FindProperty("updateInEditMode");
            followTarget = serializedObject.FindProperty("followTarget");
            followSnapInTexels = serializedObject.FindProperty("followSnapInTexels");
            priority = serializedObject.FindProperty("priority");
            emptySurfaceColor = serializedObject.FindProperty("emptySurfaceColor");
            drawBounds = serializedObject.FindProperty("drawBounds");
            showBoundsWhenUnselected = serializedObject.FindProperty("showBoundsWhenUnselected");
            boundsColor = serializedObject.FindProperty("boundsColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("缓存范围与质量", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(worldSize, new GUIContent("世界范围 XZ", "缓存覆盖的世界空间宽度和长度。"));
            EditorGUILayout.PropertyField(captureHeight, new GUIContent("垂直捕获高度", "以组件 Y 坐标为中心，上下各覆盖一半高度。"));
            EditorGUILayout.PropertyField(resolution, new GUIContent("缓存分辨率", "会自动取最接近的 2 次幂，分辨率越高显存和更新时间越高。"));
            EditorGUILayout.PropertyField(priority, new GUIContent("缓存优先级", "存在多个缓存时优先使用数值较高的缓存。"));
            EditorGUILayout.PropertyField(emptySurfaceColor, new GUIContent("空白区域颜色"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("捕获来源", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(surfaceLayers, new GUIContent("地表 Layer", "只有这些 Layer 上的 Renderer 会写入缓存。"));
            EditorGUILayout.PropertyField(automaticRendererCollection, new GUIContent("自动收集 Renderer"));
            EditorGUILayout.PropertyField(includeInactiveRenderers, new GUIContent("包括未激活 Renderer"));
            EditorGUILayout.PropertyField(explicitRenderers, new GUIContent("额外指定 Renderer"), true);
            EditorGUILayout.PropertyField(captureUnityTerrains, new GUIContent("捕获 Unity Terrain"));
            EditorGUILayout.PropertyField(explicitTerrains, new GUIContent("额外指定 Terrain"), true);
            EditorGUILayout.HelpBox(
                "缓存从上向下捕获，并通过深度只保留最高表面。草场自身和隐藏的编辑器预览对象会自动排除。",
                MessageType.Info);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("更新策略", EditorStyles.boldLabel);
            updateMode.enumValueIndex = EditorGUILayout.Popup(
                new GUIContent("更新模式"),
                updateMode.enumValueIndex,
                UpdateModeNames);
            if ((AnimeSurfaceCacheUpdateMode)updateMode.enumValueIndex == AnimeSurfaceCacheUpdateMode.Interval)
            {
                EditorGUILayout.PropertyField(updateInterval, new GUIContent("更新间隔（秒）"));
            }

            EditorGUILayout.PropertyField(updateInEditMode, new GUIContent("编辑模式更新"));
            EditorGUILayout.PropertyField(followTarget, new GUIContent("跟随目标", "只跟随目标的 XZ 位置，缓存中心高度保持不变。"));
            if (followTarget.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(
                    followSnapInTexels,
                    new GUIContent("跟随吸附（纹素）", "按若干纹素移动缓存，减少目标轻微移动造成的连续重建。"));
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("场景可视化", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(drawBounds, new GUIContent("显示缓存范围"));
            if (drawBounds.boolValue)
            {
                EditorGUILayout.PropertyField(
                    showBoundsWhenUnselected,
                    new GUIContent("未选中时也显示", "关闭时仅在选中缓存组件时显示范围。"));
                EditorGUILayout.PropertyField(boundsColor, new GUIContent("范围颜色"));
            }

            bool changed = serializedObject.ApplyModifiedProperties();
            AnimeSurfaceCache cache = (AnimeSurfaceCache)target;
            if (changed)
            {
                cache.MarkDirty();
                EditorUtility.SetDirty(cache);
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("立即重建缓存"))
                {
                    cache.RefreshNow();
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("创建遮罩 Volume"))
                {
                    CreateStamp(cache.transform.position);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("运行状态", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("缓存状态", cache.IsDirty ? "等待更新" : "已更新");
            EditorGUILayout.LabelField("捕获 Renderer", cache.LastRendererCount.ToString());
            EditorGUILayout.LabelField("捕获 Terrain", cache.LastTerrainCount.ToString());
            EditorGUILayout.LabelField("捕获绘制次数", cache.LastDrawCallCount.ToString());
            EditorGUILayout.LabelField("遮罩 Volume 数", cache.LastStampCount.ToString());
            EditorGUILayout.LabelField("最近更新帧", cache.LastUpdateFrame.ToString());
            float texelSizeX = cache.WorldSize.x / Mathf.Max(1, cache.Resolution);
            float texelSizeZ = cache.WorldSize.y / Mathf.Max(1, cache.Resolution);
            EditorGUILayout.LabelField(
                "世界纹素 X / Z",
                texelSizeX.ToString("0.###") + " / " + texelSizeZ.ToString("0.###") + " 米/像素");

            showTextures = EditorGUILayout.Foldout(showTextures, "缓存纹理预览", true);
            if (showTextures)
            {
                DrawTexturePreview("颜色", cache.ColorTexture);
                DrawTexturePreview("世界法线 / 高度", cache.DataTexture);
                DrawTexturePreview("湿润 / 积雪 / 烧焦 / 排除", cache.MaskTexture);
            }
        }

        [MenuItem("GameObject/AnimeGrass/地表属性缓存", false, 11)]
        private static void CreateSurfaceCache(MenuCommand command)
        {
            GameObject cacheObject = new GameObject("AnimeGrass 地表属性缓存");
            GameObjectUtility.SetParentAndAlign(cacheObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(cacheObject, "创建 AnimeGrass 地表属性缓存");
            cacheObject.AddComponent<AnimeSurfaceCache>();
            Selection.activeGameObject = cacheObject;
        }

        [MenuItem("GameObject/AnimeGrass/GrassVolume", false, 12)]
        private static void CreateSurfaceStamp(MenuCommand command)
        {
            Vector3 position = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot
                : Vector3.zero;
            CreateStamp(position, command.context as GameObject);
        }

        private static void CreateStamp(Vector3 position, GameObject parent = null)
        {
            GameObject stampObject = new GameObject("GrassVolume");
            GameObjectUtility.SetParentAndAlign(stampObject, parent);
            stampObject.transform.position = position;
            stampObject.transform.localScale = Vector3.one * 2f;
            Undo.RegisterCreatedObjectUndo(stampObject, "创建 GrassVolume");
            stampObject.AddComponent<GrassVolume>();
            Selection.activeGameObject = stampObject;
        }

        private static void DrawTexturePreview(string label, Texture texture)
        {
            EditorGUILayout.LabelField(label);
            Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxHeight(180f));
            if (texture != null)
            {
                EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.HelpBox(rect, "尚未创建", MessageType.None);
            }
        }
    }

    [CustomEditor(typeof(AnimeSurfaceCacheSource))]
    public sealed class AnimeSurfaceCacheSourceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("地表写入设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("excludeFromCache"), new GUIContent("从缓存排除"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("colorMultiplier"), new GUIContent("颜色乘数"));
            SerializedProperty overrideBaseMap = serializedObject.FindProperty("overrideBaseMap");
            EditorGUILayout.PropertyField(overrideBaseMap, new GUIContent("覆盖基础贴图"));
            if (overrideBaseMap.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseMap"), new GUIContent("基础贴图"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseMapScale"), new GUIContent("贴图 Tiling"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseMapOffset"), new GUIContent("贴图 Offset"));
            }

            SerializedProperty overrideBaseColor = serializedObject.FindProperty("overrideBaseColor");
            EditorGUILayout.PropertyField(overrideBaseColor, new GUIContent("覆盖基础颜色"));
            if (overrideBaseColor.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseColor"), new GUIContent("基础颜色"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("normalFlattening"), new GUIContent("法线朝上程度"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("地表遮罩", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("exclusion"), new GUIContent("排除草"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("透明裁剪覆盖", EditorStyles.boldLabel);
            SerializedProperty overrideAlphaClip = serializedObject.FindProperty("overrideAlphaClip");
            EditorGUILayout.PropertyField(overrideAlphaClip, new GUIContent("覆盖材质设置"));
            if (overrideAlphaClip.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("alphaClip"), new GUIContent("启用透明裁剪"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("alphaCutoff"), new GUIContent("裁剪阈值"));
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                ((AnimeSurfaceCacheSource)target).NotifyChanged();
            }
        }
    }

    [CustomEditor(typeof(GrassVolume))]
    public sealed class GrassVolumeEditor : UnityEditor.Editor
    {
        private static readonly string[] ShapeNames = { "球形 Volume", "盒形 Volume" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty shape = serializedObject.FindProperty("shape");
            shape.enumValueIndex = EditorGUILayout.Popup(new GUIContent("形状"), shape.enumValueIndex, ShapeNames);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hardness"), new GUIContent("边缘硬度"));

            EditorGUILayout.HelpBox(
                "GrassVolume 的位置、三轴旋转和尺寸完全由 Transform 控制；Scale X/Y/Z 分别对应本地三轴尺寸。静态模式按缓存地表高度写入遮罩，实时模式直接在 GPU 中检查草根或远景地表位置。",
                MessageType.Info);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("草场控制", EditorStyles.boldLabel);
            SerializedProperty exclusion = serializedObject.FindProperty("exclusion");
            EditorGUILayout.PropertyField(
                exclusion,
                new GUIContent("排除草强度", "0 表示不移除草，1 表示完全移除 Volume 内的草。只有静态模式需要更新地表缓存。"));
            if (exclusion.floatValue > 0.0001f)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("realtimeExclusion"),
                    new GUIContent(
                        "实时跟随 Transform",
                        "通过 GPU Volume 实时排除草，不把该 Volume 烘焙进地表缓存。挂在 Camera 或其子对象上时会自动启用。"));
                EditorGUI.indentLevel--;
            }
            SerializedProperty repelGrass = serializedObject.FindProperty("repelGrass");
            EditorGUILayout.PropertyField(
                repelGrass,
                new GUIContent("推动草叶上部", "实时将进入 Volume 的草叶上部向外推开，草根保持固定。"));
            if (repelGrass.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("grassRepulsionStrength"),
                    new GUIContent("推动距离", "草叶顶部能够向外偏移的最大世界空间距离。"));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("grassRepulsionFalloff"),
                    new GUIContent("边缘过渡", "控制从 Volume 边缘进入内部时推动强度增加的范围。"));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("grassRepulsionHeightStart"),
                    new GUIContent("受影响起始高度", "草叶归一化高度低于该值的部分保持不动。0.2 表示底部 20% 固定。"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("renderInEditMode"), new GUIContent("编辑模式生效"));

            GrassVolume volume = (GrassVolume)target;
            EditorGUILayout.HelpBox(
                volume.UsesRealtimeExclusion
                    ? "当前排除草使用实时 GPU Volume，会跟随 Transform 和父级 Camera 移动，不需要重建地表缓存。草叶推动同样实时生效；最多同时处理 16 个实时 Volume。"
                    : "当前排除草写入地表缓存；缓存设置为“仅手动更新”时需要手动重建。开启“实时跟随 Transform”可用于移动物体。草叶推动始终是实时 GPU 效果。",
                MessageType.Info);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("场景范围显示", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("showWhenUnselected"),
                new GUIContent("未选中时也显示", "关闭时只在选中该 Volume 时显示范围；开启后未选中时也会显示。"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("showSelectedFill"),
                new GUIContent("选中时显示填充", "选中 Volume 时显示淡色的三维体积填充。"));

            Bounds worldBounds = volume.WorldBounds;
            EditorGUILayout.LabelField("世界范围中心", worldBounds.center.ToString("F2"));
            EditorGUILayout.LabelField("世界包围盒大小", worldBounds.size.ToString("F2"));
            EditorGUILayout.LabelField(
                "世界高度范围",
                worldBounds.min.y.ToString("F2") + " 到 " + worldBounds.max.y.ToString("F2"));
            EditorGUILayout.HelpBox(
                "外轮廓是 Volume 作用边界；红色内轮廓是排除草完全生效区；青色内轮廓是草叶推动达到完整强度的区域。轮廓会穿透草叶显示；淡色体积填充仍需要开启 Scene 视图的 Gizmos。",
                MessageType.None);

            if (serializedObject.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }
        }

        internal static void DrawOverlayHandles(GrassVolume volume)
        {
            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            DrawVolumeHandle(volume, 1f, GetOuterHandleColor(volume));

            if (volume.Exclusion > 0.0001f && volume.Hardness > 0.0001f)
            {
                DrawVolumeHandle(
                    volume,
                    Mathf.Clamp01(volume.Hardness),
                    new Color(1f, 0.2f, 0.15f, 1f));
            }

            if (volume.RepelGrass && volume.GrassRepulsionStrength > 0.0001f)
            {
                float fullStrengthScale = Mathf.Clamp01(1f - volume.GrassRepulsionFalloff);
                if (fullStrengthScale > 0.0001f)
                {
                    DrawVolumeHandle(
                        volume,
                        fullStrengthScale,
                        new Color(0.1f, 1f, 0.9f, 1f));
                }
            }

            Handles.zTest = previousZTest;
        }

        private static void DrawVolumeHandle(
            GrassVolume volume,
            float scale,
            Color color)
        {
            Matrix4x4 matrix = volume.transform.localToWorldMatrix
                * Matrix4x4.Scale(Vector3.one * scale);
            using (new Handles.DrawingScope(color, matrix))
            {
                if (volume.Shape == GrassVolumeShape.Sphere)
                {
                    Handles.DrawWireDisc(Vector3.zero, Vector3.right, 0.5f);
                    Handles.DrawWireDisc(Vector3.zero, Vector3.up, 0.5f);
                    Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 0.5f);
                }
                else
                {
                    Handles.DrawWireCube(Vector3.zero, Vector3.one);
                }
            }
        }

        private static Color GetOuterHandleColor(GrassVolume volume)
        {
            bool hasExclusion = volume.Exclusion > 0.0001f;
            bool hasRepulsion = volume.RepelGrass && volume.GrassRepulsionStrength > 0.0001f;
            if (hasExclusion && hasRepulsion)
            {
                return new Color(1f, 0.8f, 0.1f, 1f);
            }
            if (hasExclusion)
            {
                return new Color(1f, 0.3f, 0.15f, 1f);
            }
            if (hasRepulsion)
            {
                return new Color(0.1f, 1f, 0.9f, 1f);
            }

            return Color.white;
        }
    }

    [InitializeOnLoad]
    internal static class GrassVolumeSceneOverlay
    {
        static GrassVolumeSceneOverlay()
        {
            SceneView.duringSceneGui -= DrawVisibleVolumes;
            SceneView.duringSceneGui += DrawVisibleVolumes;
        }

        private static void DrawVisibleVolumes(SceneView sceneView)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var volumes = GrassVolume.ActiveVolumes;
            for (int volumeIndex = 0; volumeIndex < volumes.Count; volumeIndex++)
            {
                GrassVolume volume = volumes[volumeIndex];
                bool isSelected = volume != null && Selection.Contains(volume.gameObject);
                if (volume == null
                    || !volume.isActiveAndEnabled
                    || (!isSelected && !volume.ShowWhenUnselected))
                {
                    continue;
                }

                GrassVolumeEditor.DrawOverlayHandles(volume);
            }
        }
    }
}
