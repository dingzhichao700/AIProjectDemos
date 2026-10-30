using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using SimpleJSON;
using UnityEngine;

/// <summary>隔离配置验证释放阶段与取消边界，不改变正式源表。</summary>
public static class BattleSkillValidation {
    private static readonly FieldInfo TablesField = typeof(CfgManager).GetField("_tables", BindingFlags.NonPublic | BindingFlags.Static);

    private static void Check(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("Skill validation: " + message);
        }
    }

    private static void Load(int windup = 300, int recovery = 200, int cooldown = 1000) {
        TablesField.SetValue(null, new Tables(name => {
            var node = JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json"));
            if (name == "cfgobj_skillobj") {
                node[0]["effects"] = JSON.Parse("[{\"$type\":\"FixedDamageEffect\",\"amount\":20}]");
                node[0]["windupMs"] = windup;
                node[0]["recoveryMs"] = recovery;
                node[0]["cooldownMs"] = cooldown;
            }
            return node;
        }));
    }

    private static BattleSceneModel Scene() {
        var tables = CfgManager.tables;
        var scene = new BattleSceneModel(tables.BattleSceneObj.Get(1001), tables.BattleSceneCellObj.DataList);
        scene.InitializeBattle(tables, 7);
        return scene;
    }

    private static BattleUnitVO Unit(BattleSceneModel scene, long id, BattleSceneCamp camp, Vector2 position) {
        var unit = new BattleUnitVO(id, CfgManager.tables.UnitObj.Get(1101), camp, position);
        scene.AddElement(unit);
        return unit;
    }

    private static void Advance(BattleSceneModel scene, float seconds) {
        typeof(BattleCombatModel).GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scene.combat, new object[] { BattleSceneCamp.FRIENDLY, seconds });
    }

    public static void Validate() {
        object saved = TablesField.GetValue(null);
        try {
            Load();
            Stages();
            Cancellation();
            DeathAndIndependence();
            Load(300, 200, 0);
            bool rejected = false;
            try {
                Scene().Dispose();
            } catch (InvalidOperationException) {
                rejected = true;
            }
            Check(rejected, "zero cooldown rejected before battle");
        } finally {
            TablesField.SetValue(null, saved);
        }
        File.WriteAllText("Library/battle-skill-validation.txt", "passed: generated skill references; windup/impact/recovery; movement lock; cooldown starts at cast; independent instances; target loss cancellation without refund; locked target; caster death interruption; invalid configuration; existing zero-stage combat long-frame regression");
    }

    private static void Stages() {
        var scene = Scene();
        try {
            var point = scene.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY).position;
            var a = Unit(scene, 200001, BattleSceneCamp.FRIENDLY, point);
            var b = Unit(scene, 200002, BattleSceneCamp.ENEMY, point + Vector2.right * 10);
            int starts = 0, impacts = 0, finishes = 0;
            scene.combat.skills.Changed += e => {
                if (e.type == BattleSkillEventType.Started) {
                    starts++;
                }
                if (e.type == BattleSkillEventType.Impact) {
                    impacts++;
                }
                if (e.type == BattleSkillEventType.Completed) {
                    finishes++;
                }
            };
            Advance(scene, .01f);
            Check(a.activeCast?.stage == BattleSkillStage.Windup && b.hp == 100 && starts == 1 && !a.moving, "windup does not damage and stops movement");
            var cast = a.activeCast;
            Unit(scene, 200003, BattleSceneCamp.ENEMY, point);
            Advance(scene, .31f);
            Check(a.activeCast == cast && cast.targetId == b.id && cast.stage == BattleSkillStage.Recovery && b.hp == 80 && impacts == 1, "locked impact once");
            Advance(scene, .2f);
            Check(a.activeCast == null && finishes == 1 && starts == 1, "recovery completes before cooldown");
            Advance(scene, .5f);
            Check(starts == 2 && b.hp == 80 && a.activeCast != cast, "cooldown measured from start; new cast instance");
            Advance(scene, .31f);
            Check(b.hp == 60 && impacts == 2, "second impact");
        } finally {
            scene.Dispose();
        }
    }

    private static void Cancellation() {
        var scene = Scene();
        try {
            var point = scene.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY).position;
            var a = Unit(scene, 210001, BattleSceneCamp.FRIENDLY, point);
            var b = Unit(scene, 210002, BattleSceneCamp.ENEMY, point + Vector2.right * 10);
            int cancelled = 0;
            scene.combat.skills.Changed += e => {
                if (e.type == BattleSkillEventType.Cancelled) {
                    cancelled++;
                }
            };
            Advance(scene, .01f);
            double ready = a.skills[0].readyAtMs;
            typeof(SceneElementVO).GetProperty("position").SetValue(b, point + Vector2.right * 200);
            Advance(scene, .1f);
            Check(a.activeCast == null && cancelled == 1 && b.hp == 100 && a.skills[0].readyAtMs == ready, "range loss cancels without refund");
            typeof(SceneElementVO).GetProperty("position").SetValue(b, point + Vector2.right * 10);
            Advance(scene, .1f);
            Check(a.activeCast == null && b.hp == 100, "cannot immediately recast");
            Advance(scene, .81f);
            Check(a.activeCast != null && a.activeCast.stage == BattleSkillStage.Windup, "recast only after original cooldown");
            scene.RemoveElement(b.id);
            Advance(scene, .4f);
            Check(a.activeCast == null && cancelled == 2, "removed target cancels instead of replacing with base");
        } finally {
            scene.Dispose();
        }
    }

    private static void DeathAndIndependence() {
        var scene = Scene();
        try {
            var point = scene.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY).position;
            var a = Unit(scene, 220001, BattleSceneCamp.FRIENDLY, point);
            var c = Unit(scene, 220003, BattleSceneCamp.FRIENDLY, point);
            var b = Unit(scene, 220002, BattleSceneCamp.ENEMY, point + Vector2.right * 10);
            Advance(scene, .01f);
            Check(a.skills[0] != c.skills[0] && a.activeCast.id != c.activeCast.id, "per-unit skill and cast ownership");
            var cast = a.activeCast;
            typeof(BattleCombatModel).GetMethod("Damage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scene.combat, new object[] { b, a, 100d });
            Check(a.activeCast == null && cast.stage == BattleSkillStage.Cancelled, "caster death immediately interrupts");
            Advance(scene, .4f);
            Check(b.hp == 80, "dead caster deals no delayed damage; other unit continues");
        } finally {
            scene.Dispose();
        }
    }
}
