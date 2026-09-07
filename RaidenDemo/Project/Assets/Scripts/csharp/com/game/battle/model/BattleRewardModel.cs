using cfg;
using cfg.resource;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗奖励数据管理
/// </summary>
/// <remarks>
/// 管理关卡奖励实例、自然补给生成和玩家拾取结算。
/// </remarks>
internal sealed class BattleRewardModel {

    /**自然补给计时由场景 Timer 驱动，暂停时不推进*/
    private float naturalSupplyCooldown;
    private readonly Func<float, float, float> randomRange;

    public BattleRewardModel(Func<float, float, float> randomRange = null) {
        this.randomRange = randomRange ?? UnityEngine.Random.Range;
    }

    /**按关卡配置的道具顺序循环，与道具表行顺序无关*/
    private int naturalSupplyCount;
    private StageConfigVO stageConfig;

    private bool playerUpgradeBlocked;

    public readonly List<RewardVO> rewards = new List<RewardVO>();

    public void Initialize(StageConfigVO config) {
        stageConfig = config;
        naturalSupplyCooldown = config != null ? config.supplyFirstDelay : 0f;
        naturalSupplyCount = 0;
        playerUpgradeBlocked = false;
    }

    public RewardVO Spawn(Vector2 position, StageItemEffectType type, Func<long> createId, Action<SceneElementVO> addElement, Action<RewardVO> onSpawned) {
        return Spawn(position, GetItemConfig(type), createId, addElement, onSpawned);
    }

    /**自然补给和编队奖励共用当前道具创建、登记与表现通知链路*/
    public RewardVO Spawn(Vector2 position, StageItemResource config, Func<long> createId, Action<SceneElementVO> addElement, Action<RewardVO> onSpawned) {
        RewardVO reward = new RewardVO(createId(), position, config);
        rewards.Add(reward);
        addElement(reward);
        onSpawned?.Invoke(reward);
        return reward;
    }

    /// <summary>
    /// 战斗模拟期间持续生成自然补给，包含 Boss 阶段；卡帧不批量追补。
    /// </summary>
    public void UpdateNaturalSupply(float deltaTime, Action<Vector2, StageItemResource> spawn) {
        if (stageConfig == null || deltaTime <= 0f) {
            return;
        }
        naturalSupplyCooldown -= deltaTime;
        if (naturalSupplyCooldown > 0f) {
            return;
        }
        naturalSupplyCooldown = stageConfig.supplyInterval;
        IReadOnlyList<StageItemResource> configs = stageConfig.supplyItems;
        if (configs.Count == 0) {
            return;
        }
        StageItemResource config = configs[naturalSupplyCount % configs.Count];
        float margin = BattleConst.NaturalSupplySpawnMargin;
        Vector2 position = new Vector2(randomRange(margin, BattleConst.BattleViewportWidth - margin), -margin);
        spawn(position, config);
        naturalSupplyCount++;
    }

    public void Update(AircraftVO player, bool playerAlive, AircraftCollisionVO collision, Func<RewardVO, bool> remove, Action<int> addLife, Action playerChanged, Action<RewardVO, int> collected) {
        for (int i = rewards.Count - 1; i >= 0; i--) {
            RewardVO reward = rewards[i];
            if (!reward.isCollected && reward.IsOutsideViewport()) {
                remove(reward);
                continue;
            }
            if (reward.isCollected) {
                continue;
            }
            if (!playerAlive || reward.type == StageItemEffectType.PLAYER_UPGRADE && playerUpgradeBlocked) {
                continue;
            }
            if (!BattleCollisionSystem.OverlapsCircle(reward.position, reward.collisionRadius, player.position, collision)) {
                continue;
            }
            int healed = 0;
            if (reward.type == StageItemEffectType.HEALTH) {
                healed = player.Heal(reward.effectValue);
                playerChanged?.Invoke();
            } else if (reward.type == StageItemEffectType.LIFE) {
                addLife(reward.effectValue);
            }
            reward.MarkCollected();
            collected?.Invoke(reward, healed);
        }
    }

    public void SetPlayerUpgradeBlocked(bool value) {
        playerUpgradeBlocked = value;
    }

    public void Clear() {
        rewards.Clear();
        Initialize(null);
    }

    private static StageItemResource GetItemConfig(StageItemEffectType type) {
        foreach (StageItemResource config in CfgManager.tables.StageItemObj.DataList) {
            if (config.EffectType == type) {
                return config;
            }
        }
        throw new InvalidOperationException($"关卡道具类型 {type} 没有对应配置");
    }
}
