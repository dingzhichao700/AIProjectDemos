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

    public RewardVO Spawn(Vector2 position, BattleRewardType type, bool isNaturalSupply, AircraftVO player, Func<long> createId, Action<SceneElementVO> addElement, Action<RewardVO> onSpawned) {
        RewardVO reward = new RewardVO(createId(), position, type, isNaturalSupply);
        reward.target = player;
        rewards.Add(reward);
        addElement(reward);
        onSpawned?.Invoke(reward);
        return reward;
    }

    public void UpdateNaturalSupply(float deltaTime, bool bossSpawned, Action<Vector2, BattleRewardType, bool> spawn) {
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
        BattleRewardType type = stageId == 1 ? BattleRewardType.PlayerUpgrade : GetWaveRewardType(naturalSupplyCount);
        float margin = BattleConst.NaturalSupplySpawnMargin;
        Vector2 position = new Vector2(UnityEngine.Random.Range(margin, 720f - margin), -margin);
        spawn(position, type, true);
        naturalSupplyCount++;
    }

    public void Update(AircraftVO player, bool playerAlive, AircraftCollisionVO collision, Func<RewardVO, bool> remove, Action<int> addLife, Action playerChanged, Action<RewardVO, int> collected) {
        for (int i = rewards.Count - 1; i >= 0; i--) {
            RewardVO reward = rewards[i];
            reward.target = playerAlive ? player : null;
            if (reward.position.y < -1340f) {
                remove(reward);
                continue;
            }
            if (!playerAlive || reward.type == BattleRewardType.PlayerUpgrade && playerUpgradeBlocked) {
                continue;
            }
            if (!BattleCollisionSystem.Overlaps(reward.position, BattleConst.UpgradeDropHitSize, player.position, collision)) {
                continue;
            }
            int healed = 0;
            if (reward.type == BattleRewardType.Health) {
                healed = player.Heal(BattleConst.HealthDropHealAmount);
                playerChanged?.Invoke();
            } else if (reward.type == BattleRewardType.Life) {
                addLife(BattleConst.LifeDropAddCount);
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

    public static BattleRewardType GetWaveRewardType(int waveIndex) {
        switch (waveIndex % 4) {
            case 0:
                return BattleRewardType.Health;
            case 1:
                return BattleRewardType.PlayerUpgrade;
            case 2:
                return BattleRewardType.WingmanUpgrade;
            default:
                return BattleRewardType.Life;
        }
    }
}
