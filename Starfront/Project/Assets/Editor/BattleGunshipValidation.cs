using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using UnityEngine;

public static class BattleGunshipValidation {
    private static void Check(bool value, string message) {
        if (!value) {
            throw new InvalidOperationException("Gunship: " + message);
        }
    }
    private static void Advance(BattleSceneModel scene, float seconds) {
        typeof(BattleCombatModel).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scene.combat, new object[] { BattleSceneCamp.FRIENDLY, seconds });
    }
    public static void Validate() {
        var tables = CfgManager.tables;
        var scene = new BattleSceneModel(tables.BattleSceneObj.Get(BattleConst.InitialSceneId), tables.BattleSceneCellObj.DataList);
        try {
            scene.InitializeBattle(tables, 7);
            var config = tables.UnitObj.Get(1201);
            Check(config.MovementType == UnitMovementType.FLYING && config.DefaultAltitude == 40 && !config.CanCaptureTerritory, "flight and capture configuration");
            Check(tables.BattleDeckObj.Get(1001).Cards.Any(c => c.CardId == 1201), "card in deck");
            Check(tables.UnitProductionObj.Get(1201).Interval == 15000 && tables.UnitProductionObj.Get(1201).BuildingId == 1201, "airport production");
            var obstacle = scene.cells.Values.First(c => !c.groundPassable);
            var goal = scene.cells.Values.First(c => c.groundPassable);
            Check(scene.navigation.FindGroundPath(obstacle, goal) == null && scene.navigation.FindPath(obstacle, goal, UnitMovementType.FLYING) != null, "flight traverses obstacle cells");
            var origin = scene.buildings.Values.First(b => b.camp == BattleSceneCamp.FRIENDLY).position;
            var gunship = new BattleUnitVO(900001, config, BattleSceneCamp.FRIENDLY, origin);
            var ground = new BattleUnitVO(900002, tables.UnitObj.Get(1101), BattleSceneCamp.ENEMY, origin + Vector2.right * 200);
            var air = new BattleUnitVO(900003, config, BattleSceneCamp.ENEMY, origin + Vector2.right * 50);
            scene.AddElement(gunship);
            scene.AddElement(ground);
            scene.AddElement(air);
            Check(!BattleSkillModel.CanTarget(gunship, air) && BattleSkillModel.CanTarget(gunship, ground), "ground-only target eligibility independent of height");
            Advance(scene, .01f);
            Check(gunship.activeCast != null && gunship.targetId == ground.id && gunship.altitude == 40, "closer airborne target skipped");
            Advance(scene, .081f);
            Check(scene.combat.projectiles.projectiles.Count == 1 && ground.hp == 100, "first launch does not damage");
            Advance(scene, .078f);
            Check(scene.combat.projectiles.projectiles.Count == 1, "second launch waits full interval");
            Advance(scene, .003f);
            Check(scene.combat.projectiles.projectiles.Count == 2 && ground.hp == 100, "second launch after 80ms");
            var offsets = scene.combat.projectiles.projectiles.Values.Select(p => (Vector2)typeof(BattleProjectileVO).GetField("launchOffset", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(p)).ToArray();
            Check(offsets[0] != offsets[1], "distinct paired launch points");
            Advance(scene, .7f);
            Check(ground.hp == 70 && scene.combat.projectiles.projectiles.Count == 0, "two independent hits total30 damage");
            Check(gunship.visual.DeathHoldMs == 0 && gunship.visual.DeathFadeMs == 0 && !string.IsNullOrEmpty(gunship.visual.DeathEffect), "baked death has no repeated hold/fade");
            Check(BattleSceneResources.CollectAnimations(scene).Contains(ResourceConst.GetFrameAnimationPath(gunship.visual.DeathEffect)), "death explosion preloaded");
        } finally {
            scene.Dispose();
        }
        var productionScene = new BattleSceneModel(tables.BattleSceneObj.Get(BattleConst.InitialSceneId), tables.BattleSceneCellObj.DataList);
        try {
            productionScene.InitializeBattle(tables, 7);
            var cell = productionScene.cells.Values.First(c => c.groundPassable && c.camp == BattleSceneCamp.FRIENDLY && !productionScene.buildings.Values.Any(b => b.cell == c));
            var airport = (BattleBuildingVO)typeof(BattleSceneModel).GetMethod("PrepareCardBuilding", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(productionScene, new object[] { cell, tables.CardObj.Get(1201) });
            typeof(BattleSceneModel).GetMethod("CommitUnlock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(productionScene, new object[] { cell, (int?)1201, airport });
            airport.OnTimeUpdate(14);
            Check(productionScene.GetUnitCount(BattleSceneCamp.FRIENDLY) == 0, "airport does not produce early");
            airport.OnTimeUpdate(1);
            var produced = productionScene.elements.Values.OfType<BattleUnitVO>().Single();
            Check(produced.config.Id == 1201 && produced.altitude == 40 && airport.productionProgress == 0, "airport produces gunship at15 seconds");
            var start = produced.position;
            produced.OnTimeUpdate(1);
            Check(Math.Abs(Vector2.Distance(start, produced.position) - 45) < .01, "produced gunship speed45");
        } finally {
            productionScene.Dispose();
        }
        File.WriteAllText("Library/battle-gunship-validation.txt", "passed: card/airport links and real15s production/speed45; flight obstacle traversal; noncapture config; ground-only selection; 80ms dual mounts; damage on impact; death timing and effect preload");
    }
}
