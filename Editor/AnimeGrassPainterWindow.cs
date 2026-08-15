using System.Collections.Generic;
using Enlyn.Grass;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Enlyn.Grass.Editor
{
    public sealed class AnimeGrassPainterWindow : EditorWindow
    {
        private enum PaintMode
        {
            None = -1,
            Single = 0,
            Brush = 1,
            Erase = 2,
            Edit
        }

        private enum EditHandleMode
        {
            Move,
            Rotate,
            MoveAndRotate
        }

        private AnimeGrassField field;
        private AnimeGrassPrototype prototypeToAdd;
        private PaintMode mode = PaintMode.None;
        private GUIContent[] brushModeIcons;
        private GUIContent editModeIcon;
        private int selectedInstanceIndex = -1;
        private float editPickRadius = 0.35f;
        private EditHandleMode editHandleMode = EditHandleMode.MoveAndRotate;
        private int prototypeIndex;
        private float brushRadius = 1.5f;
        private float brushSpacing = 0.8f;
        private int density = 10;
        [SerializeField] private Vector3 randomScaleRatio = new Vector3(0.15f, 0.15f, 0.15f);
        [SerializeField] private Vector3 randomRotationRatio = new Vector3(0f, 1f, 0f);
        private bool alignToSurfaceNormal = true;
        private Color instanceColor = Color.white;
        private float colorJitter;
        private float instanceWindWeight = 1f;
        private bool eraseOnlySelectedPrototype;
        private LayerMask paintLayers = ~0;
        private float raycastDistance = 250f;
        private int surfaceDepth;
        [SerializeField] private Collider targetSurface;
        private readonly RaycastHit[] surfaceHits = new RaycastHit[64];

        private bool hasLastStampPosition;
        private Vector3 lastStampPosition;

        public static void Open(AnimeGrassField targetField)
        {
            AnimeGrassPainterWindow window = GetWindow<AnimeGrassPainterWindow>("草场铺设工具");
            if (targetField != null)
            {
                window.field = targetField;
            }

            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DuringSceneGui;
            EnsureModeIcons();
            TryUseSelection();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
        }

        private void OnSelectionChange()
        {
            if (TryGetSelectedField(out AnimeGrassField selectedField))
            {
                field = selectedField;
            }
            else
            {
                SetMode(PaintMode.None);
            }

            if (!IsTargetFieldSelected())
            {
                GUIUtility.hotControl = 0;
                hasLastStampPosition = false;
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("目标草场", EditorStyles.boldLabel);
            field = (AnimeGrassField)EditorGUILayout.ObjectField("草场组件", field, typeof(AnimeGrassField), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("使用当前选择"))
                {
                    TryUseSelection(true);
                }

                if (GUILayout.Button("创建草场"))
                {
                    CreateField();
                }
            }

            if (field == null)
            {
                EditorGUILayout.HelpBox("请先指定或创建一个 AnimeGrassField 草场组件。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("草类型", EditorStyles.boldLabel);
            prototypeToAdd = (AnimeGrassPrototype)EditorGUILayout.ObjectField("要加入的草类型", prototypeToAdd, typeof(AnimeGrassPrototype), false);
            using (new EditorGUI.DisabledScope(prototypeToAdd == null))
            {
                if (GUILayout.Button("加入草场"))
                {
                    Undo.RecordObject(field, "加入草类型");
                    field.AddPrototype(prototypeToAdd);
                    EditorUtility.SetDirty(field);
                    prototypeToAdd = null;
                }
            }

            DrawPrototypeSelector();

            EditorGUILayout.Space(8f);
            bool targetFieldSelected = IsTargetFieldSelected();
            if (!targetFieldSelected)
            {
                EditorGUILayout.HelpBox("当前选择不是这个草场，Scene 视图会使用 Unity 默认鼠标操作，不会铺设或删除草。请在 Hierarchy 中选中草场后再使用草工具。", MessageType.Info);
            }

            DrawTargetSurface();

            EditorGUILayout.LabelField("铺设笔刷", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!targetFieldSelected))
            {
                DrawModeToolbar();
            }

            if (mode == PaintMode.None)
            {
                EditorGUILayout.HelpBox("未选择铺设笔刷，Scene 视图使用 Unity 默认鼠标操作。再次点击已选中的笔刷图标也会取消选择。", MessageType.None);
            }
            else if (mode == PaintMode.Edit)
            {
                EditorGUILayout.HelpBox("当前未启用铺设笔刷；单株编辑工具在下方单独控制。", MessageType.None);
            }
            else
            {
                brushRadius = Mathf.Max(0.05f, EditorGUILayout.FloatField("笔刷半径", brushRadius));
                brushSpacing = Mathf.Max(0.05f, EditorGUILayout.FloatField("连续铺设间距", brushSpacing));
                density = Mathf.Max(1, EditorGUILayout.IntField("每次生成数量", density));
                randomScaleRatio = ClampRatio(EditorGUILayout.Vector3Field("随机缩放比例 XYZ", randomScaleRatio));
                randomRotationRatio = ClampRatio(EditorGUILayout.Vector3Field("随机旋转比例 XYZ", randomRotationRatio));
                EditorGUILayout.HelpBox(
                    "缩放比例 0.15 表示该轴在 85% 到 115% 之间随机；旋转比例 1 表示该轴在 -180° 到 180° 之间随机。0 表示该轴不随机。",
                    MessageType.None);
                alignToSurfaceNormal = EditorGUILayout.Toggle("贴合表面法线", alignToSurfaceNormal);
                instanceColor = EditorGUILayout.ColorField("实例颜色", instanceColor);
                colorJitter = Mathf.Clamp01(EditorGUILayout.FloatField("颜色随机幅度", colorJitter));
                instanceWindWeight = Mathf.Max(0f, EditorGUILayout.FloatField("实例受风权重", instanceWindWeight));
                eraseOnlySelectedPrototype = EditorGUILayout.Toggle("只擦除当前草类型", eraseOnlySelectedPrototype);
                paintLayers = LayerMaskField("可铺设 Layer", paintLayers);
                raycastDistance = Mathf.Max(1f, EditorGUILayout.FloatField("射线检测距离", raycastDistance));
                if (targetSurface == null)
                {
                    surfaceDepth = EditorGUILayout.IntSlider(
                        new GUIContent("表面层级", "未锁定表面时使用：0 是射线命中的最上层，1 是下一层。"),
                        surfaceDepth,
                        0,
                        8);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("单株编辑", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!targetFieldSelected))
            {
                DrawEditModeToggle();
            }

            if (mode == PaintMode.Edit && targetFieldSelected)
            {
                DrawEditOptions();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("已保存草实例数", field.InstanceCount.ToString());
            EditorGUILayout.HelpBox("铺设笔刷只有 3 个模式：单株、笔刷、擦除。再次点击当前模式会取消选择并恢复 Unity 默认鼠标操作。生成的草实例会序列化保存到草场组件里。", MessageType.None);
        }

        private void DrawTargetSurface()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("目标铺设表面", EditorStyles.boldLabel);
            targetSurface = (Collider)EditorGUILayout.ObjectField(
                new GUIContent("锁定表面", "指定后，笔刷射线只检测这个 Collider，并忽略它上方的遮挡物。"),
                targetSurface,
                typeof(Collider),
                true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("使用选中物体的表面"))
                {
                    UseSelectedSurface();
                }

                using (new EditorGUI.DisabledScope(targetSurface == null))
                {
                    if (GUILayout.Button("清除表面锁定"))
                    {
                        targetSurface = null;
                        SceneView.RepaintAll();
                    }
                }
            }

            if (targetSurface != null)
            {
                EditorGUILayout.HelpBox(
                    $"已锁定：{targetSurface.name}。笔刷中心和整次随机采样都只会命中这个表面。",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("未锁定表面时，笔刷中心会使用可铺设 Layer 和表面层级自动选择 Collider。", MessageType.None);
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawPrototypeSelector()
        {
            List<AnimeGrassPrototype> prototypes = field.Prototypes;
            if (prototypes == null || prototypes.Count == 0)
            {
                EditorGUILayout.HelpBox("请先给草场加入至少一个草类型。", MessageType.Warning);
                prototypeIndex = 0;
                return;
            }

            string[] names = new string[prototypes.Count];
            for (int i = 0; i < prototypes.Count; i++)
            {
                names[i] = prototypes[i] == null ? "(草类型丢失)" : prototypes[i].name;
            }

            prototypeIndex = Mathf.Clamp(prototypeIndex, 0, prototypes.Count - 1);
            prototypeIndex = EditorGUILayout.Popup("当前草类型", prototypeIndex, names);
        }

        private void DrawModeToolbar()
        {
            EnsureModeIcons();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("模式");
                DrawModeButton(PaintMode.Single, brushModeIcons[0]);
                DrawModeButton(PaintMode.Brush, brushModeIcons[1]);
                DrawModeButton(PaintMode.Erase, brushModeIcons[2]);
            }
        }

        private void DrawEditModeToggle()
        {
            EnsureModeIcons();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("工具");
                DrawModeButton(PaintMode.Edit, editModeIcon);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawModeButton(PaintMode buttonMode, GUIContent content)
        {
            bool selected = mode == buttonMode;
            bool nextSelected = GUILayout.Toggle(selected, content, EditorStyles.toolbarButton, GUILayout.Height(30f), GUILayout.Width(42f));
            if (nextSelected == selected)
            {
                return;
            }

            SetMode(nextSelected ? buttonMode : PaintMode.None);
        }

        private void SetMode(PaintMode newMode)
        {
            if (mode == newMode)
            {
                return;
            }

            mode = newMode;
            GUIUtility.hotControl = 0;
            hasLastStampPosition = false;

            if (newMode != PaintMode.Edit)
            {
                selectedInstanceIndex = -1;
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void DrawEditOptions()
        {
            editPickRadius = Mathf.Max(0.02f, EditorGUILayout.FloatField("单株选择半径", editPickRadius));
            editHandleMode = (EditHandleMode)EditorGUILayout.Popup("变换手柄", (int)editHandleMode, new[] { "移动", "旋转", "移动 + 旋转" });
            eraseOnlySelectedPrototype = EditorGUILayout.Toggle("只选择当前草类型", eraseOnlySelectedPrototype);

            string selectedText = field != null && field.IsValidInstanceIndex(selectedInstanceIndex)
                ? "已选中草实例 #" + selectedInstanceIndex
                : "未选中草实例";
            EditorGUILayout.LabelField("当前选择", selectedText);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(field == null || !field.IsValidInstanceIndex(selectedInstanceIndex)))
                {
                    if (GUILayout.Button("聚焦选中草"))
                    {
                        FocusSelectedInstance();
                    }
                }

                if (GUILayout.Button("清除选择"))
                {
                    selectedInstanceIndex = -1;
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.HelpBox("在 Scene 视图左键点击草位置附近可选中单株草；选中后可用手柄移动或旋转。再次点击编辑单株图标可取消工具并恢复 Unity 默认鼠标操作。", MessageType.None);
        }

        private void EnsureModeIcons()
        {
            if (brushModeIcons != null && editModeIcon != null)
            {
                return;
            }

            brushModeIcons = new[]
            {
                CreateIconContent("单株放置", "d_ToolHandlePivot", "ToolHandlePivot", "d_MoveTool", "MoveTool"),
                CreateIconContent("笔刷铺设", "d_TerrainInspector.TerrainToolPlants", "TerrainInspector.TerrainToolPlants", "d_TerrainInspector.TerrainToolSplat", "TerrainInspector.TerrainToolSplat", "d_Brush", "Brush"),
                CreateIconContent("擦除", "d_TreeEditor.Trash", "TreeEditor.Trash", "d_P4_DeletedLocal", "P4_DeletedLocal", "d_Grid.EraserTool", "Grid.EraserTool")
            };

            editModeIcon = CreateIconContent("编辑单株", "d_EditCollider", "EditCollider", "d_TransformTool", "TransformTool", "d_MoveTool", "MoveTool");
        }

        private static GUIContent CreateIconContent(string tooltip, params string[] iconNames)
        {
            for (int i = 0; i < iconNames.Length; i++)
            {
                Texture icon = EditorGUIUtility.FindTexture(iconNames[i]);
                if (icon != null)
                {
                    return new GUIContent(icon, tooltip);
                }
            }

            return new GUIContent(tooltip.Substring(0, 1), tooltip);
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            Event e = Event.current;
            if (ShouldHandleSceneInput() && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                SetMode(PaintMode.None);
                Repaint();
                e.Use();
                return;
            }

            if (!ShouldHandleSceneInput())
            {
                return;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            if (mode == PaintMode.Edit)
            {
                DrawSelectedInstanceHandle();
                HandleEditPicking(e);
                return;
            }

            if (!TryGetMouseHit(e.mousePosition, out RaycastHit hit))
            {
                return;
            }

            DrawBrush(hit);

            bool leftMouse = e.button == 0 && !e.alt;
            if (!leftMouse)
            {
                return;
            }

            if (e.type == EventType.MouseDown)
            {
                hasLastStampPosition = false;
                GUIUtility.hotControl = controlId;
                Stamp(hit, true);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == controlId)
            {
                Stamp(hit, false);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
                hasLastStampPosition = false;
                e.Use();
            }
        }

        private void DrawBrush(RaycastHit hit)
        {
            Color color = mode == PaintMode.Erase
                ? new Color(1f, 0.24f, 0.18f, 0.95f)
                : new Color(0.36f, 0.9f, 0.52f, 0.95f);

            Handles.color = color;
            Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);
            Handles.DrawLine(hit.point, hit.point + hit.normal * 0.6f);
        }

        private void DrawSelectedInstanceHandle()
        {
            if (field == null || !field.IsValidInstanceIndex(selectedInstanceIndex))
            {
                return;
            }

            AnimeGrassInstance instance = field.GetInstance(selectedInstanceIndex);
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(instance.position, instance.normal.sqrMagnitude > 0.0001f ? instance.normal : Vector3.up, editPickRadius);
            Handles.DrawLine(instance.position, instance.position + Vector3.up * 0.8f);

            EditorGUI.BeginChangeCheck();

            Vector3 newPosition = instance.position;
            Quaternion newRotation = instance.rotation;

            if (editHandleMode == EditHandleMode.Move || editHandleMode == EditHandleMode.MoveAndRotate)
            {
                newPosition = Handles.PositionHandle(instance.position, instance.rotation);
            }

            if (editHandleMode == EditHandleMode.Rotate || editHandleMode == EditHandleMode.MoveAndRotate)
            {
                newRotation = Handles.RotationHandle(newRotation, newPosition);
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(field, "编辑单株草");
            instance.position = newPosition;
            instance.rotation = newRotation;
            field.SetInstance(selectedInstanceIndex, instance);
            EditorUtility.SetDirty(field);
            SceneView.RepaintAll();
        }

        private void HandleEditPicking(Event e)
        {
            bool leftMouseDown = e.type == EventType.MouseDown && e.button == 0 && !e.alt;
            if (!leftMouseDown)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            int filter = eraseOnlySelectedPrototype ? Mathf.Clamp(prototypeIndex, 0, field.Prototypes.Count - 1) : -1;
            int pickedIndex = field.FindClosestInstanceToRay(ray, editPickRadius, filter);
            selectedInstanceIndex = pickedIndex;
            SceneView.RepaintAll();
            Repaint();
            e.Use();
        }

        private void FocusSelectedInstance()
        {
            if (field == null || !field.IsValidInstanceIndex(selectedInstanceIndex))
            {
                return;
            }

            AnimeGrassInstance instance = field.GetInstance(selectedInstanceIndex);
            Bounds bounds = new Bounds(instance.position, Vector3.one * Mathf.Max(1f, editPickRadius * 4f));
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.Frame(bounds, false);
                sceneView.Repaint();
            }
        }

        private void Stamp(RaycastHit hit, bool mouseDown)
        {
            if (mode == PaintMode.Single && !mouseDown)
            {
                return;
            }

            if (mode != PaintMode.Single && hasLastStampPosition && Vector3.Distance(lastStampPosition, hit.point) < brushSpacing)
            {
                return;
            }

            if (mode == PaintMode.Erase)
            {
                Erase(hit.point);
            }
            else
            {
                Paint(hit, mode == PaintMode.Single ? 1 : density);
            }

            lastStampPosition = hit.point;
            hasLastStampPosition = true;
        }

        private void Paint(RaycastHit hit, int count)
        {
            prototypeIndex = Mathf.Clamp(prototypeIndex, 0, field.Prototypes.Count - 1);
            AnimeGrassPrototype prototype = field.Prototypes[prototypeIndex];
            if (prototype == null)
            {
                return;
            }

            List<AnimeGrassInstance> newInstances = new List<AnimeGrassInstance>(count);
            for (int i = 0; i < count; i++)
            {
                RaycastHit placementHit = hit;
                if (mode != PaintMode.Single)
                {
                    placementHit = SampleBrushHit(hit);
                }

                Vector3 normal = placementHit.normal.sqrMagnitude > 0.0001f ? placementHit.normal.normalized : Vector3.up;
                Vector3 randomEuler = BuildRandomRotation(randomRotationRatio);
                Quaternion rotation = BuildRotation(normal, randomEuler);
                Vector3 scale = BuildRandomScale(randomScaleRatio);
                Color color = JitterColor(instanceColor * prototype.DefaultInstanceColor, colorJitter);

                newInstances.Add(AnimeGrassInstance.Create(
                    placementHit.point,
                    rotation,
                    scale,
                    normal,
                    prototypeIndex,
                    color,
                    instanceWindWeight));
            }

            Undo.RecordObject(field, "铺设草实例");
            field.AddInstances(newInstances);
            EditorUtility.SetDirty(field);
            SceneView.RepaintAll();
        }

        private void Erase(Vector3 center)
        {
            int filter = eraseOnlySelectedPrototype ? Mathf.Clamp(prototypeIndex, 0, field.Prototypes.Count - 1) : -1;

            Undo.RecordObject(field, "擦除草实例");
            int removed = field.RemoveInstancesInSphere(center, brushRadius, filter);
            if (removed > 0)
            {
                EditorUtility.SetDirty(field);
                SceneView.RepaintAll();
            }
        }

        private RaycastHit SampleBrushHit(RaycastHit centerHit)
        {
            Vector2 randomPoint = Random.insideUnitCircle * brushRadius;
            Vector3 tangent = Vector3.Cross(centerHit.normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = Vector3.Cross(centerHit.normal, Vector3.right);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(centerHit.normal, tangent).normalized;
            Vector3 samplePoint = centerHit.point + tangent * randomPoint.x + bitangent * randomPoint.y;

            Ray normalRay = new Ray(samplePoint + centerHit.normal * (raycastDistance * 0.5f), -centerHit.normal);
            if (TryRaycastSurface(normalRay, out RaycastHit normalHit, centerHit.collider))
            {
                return normalHit;
            }

            Ray downRay = new Ray(samplePoint + Vector3.up * (raycastDistance * 0.5f), Vector3.down);
            if (TryRaycastSurface(downRay, out RaycastHit downHit, centerHit.collider))
            {
                return downHit;
            }

            return centerHit;
        }

        private Quaternion BuildRotation(Vector3 normal, Vector3 randomEuler)
        {
            Quaternion randomRotation = Quaternion.Euler(randomEuler);
            if (!alignToSurfaceNormal)
            {
                return randomRotation;
            }

            Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, normal);
            return surfaceRotation * randomRotation;
        }

        private static Vector3 BuildRandomScale(Vector3 ratio)
        {
            return new Vector3(
                Mathf.Max(0.001f, 1f + RandomSigned(ratio.x)),
                Mathf.Max(0.001f, 1f + RandomSigned(ratio.y)),
                Mathf.Max(0.001f, 1f + RandomSigned(ratio.z)));
        }

        private static Vector3 BuildRandomRotation(Vector3 ratio)
        {
            return new Vector3(
                RandomSigned(ratio.x) * 180f,
                RandomSigned(ratio.y) * 180f,
                RandomSigned(ratio.z) * 180f);
        }

        private static float RandomSigned(float ratio)
        {
            return ratio > 0f ? Random.Range(-ratio, ratio) : 0f;
        }

        private static Vector3 ClampRatio(Vector3 ratio)
        {
            ratio.x = Mathf.Clamp01(ratio.x);
            ratio.y = Mathf.Clamp01(ratio.y);
            ratio.z = Mathf.Clamp01(ratio.z);
            return ratio;
        }

        private bool TryGetMouseHit(Vector2 mousePosition, out RaycastHit hit)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            return TryRaycastSurface(ray, out hit, targetSurface);
        }

        private bool TryRaycastSurface(Ray ray, out RaycastHit hit, Collider requiredSurface = null)
        {
            if (requiredSurface != null)
            {
                if (!requiredSurface.enabled || !requiredSurface.gameObject.activeInHierarchy)
                {
                    hit = default;
                    return false;
                }

                return requiredSurface.Raycast(ray, out hit, raycastDistance);
            }

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                surfaceHits,
                raycastDistance,
                paintLayers.value,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
            {
                hit = default;
                return false;
            }

            for (int i = 1; i < hitCount; i++)
            {
                RaycastHit candidate = surfaceHits[i];
                int insertIndex = i - 1;
                while (insertIndex >= 0 && surfaceHits[insertIndex].distance > candidate.distance)
                {
                    surfaceHits[insertIndex + 1] = surfaceHits[insertIndex];
                    insertIndex--;
                }

                surfaceHits[insertIndex + 1] = candidate;
            }

            hit = surfaceHits[Mathf.Min(surfaceDepth, hitCount - 1)];
            return true;
        }

        private void UseSelectedSurface()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                ShowNotification(new GUIContent("请先在 Hierarchy 或 Scene 中选择带 Collider 的物体"));
                return;
            }

            Collider selectedCollider = selectedObject.GetComponent<Collider>();
            if (selectedCollider == null)
            {
                selectedCollider = selectedObject.GetComponentInParent<Collider>();
            }

            if (selectedCollider == null)
            {
                selectedCollider = selectedObject.GetComponentInChildren<Collider>();
            }

            if (selectedCollider == null)
            {
                ShowNotification(new GUIContent("选中物体及其父子对象没有 Collider"));
                return;
            }

            targetSurface = selectedCollider;
            ShowNotification(new GUIContent($"已锁定表面：{targetSurface.name}"));
            SceneView.RepaintAll();
            Repaint();
        }

        private bool ShouldHandleSceneInput()
        {
            return mode != PaintMode.None
                && field != null
                && field.Prototypes != null
                && field.Prototypes.Count > 0
                && IsTargetFieldSelected();
        }

        private bool IsTargetFieldSelected()
        {
            return TryGetSelectedField(out AnimeGrassField selectedField) && selectedField == field;
        }

        private static bool TryGetSelectedField(out AnimeGrassField selectedField)
        {
            selectedField = null;
            if (Selection.activeGameObject == null)
            {
                return false;
            }

            selectedField = Selection.activeGameObject.GetComponent<AnimeGrassField>();
            return selectedField != null;
        }

        private void TryUseSelection(bool force = false)
        {
            if (!force && field != null)
            {
                return;
            }

            if (TryGetSelectedField(out AnimeGrassField selectedField))
            {
                field = selectedField;
                return;
            }

            if (force)
            {
                return;
            }

            field = Object.FindFirstObjectByType<AnimeGrassField>();
        }

        private void CreateField()
        {
            GameObject go = new GameObject("二次元草场");
            Undo.RegisterCreatedObjectUndo(go, "创建二次元草场");
            field = go.AddComponent<AnimeGrassField>();
            Selection.activeGameObject = go;
        }

        private static Color JitterColor(Color color, float jitter)
        {
            if (jitter <= 0f)
            {
                return color;
            }

            float r = Mathf.Clamp01(color.r + Random.Range(-jitter, jitter));
            float g = Mathf.Clamp01(color.g + Random.Range(-jitter, jitter));
            float b = Mathf.Clamp01(color.b + Random.Range(-jitter, jitter));
            return new Color(r, g, b, color.a);
        }

        private static LayerMask LayerMaskField(string label, LayerMask layerMask)
        {
            string[] layerNames = InternalEditorUtility.layers;
            int editorMask = 0;

            for (int i = 0; i < layerNames.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layerNames[i]);
                if ((layerMask.value & (1 << layer)) != 0)
                {
                    editorMask |= 1 << i;
                }
            }

            editorMask = EditorGUILayout.MaskField(label, editorMask, layerNames);

            int mask = 0;
            for (int i = 0; i < layerNames.Length; i++)
            {
                if ((editorMask & (1 << i)) == 0)
                {
                    continue;
                }

                int layer = LayerMask.NameToLayer(layerNames[i]);
                mask |= 1 << layer;
            }

            return mask;
        }
    }
}
