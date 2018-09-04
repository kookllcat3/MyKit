using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class FindReferences
{
    [MenuItem("Tools/Find References In Hierarchy", false)]
    private static void FindReferencesFromScene()
    {
        foreach (var assetObject in Selection.objects)
        {
            FindReferencesTo(assetObject);
        }
    }

    [MenuItem("Tools/Find References In Hierarchy", true)]
    private static bool BFindScene()
    {
        return Selection.objects.Length > 0;
    }

    private static void FindReferencesTo(Object to)
    {
        if (to == null)
            return;

        var referencedBy = new List<Object>();

        var allObjects = Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[];
        foreach (var go in allObjects)
        {
            if (go.hideFlags != HideFlags.None)
                continue;

            if (PrefabUtility.GetPrefabType(go) == PrefabType.Prefab || PrefabUtility.GetPrefabType(go) == PrefabType.ModelPrefab)
                continue;

            if (PrefabUtility.GetPrefabType(go) == PrefabType.PrefabInstance)
            {
                if (PrefabUtility.GetPrefabParent(go) == to)
                {
                    Debug.Log(string.Format("{0} referenced by {1}, {2}", to, GetPath(go.transform), go.GetType()), go);
                    referencedBy.Add(go);
                    continue;
                }
            }

            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (iterator.objectReferenceValue == to)
                        {
                            Debug.Log(string.Format("{0}({1}) referenced by {2}, {3}", to.name, to.GetType(), GetPath(component.transform), component.GetType()), component);
                            referencedBy.Add(component);
                        }
                    }
                }
            }
        }
        if (referencedBy.Count == 0)
            Debug.Log(to + " no references in hierarchy");
    }

    public static string GetPath(Transform transform)
    {
        string path = transform.name;

        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }


    [MenuItem("Tools/Find References In Files", false)]
    private static void FindReferencesInFiles()
    {
        EditorSettings.serializationMode = SerializationMode.ForceText;
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!string.IsNullOrEmpty(path))
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            string withoutExtensions = "*.prefab*.unity*.mat*.asset";
            string[] files = Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories).
                Where(str => withoutExtensions.Contains(Path.GetExtension(str).ToLower())).ToArray();
            int startIndex = 0;

            EditorApplication.update = delegate ()
            {
                string file = files[startIndex];
                bool isCancel = EditorUtility.DisplayCancelableProgressBar("资源匹配中", file, (float)startIndex / (float)files.Length);
                if (Regex.IsMatch(File.ReadAllText(file), guid))
                {
                    Debug.Log(file, AssetDatabase.LoadAssetAtPath(GetRelativeAssetsPath(file), typeof(UnityEngine.Object)));
                }
                startIndex++;
                if (isCancel || startIndex >= files.Length)
                {
                    EditorUtility.ClearProgressBar();
                    EditorApplication.update = null;
                    startIndex = 0;
                    Debug.Log("匹配结束");
                }
            };
        }
    }

    [MenuItem("Tools/Find References In Files", true)]
    private static bool BFindFiles()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return (!string.IsNullOrEmpty(path));
    }

    private static string GetRelativeAssetsPath(string path)
    {
        return "Assets" + Path.GetFullPath(path).Replace(Path.GetFullPath(Application.dataPath), "").Replace('\\', '/');
    }
}