using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家编队数据管理
/// </summary>
/// <remarks>
/// 管理玩家主机、僚机编队、玩家碰撞形状和本局剩余生命。
/// </remarks>
internal sealed class BattleFormationModel {
    private readonly List<AircraftVO> wingmanUnits = new List<AircraftVO>();
    private WingmanConfigVO wingmanConfig;
    private bool firingEnabled;

    public AircraftVO player { get; private set; }
    public IReadOnlyList<AircraftVO> wingmen => wingmanUnits;
    public AircraftCollisionVO playerCollision { get; private set; }
    public int playerLives { get; private set; }
    public bool isPlayerAlive => player != null && player.lifecycleState == PlayerLifecycleState.Alive;

    public void Initialize() {
        playerLives = BattleConst.PlayerInitialLifeCount;
        wingmanConfig = null;
        wingmanUnits.Clear();
        firingEnabled = false;
    }

    public void SetPlayer(AircraftVO unit) {
        player = unit;
    }

    public void ConfigureWingman(WingmanConfigVO config) {
        wingmanConfig = config;
    }

    /**数量未满时创建下一槽位僚机；满员后不再变化。*/
    public AircraftVO ApplyWingmanReward(Func<WingmanConfigVO, int, Vector2, AircraftVO> createUnit, out bool created) {
        created = false;
        if (player == null || wingmanConfig == null || wingmanUnits.Count >= wingmanConfig.maxCount) {
            return null;
        }
        int slotIndex = wingmanUnits.Count;
        Vector2 offset = wingmanConfig.formationOffsets[slotIndex];
        AircraftVO wingman = createUnit(wingmanConfig, slotIndex, player.position + offset);
        wingman.ConfigureFollow(player, offset, wingmanConfig.followSpeed);
        wingman.SetFiringEnabled(firingEnabled);
        wingmanUnits.Add(wingman);
        created = true;
        return wingman;
    }

    public void SetCollision(AircraftCollisionVO collision) {
        playerCollision = collision;
    }

    /**统一控制玩家飞机和全部僚机的发射器状态。*/
    public void SetFiringEnabled(bool enabled) {
        firingEnabled = enabled;
        player?.SetFiringEnabled(enabled);
        foreach (AircraftVO wingman in wingmanUnits) wingman.SetFiringEnabled(enabled);
    }

    /**重置玩家飞机和全部僚机的发射节奏。*/
    public void ResetLaunchers() {
        player?.ResetLaunchers();
        foreach (AircraftVO wingman in wingmanUnits) wingman.ResetLaunchers();
    }

    /**玩家死亡时移除全部僚机，下一次需重新拾取。*/
    public void ClearWingmen(Action<long> removeElement) {
        foreach (AircraftVO wingman in wingmanUnits) removeElement?.Invoke(wingman.id);
        wingmanUnits.Clear();
    }

    public int AddLife(int value) {
        playerLives += Mathf.Max(0, value);
        return playerLives;
    }

    public int ConsumeLife() {
        playerLives = Mathf.Max(0, playerLives - 1);
        return playerLives;
    }

    public void Clear() {
        player = null;
        wingmanConfig = null;
        wingmanUnits.Clear();
        playerCollision = null;
        Initialize();
    }
}
