using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using SimpleJSON;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>当前编辑器内的隔离战斗回归；不修改存档或线上配置。</summary>
[InitializeOnLoad]
public static class BattleCombatValidation {
    private const string RequestPath = "Library/battle-combat-validation-request.json";
    [Serializable]
    private sealed class Request {
        public string requestId;
        public int processId;
    }
    [Serializable]
    private sealed class Result {
        public string requestId;
        public string status;
        public string details;
    }
    static BattleCombatValidation() {
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
            BattleUnitActionValidation.RegisterAssets();
            BattleProjectileValidation.ImportAssets();
            Validate();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("scene");
            Check(group != null, "scene Addressables group");
            foreach (string file in new[] { "HP_BG_37", "HP_BG_61", "HP_Fill_Friendly_37", "HP_Fill_Friendly_61", "HP_Fill_Enemy_37", "HP_Fill_Enemy_61" }) {
                string path = "Assets/Art/unpack/scene/battle/appearance/" + file + ".png";
                Check(AssetDatabase.LoadAssetAtPath<Sprite>(path) != null, "health sprite " + file);
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).SetAddress(path);
            }
            AssetDatabase.SaveAssets();
            result.status = "passed";
            result.details = "configuration; targeting/lock; range; cooldown/long frame; friendly fire; death once; population refill; building removal; base victory; territory win/loss/draw; navigation without oscillation; health resource import/registration";
        } catch (Exception e) {
            result.details = e.ToString();
            Debug.LogError(e);
        }
        File.WriteAllText("Library/battle-combat-validation-result.json", JsonUtility.ToJson(result, true));
    }
    private static object Call(object target, string method, params object[] args) {
        return target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
    private static void Check(bool value, string label) {
        if (!value) {
            throw new InvalidOperationException("Combat validation: " + label);
        }
    }
    private static BattleSceneModel Create() {
        var t = CfgManager.tables;
        var model = new BattleSceneModel(t.BattleSceneObj.Get(1001), t.BattleSceneCellObj.DataList);
        model.InitializeBattle(t, 7);
        return model;
    }
    private static BattleUnitVO Unit(BattleSceneModel model, long id, BattleSceneCamp camp, Vector2 point) {
        var unit = new BattleUnitVO(id, CfgManager.tables.UnitObj.Get(1101), camp, point);
        model.AddElement(unit);
        return unit;
    }
    private static void Advance(BattleSceneModel model, float seconds) {
        Call(model.combat, "Advance", BattleSceneCamp.FRIENDLY, seconds);
    }
    public static void Validate() {
        var field = typeof(CfgManager).GetField("_tables", BindingFlags.NonPublic | BindingFlags.Static);
        object previous = field.GetValue(null);
        try {
            field.SetValue(null, new Tables(name => JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json"))));
            typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Check(CfgManager.tables.UnitObj.DataList.Where(u => u.Id >= 1101 && u.Id <= 1104).All(u => u.MaxHp == 100 && u.SkillIds.Count == 1 && u.SkillIds[0] == 1101), "unit values");
            Check(BattleConst.TargetScanMs == 200 && BattleConst.RepathMs == 500, "constant values");
            TargetAndDamage();
            ProductionAndVictory();
            TimeoutAndPath();
            BattleTerritoryValidation.Validate();
            BattleSkillValidation.Validate();
            BattleProjectileValidation.Validate();
        } finally {
            field.SetValue(null, previous);
            typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
    }
    public static void ValidateHealthViews() {
        var model = Create();
        var root = new GameObject("CombatHealthValidation", typeof(RectTransform)).GetComponent<RectTransform>();
        var lod = new GameObject("LOD", typeof(RectTransform)).GetComponent<RectTransform>();
        lod.SetParent(root, false);
        var view = new BattleHealthView(model, root, lod);
        try {
            var target = Unit(model, 100001, BattleSceneCamp.FRIENDLY, new Vector2(300, 700));
            var enemy = model.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY);
            Call(model.combat, "Damage", enemy, target, 20d);
            view.Refresh(new Rect(0, 0, 2000, 2000), 1);
            Check(view.visibleCount == 3, "health bars for units and buildings");
            var bar = lod.Find("health_" + target.id);
            var fill = bar.Find("Fill").GetComponent<UnityEngine.UI.Image>();
            var bg = bar.Find("Background").GetComponent<UnityEngine.UI.Image>();
            float ground = model.config.GroundThickness;
            Check(target.visual.NormalHeight == 44 && BattleConst.UnitHealthGap == 6 && BattleConst.BuildingHealthOffset == 55, "configured visual height, gap and building offset");
            Check(Math.Abs(bar.position.y - bg.rectTransform.rect.height / 2 - (-target.position.y + ground + 44 + 6)) < .001f, "unit health lower edge is 6 above normal head");
            var buildingBar = lod.Find("health_" + enemy.id);
            Check(Math.Abs(buildingBar.position.y - (-enemy.position.y + ground + 55)) < .001f, "building fixed 55 above ground");
            var stablePosition = bar.position;
            foreach (var heading in new[] { Vector2.up, Vector2.left, Vector2.right, Vector2.down }) {
                Call(target, "Face", target.position + heading);
                view.Refresh(new Rect(0, 0, 2000, 2000), 1);
                Check(Vector3.Distance(bar.position, stablePosition) < .001f, "direction does not shift health bar");
            }
            var beforeHeight = bar.position;
            Call(target, "SetAltitude", 40f);
            view.Refresh(new Rect(0, 0, 2000, 2000), 1);
            Check(Vector3.Distance(bar.position - beforeHeight, Vector3.up * 40) < .001f, "health follows visual altitude");
            Call(target, "SetAltitude", 0f);
            Check(Math.Abs(fill.fillAmount - .8f) < .001f, "health fill reads authoritative hp");
            view.Refresh(new Rect(-10000, -10000, 1, 1), 1);
            Check(view.visibleCount == 0 && target.alive && target.hp == 80, "health culling leaves logic unchanged");
            view.Refresh(new Rect(0, 0, 2000, 2000), 2);
            Check(view.visibleCount == 3 && lod.Find("health_" + target.id).localScale.x == 2, "health pooling and zoom");
            Call(model.combat, "Damage", enemy, target, 80d);
            view.Refresh(new Rect(0, 0, 2000, 2000), 1);
            Check(view.visibleCount == 2, "death recycles health bar");
        } finally {
            view.Dispose();
            Check(root.GetComponentsInChildren<UnityEngine.UI.Image>(true).All(i => i.sprite == null), "health disposal releases sprite refs");
            UnityEngine.Object.Destroy(root.gameObject);
            model.Dispose();
        }
        File.WriteAllText("Library/battle-combat-health-validation.txt", "passed: configured normal height44; lower-edge gap6; building offset55; stable directions; altitude40; preloaded sprites; unit/building HP; damage fill 0.8; viewport recycling; zoom once; death recycling; reference cleanup");
    }
    private static void TargetAndDamage() {
        var model = Create();
        try {
            var enemyBase = model.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY);
            var attacker = Unit(model, 100001, BattleSceneCamp.FRIENDLY, enemyBase.position);
            var target = Unit(model, 100002, BattleSceneCamp.ENEMY, enemyBase.position + Vector2.right * 10);
            int died = 0;
            model.combat.Changed += change => { if (change.type == BattleCombatEventType.Died) { died++; } };
            Advance(model, .01f);
            Check(target.hp == 100, "cast starts before projectile hit");
            Advance(model, .1f);
            Check(attacker.targetId == target.id && target.hp == 80 && enemyBase.hp == enemyBase.maxHp, "unit before closer building; first hit");
            var closer = Unit(model, 100003, BattleSceneCamp.ENEMY, attacker.position);
            Advance(model, .5f);
            Check(attacker.targetId == target.id && target.hp == 80, "stable lock and cooldown");
            Advance(model, .5f);
            Check(target.hp == 60, "exact interval");
            Advance(model, 3f);
            Check(target.destroyed && !model.elements.ContainsKey(target.id) && died == 1, "long frame kills once");
            Check(!(bool)Call(model.combat, "Damage", attacker, target, 20d), "dead duplicate rejected");
            Check(!(bool)Call(model.combat, "Damage", attacker, attacker, 20d), "friendly fire rejected");
            Advance(model, .4f);
            Advance(model, .01f);
            Check(attacker.targetId == closer.id, "retarget after death");
            model.RemoveElement(closer.id);
            model.RemoveElement(attacker.id);
            attacker = Unit(model, 100004, BattleSceneCamp.FRIENDLY, enemyBase.position + Vector2.right * 181);
            Advance(model, 10f);
            Check(enemyBase.hp == enemyBase.maxHp, "outside attack range does not damage");
        } finally {
            model.Dispose();
        }
    }
    private static void ProductionAndVictory() {
        var model = Create();
        try {
            var producer = (BattleBuildingVO)typeof(BattleUnitProductionValidation).GetMethod("BuildProducer", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { model, BattleSceneCamp.FRIENDLY });
            producer.OnTimeUpdate(1010);
            Check(model.GetUnitCount(BattleSceneCamp.FRIENDLY) == 100 && producer.productionProgress == 1, "full producer waits");
            var victim = model.elements.Values.OfType<BattleUnitVO>().First();
            var attacker = Unit(model, 100001, BattleSceneCamp.ENEMY, victim.position);
            Call(model.combat, "Damage", attacker, victim, 100d);
            Check(victim.destroyed && model.GetUnitCount(BattleSceneCamp.FRIENDLY) == 100 && producer.productionProgress == 0, "death refills one slot");
            Call(model.combat, "Damage", attacker, producer, 10000d);
            Check(producer.destroyed && !model.buildings.ContainsKey(producer.id), "destroyed building removed");
            int count = model.GetUnitCount(BattleSceneCamp.FRIENDLY);
            producer.OnTimeUpdate(100);
            Check(model.GetUnitCount(BattleSceneCamp.FRIENDLY) == count, "dead producer cannot spawn");
            int ended = 0;
            model.BattleEnded += _ => ended++;
            var friendlyBase = model.buildings.Values.Single(b => b.camp == BattleSceneCamp.FRIENDLY);
            Call(model.combat, "Damage", attacker, friendlyBase, 10000d);
            Call(model, "OnBattleTimeUpdated", 200f);
            Check(model.expired && ended == 1 && model.result.winner == BattleSceneCamp.ENEMY && model.result.reason == BattleEndReason.BaseDestroyed, "base ends once and freezes timeout");
        } finally {
            model.Dispose();
        }
    }
    private static void TimeoutAndPath() {
        foreach (var winner in new[] { BattleSceneCamp.ABSOLUTE_NEUTRAL, BattleSceneCamp.FRIENDLY, BattleSceneCamp.ENEMY }) {
            var model = Create();
            try {
                if (winner != BattleSceneCamp.ABSOLUTE_NEUTRAL) {
                    var cell = model.cells.Values.First(c => c.groundPassable && c.camp != winner && !c.unlocked);
                    // 隔离模型中的不同地图归属场景，不改变配置文件或正式局。
                    typeof(BattleCellVO).GetField("<camp>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cell, winner);
                }
                Call(model, "OnBattleTimeUpdated", 180f);
                Check(model.result.winner == winner && model.result.reason == BattleEndReason.TimeExpired, "territory outcome " + winner);
            } finally {
                model.Dispose();
            }
        }
        var walking = Create();
        try {
            var start = walking.buildings.Values.Single(b => b.camp == BattleSceneCamp.FRIENDLY);
            var end = walking.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY);
            var unit = Unit(walking, 100001, BattleSceneCamp.FRIENDLY, start.position);
            for (int i = 0; i < 1500 && end.hp == end.maxHp; i++) {
                unit.OnTimeUpdate(.1f);
                Advance(walking, .1f);
                Check(walking.FindCell(unit.position)?.groundPassable == true, "never crosses obstacle");
            }
            Check(end.hp < end.maxHp && unit.state == BattleUnitState.Attacking, "repath progresses to attack");
        } finally {
            walking.Dispose();
        }
    }
}

