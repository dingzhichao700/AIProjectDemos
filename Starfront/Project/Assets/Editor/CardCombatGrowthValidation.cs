using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using cfg;
using SimpleJSON;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>定向验证真实升级、入场快照、生产、移动及弹道命中；不改玩家存档。</summary>
[InitializeOnLoad]
public static class CardCombatGrowthValidation {
    [Serializable] private class Request { public string requestId; public int processId; }
    [Serializable] private class Result { public string requestId; public string status; public string error; public string report; }
    private const string Key = "CardCombatGrowthValidation";
    [Serializable] private class SceneBackup { public SceneSetup[] scenes; }
    static CardCombatGrowthValidation() {
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.update += Poll;
    }
    private static void Poll() {
        if (EditorApplication.isPlaying && !string.IsNullOrEmpty(SessionState.GetString(Key, ""))) {
            EditorApplication.QueuePlayerLoopUpdate();
        }
        const string path = "Library/AIUI/pending-card-combat-growth.json";
        if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) {
            return;
        }
        Request request = null;
        try {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(path).TrimStart('\uFEFF'));
            File.Delete(path);
            if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId)) {
                throw new InvalidOperationException("request identity mismatch");
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (string name in ConfigManifest.Names) {
                string asset = ResourceConst.PATH_CONFIG + name + ".json";
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(asset), settings.FindGroup("configData")).address = asset;
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                if (SceneManager.GetSceneAt(i).isDirty) {
                    throw new InvalidOperationException("Unsaved scene; cannot enter isolated Play");
                }
            }
            SessionState.SetString(Key + ".scenes", JsonUtility.ToJson(new SceneBackup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetInt(Key + ".mode", settings.ActivePlayModeDataBuilderIndex);
            settings.ActivePlayModeDataBuilderIndex = 0;
            SessionState.SetString(Key, request.requestId);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        } catch (Exception error) {
            Debug.LogException(error);
            File.WriteAllText("Library/AIUI/card-combat-growth-result.json", JsonUtility.ToJson(new Result { requestId = request == null ? "unreadable" : request.requestId, status = "failed", error = error.ToString() }, true));
        }
    }
    private static async void OnPlayState(PlayModeStateChange state) {
        string requestId = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(requestId)) {
            return;
        }
        if (state == PlayModeStateChange.EnteredEditMode) {
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".mode", 0);
            var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + ".scenes", ""));
            EditorSceneManager.RestoreSceneManagerSetup(backup.scenes);
            SessionState.EraseString(Key);
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode) {
            return;
        }
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        try {
            new GameObject("CardCombatGrowthDispatcher", typeof(UnityMainThreadDispatcher));
            ResourcePresentation.Install(new GameResourcePresentationPolicy());
            var configs = CfgManager.EnsureLoadedAsync();
            if (await System.Threading.Tasks.Task.WhenAny(configs, System.Threading.Tasks.Task.Delay(45000)) != configs) {
                throw new TimeoutException("Configuration load timed out");
            }
            await configs;
            var requests = CfgManager.tables.UnitObj.DataList.SelectMany(u => u.Appearances)
                .Select(a => ResourceConst.GetFrameAnimationPath(a.Resource)).Distinct()
                .Select(path => new ResLoadInfo(path, ResType.FrameAnim)).ToList();
            var resources = ResourceLoader.LoadListAsync(requests, maxConcurrent: 4);
            if (await System.Threading.Tasks.Task.WhenAny(resources, System.Threading.Tasks.Task.Delay(45000)) != resources) {
                throw new TimeoutException("Animation load timed out");
            }
            await resources;
            Run();
            File.WriteAllText("Library/AIUI/card-combat-growth-result.json", JsonUtility.ToJson(new Result { requestId = requestId, status = "passed", report = "Library/AIUI/card-combat-growth-validation.txt" }, true));
        } catch (Exception error) {
            Debug.LogException(error);
            File.WriteAllText("Library/AIUI/card-combat-growth-result.json", JsonUtility.ToJson(new Result { requestId = requestId, status = "failed", error = error.ToString() }, true));
        } finally {
            Application.runInBackground = background;
            EditorApplication.isPlaying = false;
        }
    }
    private static object Call(object target, string name, params object[] args) {
        return target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
    private static void Check(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("Card combat growth: " + message);
        }
    }
    private static BattleSceneModel Scene(Tables tables) {
        var model = new BattleSceneModel(tables.BattleSceneObj.Get(1001), tables.BattleSceneCellObj.DataList);
        try {
            model.InitializeBattle(tables, 7);
            return model;
        } catch {
            model.Dispose();
            throw;
        }
    }
    private static BattleBuildingVO Producer(BattleSceneModel scene, BattleSceneCamp camp) {
        return (BattleBuildingVO)typeof(BattleUnitProductionValidation).GetMethod("BuildProducer", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { scene, camp });
    }
    public static void Run() {
        var configField = typeof(CfgManager).GetField("_tables", BindingFlags.NonPublic | BindingFlags.Static);
        object previousTables = configField.GetValue(null);
        var fields = new[] { "_timer", "_sceneTimer", "_playerTimer", "_enemyTimer" }.Select(n => typeof(RookieEngine).GetField(n, BindingFlags.NonPublic | BindingFlags.Static)).ToArray();
        var previousTimers = fields.Select(f => f.GetValue(null)).ToArray();
        var tables = new Tables(n => JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + n + ".json")));
        configField.SetValue(null, tables);
        typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        foreach (var field in fields) {
            field.SetValue(null, new Timer());
        }
        string storePath = "Library/AIUI/CardCombatGrowth/" + Guid.NewGuid().ToString("N") + "/player.json";
        var store = new PlayerProgressStore(storePath);
        var session = new PlayerProgressControl(tables, null, store.Save);
        var collection = session.Cards.model;
        var previousCollection = PlayerProgressControl.InstallValidationSession(session);
        var report = new List<string>();
        try {
            Check(tables.CardBuildingLevelObj.DataList.Count == 3 && tables.CardUnitLevelObj.DataList.Count == 3, "generated level records");
            var buildHp = new[] { 300d, 330d, 365d };
            var unitHp = new[] { 100d, 110d, 122d };
            var intervals = new[] { 10000, 9500, 9000 };
            var skills = new[] { 1101, 110102, 110103 };
            var damage = new[] { 20d, 22d, 25d };
            var cooldown = new[] { 1000, 950, 900 };
            for (int level = 1; level <= 3; level++) {
                if (level > 1) {
                    var before = collection.GetState(1101);
                    Check(session.Inventory.TryGrantItem(3101, before.requiredFragments, out _), "grant upgrade cost");
                    before = collection.GetState(1101);
                    Check(collection.TryUpgrade(1101, before.level, before.revision, out string reason), "manual upgrade " + level + ": " + reason);
                    Check(new PlayerProgressControl(tables, store.Load(), store.Save).Cards.model.GetState(1101).level == level, "persisted upgrade " + level);
                }
                var scene = Scene(tables);
                try {
                    Check(scene.camps[BattleSceneCamp.FRIENDLY].cardLevels[1101] == level && scene.camps[BattleSceneCamp.ENEMY].cardLevels[1101] == 1, "independent camp snapshots");
                    var producer = Producer(scene, BattleSceneCamp.FRIENDLY);
                    Check(producer.maxHp == buildHp[level - 1] && producer.hp == producer.maxHp && producer.cardLevel == level && producer.sourceCardId == 1101, "building max/current hp and source");
                    Check(producer.productionIntervalMs == intervals[level - 1] && producer.skillIds.Count == 0, "building period and empty skills");
                    producer.OnTimeUpdate((intervals[level - 1] - 10) / 1000f);
                    Check(scene.GetUnitCount(BattleSceneCamp.FRIENDLY) == 0 && producer.productionProgress < 1, "no early spawn");
                    producer.OnTimeUpdate(.02f);
                    var unit = scene.elements.Values.OfType<BattleUnitVO>().Single();
                    Check(unit.maxHp == unitHp[level - 1] && unit.hp == unit.maxHp && unit.cardLevel == level && unit.sourceCardId == 1101, "produced unit hp and inherited level");
                    Check(unit.moveSpeed == 29 + level && unit.skills.Single().config.Id == skills[level - 1], "speed and level skill");
                    var start = unit.position;
                    unit.OnTimeUpdate(1);
                    Check(Math.Abs(Vector2.Distance(start, unit.position) - unit.moveSpeed) < .01, "actual movement distance");
                    Call(unit, "StopMoving");
                    var target = new BattleUnitVO(900001, tables.UnitObj.Get(1101), BattleSceneCamp.ENEMY, unit.position + Vector2.right * 100);
                    scene.AddElement(target);
                    Call(scene.combat.skills, "Advance", unit, target, 10d, false);
                    Call(scene.combat.skills, "Advance", unit, target, 81d, true);
                    Call(scene.combat.projectiles, "Advance", BattleSceneCamp.FRIENDLY, 1000d);
                    Check(target.hp == 100 - damage[level - 1], "actual projectile hit damage");
                    Check(Math.Abs(unit.skills.Single().readyAtMs - (10 + cooldown[level - 1])) < .001, "actual cooldown scheduling");
                    Check(session.Inventory.TryGrantItem(3101, 100, out _), "grant during battle");
                    var blocked = collection.GetState(1101);
                    long fragments = blocked.fragments;
                    Check(!blocked.canUpgrade && !collection.TryUpgrade(1101, blocked.level, blocked.revision, out string blockedReason) && blockedReason.Contains("战斗"), "in-battle upgrade rejected");
                    Check(collection.GetState(1101).fragments == fragments && scene.camps[BattleSceneCamp.FRIENDLY].cardLevels[1101] == level, "no charge and frozen snapshot");
                    // Subsequent production also inherits the same snapshot.
                    producer.OnTimeUpdate(producer.productionIntervalMs / 1000f);
                    Check(scene.elements.Values.OfType<BattleUnitVO>().Where(u => u.camp == BattleSceneCamp.FRIENDLY).All(u => u.cardLevel == level), "later spawns retain snapshot");
                    var enemy = Producer(scene, BattleSceneCamp.ENEMY);
                    Check(enemy.maxHp == 300 && enemy.productionIntervalMs == 10000, "opponent stays configured initial level");
                    Check(BattleSceneResources.Collect(scene).Count > 0, "level skills participate in dependency collection");
                    report.Add("PASS Lv" + level + ": building HP=" + producer.maxHp + ", spawn=" + producer.productionIntervalMs + "ms; unit HP=" + unit.maxHp + ", speed=" + unit.moveSpeed + "; skill=" + skills[level - 1] + ", actual hit=" + damage[level - 1] + ", cooldown=" + cooldown[level - 1] + "ms; frozen source; opponent Lv1; blocked upgrade without spending.");
                } finally {
                    scene.Dispose();
                }
                Check(collection.GetState(1101).canUpgrade, "leaving battle releases upgrade restriction");
            }
            var state = collection.GetState(1101);
            Check(collection.TryUpgrade(1101, state.level, state.revision, out string ignored), "fixture advance to missing level4");
            bool rejected = false;
            try {
                Scene(tables).Dispose();
            } catch (InvalidOperationException error) {
                rejected = error.Message.Contains("等级配置");
            }
            Check(rejected && collection.GetState(1101).canUpgrade, "missing combat level rejected without leaked battle lock");
            report.Add("PASS missing level4 rejects entry; no fallback or leaked lock. Actual player save untouched; isolated save=" + storePath);
            CardGrowthValidation.ValidateCore(tables);
            // Existing low-level regressions use an isolated new-player level1.
            PlayerProgressControl.InstallValidationSession(new PlayerProgressControl(tables, null, _ => { }));
            BattleUnitProductionValidation.ValidateLogic();
            BattleSkillValidation.Validate();
            BattleProjectileValidation.Validate();
            report.Add("PASS existing collection transactions, production/population, skill stages and projectile regressions.");
            File.WriteAllLines("Library/AIUI/card-combat-growth-validation.txt", report);
        } finally {
            PlayerProgressControl.InstallValidationSession(previousCollection);
            configField.SetValue(null, previousTables);
            typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            for (int i = 0; i < fields.Length; i++) {
                fields[i].SetValue(null, previousTimers[i]);
            }
        }
    }
}


