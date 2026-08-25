#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 确保 Addressables Settings 已创建且 EditorBuildSettings 已正确链接（等同 Create Addressables Settings）。
/// </summary>
public static class AddressablesProjectBootstrap
{
    const string DefaultObjectPath = "Assets/AddressableAssetsData/DefaultObject.asset";

    [InitializeOnLoadMethod]
    static void AutoRepairOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                EnsureReady(log: false);
            }
        };
    }

    /// <summary>等同 Window → Addressables → Create Addressables Settings，并补全五组。</summary>
    [MenuItem("Tools/Addressables/Create Addressables Settings")]
    public static void CreateAddressablesSettingsMenu()
    {
        EnsureReady(log: true);
        FrameworkSetup.EnsureAddressablesOnly();
        AssetDatabase.SaveAssets();
        Debug.Log("Addressables Settings created/linked. Next: Build All (Assign + Player Content).");
    }

    public static void EnsureReady(bool log)
    {
        RepairEditorBuildSettingsLink(log);

        if (AddressableAssetSettingsDefaultObject.Settings == null)
        {
            if (log)
            {
                Debug.Log("[Addressables] Creating AddressableAssetSettings...");
            }

            AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
                AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                true,
                true);
        }

        RepairEditorBuildSettingsLink(log);
    }

    static void RepairEditorBuildSettingsLink(bool log)
    {
        if (!File.Exists(DefaultObjectPath))
        {
            return;
        }

        var defaultObject =
            AssetDatabase.LoadAssetAtPath<AddressableAssetSettingsDefaultObject>(DefaultObjectPath);
        if (defaultObject == null)
        {
            return;
        }

        if (EditorBuildSettings.TryGetConfigObject(
                AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName,
                out AddressableAssetSettingsDefaultObject existing) &&
            existing == defaultObject &&
            AddressableAssetSettingsDefaultObject.Settings != null)
        {
            return;
        }

        EditorBuildSettings.AddConfigObject(
            AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName,
            defaultObject,
            true);

        if (log)
        {
            Debug.Log("[Addressables] Linked DefaultObject in EditorBuildSettings.");
        }
    }
}
#endif
