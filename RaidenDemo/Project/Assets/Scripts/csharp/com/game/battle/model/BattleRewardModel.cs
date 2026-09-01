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

    private int stageId;
    private float naturalSupplyCooldown;
    private int naturalSupplyCount;
    private bool playerUpgradeBlocked;

    public readonly List<RewardVO> rewards = new List<RewardVO>();

    public void Initialize(int currentStageId) {
        stageId = currentStageId;
        naturalSupplyCooldown = BattleConst.NaturalSupplyFirstDelay;
        naturalSupplyCount = 0;
        playerUpgradeBlocked = false;
    }

    public RewardVO Spawn(Vector2 position, StageItemType type, bool isNaturalSupply, Func<long> createId, Action<SceneElementVO> addElement, Action<RewardVO> onSpawned) {
        StageItemResource config = GetItemConfig(type);
        RewardVO reward = new RewardVO(createId(), position, config, isNaturalSupply);
        rewards.Add(reward);
        addElement(reward);
        onSpawned?.Invoke(reward);
        return reward;
    }

    public void UpdateNaturalSupply(float deltaTime, bool bossSpawned, Action<Vector2, StageItemType, bool> spawn) {
        if (bossSpawned) {
            return;
        }
        naturalSupplyCooldown -= deltaTime;
        if (naturalSupplyCooldown > 0f) {
            return;
        }
        foreach (RewardVO reward in rewards) {
            if (reward.isNaturalSupply) {
                return;
            }
        }
        naturalSupplyCooldown = BattleConst.NaturalSupplyInterval;
        StageItemType type = stageId == 1 ? StageItemType.PLAYER_UPGRADE : GetWaveRewardType(naturalSupplyCount);
        float margin = BattleConst.NaturalSupplySpawnMargin;
        Vector2 position = new Vector2(UnityEngine.Random.Range(margin, 720f - margin), -margin);
        spawn(position, type, true);
        naturalSupplyCount++;
    }

    public void Update(AircraftVO player, bool playerAlive, AircraftCollisionVO collision, Func<RewardVO, bool> remove, Action<int> addLife, Action playerChanged, Action<RewardVO, int> collected) {
        for (int i = rewards.Count - 1; i >= 0; i--) {
            RewardVO reward = rewards[i];
            if (reward.position.y < -1340f) {
                remove(reward);
                continue;
            }
            if (!playerAlive || reward.type == StageItemType.PLAYER_UPGRADE && playerUpgradeBlocked) {
                continue;
            }
            if (!BattleCollisionSystem.OverlapsCircle(reward.position, reward.collisionRadius, player.position, collision)) {
                continue;
            }
            int healed = 0;
            if (reward.type == StageItemType.HEALTH) {
                healed = player.Heal(reward.effectValue);
                playerChanged?.Invoke();
            } else if (reward.type == StageItemType.LIFE) {
                addLife(reward.effectValue);
            }
            remove(reward);
            collected?.Invoke(reward, healed);
        }
    }

    public void SetPlayerUpgradeBlocked(bool value) {
        playerUpgradeBlocked = value;
    }

    public void Clear() {
        rewards.Clear();
        Initialize(0);
    }

    public static StageItemType GetWaveRewardType(int waveIndex) {
        switch (waveIndex % 4) {
            case 0:
                return StageItemType.HEALTH;
            case 1:
                return StageItemType.PLAYER_UPGRADE;
            case 2:
                return StageItemType.WINGMAN_UPGRADE;
            default:
                return StageItemType.LIFE;
        }
    }

    private static StageItemResource GetItemConfig(StageItemType type) {
        foreach (StageItemResource config in CfgManager.tables.StageItemObj.DataList) {
            if (config.Type == type) {
                return config;
            }
        }
        throw new InvalidOperationException($"关卡道具类型 {type} 没有对应配置");
    }
}
