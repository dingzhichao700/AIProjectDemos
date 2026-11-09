using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 确保连接面板预制已登记到 Addressables。
/// </summary>
public static class ConnectPanelSetup {

    const string PrefabPath = "Assets/Prefab/title/connect/ConnectPanel.prefab";

    [MenuItem("Tools/Starfront/Ensure ConnectPanel Addressable")]
    public static void EnsureAddressable() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) {
            Debug.LogError("找不到预制: " + PrefabPath);
            return;
        }
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) {
            Debug.LogError("AddressableAssetSettings 不存在，先执行 Tools/Addressables/Initialize Addressables");
            return;
        }
        string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
        AddressableAssetEntry entry = settings.FindAssetEntry(guid);
        if (entry == null) {
            AddressableAssetGroup group = settings.FindGroup("title");
            if (group == null) {
                group = settings.DefaultGroup;
            }
            entry = settings.CreateOrMoveEntry(guid, group);
        }
        entry.SetAddress(PrefabPath);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();
        Debug.Log("ConnectPanel 已登记 Addressables: " + PrefabPath);
    }

}
