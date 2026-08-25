using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PausePanelBindingStage8
{
    private const string PrefabPath = "Assets/Prefab/default/tetris/PausePanel.prefab";
    private const string ViewPath = "Assets/Scripts/csharp/com/game/tetris/view/PausePanel.cs";
    private const string SessionKey = "AIUI.PausePanel.Stage8.BindingExportV3.Achievements";

    static PausePanelBindingStage8() { EditorApplication.delayCall += TryApply; }

    [MenuItem("Tools/AIUI/Generate Tetris2/PausePanel Binding Export (Stage 8)")]
    public static void Apply()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        var instance = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (instance == null) throw new InvalidOperationException("Prefab missing: " + PrefabPath);
        try
        {
            var binder = instance.GetComponent<UIBinder>();
            if (binder == null) throw new InvalidOperationException("Root UIBinder missing: " + PrefabPath);
            binder.csharpAssetPath = ViewPath;
            binder.uiList = new List<UIBindComponentData>();
            int id = 1;
            AddButton(binder, ref id, instance.transform, "btnResume");
            AddButton(binder, ref id, instance.transform, "btnRestart");
            AddButton(binder, ref id, instance.transform, "btnQuitToTitle");
            AddButton(binder, ref id, instance.transform, "btnAchievements");
            EditorUtility.SetDirty(binder);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(instance); }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var reloadedBinder = prefab.GetComponent<UIBinder>();
        InvokeInspectorGenerator(reloadedBinder);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[AIUI] PausePanel Stage 8 binding exported: btnResume, btnRestart, btnQuitToTitle, btnAchievements");
    }

    private static void TryApply()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try {
            Apply();
            // Generators may recreate the prefab in the same editor tick; persist the binding once more afterward.
            EditorApplication.delayCall += () => {
                try { SessionState.EraseBool(SessionKey); Apply(); }
                catch (Exception error) { Debug.LogError("[AIUI] PausePanel Stage 8 retry failed: " + error); }
            };
        } catch (Exception error) { Debug.LogError("[AIUI] PausePanel Stage 8 failed: " + error); }
    }

    private static void AddButton(UIBinder binder, ref int id, Transform root, string name)
    {
        var target = Find(root, name);
        if (target == null) throw new InvalidOperationException("Missing runtime button: " + name);
        binder.uiList.Add(new UIBindComponentData { id = id++, go = target.gameObject, uiName = name, uiTypeName = "GameButton", isCustomClass = false, customClassName = string.Empty });
    }

    private static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++) { var found = Find(root.GetChild(i), name); if (found != null) return found; }
        return null;
    }

    private static void InvokeInspectorGenerator(UIBinder binder)
    {
        var editorType = Type.GetType("UIBinderInspector");
        if (editorType == null) throw new InvalidOperationException("UIBinderInspector type not found.");
        var editor = Editor.CreateEditor(binder, editorType);
        try
        {
            var method = editorType.GetMethod("GenerateUiBind", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("UIBinderInspector.GenerateUiBind not found.");
            method.Invoke(editor, new object[] { ViewPath });
        }
        finally { UnityEngine.Object.DestroyImmediate(editor); }
    }
}
