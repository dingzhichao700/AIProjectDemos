using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 验证产兵、名额争用、移动和表现生命周期。
/// </summary>
/// <remarks>业务边界使用隔离模型；实局等待真实十秒周期，不注入单位或余额。</remarks>
public static class BattleUnitProductionValidation {

    public static void ValidateLogic() {
        var tables = CfgManager.tables;
        var model = new BattleSceneModel(tables.BattleSceneObj.Get(BattleConst.InitialSceneId), tables.BattleSceneCellObj.DataList);
        float playerScale = RookieEngine.playerTimer.scale;
        float enemyScale = RookieEngine.enemyTimer.scale;
        try {
            model.InitializeBattle(tables, 7);
            Require(tables.UnitObj.DataList.All(unit => unit.MoveSpeed == 30), "all four configured speeds");
            var producer = BuildProducer(model, BattleSceneCamp.FRIENDLY);
            producer.OnTimeUpdate(9);
            Require(model.GetUnitCount(BattleSceneCamp.FRIENDLY) == 0 && Math.Abs(producer.productionProgress - .9) < .001, "no early unit; configured progress");
            producer.OnTimeUpdate(1);
            var first = model.elements.Values.OfType<BattleUnitVO>().Single();
            Require(model.GetUnitCount(first.camp) == 1 && producer.productionProgress == 0, "exact period spawns once and resets");
            var origin = first.position;
            first.OnTimeUpdate(1);
            Require(Math.Abs(Vector2.Distance(origin, first.position) - 30) < .01, "30 units per scaled second");
            first.OnTimeUpdate(10000);
            var enemyBase = model.GetCell(model.config.EnemyBaseQ, model.config.EnemyBaseK);
            Require(first.position == enemyBase.position && !first.moving, "long frame reaches destination without overshooting");
            var route = model.navigation.FindGroundPath(producer.cell, enemyBase);
            var previous = producer.cell;
            foreach (var point in route) {
                var cell = model.FindCell(point);
                Require(cell.groundPassable && model.GetNeighbors(previous).Any(next => next.id == cell.id), "path follows passable adjacent cells");
                previous = cell;
            }
            Require(model.navigation.FindGroundPath(model.cells.Values.First(cell => !cell.groundPassable), enemyBase) == null, "obstacle cannot become route origin");
            producer.OnTimeUpdate(990);
            Require(model.GetUnitCount(first.camp) == 100 && producer.productionProgress == 0, "large delta produces exactly available population");
            producer.OnTimeUpdate(5);
            Require(Math.Abs(producer.productionProgress - .5) < .001, "full population still advances incomplete period");
            producer.OnTimeUpdate(5);
            producer.OnTimeUpdate(10000);
            Require(model.GetUnitCount(first.camp) == 100 && producer.productionProgress == 1, "full progress waits without accumulating batches");
            var second = BuildProducer(model, BattleSceneCamp.FRIENDLY);
            second.OnTimeUpdate(10);
            var remove = typeof(BattleSceneModel).GetMethod("RemoveCombatUnit", BindingFlags.Instance | BindingFlags.NonPublic);
            remove.Invoke(model, new object[] { first.id });
            Require(model.GetUnitCount(first.camp) == 100 && (producer.productionProgress == 0 ^ second.productionProgress == 0), "one freed slot permits one waiting producer");
            var another = model.elements.Values.OfType<BattleUnitVO>().First();
            remove.Invoke(model, new object[] { another.id });
            Require(model.GetUnitCount(first.camp) == 100 && producer.productionProgress == 0 && second.productionProgress == 0, "second slot resumes second waiting producer without backlog");
            var enemyProducer = BuildProducer(model, BattleSceneCamp.ENEMY);
            enemyProducer.OnTimeUpdate(10);
            Require(model.GetUnitCount(BattleSceneCamp.ENEMY) == 1 && model.GetUnitCount(BattleSceneCamp.FRIENDLY) == 100, "camp populations independent");
            var friendlyUnit = model.elements.Values.OfType<BattleUnitVO>().First(unit => unit.camp == BattleSceneCamp.FRIENDLY);
            var enemyUnit = model.elements.Values.OfType<BattleUnitVO>().Single(unit => unit.camp == BattleSceneCamp.ENEMY);
            Vector2 friendBefore = friendlyUnit.position;
            Vector2 enemyBefore = enemyUnit.position;
            model.StartTimeFlow();
            RookieEngine.playerTimer.scale = 0;
            RookieEngine.enemyTimer.scale = 1;
            RookieEngine.playerTimer.SyncTime(1000);
            RookieEngine.enemyTimer.SyncTime(1000);
            Require(friendlyUnit.position == friendBefore && Vector2.Distance(enemyUnit.position, enemyBefore) > 29, "independent movement pause");
            model.StopTimeFlow();
            var parent = new GameObject("UnitValidation", typeof(RectTransform)).GetComponent<RectTransform>();
            var presentation = new BattleUnitView(model, parent);
            try {
                presentation.Refresh(new Rect(0, 0, 2000, 2000));
                Require(presentation.visibleCount == 101, "all live units displayed from shared preloaded animations");
                presentation.Refresh(new Rect(-10000, -10000, 1, 1));
                Require(presentation.visibleCount == 0, "offscreen unit views recycled");
                origin = friendlyUnit.position;
                friendlyUnit.OnTimeUpdate(1);
                Require(Vector2.Distance(friendlyUnit.position, origin) > 29, "offscreen unit logic continues");
                presentation.Refresh(new Rect(0, 0, 2000, 2000));
                var image = parent.Find("unit_" + friendlyUnit.id).GetComponent<Image>();
                Require(Vector2.Distance(image.rectTransform.anchoredPosition, new Vector2(friendlyUnit.position.x, -friendlyUnit.position.y + model.config.GroundThickness)) < .01, "recycled unit view restores current position");
                Require(BattleUnitView.ResolveDirection(new Vector2(-1, -1)) == UnitAppearanceDirection.UP_RIGHT && BattleUnitView.ResolveDirection(Vector2.left) == UnitAppearanceDirection.RIGHT && image.rectTransform.localScale.x == (friendlyUnit.heading.x < 0 ? -1 : 1), "left source direction and anchor mirror");
            } finally {
                presentation.Dispose();
                Require(parent.GetComponentsInChildren<Image>(true).All(image => image.sprite == null), "dispose clears frame references");
                UnityEngine.Object.Destroy(parent.gameObject);
            }
            producer.Destroy();
            int count = model.GetUnitCount(first.camp);
            producer.OnTimeUpdate(100);
            Require(model.GetUnitCount(first.camp) == count, "destroyed producer stops");
            BattleUnitPivotValidation.Validate();
            File.WriteAllText("Library/battle-unit-production-validation.txt", "passed: exact 10s cycle; no early spawn; multi-cycle remainder; 100 cap; incomplete cycle advances at cap; full-progress wait; no backlog; two-producer slot contention; immediate refill; independent camps; passable six-neighbor path; speed 30; long-frame arrival; independent timer pause; offscreen logic and view reuse; direction mirror; frame-reference cleanup; destroyed producer stops");
        } finally {
            model.Dispose();
            RookieEngine.playerTimer.scale = playerScale;
            RookieEngine.enemyTimer.scale = enemyScale;
        }
    }

