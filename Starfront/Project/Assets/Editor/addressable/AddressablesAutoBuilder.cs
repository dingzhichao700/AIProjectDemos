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
        "material",
        "skeletalAnimation"
    };

    /// <summary>Step 1：创建/链接Settings，按已批准资源状态初始化。</summary>
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
        ResourceGovernanceGate.ValidateOrThrow("addressables-assign");
        LoadRuntimeExclusions();
        AddressablesProjectBootstrap.EnsureReady(log: true);

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found! Run Tools/Addressables/Initialize Addressables first.");
            return;
        }

        if(ResourcePackRouting.Enabled){ResourcePackRouting.AssignDeclared(settings);return;}
        ProcessConfigBin(settings);
        ProcessRoot(settings, PREFAB_ROOT);
        if (Directory.Exists("Assets/ResourceData")) ProcessRoot(settings, "Assets/ResourceData");

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
        ResourceGovernanceGate.ValidateOrThrow("addressables-content");
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found! Run Initialize Addressables first.");
            return;
        }

        AddressablesPrefabDependencyChecker.ValidateOrThrow();

        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error))
        {
            throw new System.InvalidOperationException($"Addressables BuildPlayerContent failed: {result.Error}");
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

        var targetGroup = GetGroup(settings, "configData");
        ConfigureGroup(targetGroup, "configData");
        AddAssetsRecursively(settings, targetGroup, CONFIG_BIN_ROOT, null);
    }

    #endregion

    #region Core

    [System.Serializable] private class RetiredAsset { public string path,guid; }
    [System.Serializable] private class RetirementPolicy { public RetiredAsset[] retiredRuntimeAssets; }
    private static RetirementPolicy runtimeExclusions;
    private static void LoadRuntimeExclusions() {
        runtimeExclusions=JsonUtility.FromJson<RetirementPolicy>(File.ReadAllText("ProjectSettings/ResourceGovernance.json"));
    }
    private static bool IsRetiredRuntimeAsset(string path,string guid) {
        return runtimeExclusions?.retiredRuntimeAssets!=null && System.Array.Exists(runtimeExclusions.retiredRuntimeAssets,entry=>entry.path==path&&entry.guid==guid);
    }

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
            if (IsRetiredRuntimeAsset(assetPath,guid)) { continue; }
            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            // Battle map assets pack by scene ID, not by their shared "battle" parent.
            string mapPrefix = rootPath.Replace('\\', '/') + "/";
            if (group.Name == "scene" && label == "battle" && assetPath.StartsWith(mapPrefix)) {
                string first = assetPath.Substring(mapPrefix.Length).Split('/')[0];
                if (int.TryParse(first, out int mapId)) {
                    var mapEntry = settings.CreateOrMoveEntry(guid, group);
                    mapEntry.address = assetPath;
                    mapEntry.SetLabel("battle", false);
                    mapEntry.SetLabel("scene" + mapId, true, true);
                    continue;
                }
            }
            var entry = settings.FindAssetEntry(guid);
            if (entry == null || (group == settings.DefaultGroup && entry.parentGroup.Name == "scene") || (assetPath.StartsWith(CONFIG_BIN_ROOT + "/") && entry.parentGroup.Name == "title")) {
                entry = settings.CreateOrMoveEntry(guid, group);
            }
            entry.address = assetPath;
            if (entry.parentGroup == settings.DefaultGroup) {
                settings.AddLabel("main-content");
                entry.SetLabel("main-content", true);
            }

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
        // 配置表独立打包，单表更新不会带动其他配置表重新下载。
        schema.BundleMode = logicalGroupName == "configData"
            ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately
            : AddressAblesConst.PACK_TOGETHER_GROUPS.Contains(logicalGroupName)
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
