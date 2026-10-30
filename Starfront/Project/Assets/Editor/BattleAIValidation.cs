using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using SimpleJSON;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

[InitializeOnLoad]
public static class BattleAIValidation {
    private const string RequestPath = "Library/battle-ai-validation-request.json";
    [Serializable] private sealed class Request { public string requestId; public int processId; }
    [Serializable] private sealed class Result { public string requestId; public string status; public string details; }
    static BattleAIValidation() {
        EditorApplication.update += Poll;
    }
    private static void Poll() {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
        File.Delete(RequestPath);
        var result = new Result { requestId = request?.requestId, status = "failed" };
        try {
            Check(request != null && request.processId == System.Diagnostics.Process.GetCurrentProcess().Id && !string.IsNullOrEmpty(request.requestId), "request ownership");
            RegisterConfig();
            WithTables(null, () => ValidateLoaded());
            result.status = "passed";
            result.details = "AI timing/one action; economy independence; priorities; fairness; human/friendly AI; invalid profile; seeded 180-second battles; teardown; config registration";
        } catch (Exception e) {
            result.details = e.ToString();
            Debug.LogError(e);
        }
        File.WriteAllText("Library/battle-ai-validation-result.json", JsonUtility.ToJson(result, true));
    }
    private static void RegisterConfig() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("configData");
        Check(group != null && group.GetSchema<BundledAssetGroupSchema>().BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately, "configData separate packing");
        string path = "Assets/ConfigBin/cfgobj_battleaiprofileobj.json";
        Check(ConfigManifest.Names.Contains("cfgobj_battleaiprofileobj") && AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null, "preload manifest and data");
        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = path;
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(group);
        AssetDatabase.SaveAssets();
    }
    private static object Call(object target, string method, params object[] args) {
        return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
    private static void Check(bool value, string message) {
        if (!value) {
            throw new InvalidOperationException("AI validation: " + message);
        }
    }
    private static void WithTables(Action<string, JSONNode> mutate, Action test) {
        var field = typeof(CfgManager).GetField("_tables", BindingFlags.Static | BindingFlags.NonPublic);
        var before = field.GetValue(null);
        try {
            var tables = new Tables(name => {
                var data = JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json"));
                mutate?.Invoke(name, data);
                return data;
            });
            field.SetValue(null, tables);
            test();
        } finally {
            field.SetValue(null, before);
        }
    }
    private static BattleSceneModel Create(int id = 1001, int seed = 7) {
        var t = CfgManager.tables;
        var scene = new BattleSceneModel(t.BattleSceneObj.Get(id), t.BattleSceneCellObj.DataList);
        try {
            scene.InitializeBattle(t, seed);
            return scene;
        } catch {
            scene.Dispose();
            throw;
        }
    }
    private static void Step(BattleSceneModel scene, double seconds, BattleSceneCamp camp = BattleSceneCamp.ENEMY) {
        Call(scene.ai, "Advance", camp, (float)seconds);
    }
    public static void ValidateLoaded() {
        foreach (int id in new[] { 1001, 1002 }) {
            var scene = Create(id);
            try {
                Check(scene.ai.states.Count == 1 && !scene.ai.states.ContainsKey(BattleSceneCamp.FRIENDLY), "only configured computer has state");
                var state = scene.ai.states[BattleSceneCamp.ENEMY];
                var human = scene.camps[BattleSceneCamp.FRIENDLY];
                Step(scene, 2.999);
                Check(state.unlockCount == 0 && state.owner.currency == 50, "initial wait");
                Step(scene, .0011);
                Check(state.unlockCount == 1 && state.owner.currency == 0 && human.currency == 50, "first purchase spends only AI money");
                Check(scene.buildings.Values.Count(b => b.camp == state.owner.camp && b.config.Type == BuildingType.UNIT_PRODUCER) == 1, "first barracks guaranteed choice");
                Step(scene, 1.9);
                Check(state.decisionCount == 1, "interval wait");
                Step(scene, .11);
                Check(state.decisionCount == 2 && state.unlockCount == 1, "no funds waits");
                var home = scene.buildings.Values.Single(b => b.camp == state.owner.camp && b.config.Type == BuildingType.BASE);
                home.OnTimeUpdate(25);
                Step(scene, 2);
                Check(scene.buildings.Values.Any(b => b.camp == state.owner.camp && b.config.Type == BuildingType.COLLECTOR), "second purchase collector after real income");
                Call(state.owner, "AddCurrency", 10000L);
                int before = state.unlockCount;
                Step(scene, 1000);
                Check(state.unlockCount == before + 1 && state.remainingMs == 2000, "long frame one action, no backlog");
                Step(scene, .1);
                Check(state.unlockCount == before + 1, "no deferred burst");
                before = state.decisionCount;
                Step(scene, 0);
                Check(state.decisionCount == before, "zero delta pause");
                var next = scene.cells.Values.First(c => scene.unlocks.CanUnlock(c.id, state.owner.camp, out _));
                Call(next, "TrySetCamp", BattleSceneCamp.FRIENDLY);
                Step(scene, 2);
                Check(!next.unlocked, "lost territory cannot be purchased");
                Call(scene, "OnBattleTimeUpdated", 180f);
                before = state.decisionCount;
                Step(scene, 50);
                Check(state.decisionCount == before, "ended game stops AI");
                var ai = scene.ai;
                scene.Dispose();
                Check(ai.states.Count == 0 && scene.ai == null, "disposal clears AI");
            } finally {
                scene.Dispose();
            }
        }
        HumanAndConfig();
        FullBattles();
        File.WriteAllText("Library/battle-ai-logic-validation.txt", "passed: both scenes; initial 3s / interval2s / one action; first barracks then collector; owned currency; no-funds wait; long-frame no burst; pause; lost ownership rejected; end/disposal; human/friendly computer modes; invalid config; reserve; no-candidate; seeded full simulations");
    }
    private static void HumanAndConfig() {
        WithTables((name, data) => {
            if (name == "cfgobj_battlestartobj") {
                foreach (var row in data.Children) {
                    row["controlType"] = (int)BattlePlayerControlType.HUMAN;
                    row["aiProfileId"] = 0;
                }
            }
        }, () => {
            var scene = Create();
            try {
                Step(scene, 100);
                Check(scene.ai.states.Count == 0 && scene.buildings.Count == 2, "human opponent not automated");
            } finally {
                scene.Dispose();
            }
        });
        WithTables((name, data) => {
            if (name == "cfgobj_battlestartobj") {
                foreach (var row in data.Children) {
                    bool friendly = row["camp"].AsInt == (int)BattleSceneCamp.FRIENDLY;
                    row["controlType"] = (int)(friendly ? BattlePlayerControlType.COMPUTER : BattlePlayerControlType.HUMAN);
                    row["aiProfileId"] = friendly ? 1 : 0;
                }
            }
        }, () => {
            var scene = Create();
            try {
                Step(scene, 3, BattleSceneCamp.FRIENDLY);
                Check(scene.ai.states.ContainsKey(BattleSceneCamp.FRIENDLY) && scene.camps[BattleSceneCamp.ENEMY].currency == 50 && scene.buildings.Count == 3, "computer is independent of enemy identity");
            } finally {
                scene.Dispose();
            }
        });
        WithTables((name, data) => {
            if (name == "cfgobj_battleaiprofileobj") {
                data.AsArray[0]["currencyReserve"] = 50;
            }
        }, () => {
            var scene = Create();
            try {
                Step(scene, 3);
                Check(scene.buildings.Count == 2, "configured reserve blocks spend");
                foreach (var cell in scene.cells.Values.Where(c => c.groundPassable && c.camp == BattleSceneCamp.ENEMY && !c.unlocked)) {
                    Call(cell, "MarkUnlocked");
                }
                Call(scene.camps[BattleSceneCamp.ENEMY], "AddCurrency", 10000L);
                Step(scene, 2);
                Check(scene.ai.states[BattleSceneCamp.ENEMY].unlockCount == 0, "no candidates waits without spending");
            } finally {
                scene.Dispose();
            }
        });
        foreach (int mode in Enumerable.Range(0, 4)) {
            WithTables((name, data) => {
                if (mode == 0 && name == "cfgobj_battleaiprofileobj") {
                    data.AsArray[0]["decisionIntervalMs"] = 0;
                }
                if (name == "cfgobj_battlestartobj") {
                    if (mode == 1) {
                        data.AsArray[0]["controlType"] = 99;
                    }
                    if (mode == 2) {
                        data.AsArray[0]["aiProfileId"] = 1;
                    }
                    if (mode == 3) {
                        data.AsArray[1]["aiProfileId"] = 9999;
                    }
                }
            }, () => {
                bool rejected = false;
                try {
                    var scene = Create();
                    scene.Dispose();
                } catch (InvalidOperationException) {
                    rejected = true;
                }
                Check(rejected, "invalid config rejected mode " + mode);
            });
        }
    }
    private static void FullBattles() {
        foreach (int id in new[] { 1001, 1002 }) {
            foreach (int seed in new[] { 1, 7, 99 }) {
                var scene = Create(id, seed);
                try {
                    // 实际模型的生产、移动、技能和AI事件链；不赠送金币。
                    for (int tick = 0; tick < 1800 && !scene.expired; tick++) {
                        typeof(SceneModel).GetMethod("OnSceneTimeUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scene, new object[] { .1f });
                        if (!scene.expired) {
                            typeof(SceneModel).GetMethod("OnPlayerTimeUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scene, new object[] { .1f });
                            typeof(SceneModel).GetMethod("OnEnemyTimeUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scene, new object[] { .1f });
                        }
                        Check(scene.camps.Values.All(c => c.currency >= 0), "currency never negative");
                        Check(scene.buildings.Values.All(b => b.camp == b.cell.camp), "building owner matches protected cell");
                        Check(scene.GetUnitCount(BattleSceneCamp.ENEMY) <= scene.camps[BattleSceneCamp.ENEMY].unitLimit, "AI respects unit cap");
                    }
                    Check(scene.expired && scene.ai.states[BattleSceneCamp.ENEMY].unlockCount >= 2, "full battle develops and settles " + id + "/" + seed);
                } finally {
                scene.Dispose();
            }
            }
        }
    }
}
