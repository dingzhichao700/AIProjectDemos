using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PausePanelBindingInitializer
{
    private const string PrefabPath = "Assets/Prefab/default/tetris/PausePanel.prefab";
    private const string ViewPath = "Assets/Scripts/csharp/com/game/tetris/view/PausePanel.cs";
    private const string ReportPath = "Library/AIUI/PausePanel.binding.json";
    private const string SessionKey = "AIUI.PausePanel.Stage7.BindingInitializedV1";

    static PausePanelBindingInitializer() { EditorApplication.delayCall += Initialize; }

    [MenuItem("Tools/AIUI/Generate Tetris2/PausePanel Binding (Stage 7)")]
    public static void Initialize()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        var viewAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(ViewPath);
        if (viewAsset == null) { Debug.LogError("[AIUI] PausePanel Stage 7 missing view script: " + ViewPath); return; }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) { Debug.LogError("[AIUI] PausePanel Stage 7 missing prefab: " + PrefabPath); return; }
        try
        {
            var binders = root.GetComponents<UIBinder>();
            var binder = binders.Length == 0 ? root.AddComponent<UIBinder>() : binders[0];
            for (var i = 1; i < binders.Length; i++) Object.DestroyImmediate(binders[i]);
            binder.csharpAssetPath = ViewPath;
            binder.csharpAsset = viewAsset.name;
            binder.uiList = new List<UIBindComponentData>();
            EditorUtility.SetDirty(binder);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.GetFullPath(ReportPath), "{\n  \"artifactRole\": \"diagnostic-execution-report\",\n  \"authoritative\": false,\n  \"stage\": 7,\n  \"prefab\": \"" + PrefabPath + "\",\n  \"csharpAssetPath\": \"" + ViewPath + "\",\n  \"uiBinderCount\": 1,\n  \"runtimeMemberCount\": 0,\n  \"memberSelection\": \"pending-user-approval\",\n  \"scrimAlpha\": 150\n}\n");
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[AIUI] PausePanel Stage 7 binding initialized: " + PrefabPath);
    }
}
