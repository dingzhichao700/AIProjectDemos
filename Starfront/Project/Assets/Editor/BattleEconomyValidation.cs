using System;
using System.IO;
using System.Linq;
using cfg;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>本轮资源登记和真实入口回归的批处理入口，仅用于自有验证编辑器。</summary>
public static class BattleEconomyValidation {
    public static void RunBatch() {
        try {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var configGroup = settings.FindGroup("configData");
            var sceneGroup = settings.FindGroup("scene");
            Require(configGroup != null && sceneGroup != null, "configuration and scene groups exist");
            Require(configGroup.GetSchema<BundledAssetGroupSchema>().BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately, "configuration bundles remain separate");
            foreach (string name in ConfigManifest.Names) {
                string path = ResourceConst.PATH_CONFIG + name + ".json";
                string guid = AssetDatabase.AssetPathToGUID(path);
                Require(!string.IsNullOrEmpty(guid), "configuration imported: " + path);
                settings.CreateOrMoveEntry(guid, configGroup).address = path;
            }
            const string animationRoot = "Assets/Art/frameAnimation/scene/Colonial/Infantry";
            foreach (string path in Directory.GetFiles(animationRoot)) {
                if (path.EndsWith(".png")) {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
                if (path.EndsWith(".png") || path.EndsWith(".json")) {
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    Require(!string.IsNullOrEmpty(guid), "animation imported: " + path);
                    var entry = settings.CreateOrMoveEntry(guid, sceneGroup);
                    entry.address = path.Replace('\\', '/');
                    entry.SetLabel("Colonial", true, true);
                }
            }
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(configGroup);
            EditorUtility.SetDirty(sceneGroup);
            AssetDatabase.SaveAssets();
            BattleSceneRuntimeValidation.RunBatch();
        } catch (Exception exception) {
            Debug.LogException(exception);
            File.WriteAllText("Library/battle-economy-validation.txt", "failed: " + exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ValidateLoadedConfiguration() {
        ResourcePolicyValidation.Validate();
        Require(CfgManager.tables.BattleStartObj.DataList.Count(start => start.SceneId == BattleConst.InitialSceneId) == 2, "two start records");
        Require(CfgManager.tables.UnitObj.DataList.Count == 5, "four placeholder units");
        Require(CfgManager.tables.UnitObj.DataList.Where(u => u.Id >= 1101 && u.Id <= 1104).All(unit => unit.Appearances.Count == 20 && unit.Appearances.Count(a => a.Action == cfg.UnitActionType.MOVE) == 5 && unit.Appearances.Count(a => a.Action == cfg.UnitActionType.IDLE) == 5 && unit.Appearances.Count(a => a.Action == cfg.UnitActionType.ATTACK_1) == 5 && unit.Appearances.Count(a => a.Action == cfg.UnitActionType.DEATH && !a.Loop) == 5), "four units each have five move, attack, idle and death records");
        var scene = CfgManager.tables.BattleSceneObj.Get(BattleConst.InitialSceneId);
        var model = new BattleSceneModel(scene, CfgManager.tables.BattleSceneCellObj.DataList);
        model.InitializeBattle(CfgManager.tables);
        var friendly = model.camps[BattleSceneCamp.FRIENDLY];
        var enemy = model.camps[BattleSceneCamp.ENEMY];
        var building = model.buildings.Values.Single(item => item.owner == friendly);
        Require(friendly.currency == 50 && enemy.currency == 50, "configured initial currency");
        Require(model.buildings.Count == 2 && model.cells.Values.Count(cell => cell.unlocked) == 2, "only bases unlocked");
        int changes = 0;
        model.CurrencyChanged += owner => changes++;
        building.OnTimeUpdate(4f);
        Require(friendly.currency == 50, "no payout before period");
        building.OnTimeUpdate(1f);
        Require(friendly.currency == 60 && enemy.currency == 50 && changes == 1, "period boundary and independent balances");
        building.OnTimeUpdate(12f);
        Require(friendly.currency == 80 && Math.Abs(building.productionElapsedMs - 2000) < 0.01, "multiple periods retain remainder");
        // 从正式所属 Timer 分发，暂停一方不会冻结另一方。
        float playerScale = RookieEngine.playerTimer.scale;
        float enemyScale = RookieEngine.enemyTimer.scale;
        try {
            model.StartTimeFlow();
            model.StartTimeFlow();
            RookieEngine.playerTimer.scale = 0;
            RookieEngine.enemyTimer.scale = 1;
            RookieEngine.playerTimer.SyncTime(5000);
            RookieEngine.enemyTimer.SyncTime(5000);
            Require(friendly.currency == 80 && enemy.currency == 10 && model.ai.states[BattleSceneCamp.ENEMY].unlockCount == 1, "timer pause and independent enemy flow");
            RookieEngine.enemyTimer.scale = 0;
            int decisions = model.ai.states[BattleSceneCamp.ENEMY].decisionCount;
            RookieEngine.enemyTimer.SyncTime(5000);
            Require(model.ai.states[BattleSceneCamp.ENEMY].decisionCount == decisions, "paused enemy Timer pauses AI");
            RookieEngine.playerTimer.scale = 1;
            RookieEngine.playerTimer.SyncTime(3000);
            Require(friendly.currency == 90, "resume remainder and no duplicate subscription");
            model.StopTimeFlow();
            RookieEngine.playerTimer.SyncTime(5000);
            Require(friendly.currency == 90, "stop removes timer subscription");
        } finally {
            model.StopTimeFlow();
            RookieEngine.playerTimer.scale = playerScale;
            RookieEngine.enemyTimer.scale = enemyScale;
        }
        building.Destroy();
        building.OnTimeUpdate(10f);
        Require(friendly.currency == 90 && building.cell.unlocked, "destroy stops income without relocking cell");
        model.Dispose();
        Require(model.elements.Count == 0 && model.buildings.Count == 0 && model.camps.Count == 0, "dispose clears authoritative state");
        building.OnTimeUpdate(10f);
        Require(friendly.currency == 90, "disposed building remains inactive");
        File.WriteAllText("Library/battle-economy-validation.txt", "passed: 16 configuration tables; union start keys; 20 movement appearances; two bases; exact period boundaries; independent balances; multi-period remainder; player/enemy Timer isolation; pause/resume; duplicate subscription guard; offscreen income; stop/dispose; destroyed base retains unlocked cell");
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("Battle economy validation: " + message);
        }
    }
}
