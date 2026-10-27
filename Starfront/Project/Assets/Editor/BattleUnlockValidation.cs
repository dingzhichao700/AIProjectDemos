using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 配置驱动解锁的业务边界及真实输入验证。
/// </summary>
/// <remarks>隔离模型固定随机种子；实局沿用正式入口与自然产币，不注入测试余额。</remarks>
public static class BattleUnlockValidation {

    private static readonly MethodInfo unlockMethod = typeof(BattleUnlockModel).GetMethod("TryUnlock", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo addCurrency = typeof(BattleCampVO).GetMethod("AddCurrency", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void ValidateLogic() {
        var tables = CfgManager.tables;
        bool sawEmptyExpansion = false;
        bool sawCollector = false;
        bool sawBarracks = false;
        for (int seed = 0; seed < 12; seed++) {
            var model = new BattleSceneModel(tables.BattleSceneObj.Get(BattleConst.InitialSceneId), tables.BattleSceneCellObj.DataList);
            try {
                model.InitializeBattle(tables, seed);
                var friendly = model.camps[BattleSceneCamp.FRIENDLY];
                var start = model.GetCell(model.config.FriendlyBaseQ, model.config.FriendlyBaseK);
                foreach (var rule in tables.CellUnlockObj.DataList.Where(rule => rule.GuaranteedNearBase)) {
                    Require(model.unlocks.Neighbors(start).Any(cell => cell.unlockConfig == rule), "distinct near-base guarantees");
                }
                foreach (var cell in model.cells.Values.Where(cell => cell.unlockConfig != null)) {
                    Require(model.GetCell(-cell.q, -cell.k).unlockConfig == cell.unlockConfig, "all initial types are rotation-paired");
                }
                var hidden = model.cells.Values.First(cell => cell.camp == BattleSceneCamp.FRIENDLY && cell.unlockConfig != null && !model.unlocks.IsRevealed(cell));
                Require(!Unlock(model, hidden.id, out string hiddenReason) && hiddenReason == "请先解锁相邻格子", "remote owned type remains hidden and cannot be bought");
                var enemy = model.cells.Values.First(cell => cell.camp == BattleSceneCamp.ENEMY && cell.groundPassable);
                Require(!Unlock(model, enemy.id, out string enemyReason) && enemyReason == "只能解锁己方格子", "enemy cannot be bought");
                var obstacle = model.cells.Values.First(cell => !cell.groundPassable);
                Require(!Unlock(model, obstacle.id, out _), "neutral obstacle cannot be bought");
                var collectorCell = model.cells.Values.First(cell => model.unlocks.IsRevealed(cell) && cell.camp == BattleSceneCamp.FRIENDLY && cell.unlockConfig.CollectorWeight > 0);
                long before = friendly.currency;
                Require(Unlock(model, collectorCell.id, out _) && friendly.currency == before - collectorCell.unlockConfig.Cost, "exact one payment");
                var collector = model.buildings.Values.Single(building => building.cell == collectorCell);
                Require(collector.currencyProduction != null && collector.productionProgress == 0, "collector created with fresh production cycle");
                Require(!Unlock(model, collectorCell.id, out _) && friendly.currency == 0, "duplicate purchase rejected without payment");
                var poorTarget = model.cells.Values.First(cell => cell.camp == BattleSceneCamp.FRIENDLY && model.unlocks.IsRevealed(cell));
                int count = model.buildings.Count;
                Require(!Unlock(model, poorTarget.id, out string poorReason) && poorReason == "货币不足" && !poorTarget.unlocked && model.buildings.Count == count, "insufficient funds never mutate result or buildings");
                collector.OnTimeUpdate(4);
                Require(friendly.currency == 0, "new collector does not pay immediately");
                collector.OnTimeUpdate(1);
                Require(friendly.currency == 10, "new collector uses configured period");
                collector.Destroy();
                Require(collectorCell.unlocked && !Unlock(model, collectorCell.id, out _), "destroyed building does not relock cell");
                // 仅隔离模型补充测试余额，遍历正式类型与结果池；不修改配置权重。
                addCurrency.Invoke(friendly, new object[] { 100000L });
                while (true) {
                    var cell = model.cells.Values.Where(cell => cell.camp == BattleSceneCamp.FRIENDLY && model.unlocks.IsRevealed(cell)).OrderBy(cell => cell.configId).FirstOrDefault();
                    if (cell == null) {
                        break;
                    }
                    var newlyReachable = model.unlocks.Neighbors(cell).Where(next => next.camp == BattleSceneCamp.FRIENDLY && next.unlockConfig != null && !next.unlocked && !model.unlocks.IsRevealed(next)).ToArray();
                    long balance = friendly.currency;
                    Require(Unlock(model, cell.id, out _) && friendly.currency == balance - cell.unlockConfig.Cost, "frontier purchase pays configured cost");
                    if (!cell.unlockedCardId.HasValue) {
                        Require(cell.unlockConfig.EmptyWeight > 0 && !model.buildings.Values.Any(building => building.cell == cell), "empty result creates no building");
                        if (newlyReachable.Length > 0) {
                            Require(newlyReachable.All(next => model.unlocks.IsRevealed(next)), "empty cell continues expansion");
                            sawEmptyExpansion = true;
                        }
                    } else {
                        int cardId = cell.unlockedCardId.Value;
                        Require(friendly.cardIds.Contains(cardId), "result stays inside current deck");
                        var card = tables.CardObj.Get(cardId);
                        var building = model.buildings.Values.Single(item => item.cell == cell);
                        if (card.Content is UnitCardContent unit) {
                            Require(cell.unlockConfig.UnitQualityWeights.Any(item => item.Quality == card.Quality && item.Weight > 0), "unit quality belongs to configured outcome pool");
                            Require(building.unitProduction.UnitId == unit.UnitId && building.config.Id == tables.UnitProductionObj.Get(unit.UnitId).BuildingId, "unit card produces matching barracks");
                            sawBarracks = true;
                        } else {
                            Require(cell.unlockConfig.CollectorWeight > 0 && building.currencyProduction != null, "collector result respects pool");
                            sawCollector = true;
                        }
                    }
                }
                Require(model.GetTerritoryCount(BattleSceneCamp.FRIENDLY) == 73 && model.GetUnitCount(BattleSceneCamp.FRIENDLY) == 0, "unlock does not change camp territory or spawn combat units");
            } finally {
                model.Dispose();
            }
        }
        Require(sawEmptyExpansion && sawCollector && sawBarracks, "fixed-seed coverage includes empty expansion, collectors and barracks");
        File.WriteAllText("Library/battle-unlock-logic-validation.txt", "passed: 12 fixed seeds; rotation-paired initial types; base guarantees; reveal/adjacency; enemy/obstacle/remote rejection; exact payment and duplicate rejection; insufficient funds atomicity; collector period; destroyed building retains unlock; empty-result expansion; all results stay in configured deck/quality pools; matching barracks; territory/population unchanged");
    }

    /**从场景输入回调进入Panel与Control，验证点击后建筑及动态提示同步。*/
    public static void ValidateLiveCollector(BattleScenePanel panel) {
        var model = BattleControl.ins.model;
        var view = panel.GetComponent<BattleSceneView>();
        var cell = model.cells.Values.First(cell => cell.camp == BattleSceneCamp.FRIENDLY && model.unlocks.IsRevealed(cell) && cell.unlockConfig.CollectorWeight > 0);
        Focus(view, cell);
        Require(view.unlockVisibleCount > 0, "visible friendly unlock hints");
        var guides = view.layers[(int)BattleSceneVisualLayer.GROUND_GUIDE - 1];
        var root = guides.Find("unlock_" + cell.id);
        Require(root != null && root.Find("cost").GetComponent<TextMeshProUGUI>().text == cell.unlockConfig.Cost.ToString(), "visible configured cost");
        var glow = root.Find("unlockGlow_" + cell.id).GetComponent<Image>();
        Require(glow.sprite.name == "UI_unlock-glow" && glow.color.a >= BattleConst.UnlockMinAlpha && glow.color.a <= 1, "existing glow loaded and breath alpha bounded");
        Require(view.lod.Find("unlock_" + cell.id) == null && glow.transform.parent == root, "entire hint belongs to ground guides");
        Require(root.localScale == Vector3.one && glow.rectTransform.anchoredPosition == Vector2.zero, "single inherited scene scale and centered glow");
        Vector3 expected = view.sceneRoot.TransformPoint(new Vector3(cell.position.x, -cell.position.y + model.config.GroundThickness, 0));
        Require(Vector3.Distance(root.position, expected) < .01f, "ground hint follows top surface after pan and zoom");
        Require(guides.GetSiblingIndex() < view.layers[(int)BattleSceneVisualLayer.ABOVE_GROUND - 1].GetSiblingIndex(), "ground hints render behind buildings and units");
        Require(root.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), "whole ground hint leaves map input unblocked");
        long before = model.camps[BattleSceneCamp.FRIENDLY].currency;
        var groundBeforeUnlock = view.groundTexture;
        Click(view, cell);
        Require(ReferenceEquals(groundBeforeUnlock, view.groundTexture), "unlock reuses complete ground target");
        Require(cell.unlocked && model.buildings.Count == 3 && view.totalVisualCount == model.config.Appearances.Count + model.buildings.Values.Sum(item => item.config.Appearances.Count), "real click creates collector and shadow");
        Require(model.camps[BattleSceneCamp.FRIENDLY].currency == before - cell.unlockConfig.Cost, "real click deducts configured cost");
        Require(!BattleControl.ins.TryUnlockCell(cell.id, out string duplicate) && duplicate == "格子已解锁", "live duplicate rejected");
        var poor = model.cells.Values.First(item => item.camp == BattleSceneCamp.FRIENDLY && model.unlocks.IsRevealed(item));
        Focus(view, poor);
        Click(view, poor);
        Require(!poor.unlocked && panel.lastInteractionHint == "货币不足", "live insufficient feedback");
        var poorRoot = guides.Find("unlock_" + poor.id);
        Require(poorRoot.Find("cost").GetComponent<TextMeshProUGUI>().color.r > .9f && poorRoot.Find("cost").GetComponent<TextMeshProUGUI>().color.g < .5f, "unaffordable price red; hint retained");
        Require(view.lod.Find("unlockMessage").GetComponent<TextMeshProUGUI>().text == "货币不足", "short message uses business rejection");
        Require(view.lod.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), "hint UI does not intercept map input");
        File.WriteAllText("Library/battle-unlock-live-validation.txt", "collector: real input pays once, creates body/shadow and expands frontier; insufficient cost turns red, retains glow and displays message");
    }

