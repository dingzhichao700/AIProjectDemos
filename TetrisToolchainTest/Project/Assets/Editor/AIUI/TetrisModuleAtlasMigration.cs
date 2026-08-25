using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TetrisModuleAtlasMigration
{
    private const string SessionKey = "AIUI.TetrisModule.AtlasMigrationV4";
    static TetrisModuleAtlasMigration() { EditorApplication.delayCall += Run; }
    private static void Run()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        try
        {
            TetrisGamePanelCellBitmapUpdater.ApplyAndValidate();
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[AIUI] Tetris module atlas migration passed: all cell sources use default/tetris and atlas tetris.png.");
        }
        catch (System.Exception error) { Debug.LogError("[AIUI] Tetris module atlas migration failed: " + error); }
    }
}
