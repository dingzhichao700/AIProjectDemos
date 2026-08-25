using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class AddressablesAutoBuilder
{
    private const string ART_ROOT = "Assets/Art";
    private const string PREFAB_ROOT = "Assets/Prefab";
    private const string CONFIG_BIN_ROOT = "Assets/ConfigBin";

    private static readonly HashSet<string> ValidArtTypes = new()
    {
        "atlas",
        "unpack",
        "audio",
        "frameAnimation",
        "material"
    };

    /// <summary>Step 1：创建/链接 Settings + 五组。</summary>
    [MenuItem("Tools/Addressables/Initialize Addressables")]
    public static void InitializeAddressables()
    {
        AddressablesProjectBootstrap.EnsureReady(log: true);
        FrameworkSetup.EnsureAddressablesOnly();
    }

    /// <summary>Step 2：扫描 Prefab/Art/ConfigBin，登记 entry（address = 资产路径）。</summary>
    [MenuItem("Tools/Addressables/Build Game Addressables")]
    public static void BuildGameAddressables()
    {
        AssignGameAddressables();
    }

    /// <summary>Step 3：BuildPlayerContent，生成 catalog / bundle（Play 必需）。</summary>
    [MenuItem("Tools/Addressables/Build Player Content")]
    public static void BuildPlayerContentMenu()
    {
        BuildPlayerContent();
    }

    /// <summary>Step 2 + 3 一键执行。</summary>
    [MenuItem("Tools/Addressables/Build All (Assign + Player Content)")]
    public static void BuildAllMenu()
    {
        AssignGameAddressables();
        BuildPlayerContent();
    }

    public static void AssignGameAddressables()
    {
        AddressablesProjectBootstrap.EnsureReady(log: true);

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found! Run Tools/Addressables/Initialize Addressables first.");
            return;
        }

        ProcessConfigBin(settings);
        ProcessRoot(settings, PREFAB_ROOT);

        foreach (var artType in ValidArtTypes)
        {
            string artTypePath = $"{ART_ROOT}/{artType}";
            if (Directory.Exists(artTypePath))
            {
                ProcessRoot(settings, artTypePath);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Game Addressables assigned (entries use full asset paths).");
    }

    public static void BuildPlayerContent()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found! Run Initialize Addressables first.");
            return;
        }

        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error))
        {
            Debug.LogError($"Addressables BuildPlayerContent failed: {result.Error}");
            return;
        }

        Debug.Log($"Addressables player content built. Duration={result.Duration}s Output={result.OutputPath}");
    }

    #region ConfigBin

    private static void ProcessConfigBin(AddressableAssetSettings settings)
    {
        if (!Directory.Exists(CONFIG_BIN_ROOT))
        {
            return;
        }

        var targetGroup = GetGroup(settings, "title");
        ConfigureGroup(targetGroup, "title");
        AddAssetsRecursively(settings, targetGroup, CONFIG_BIN_ROOT, null);
    }

    #endregion

    #region Core

    private static void ProcessRoot(AddressableAssetSettings settings, string rootPath)
    {
        foreach (var groupDir in Directory.GetDirectories(rootPath))
        {
            string logicalGroupName = Path.GetFileName(groupDir);

            if (!IsValidGroup(logicalGroupName))
            {
                continue;
            }

            AddressableAssetGroup group = GetGroup(settings, logicalGroupName);
            ConfigureGroup(group, logicalGroupName);

            if (AddressAblesConst.PACK_TOGETHER_GROUPS.Contains(logicalGroupName))
            {
                AddAssetsRecursively(settings, group, groupDir, null);
            }
            else
            {
                foreach (var labelDir in Directory.GetDirectories(groupDir))
                {
                    string label = Path.GetFileName(labelDir);
                    AddAssetsRecursively(settings, group, labelDir, label);
                }
            }
        }
    }

    private static void AddAssetsRecursively(AddressableAssetSettings settings, AddressableAssetGroup group,
        string rootPath, string label)
    {
        foreach (string file in Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta"))
            {
                continue;
            }

            string assetPath = file.Replace("\\", "/");
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = assetPath;

            if (!string.IsNullOrEmpty(label))
            {
                entry.SetLabel(label, true, true);
            }
        }
    }

    #endregion

    #region Group Mapping

    private static AddressableAssetGroup GetGroup(AddressableAssetSettings settings, string logicalGroupName)
    {
        if (logicalGroupName == "default")
        {
            return settings.DefaultGroup;
        }

        var group = settings.FindGroup(logicalGroupName);
        if (group != null)
        {
            return group;
        }

        return settings.CreateGroup(logicalGroupName, false, false, false, null,
            typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
    }

    private static void ConfigureGroup(AddressableAssetGroup group, string logicalGroupName)
    {
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        schema.BundleMode = AddressAblesConst.PACK_TOGETHER_GROUPS.Contains(logicalGroupName)
            ? BundledAssetGroupSchema.BundlePackingMode.PackTogether
            : BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
        schema.UseAssetBundleCache = true;
        schema.UseAssetBundleCrc = true;
    }

    private static bool IsValidGroup(string groupName)
    {
        return AddressAblesConst.PACK_TOGETHER_GROUPS.Contains(groupName)
               || AddressAblesConst.PACK_BY_LABEL_GROUPS.Contains(groupName);
    }

    #endregion
}
