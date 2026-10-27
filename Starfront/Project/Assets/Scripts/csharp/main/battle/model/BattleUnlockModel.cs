using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using cfg.resource;

/// <summary>
/// 管理解锁内容分配、连续扩展资格及支付后的抽取。
/// </summary>
/// <remarks>只处理本局逻辑，由BattleControl接收玩家请求；不持有表现对象。</remarks>
public sealed class BattleUnlockModel {

    private readonly BattleSceneModel scene;

    private readonly Tables tables;

    private readonly Random random;

    private readonly List<CellUnlockResource> rules;

    internal BattleUnlockModel(BattleSceneModel scene, Tables tables, Random random) {
        this.scene = scene;
        this.tables = tables;
        this.random = random;
        rules = tables.CellUnlockObj.DataList.OrderBy(rule => rule.Id).ToList();
        ValidateRules();
        AssignTypes();
    }

    /**加倍坐标下的六个直接邻格，地图缺失的邻格直接跳过。*/
    public IEnumerable<BattleCellVO> Neighbors(BattleCellVO cell) {
        return scene.GetNeighbors(cell);
    }

    /**揭示由相邻已解锁格决定；余额不足仍可看到类型、费用与光效。*/
    public bool IsRevealed(BattleCellVO cell) {
        return cell != null && cell.groundPassable && !cell.unlocked && cell.unlockConfig != null && Neighbors(cell).Any(neighbor => neighbor.camp == cell.camp && neighbor.unlocked);
    }

    public bool CanUnlock(long cellId, BattleSceneCamp actor, out string reason) {
        var cell = scene.GetCell(cellId);
        if (scene.expired || scene.camps.Count == 0) {
            reason = "对战已结束";
        } else if (cell == null) {
            reason = "格子不存在";
        } else if (!cell.groundPassable || cell.camp == BattleSceneCamp.ABSOLUTE_NEUTRAL) {
            reason = "该格子不能解锁";
        } else if (cell.camp != actor) {
            reason = "只能解锁己方格子";
        } else if (cell.unlocked) {
            reason = "格子已解锁";
        } else if (!IsRevealed(cell)) {
            reason = "请先解锁相邻格子";
        } else if (scene.camps[actor].currency < cell.unlockConfig.Cost) {
            reason = "货币不足";
        } else {
            reason = string.Empty;
            return true;
        }
        return false;
    }

    /**先完成结果与建筑配置校验，再提交一次扣费和解锁，避免半完成状态。*/
    internal bool TryUnlock(long cellId, BattleSceneCamp actor, out string reason) {
        if (!CanUnlock(cellId, actor, out reason)) {
            return false;
        }
        var cell = scene.GetCell(cellId);
        var owner = scene.camps[actor];
        int? cardId = DrawCard(cell.unlockConfig, owner);
        BattleBuildingVO building = cardId.HasValue ? scene.PrepareCardBuilding(cell, tables.CardObj.Get(cardId.Value)) : null;
        scene.CommitUnlock(cell, cardId, building);
        owner.SpendCurrency(cell.unlockConfig.Cost);
        scene.NotifyStructureChanged();
        return true;
    }

    private void ValidateRules() {
        if (rules.Count == 0 || rules.Sum(rule => (long)rule.GenerationWeight) <= 0 || rules.Sum(rule => (long)rule.GenerationWeight) > int.MaxValue) {
            throw new InvalidOperationException("解锁类型权重缺失或溢出。");
        }
        foreach (var rule in rules) {
            long resultWeight = rule.EmptyWeight + (long)rule.CollectorWeight + rule.UnitQualityWeights.Sum(item => (long)item.Weight);
            if (rule.Cost < 0 || rule.GenerationWeight < 0 || rule.EmptyWeight < 0 || rule.CollectorWeight < 0 || resultWeight <= 0 || resultWeight > int.MaxValue || rule.UnitQualityWeights.Any(item => item.Weight <= 0) || rule.UnitQualityWeights.Select(item => item.Quality).Distinct().Count() != rule.UnitQualityWeights.Count) {
                throw new InvalidOperationException("解锁配置费用或结果权重非法：" + rule.Id);
            }
        }
    }

    /**初始类型一次生成并旋转配对，随后用不重复邻格覆盖基地保底。*/
    private void AssignTypes() {
        int total = rules.Sum(rule => rule.GenerationWeight);
        foreach (var cell in scene.cells.Values.Where(cell => cell.camp == BattleSceneCamp.FRIENDLY && cell.groundPassable && !cell.unlocked).OrderBy(cell => cell.configId)) {
            int value = random.Next(total);
            var rule = rules.First(item => (value -= item.GenerationWeight) < 0);
            AssignPair(cell, rule);
        }
        var start = scene.GetCell(scene.config.FriendlyBaseQ, scene.config.FriendlyBaseK);
        var candidates = Neighbors(start).Where(cell => cell.camp == BattleSceneCamp.FRIENDLY && cell.groundPassable && !cell.unlocked).OrderBy(cell => cell.configId).ToList();
        foreach (var rule in rules.Where(rule => rule.GuaranteedNearBase)) {
            if (candidates.Count == 0) {
                throw new InvalidOperationException("基地直接邻格不足以放置所有保底类型。");
            }
            int index = random.Next(candidates.Count);
            AssignPair(candidates[index], rule);
            candidates.RemoveAt(index);
        }
    }

    private void AssignPair(BattleCellVO friendly, CellUnlockResource rule) {
        var enemy = scene.GetCell(-friendly.q, -friendly.k);
        if (enemy == null || enemy.camp != BattleSceneCamp.ENEMY || !enemy.groundPassable || enemy.unlocked) {
            throw new InvalidOperationException("解锁内容的旋转对应格非法：" + friendly.configId);
        }
        friendly.SetUnlockConfig(rule);
        enemy.SetUnlockConfig(rule);
    }

    /**权重决定空/采集站/品质，命中品质后在本局同品质卡中等概率选取。*/
    private int? DrawCard(CellUnlockResource rule, BattleCampVO owner) {
        int value = random.Next(rule.EmptyWeight + rule.CollectorWeight + rule.UnitQualityWeights.Sum(item => item.Weight));
        if ((value -= rule.EmptyWeight) < 0) {
            return null;
        }
        if ((value -= rule.CollectorWeight) < 0) {
            return owner.cardIds.Single(id => tables.CardObj.Get(id).Content is BuildingCardContent);
        }
        foreach (var quality in rule.UnitQualityWeights) {
            if ((value -= quality.Weight) < 0) {
                var candidates = owner.cardIds.Where(id => tables.CardObj.Get(id).Quality == quality.Quality && tables.CardObj.Get(id).Content is UnitCardContent).ToList();
                if (candidates.Count == 0) {
                    throw new InvalidOperationException("卡组缺少已抽中的品质：" + quality.Quality);
                }
                return candidates[random.Next(candidates.Count)];
            }
        }
        throw new InvalidOperationException("解锁结果权重未命中。");
    }

}
