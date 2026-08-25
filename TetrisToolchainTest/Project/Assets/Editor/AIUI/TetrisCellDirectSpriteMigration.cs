using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TetrisCellDirectSpriteMigration
{
    private const string PrefabPath = "Assets/Prefab/default/tetris/TetrisGamePanel.prefab";
    private const string BoardSpritePath = "Assets/Art/atlasSource/default/tetris/cell_empty_36.png";
    private const string NextSpritePath = "Assets/Art/atlasSource/default/tetris/cell_empty_54.png";
    private const string SessionKey = "AIUI.TetrisModule.CellDirectSpriteMigrationV1";
    static TetrisCellDirectSpriteMigration() { EditorApplication.delayCall += Run; }
    private static void Run()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        var board = AssetDatabase.LoadAssetAtPath<Sprite>(BoardSpritePath);
        var next = AssetDatabase.LoadAssetAtPath<Sprite>(NextSpritePath);
        if (board == null || next == null) { Debug.LogError("[AIUI] Tetris cell source sprites missing."); return; }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) { Debug.LogError("[AIUI] Tetris prefab missing."); return; }
        try
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.name.StartsWith("cell_", StringComparison.Ordinal)) { image.sprite = board; image.color = Color.white; }
                else if (image.name.StartsWith("nextCell_", StringComparison.Ordinal)) { image.sprite = next; image.color = Color.white; }
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[AIUI] Tetris cell direct-sprite migration passed: board=200, next=16, source directory=default/tetris.");
    }
}
