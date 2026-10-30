using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>登记场景配置，并通过正式资源加载器验证生成表读取和释放后重载。</summary>
public static class BattleSceneConfigValidation
{
    private const string ResultPath = "Library/battle-scene-config-validation.txt";

    public static async void RunBatch()
    {
        try
        {
            RegisterTables();
            AddressablesAutoBuilder.BuildPlayerContent();
            await ValidateLoading();
            File.WriteAllText(ResultPath, "passed: Addressables -> ResourceLoader -> CfgManager; scene 1001; 171 cells; 13 appearances; concurrent load and release/reload");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText(ResultPath, "failed: " + exception);
            EditorApplication.Exit(1);
        }
    }

    private static void RegisterTables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("configData");
        if (group == null)
        {
            throw new InvalidOperationException("Missing configData group.");
        }
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null || schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
        {
            throw new InvalidOperationException("configData must use PackSeparately.");
        }
        foreach (string name in new[] { "cfgobj_battlesceneobj", "cfgobj_battlescenecellobj" })
        {
            string path = ResourceConst.PATH_CONFIG + name + ".json";
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid) || AssetDatabase.LoadAssetAtPath<TextAsset>(path) == null || !ConfigManifest.Names.Contains(name))
            {
                throw new InvalidOperationException("Missing configuration or preload entry: " + path);
            }
            settings.CreateOrMoveEntry(guid, group).address = path;
        }
        EditorUtility.SetDirty(group);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static async Task ValidateLoading()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            CfgManager.Clear();
            await Task.WhenAll(CfgManager.EnsureLoadedAsync(), CfgManager.EnsureLoadedAsync());
            var scene = CfgManager.tables.BattleSceneObj.Get(1001);
            var cells = CfgManager.tables.BattleSceneCellObj.DataList.Where(cell => cell.SceneId == scene.Id).ToList();
            if (scene.Appearances.Count != 13 || cells.Count != 171 || scene.FriendlyBaseQ != 0 || scene.FriendlyBaseK != 10 || scene.EnemyBaseK != -10)
            {
                throw new InvalidOperationException("Scene 1001 configuration differs from the approved fixture.");
            }
            if (cells.Select(cell => (cell.Q, cell.K)).Distinct().Count() != cells.Count || cells.Any(cell => (cell.K - cell.Q) % 2 != 0))
            {
                throw new InvalidOperationException("Duplicate or invalid cell coordinates.");
            }
            var first = scene.Appearances[0];
            if (first.Cells.Count != 1 || first.Cells[0].Q != -5 || first.Cells[0].K != -15 || first.Anchor.Q != -5 || first.Offset.X != 0.25f)
            {
                throw new InvalidOperationException("Nested coordinates or offset failed to deserialize.");
            }
        }
        CfgManager.Clear();
    }
}