    /**第一次兵营产兵后验证正式人口HUD、生产圆环与角色显示。*/
    public static BattleUnitVO ValidateLive(BattleScenePanel panel) {
        var model = BattleControl.ins.model;
        var unit = model.elements.Values.OfType<BattleUnitVO>().First(item => item.camp == BattleSceneCamp.FRIENDLY);
        var view = panel.GetComponent<BattleSceneView>();
        var delta = new Vector2(view.cameraCenter.x - unit.position.x, unit.position.y - view.cameraCenter.y) * view.zoom;
        typeof(BattleSceneView).GetMethod("OnDrag", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { delta });
        var hud = PanelMgr.ins.GetPanel(UIEnum.BATTLE_INFO_PANEL) as BattleInfoPanel;
        Require(hud.txtFriendlyPopulation.text == "1 / 100" && hud.txtEnemyPopulation.text == "0 / 100", "spawn refreshes population HUD immediately");
        Require(view.unitVisibleCount == 1, "spawned unit visible");
        var producer = model.buildings.Values.Single(building => building.unitProduction != null);
        var ring = view.lod.Find("production_" + producer.id);
        Require(ring != null && ring.GetComponentInChildren<Image>().gameObject.activeInHierarchy && producer.productionProgress < .2, "unit producer ring resets after output");
        return unit;
    }

    private static BattleBuildingVO BuildProducer(BattleSceneModel model, BattleSceneCamp camp) {
        var owner = model.camps[camp];
        typeof(BattleCampVO).GetMethod("AddCurrency", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, new object[] { 10000L });
        while (true) {
            var target = model.cells.Values.Where(cell => cell.camp == camp && model.unlocks.IsRevealed(cell)).OrderByDescending(cell => cell.unlockConfig.EmptyWeight == 0 && cell.unlockConfig.UnitQualityWeights.Count > 0).First();
            object[] args = { target.id, camp, null };
            Require((bool)typeof(BattleUnlockModel).GetMethod("TryUnlock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model.unlocks, args), "fixture unlock");
            var producer = model.buildings.Values.FirstOrDefault(building => building.cell == target && building.unitProduction != null);
            if (producer != null) {
                return producer;
            }
        }
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("Unit production validation: " + message);
        }
    }

}
