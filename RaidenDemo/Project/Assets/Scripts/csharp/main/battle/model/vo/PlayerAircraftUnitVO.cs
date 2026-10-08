using System;
using UnityEngine;

/// <summary>
/// 玩家飞机单位数据
/// </summary>
/// <remarks>
/// 管理玩家飞机的受击、死亡、复活、无敌与入场状态。
/// </remarks>
internal sealed class PlayerAircraftUnitVO : AircraftVO {

    /**玩家完成复活入场时调用的回调*/
    private Action playerRespawnCompleted;

    /**玩家飞机当前生命周期状态*/
    public PlayerLifecycleState lifecycleState { get; private set; } = PlayerLifecycleState.Alive;

    /**当前生命周期阶段剩余时间*/
    public float lifecycleRemaining { get; private set; }

    /**玩家飞机剩余无敌时间*/
    public float invincibleRemaining { get; private set; }

    /**无敌闪烁使用的时间间隔*/
    public float invincibleFlashInterval { get; private set; } = BattleConst.PlayerFlashInterval;

    /**玩家受击震动剩余时间*/
    public float hitShakeRemaining { get; private set; }

    /// <summary>
    /// 创建玩家飞机单位。
    /// </summary>
    /// <param name="id">实体ID</param>
    /// <param name="semanticName">语义名称</param>
    /// <param name="position">初始坐标</param>
    public PlayerAircraftUnitVO(long id, string semanticName, Vector2 position)
        : base(id, semanticName, SceneElementFaction.PLAYER, TimerType.PLAYER, position) {
    }

    /**设置复活完成回调*/
    public void ConfigurePlayerLifecycle(Action respawnCompleted) {
        playerRespawnCompleted = respawnCompleted;
    }

    /// <summary>
    /// 尝试伤害玩家飞机。
    /// </summary>
    /// <param name="value">伤害值</param>
    /// <returns>是否成功造成伤害</returns>
    /// <remarks>
    /// 死亡、复活和无敌状态下不会造成伤害。
    /// </remarks>
    public bool TryTakeDamage(int value) {
        if (lifecycleState != PlayerLifecycleState.Alive || invincibleRemaining > 0f) {
            return false;
        }
        bool defeated = TakeDamage(value);
        if (defeated) {
            lifecycleState = PlayerLifecycleState.Dying;
            lifecycleRemaining = 0f;
            invincibleRemaining = 0f;
            hitShakeRemaining = 0f;
            SetFiringEnabled(false);
        } else {
            invincibleRemaining = BattleConst.PlayerInvincibleDuration;
            invincibleFlashInterval = BattleConst.PlayerHitFlashInterval;
            hitShakeRemaining = BattleConst.PlayerHitShakeDuration;
        }
        return true;
    }

    /// <summary>
    /// 开始玩家复活。
    /// </summary>
    /// <remarks>
    /// 恢复生命并重置坐标、无敌、震动和发射状态。
    /// </remarks>
    public void BeginRespawn() {
        ResetDeathState();
        RestoreFullHealth();
        position = BattleConst.PlayerRespawnStart;
        lifecycleState = PlayerLifecycleState.Respawning;
        lifecycleRemaining = BattleConst.PlayerRespawnEnterDuration;
        invincibleRemaining = BattleConst.PlayerRespawnInvincibleDuration;
        invincibleFlashInterval = BattleConst.PlayerFlashInterval;
        hitShakeRemaining = 0f;
        SetFiringEnabled(false);
    }

    /**设置无敌时间*/
    public void GrantInvincibility(float duration) {
        invincibleRemaining = Mathf.Max(invincibleRemaining, duration);
        invincibleFlashInterval = BattleConst.PlayerFlashInterval;
        hitShakeRemaining = 0f;
    }

    /// <summary>
    /// 更新玩家生命周期。
    /// </summary>
    /// <param name="deltaTime">距上次更新经过的时间（秒）</param>
    protected override void UpdateAircraftMovement(float deltaTime) {
        invincibleRemaining = Mathf.Max(0f, invincibleRemaining - deltaTime);
        hitShakeRemaining = Mathf.Max(0f, hitShakeRemaining - deltaTime);
        if (lifecycleState == PlayerLifecycleState.Alive) {
            return;
        }
        lifecycleRemaining = Mathf.Max(0f, lifecycleRemaining - deltaTime);
        if (lifecycleState == PlayerLifecycleState.Dying) {
            return;
        }
        float duration = BattleConst.PlayerRespawnEnterDuration;
        float progress = duration <= 0f ? 1f : 1f - lifecycleRemaining / duration;
        position = Vector2.Lerp(BattleConst.PlayerRespawnStart, BattleConst.PlayerStart, Mathf.Clamp01(progress));
        if (lifecycleRemaining > 0f) {
            return;
        }
        position = BattleConst.PlayerStart;
        lifecycleState = PlayerLifecycleState.Alive;
        SetFiringEnabled(true);
        ResetLaunchers();
        playerRespawnCompleted?.Invoke();
    }

}
