using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Enlyn.Grass.Editor
{
    [InitializeOnLoad]
    internal static class AnimeGrassAssetRepairUtility
    {
        private const string ProjectAssetRoot = "Assets";
        private static bool autoRepairQueued;

        static AnimeGrassAssetRepairUtility()
        {
            EditorApplication.delayCall += QueueAutoRepair;
        }

        [MenuItem("AnimeGress/诊断并修复草资源")]
        public static void DiagnoseAndRepairMenu()
        {
            DiagnoseAndRepair(true);
        }

        private static void QueueAutoRepair()
        {
            if (autoRepairQueued)
            {
                return;
            }

            autoRepairQueued = true;
            DiagnoseAndRepair(false);
        }

        private static void DiagnoseAndRepair(bool verbose)
        {
            string[] prototypeGuids = AssetDatabase.FindAssets("t:AnimeGrassPrototype", new[] { ProjectAssetRoot });
            int repairedCount = 0;
            int missingMeshCount = 0;

            for (int i = 0; i < prototypeGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prototypeGuids[i]);
                AnimeGrassPrototype prototype = AssetDatabase.LoadAssetAtPath<AnimeGrassPrototype>(path);
                if (prototype == null)
                {
                    continue;
                }

                if (RepairPrototype(path, prototype, verbose, ref missingMeshCount))
                {
                    repairedCount++;
                }
            }

            if (repairedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (verbose || repairedCount > 0 || missingMeshCount > 0)
            {
                Debug.Log("[AnimeGress] 草资源诊断完成。草类型: " + prototypeGuids.Length
                    + "，已修复: " + repairedCount
                    + "，仍缺 Mesh: " + missingMeshCount);
            }
        }

        private static bool RepairPrototype(string prototypePath, AnimeGrassPrototype prototype, bool verbose, ref int missingMeshCount)
        {
            SerializedObject serializedPrototype = new SerializedObject(prototype);
            SerializedProperty lods = serializedPrototype.FindProperty("lods");
            if (lods == null || !lods.isArray)
            {
                return false;
            }

            bool changed = false;
            for (int lodIndex = 0; lodIndex < lods.arraySize; lodIndex++)
            {
                SerializedProperty lod = lods.GetArrayElementAtIndex(lodIndex);
                SerializedProperty mesh = lod.FindPropertyRelative("mesh");
                SerializedProperty material = lod.FindPropertyRelative("material");
                if (mesh == null || mesh.objectReferenceValue != null)
                {
                    continue;
                }

                missingMeshCount++;
                Mesh replacement = FindSingleMeshNearPrototype(prototypePath);
                if (replacement == null)
                {
                    if (verbose)
                    {
                        Debug.LogWarning("[AnimeGress] " + prototypePath + " 的 LOD " + lodIndex
                            + " 缺少 Mesh，且同目录没有唯一可自动绑定的 FBX Mesh。", prototype);
                    }

                    continue;
                }

                mesh.objectReferenceValue = replacement;
                if (material != null && material.objectReferenceValue == null)
                {
                    Material grassMaterial = FindMaterialNearPrototype(prototypePath);
                    if (grassMaterial != null)
                    {
                        material.objectReferenceValue = grassMaterial;
                    }
                }

                changed = true;
                missingMeshCount--;
                Debug.Log("[AnimeGress] 已修复 " + prototypePath + " 的 LOD " + lodIndex
                    + " Mesh -> " + AssetDatabase.GetAssetPath(replacement) + " / " + replacement.name, prototype);
            }

            if (changed)
            {
                serializedPrototype.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prototype);
            }

            return changed;
        }

        private static Mesh FindSingleMeshNearPrototype(string prototypePath)
        {
            string folder = Path.GetDirectoryName(prototypePath);
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }

            List<Mesh> meshes = new List<Mesh>();
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folder.Replace('\\', '/') });
            for (int i = 0; i < modelGuids.Length; i++)
            {
                string modelPath = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    if (assets[assetIndex] is Mesh mesh && !meshes.Contains(mesh))
                    {
                        meshes.Add(mesh);
                    }
                }
            }

            return meshes.Count == 1 ? meshes[0] : null;
        }

        private static Material FindMaterialNearPrototype(string prototypePath)
        {
            string folder = Path.GetDirectoryName(prototypePath);
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }

            string[] materialGuids = AssetDatabase.FindAssets("Gress t:Material", new[] { folder.Replace('\\', '/') });
            if (materialGuids.Length == 0)
            {
                materialGuids = AssetDatabase.FindAssets("t:Material", new[] { folder.Replace('\\', '/') });
            }

            return materialGuids.Length == 1
                ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(materialGuids[0]))
                : null;
        }
    }
}
