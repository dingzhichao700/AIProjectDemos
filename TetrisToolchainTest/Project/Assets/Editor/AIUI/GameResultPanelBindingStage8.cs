using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GameResultPanelBindingStage8
{
    const string PrefabPath = "Assets/Prefab/default/tetris/GameResultPanel.prefab";
    const string ViewPath = "Assets/Scripts/csharp/com/game/tetris/view/GameResultPanel.cs";
    const string SessionKey = "AIUI.GameResultPanel.Stage8.BindingExportR1";
    static GameResultPanelBindingStage8() { EditorApplication.delayCall += TryApply; }

    [MenuItem("Tools/AIUI/Generate Tetris/GameResultPanel Binding Export (Stage 8)")]
    public static void Apply()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        var instance = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (instance == null) throw new InvalidOperationException("Prefab missing: " + PrefabPath);
        try
        {
            var binder = instance.GetComponent<UIBinder>() ?? instance.AddComponent<UIBinder>();
            binder.csharpAssetPath = ViewPath;
            binder.uiList = new List<UIBindComponentData>();
            int id = 1;
            Add(binder, ref id, instance.transform, "btnRetry", "GameButton");
            Add(binder, ref id, instance.transform, "btnQuitToTitle", "GameButton");
            Add(binder, ref id, instance.transform, "txtScore", "TextMeshProUGUI");
            Add(binder, ref id, instance.transform, "txtLines", "TextMeshProUGUI");
            Add(binder, ref id, instance.transform, "txtLevel", "TextMeshProUGUI");
            EditorUtility.SetDirty(binder);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(instance); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        InvokeInspectorGenerator(prefab.GetComponent<UIBinder>());
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[AIUI] GameResultPanel Stage 8 binding exported: btnRetry, btnQuitToTitle, txtScore, txtLines, txtLevel");
    }

    static void TryApply() { if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return; try { Apply(); } catch (Exception e) { Debug.LogError("[AIUI] GameResultPanel Stage 8 failed: " + e); } }
    static void Add(UIBinder binder, ref int id, Transform root, string name, string typeName) { var target=Find(root,name); if(target==null)throw new InvalidOperationException("Missing runtime node: "+name); binder.uiList.Add(new UIBindComponentData{id=id++,go=target.gameObject,uiName=name,uiTypeName=typeName,isCustomClass=false,customClassName=string.Empty}); }
    static Transform Find(Transform root,string name){if(root.name==name)return root;for(int i=0;i<root.childCount;i++){var f=Find(root.GetChild(i),name);if(f!=null)return f;}return null;}
    static void InvokeInspectorGenerator(UIBinder binder){var editorType=Type.GetType("UIBinderInspector");if(editorType==null)throw new InvalidOperationException("UIBinderInspector type not found.");var editor=Editor.CreateEditor(binder,editorType);try{var method=editorType.GetMethod("GenerateUiBind",BindingFlags.Instance|BindingFlags.NonPublic);if(method==null)throw new InvalidOperationException("UIBinderInspector.GenerateUiBind not found.");method.Invoke(editor,new object[]{ViewPath});}finally{UnityEngine.Object.DestroyImmediate(editor);}}
}