    public static BattleCellVO GetLiveBarracks() {
        var model = BattleControl.ins.model;
        return model.cells.Values.First(cell => cell.camp == BattleSceneCamp.FRIENDLY && model.unlocks.IsRevealed(cell) && cell.unlockConfig.GuaranteedNearBase && cell.unlockConfig.CollectorWeight == 0);
    }

    public static void ValidateLiveBarracks(BattleScenePanel panel) {
        var model = BattleControl.ins.model;
        var cell = GetLiveBarracks();
        var view = panel.GetComponent<BattleSceneView>();
        Focus(view, cell);
        Click(view, cell);
        var building = model.buildings.Values.Single(item => item.cell == cell);
        Require(building.unitProduction != null && model.buildings.Count == 4 && view.totalVisualCount == model.config.Appearances.Count + model.buildings.Values.Sum(item => item.config.Appearances.Count), "real income funds barracks generation with body and shadow");
        Require(model.GetUnitCount(BattleSceneCamp.FRIENDLY) == 0, "barracks does not instantly spawn unit");
        Require(model.GetTerritoryCount(BattleSceneCamp.FRIENDLY) == 73, "territory measures ownership throughout unlocks");
        File.AppendAllText("Library/battle-unlock-live-validation.txt", "; barracks: natural base/collector income funds purchase, matches drawn card configuration, no premature unit spawn");
    }

    private static void Focus(BattleSceneView view, BattleCellVO cell) {
        var delta = new Vector2(view.cameraCenter.x - cell.position.x, cell.position.y - view.cameraCenter.y) * view.zoom;
        typeof(BattleSceneView).GetMethod("OnDrag", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { delta });
        Canvas.ForceUpdateCanvases();
    }

    private static void Click(BattleSceneView view, BattleCellVO cell) {
        Vector2 delta = cell.position - view.cameraCenter;
        Vector2 point = view.viewport.rect.center + new Vector2(delta.x, -delta.y) * view.zoom;
        typeof(BattleSceneView).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { point });
    }

    private static bool Unlock(BattleSceneModel model, long cellId, out string reason) {
        object[] args = { cellId, BattleSceneCamp.FRIENDLY, null };
        bool success = (bool)unlockMethod.Invoke(model.unlocks, args);
        reason = (string)args[2];
        return success;
    }

    private static void Require(bool condition, string reason) {
        if (!condition) {
            throw new InvalidOperationException("Unlock validation: " + reason);
        }
    }

}
