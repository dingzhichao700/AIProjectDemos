using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

[InitializeOnLoad]
public static class PausePanelModuleMigration
{
    private const string PrefabPath = "Assets/Prefab/default/tetris/PausePanel.prefab";
    private const string Address = "default/tetris/PausePanel";
    private const string SessionKey = "AIUI.PausePanel.ModuleMigration.TetrisV1";
    static PausePanelModuleMigration() { EditorApplication.delayCall += Migrate; }
    private static void Migrate()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var guid = AssetDatabase.AssetPathToGUID(PrefabPath);
        if (settings == null || string.IsNullOrEmpty(guid)) return;
        var group = settings.FindGroup("Default Local Group") ?? settings.DefaultGroup;
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = Address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[AIUI] PausePanel migrated to tetris module: " + Address);
    }
}
