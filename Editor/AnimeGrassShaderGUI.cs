using Enlyn.Grass;
using UnityEditor;
using UnityEngine;

namespace Enlyn.Grass.Editor
{
    public sealed class AnimeGrassShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            DrawSurfaceCacheStatus(properties);
            materialEditor.PropertiesDefaultGUI(properties);
        }

        private static void DrawSurfaceCacheStatus(MaterialProperty[] properties)
        {
            if (!UsesSurfaceCache(properties))
            {
                return;
            }

            AnimeSurfaceCache[] caches = Object.FindObjectsByType<AnimeSurfaceCache>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (caches.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前场景没有 AnimeSurfaceCache。地表颜色、法线和交互遮罩不会影响草。",
                    MessageType.Warning);
                if (GUILayout.Button("创建地表属性缓存"))
                {
                    CreateSurfaceCache();
                }

                EditorGUILayout.Space(6f);
                return;
            }

            bool hasReadyCache = false;
            for (int i = 0; i < caches.Length; i++)
            {
                if (caches[i] != null && caches[i].isActiveAndEnabled && caches[i].LastUpdateFrame >= 0)
                {
                    hasReadyCache = true;
                    break;
                }
            }

            if (!hasReadyCache)
            {
                EditorGUILayout.HelpBox(
                    "场景中的地表缓存尚未生成。请选择缓存对象并点击“立即重建缓存”。",
                    MessageType.Warning);
                EditorGUILayout.Space(6f);
            }
        }

        private static bool UsesSurfaceCache(MaterialProperty[] properties)
        {
            return GetFloat(properties, "_SurfaceCacheColorInfluence") > 0.001f
                || GetFloat(properties, "_SurfaceCacheNormalInfluence") > 0.001f
                || GetFloat(properties, "_SurfaceCacheExclusionInfluence") > 0.001f;
        }

        private static float GetFloat(MaterialProperty[] properties, string propertyName)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (property != null && property.name == propertyName)
                {
                    return property.floatValue;
                }
            }

            return 0f;
        }

        private static void CreateSurfaceCache()
        {
            GameObject cacheObject = new GameObject("AnimeGress 地表属性缓存");
            Undo.RegisterCreatedObjectUndo(cacheObject, "创建 AnimeGress 地表属性缓存");
            AnimeSurfaceCache cache = cacheObject.AddComponent<AnimeSurfaceCache>();
            if (SceneView.lastActiveSceneView != null)
            {
                cacheObject.transform.position = SceneView.lastActiveSceneView.pivot;
            }

            Selection.activeGameObject = cacheObject;
            EditorGUIUtility.PingObject(cache);
        }
    }
}
