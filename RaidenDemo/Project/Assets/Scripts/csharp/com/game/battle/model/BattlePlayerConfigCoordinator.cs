using System;
using UnityEngine;

/// <summary>
/// 玩家飞机配置协调
/// </summary>
/// <remarks>
/// 管理本局出战飞机选择、临时等级配置及战斗属性应用。
/// </remarks>
internal sealed class BattlePlayerConfigCoordinator {

    private readonly BattleConfigProvider configProvider;
    private int maxLevel;

    public int aircraftId { get; private set; }
    public int level => current?.level ?? 0;
    public PlayerAircraftBattleLevelVO current { get; private set; }

    public BattlePlayerConfigCoordinator(BattleConfigProvider configProvider) {
        this.configProvider = configProvider;
    }

    /**从机库选择和默认等级初始化本局飞机配置。*/
    public void Initialize() {
        PlayerAircraftVO selected = configProvider.GetSelectedPlayerAircraft();
        if (selected == null) {
            throw new InvalidOperationException("当前没有可用的出战玩家飞机");
        }
        aircraftId = selected.id;
        maxLevel = selected.maxLevel;
        int defaultLevel = Mathf.Clamp(configProvider.defaultAircraftLevel, 1, maxLevel);
        if (!TrySetLevel(defaultLevel)) {
            throw new InvalidOperationException(
                $"玩家飞机 {aircraftId} 无法初始化等级 {defaultLevel}");
        }
    }

    /**切换临时等级；缺少对应等级配置时保持原状态。*/
    public bool TrySetLevel(int targetLevel) {
        PlayerAircraftBattleLevelVO config = configProvider.GetPlayerAircraftLevel(aircraftId, targetLevel);
        if (config == null) {
            return false;
        }
        current = config;
        return true;
    }

    /**返回下一临时等级；达到上限时返回 false。*/
    public bool TryGetUpgradeLevel(out int targetLevel) {
        targetLevel = level + 1;
        return current != null && targetLevel <= maxLevel;
    }

    /**返回本机型经过等级上限约束后的默认临时等级。*/
    public int GetDefaultLevel() {
        return Mathf.Clamp(configProvider.defaultAircraftLevel, 1, maxLevel);
    }

    /**将当前等级的碰撞、血量和发射器应用到逻辑对象。*/
    public void ApplyBattleStats(BattleModel model, AircraftVO player) {
        if (current == null || player == null) {
            return;
        }
        model.SetPlayerCollision(current.collision);
        player.ApplyPlayerAircraftStats(current.baseHealth);
        model.ConfigurePlayerLaunchers(current.bulletLaunchers);
    }

    /**清除本局选择和临时等级。*/
    public void Clear() {
        aircraftId = 0;
        maxLevel = 0;
        current = null;
    }
}
