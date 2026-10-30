using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using cfg.resource;
using SimpleJSON;
using UnityEngine;

/// <summary>独立模型验证占领规则；正式视图验证事件到染色/HUD的闭环。</summary>
public static class BattleTerritoryValidation {
    private static object Call(object value, string method, params object[] args) {
        return value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
    }
    private static void Check(bool ok, string message) {
        if (!ok) {
            throw new InvalidOperationException("Territory: " + message);
        }
    }
    private static BattleSceneModel Create(int id) {
        var t = CfgManager.tables;
        var scene = new BattleSceneModel(t.BattleSceneObj.Get(id), t.BattleSceneCellObj.DataList);
        scene.InitializeBattle(t, 7);
        return scene;
    }
    private static BattleUnitVO Unit(BattleSceneModel scene, long id, BattleSceneCamp camp, Vector2 position, UnitResource config = null) {
        var unit = new BattleUnitVO(id, config ?? CfgManager.tables.UnitObj.Get(1101), camp, position);
        scene.AddElement(unit);
        return unit;
    }
    private static void Resolve(BattleSceneModel scene) {
        Call(scene.territory, "Resolve");
    }
    private static void Move(BattleUnitVO unit, Vector2 position) {
        typeof(SceneElementVO).GetProperty("position").SetValue(unit, position);
    }
    private static BattleBuildingVO Producer(BattleSceneModel scene, BattleSceneCamp camp) {
        return (BattleBuildingVO)typeof(BattleUnitProductionValidation).GetMethod("BuildProducer", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { scene, camp });
    }
    public static void Validate() {
        Check(CfgManager.tables.UnitObj.DataList.Where(u => u.Id >= 1101 && u.Id <= 1104).All(u => u.CanCaptureTerritory), "all current infantry can capture");
        foreach (int id in new[] { 1001, 1002 }) {
            CaptureAndContest(id);
            LongMovementAndEligibility(id);
            BuildingsAndProjectile(id);
        }
        File.WriteAllText("Library/battle-territory-validation.txt", "passed: both scenes; enter/leave/recapture; simultaneous contest; survivor occupation; neutral/obstacle immunity; building protection; long-frame multi-cell movement; independent eligibility/altitude; destroyed-building transfer; dead shooter projectile credit; unlock persistence; timeout count; teardown");
    }
    private static void CaptureAndContest(int id) {
        var scene = Create(id);
        try {
            var cell = scene.cells.Values.First(c => c.camp == BattleSceneCamp.ENEMY && c.groundPassable && !c.unlocked);
            int count = scene.GetTerritoryCount(BattleSceneCamp.FRIENDLY), changes = 0;
            scene.territory.Changed += () => changes++;
            var a = Unit(scene, 600001, BattleSceneCamp.FRIENDLY, cell.position);
            var b = Unit(scene, 600002, BattleSceneCamp.ENEMY, cell.position);
            Resolve(scene);
            Resolve(scene);
            Check(cell.camp == BattleSceneCamp.ENEMY && changes == 0, "simultaneous presence preserves owner");
            scene.RemoveElement(b.id);
            Resolve(scene);
            Check(cell.camp == a.camp && !cell.unlocked && changes == 1 && scene.GetTerritoryCount(a.camp) == count + 1, "survivor captures without unlocking");
            Resolve(scene);
            Check(changes == 1, "stationary same-camp does not emit again");
            scene.RemoveElement(a.id);
            Resolve(scene);
            Check(cell.camp == BattleSceneCamp.FRIENDLY, "leave retains territory");
            b = Unit(scene, 600003, BattleSceneCamp.ENEMY, cell.position);
            Resolve(scene);
            Check(cell.camp == b.camp, "enemy recaptures");
            Call(cell, "MarkUnlocked");
            scene.RemoveElement(b.id);
            a = Unit(scene, 600004, BattleSceneCamp.FRIENDLY, cell.position);
            Resolve(scene);
            Check(cell.unlocked && !scene.unlocks.CanUnlock(cell.id, a.camp, out _), "revealed empty land cannot reroll after capture");
            Call(scene, "OnBattleTimeUpdated", 180f);
            Check(scene.result.winner == a.camp && scene.result.friendlyTerritory == count + 1, "timeout uses captured territory");
            Move(a, scene.cells.Values.First(c => c.camp == BattleSceneCamp.ENEMY).position);
            Resolve(scene);
            Check(scene.GetTerritoryCount(a.camp) == count + 1, "finished game cannot capture");
        } finally {
            scene.Dispose();
            Check(scene.territory == null, "territory disposed");
        }
    }
    private static void LongMovementAndEligibility(int id) {
        var scene = Create(id);
        try {
            var start = scene.buildings.Values.Single(b => b.camp == BattleSceneCamp.FRIENDLY).cell;
            var end = scene.buildings.Values.Single(b => b.camp == BattleSceneCamp.ENEMY).cell;
            var route = scene.navigation.FindGroundPath(start, end);
            var unit = Unit(scene, 600001, BattleSceneCamp.FRIENDLY, start.position);
            Call(unit, "SetRoute", route);
            unit.OnTimeUpdate(1000);
            Resolve(scene);
            Check(route.Select(scene.FindCell).Where(c => c != end).All(c => c.camp == unit.camp), "long frame captures traversed cells, not only endpoint");
            Check(end.camp == BattleSceneCamp.ENEMY, "living base protects its cell despite passage");
            foreach (var cell in scene.cells.Values.Where(c => !c.groundPassable)) {
                Move(unit, cell.position);
                Resolve(scene);
                Check(cell.camp == BattleSceneCamp.ABSOLUTE_NEUTRAL, "mountain/water remain neutral");
                Check(!(bool)Call(cell, "TrySetCamp", unit.camp), "mutation rejects obstacle");
            }
            scene.RemoveElement(unit.id);
            var target = scene.cells.Values.First(c => c.camp == BattleSceneCamp.ENEMY && !c.unlocked);
            var json = JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_unitobj.json")).AsArray[0];
            json["canCaptureTerritory"] = false;
            var noCapture = UnitResource.DeserializeUnitResource(json);
            unit = Unit(scene, 600002, BattleSceneCamp.FRIENDLY, target.position, noCapture);
            Resolve(scene);
            Check(target.camp == BattleSceneCamp.ENEMY, "explicit false blocks ground unit capture");
            scene.RemoveElement(unit.id);
            unit = Unit(scene, 600003, BattleSceneCamp.FRIENDLY, target.position);
            Call(unit, "SetAltitude", 80f);
            Resolve(scene);
            Check(target.camp == unit.camp, "visual altitude does not change capture eligibility");
        } finally {
            scene.Dispose();
        }
    }
    private static void BuildingsAndProjectile(int id) {
        var scene = Create(id);
        try {
            var building = Producer(scene, BattleSceneCamp.ENEMY);
            var cell = building.cell;
            var originalCard = cell.unlockedCardId;
            var unit = Unit(scene, 600001, BattleSceneCamp.FRIENDLY, cell.position);
            Resolve(scene);
            Check(cell.camp == BattleSceneCamp.ENEMY, "living producer protects ownership");
            Move(unit, cell.position + Vector2.down * 150);
            Call(scene.combat, "Damage", unit, building, building.maxHp - 20);
            // 创建真实技能释放和弹道，再移除射手；命中时仍从弹道快照取阵营。
            Call(scene.combat.skills, "Advance", unit, building, 1d, false);
            Call(scene.combat.skills, "Advance", unit, building, 80d, true);
            Check(scene.combat.projectiles.projectiles.Count == 1, "projectile launched before destruction");
            scene.RemoveElement(unit.id);
            Call(scene.combat.projectiles, "Advance", BattleSceneCamp.FRIENDLY, 1000d);
            Check(building.destroyed && !scene.buildings.ContainsKey(building.id) && cell.camp == BattleSceneCamp.FRIENDLY, "dead shooter still gets remote building capture");
            Check(cell.unlocked && cell.unlockedCardId == originalCard && !scene.unlocks.CanUnlock(cell.id, BattleSceneCamp.FRIENDLY, out _), "building destruction does not reset unlock/card");
            Check(!scene.buildings.Values.Any(b => b.cell == cell), "destroyed building is not converted");
            var neighbor = scene.GetNeighbors(cell).First(c => c.groundPassable && !c.unlocked);
            var scout = Unit(scene, 600002, BattleSceneCamp.FRIENDLY, neighbor.position);
            Resolve(scene);
            Check(scene.unlocks.IsRevealed(neighbor) && scene.unlocks.CanUnlock(neighbor.id, scout.camp, out _), "captured locked land uses current-owner adjacency");
            Check(!scene.unlocks.CanUnlock(neighbor.id, BattleSceneCamp.ENEMY, out _), "former owner cannot buy lost territory");
        } finally {
            scene.Dispose();
        }
    }
    public static void ValidateView(BattleSceneView view, BattleSceneModel scene) {
        var cell = scene.cells.Values.First(c => c.camp == BattleSceneCamp.ENEMY && !c.unlocked && c.groundPassable);
        var texture = view.groundTexture;
        var unit = Unit(scene, 600001, BattleSceneCamp.FRIENDLY, cell.position);
        try {
            Resolve(scene);
            Call(view, "LateUpdate");
            Check(view.groundTexture == texture, "capture reuses ground texture");
            BattleGroundValidation.Validate(view, scene);
            var hud = (BattleInfoPanel)PanelMgr.ins.GetPanel(MainUIEnum.BATTLE_INFO_PANEL);
            Check(hud.txtFriendlyTerritory.text == scene.GetTerritoryCount(unit.camp) + "格", "HUD event reflects capture");
            Check(hud.txtEnemyTerritory.text == scene.GetTerritoryCount(BattleSceneCamp.ENEMY) + "格", "opponent count decreases");
            scene.RemoveElement(unit.id);
            unit = Unit(scene, 600002, BattleSceneCamp.ENEMY, cell.position);
            Resolve(scene);
            Call(view, "LateUpdate");
            BattleGroundValidation.Validate(view, scene);
            Check(hud.txtFriendlyTerritory.text == "73格" && hud.txtEnemyTerritory.text == "73格", "recapture restores HUD");
            File.WriteAllText("Library/battle-territory-view-validation.txt", "passed: runtime capture/recapture -> event -> reused ground target pixel colors + HUD counts; permanent neutral/alpha preserved");
        } finally {
            scene.RemoveElement(unit.id);
        }
    }
}
