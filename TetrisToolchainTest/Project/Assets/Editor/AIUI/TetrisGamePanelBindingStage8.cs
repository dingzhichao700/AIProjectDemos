#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TetrisGamePanelBindingStage8
{
    const string PrefabPath = "Assets/Prefab/default/tetris/TetrisGamePanel.prefab";
    const string ViewScriptPath = "Assets/Scripts/csharp/com/game/tetris/view/TetrisGamePanel.cs";

    [MenuItem("Tools/AIUI/Apply TetrisGamePanel Stage 8 Binding")]
    public static void Apply()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException("Prefab missing: " + PrefabPath);
        }

        GameObject instance = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            UIBinder binder = instance.GetComponent<UIBinder>();
            if (binder == null)
            {
                throw new InvalidOperationException("Root UIBinder missing on prefab: " + PrefabPath);
            }

            binder.csharpAssetPath = ViewScriptPath;
            binder.csharpAsset = string.Empty;
            binder.uiList = BuildExportList(instance.transform);

            EditorUtility.SetDirty(binder);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }

        GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        UIBinder reloadedBinder = reloaded.GetComponent<UIBinder>();
        if (reloadedBinder == null)
        {
            throw new InvalidOperationException("Reloaded prefab lost UIBinder: " + PrefabPath);
        }

        InvokeInspectorGenerator(reloadedBinder);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[AIUI] Stage 8 binding applied: " + PrefabPath);
    }

    static UIBindComponentData CreateItem(int id, GameObject go)
    {
        if (go == null)
        {
            throw new InvalidOperationException("Missing node for binding item id=" + id);
        }

        Component component = GetComponentForExport(go);
        if (component == null)
        {
            throw new InvalidOperationException("No exportable component found on node: " + go.name);
        }

        return new UIBindComponentData
        {
            id = id,
            go = go,
            uiName = go.name,
            uiTypeName = component.GetType().Name,
            isCustomClass = false,
            customClassName = string.Empty,
        };
    }

    static Component GetComponentForExport(GameObject go)
    {
        Component component = go.GetComponent<GameButton>();
        if (component != null) return component;

        component = go.GetComponent<UnityEngine.UI.Image>();
        if (component != null) return component;

        component = go.GetComponent<TMPro.TextMeshProUGUI>();
        if (component != null) return component;

        component = go.GetComponent<RectTransform>();
        if (component != null) return component;

        return null;
    }

    static System.Collections.Generic.List<UIBindComponentData> BuildExportList(Transform root)
    {
        var result = new System.Collections.Generic.List<UIBindComponentData>();
        int id = 1;

        AddIfExists(result, ref id, FindDeep(root, "btnPause"));
        AddIfExists(result, ref id, FindDeep(root, "gridBoard"));
        AddIfExists(result, ref id, FindDeep(root, "gridNext"));
        AddIfExists(result, ref id, FindDeep(root, "txtScore"));
        AddIfExists(result, ref id, FindDeep(root, "txtHighScore"));
        AddIfExists(result, ref id, FindDeep(root, "txtLevel"));
        AddIfExists(result, ref id, FindDeep(root, "txtLines"));
        AddIfExists(result, ref id, FindDeep(root, "txtCountdown"));

        CollectImages(root, result, ref id);
        return result;
    }

    static void AddIfExists(System.Collections.Generic.List<UIBindComponentData> list, ref int id, GameObject go)
    {
        if (go == null)
        {
            return;
        }

        if (list.Exists(item => item.go == go))
        {
            return;
        }

        list.Add(CreateItem(id++, go));
    }

    static void CollectImages(Transform root, System.Collections.Generic.List<UIBindComponentData> list, ref int id)
    {
        if (root == null)
        {
            return;
        }

        GameObject go = root.gameObject;
        if (go.GetComponent<UnityEngine.UI.Image>() != null && !list.Exists(item => item.go == go))
        {
            list.Add(CreateItem(id++, go));
        }

        for (int i = 0; i < root.childCount; i++)
        {
            CollectImages(root.GetChild(i), list, ref id);
        }
    }

    static GameObject FindDeep(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        Transform found = FindDeepTransform(root, name);
        return found == null ? null : found.gameObject;
    }

    static Transform FindDeepTransform(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindDeepTransform(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static void InvokeInspectorGenerator(UIBinder binder)
    {
        var editorType = Type.GetType("UIBinderInspector");
        if (editorType == null)
        {
            throw new InvalidOperationException("UIBinderInspector type not found.");
        }

        var editor = Editor.CreateEditor(binder, editorType);
        try
        {
            MethodInfo method = editorType.GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("UIBinderInspector.GenerateUiBind not found.");
            }

            method.Invoke(editor, new object[] { ViewScriptPath });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(editor);
        }
    }
}
#endif
