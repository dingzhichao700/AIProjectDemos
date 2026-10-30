using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>电脑玩家只决定买哪格，通过公共业务入口提交；不预测抽卡或赠送货币。</summary>
public sealed class BattleAIModel : IDisposable {
    private readonly BattleSceneModel scene;
    private readonly Dictionary<BattleSceneCamp, BattleAIPlayerVO> players = new Dictionary<BattleSceneCamp, BattleAIPlayerVO>();
    public IReadOnlyDictionary<BattleSceneCamp, BattleAIPlayerVO> states { get; }

    internal BattleAIModel(BattleSceneModel scene, Tables tables) {
        this.scene = scene;
        states = new ReadOnlyDictionary<BattleSceneCamp, BattleAIPlayerVO>(players);
        foreach (var owner in scene.camps.Values) {
            var start = owner.config;
            if (start.ControlType == BattlePlayerControlType.HUMAN) {
                if (start.AiProfileId != 0) {
                    throw new InvalidOperationException("真人阵营不应配置电脑策略：" + start.SceneId + "/" + start.Camp);
                }
                continue;
            }
            if (start.ControlType != BattlePlayerControlType.COMPUTER) {
                throw new InvalidOperationException("未知玩家控制类型：" + start.ControlType);
            }
            var profile = tables.BattleAIProfileObj.GetOrDefault(start.AiProfileId);
            ValidateProfile(profile);
            players.Add(owner.camp, new BattleAIPlayerVO(owner, profile));
        }
        scene.playerTimeUpdated += OnPlayerTime;
        scene.enemyTimeUpdated += OnEnemyTime;
    }

    private static void ValidateProfile(BattleAIProfileResource p) {
        if (p == null || p.InitialDelayMs < 0 || p.DecisionIntervalMs <= 0 || p.TargetCollectors < 0
            || p.ProducersPerCollector <= 0 || p.CurrencyReserve < 0
            || !ValidWeight(p.NeedWeight) || !ValidWeight(p.ForwardWeight) || !ValidWeight(p.ExpansionWeight)
            || (double)p.NeedWeight + p.ForwardWeight + p.ExpansionWeight <= 0) {
            throw new InvalidOperationException("电脑策略缺失或数值非法。");
        }
    }

    private static bool ValidWeight(float value) {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    }

    private void OnPlayerTime(float delta) {
        Advance(BattleSceneCamp.FRIENDLY, delta);
    }

    private void OnEnemyTime(float delta) {
        Advance(BattleSceneCamp.ENEMY, delta);
    }

    internal void Advance(BattleSceneCamp camp, float delta) {
        if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) {
            throw new ArgumentOutOfRangeException(nameof(delta));
        }
        if (delta == 0 || scene.expired || !players.TryGetValue(camp, out var state)) {
            return;
        }
        state.remainingMs -= delta * 1000d;
        if (state.remainingMs > .0001d) {
            return;
        }
        // 一个长帧最多一次操作，不补发过去的决策，也不积压到下一帧。
        state.remainingMs = state.profile.DecisionIntervalMs;
        state.decisionCount++;
        var choice = ChooseCell(state);
        if (choice != null && BattleControl.TryUnlockForCamp(scene, camp, choice.id, out _)) {
            state.unlockCount++;
        }
    }

    private BattleCellVO ChooseCell(BattleAIPlayerVO state) {
        var camp = state.owner.camp;
        var profile = state.profile;
        int producers = 0, collectors = 0;
        foreach (var building in scene.buildings.Values) {
            if (!building.alive || building.camp != camp) {
                continue;
            }
            if (building.config.Type == BuildingType.UNIT_PRODUCER) {
                producers++;
            } else if (building.config.Type == BuildingType.COLLECTOR) {
                collectors++;
            }
        }
        bool needProducer = producers == 0 || collectors >= profile.TargetCollectors
            || (collectors > 0 && producers < (long)collectors * profile.ProducersPerCollector);
        var home = scene.buildings.Values.FirstOrDefault(b => b.alive && b.camp == camp && b.config.Type == BuildingType.BASE);
        var opponent = scene.buildings.Values.FirstOrDefault(b => b.alive && b.camp != camp && b.config.Type == BuildingType.BASE);
        if (home == null || opponent == null) {
            return null;
        }
        float baseDistance = Mathf.Max(1, Vector2.Distance(home.position, opponent.position));
        BattleCellVO selected = null;
        double best = double.NegativeInfinity;
        foreach (var cell in scene.cells.Values) {
            if (!scene.unlocks.CanUnlock(cell.id, camp, out _) || state.owner.currency - cell.unlockConfig.Cost < profile.CurrencyReserve) {
                continue;
            }
            var rule = cell.unlockConfig;
            double unitWeight = rule.UnitQualityWeights.Sum(w => (double)w.Weight);
            double total = rule.EmptyWeight + rule.CollectorWeight + unitWeight;
            double needChance = (needProducer ? unitWeight : rule.CollectorWeight) / total;
            double forward = Mathf.Clamp01(1 - Vector2.Distance(cell.position, opponent.position) / baseDistance);
            int expansion = 0, neighborCount = 0;
            foreach (var neighbor in scene.GetNeighbors(cell)) {
                neighborCount++;
                if (neighbor.camp == camp && neighbor.groundPassable && !neighbor.unlocked && !scene.unlocks.IsRevealed(neighbor)) {
                    expansion++;
                }
            }
            double score = needChance * profile.NeedWeight + forward * profile.ForwardWeight
                + expansion / (double)Math.Max(1, neighborCount) * profile.ExpansionWeight;
            if (selected == null || score > best || (score == best && (rule.Cost < selected.unlockConfig.Cost
                || (rule.Cost == selected.unlockConfig.Cost && cell.configId < selected.configId)))) {
                selected = cell;
                best = score;
            }
        }
        return selected;
    }

    public void Dispose() {
        scene.playerTimeUpdated -= OnPlayerTime;
        scene.enemyTimeUpdated -= OnEnemyTime;
        players.Clear();
    }
}
