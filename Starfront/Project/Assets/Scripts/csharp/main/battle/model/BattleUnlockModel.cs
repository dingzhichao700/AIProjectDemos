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
    // 友方报价随整局保存，丢失视野、重复点击或切换阵容均不会重抽。
    private readonly Dictionary<(long, BattleSceneCamp), CardResource> playerOffers = new Dictionary<(long, BattleSceneCamp), CardResource>();

    public CardResource OfferedUnit(BattleCellVO cell) {
        playerOffers.TryGetValue((cell.id, cell.camp), out var offer);
        return offer;
    }

    public int Cost(BattleCellVO cell) {
        return OfferedUnit(cell)?.ConstructionCost ?? cell.unlockConfig.Cost;
    }

    private static bool IsMilitary(CellUnlockResource rule) {
        return rule.UnitQualityWeights.Count > 0 && rule.EmptyWeight == 0 && rule.CollectorWeight == 0;
    }

    private void AssignPlayerOffers() {
        foreach (var player in scene.camps.Values)
        {
            var candidates = player.cardIds.Select(id => tables.CardObj.Get(id)).Where(c => c.Content is UnitCardContent).ToArray();
            if (candidates.Any(c => c.ConstructionCost <= 0))
            {
                throw new InvalidOperationException("出战兵种缺少建造费用。");
            }
            if (candidates.Length == 0)
            {
                continue;
            }
            long combinedWeight = candidates.Sum(c => (long)c.UnlockWeight);
            if (combinedWeight <= 0 || combinedWeight > int.MaxValue)
            {
                throw new InvalidOperationException("本局兵种候选总权重非法或溢出。");
            }
            int total = (int)combinedWeight;
            foreach (var cell in scene.cells.Values.OrderBy(c => c.configId))
            {
                var rule = cell.unlockConfig;
                if (rule == null || rule.UnitQualityWeights.Count == 0)
                {
                    continue;
                }
                if (!IsMilitary(rule) && random.Next(rule.EmptyWeight + rule.UnitQualityWeights.Sum(w => w.Weight)) < rule.EmptyWeight)
                {
                    continue;
                }
                int roll = random.Next(total);
                playerOffers.Add((cell.id, player.camp), candidates.First(c => (roll -= c.UnlockWeight) < 0));
            }
        }
    }

    internal BattleUnlockModel(BattleSceneModel scene, Tables tables, Random random) {
        this.scene = scene;
        this.tables = tables;
        this.random = random;
        rules = tables.CellUnlockObj.DataList.OrderBy(rule => rule.Id).ToList();
        ValidateRules();
        AssignTypes();
        AssignPlayerOffers();
    }

    /**加倍坐标下的六个直接邻格，地图缺失的邻格直接跳过。*/
    public IEnumerable<BattleCellVO> Neighbors(BattleCellVO cell) {
        return scene.GetNeighbors(cell);
    }

    /**揭示由相邻已解锁格决定；余额不足仍可看到类型、费用与光效。*/
    public bool IsRevealed(BattleCellVO cell) {
        return cell != null && cell.capturable && (cell.camp == BattleSceneCamp.FRIENDLY || cell.camp == BattleSceneCamp.ENEMY) && !cell.unlocked && cell.unlockConfig != null && (!IsMilitary(cell.unlockConfig) || OfferedUnit(cell) != null) && Neighbors(cell).Any(neighbor => neighbor.camp == cell.camp && neighbor.unlocked);
    }

    /// <summary>规划资格只检查场地与归属；不授予支付或建造权限。</summary>
    public bool CanPlanUnlock(long cellId, BattleSceneCamp actor, out string reason) {
        var cell = scene.GetCell(cellId);
        if (scene.expired || scene.camps.Count == 0) {
            reason = "对战已结束";
        } else if (cell == null) {
            reason = "格子不存在";
        } else if (!cell.capturable) {
            reason = "该格子不能解锁";
        } else if (cell.camp != actor) {
            reason = "只能解锁己方格子";
        } else if (cell.unlocked) {
            reason = "格子已解锁";
        } else if (!IsRevealed(cell)) {
            reason = "请先解锁相邻格子";
        } else {
            reason = string.Empty;
            return true;
        }
        return false;
    }

    public bool CanUnlock(long cellId, BattleSceneCamp actor, out string reason)
    {
        if (!CanPlanUnlock(cellId, actor, out reason))
        {
            return false;
        }
        if (scene.camps[actor].currency < Cost(scene.GetCell(cellId)))
        {
            reason = "货币不足";
            return false;
        }
        return true;
    }

    /**先完成结果与建筑配置校验，再提交一次扣费和解锁，避免半完成状态。*/
    internal bool TryUnlock(long cellId, BattleSceneCamp actor, out string reason) {
        if (!CanUnlock(cellId, actor, out reason)) {
            return false;
        }
        var cell = scene.GetCell(cellId);
        var owner = scene.camps[actor];
        int? cardId = cell.unlockConfig.UnitQualityWeights.Count > 0
            ? OfferedUnit(cell)?.Id : DrawCard(cell.unlockConfig, owner);
        BattleBuildingVO building = cardId.HasValue ? scene.PrepareCardBuilding(cell, tables.CardObj.Get(cardId.Value)) : null;
        scene.CommitUnlock(cell, cardId, building);
        owner.SpendCurrency(Cost(cell));
        scene.territory.ExpandFrom(cell, actor);
        scene.NotifyStructureChanged();
        scene.NotifyUnlockCompleted(cell, actor);
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
        foreach (var cell in scene.cells.Values.Where(cell => cell.capturable && !cell.unlocked && (cell.q < 0 || (cell.q == 0 && cell.k < 0))).OrderBy(cell => cell.configId)) {
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
        if (enemy == null || !enemy.capturable || enemy.unlocked) {
            throw new InvalidOperationException("解锁内容的旋转对应格非法：" + friendly.configId);
        }
        friendly.SetUnlockConfig(rule);
        enemy.SetUnlockConfig(rule);
    }

    /**权重决定空/采集站/品质，命中品质后在本局同品质兵种卡中按配置权重抽取。*/
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
                long totalWeight = candidates.Sum(id => (long)tables.CardObj.Get(id).UnlockWeight);
                if (totalWeight <= 0 || totalWeight > int.MaxValue || candidates.Any(id => tables.CardObj.Get(id).UnlockWeight <= 0)) {
                    throw new InvalidOperationException("同品质卡牌解锁权重非法：" + quality.Quality);
                }
                int cardRoll = random.Next((int)totalWeight);
                foreach (int id in candidates) {
                    cardRoll -= tables.CardObj.Get(id).UnlockWeight;
                    if (cardRoll < 0) {
                        return id;
                    }
                }
                throw new InvalidOperationException("同品质卡牌解锁权重未命中。");
            }
        }
        throw new InvalidOperationException("解锁结果权重未命中。");
    }

}

