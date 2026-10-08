using System;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>已验证的单个敌机波次配置。</summary>
/// <remarks>在进入关卡前转换配置、验证参数并计算编队布局；不保存运行时间。</remarks>
public sealed class EnemyWaveVO {
    public readonly int id;
    public readonly EnemyConfigVO enemy;
    public EnemyClass enemyClass => enemy.enemyClass;
    public readonly EnemyMotionType motionType;
    public readonly EnemyFormationType formationType;
    public readonly int count;
    public readonly Vector2 spawnCenter;
    public readonly float spacing;
    public readonly float direction;
    public readonly float entrySpeedMultiplier;
    public readonly float prepareDuration;
    public readonly float attackDuration;
    public readonly float patrolAmplitude;
    public readonly float stationHeightRatio;
    public readonly Vector2 exitDirection;
    public readonly int rewardItemId;
    public readonly EnemyFormationLayoutVO layout;

    public EnemyWaveVO(EnemyConfigVO enemy, StageWaveResource config) {
        this.enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        id = config.Id;
        motionType = config.MotionType;
        formationType = config.FormationType;
        count = config.EnemyCount;
        spawnCenter = new Vector2(config.SpawnCenter.X, config.SpawnCenter.Y);
        spacing = config.Spacing;
        direction = config.MotionDirection;
        entrySpeedMultiplier = config.EntrySpeedMultiplier;
        prepareDuration = config.PrepareDurationMs / 1000f;
        attackDuration = config.AttackDurationMs / 1000f;
        patrolAmplitude = config.PatrolAmplitude;
        stationHeightRatio = config.StationHeightRatio;
        exitDirection = new Vector2(config.ExitDirection.X, config.ExitDirection.Y).normalized;
        rewardItemId = config.RewardItemId;
        if (!Enum.IsDefined(typeof(EnemyMotionType), motionType) || !Enum.IsDefined(typeof(EnemyFormationType), formationType) ||
            count <= 0 || spacing < 0f || Mathf.Abs(direction) != 1f || entrySpeedMultiplier <= 0f ||
            prepareDuration < 0f || attackDuration <= prepareDuration || patrolAmplitude < 0f ||
            stationHeightRatio < 0f || stationHeightRatio > 1f || exitDirection.sqrMagnitude == 0f || exitDirection.y < 0f ||
            !IsFinite(entrySpeedMultiplier) || !IsFinite(spacing) || !IsFinite(patrolAmplitude) || !IsFinite(stationHeightRatio) ||
            !IsFinite(spawnCenter.x) || !IsFinite(spawnCenter.y) || !IsFinite(exitDirection.x) || !IsFinite(exitDirection.y)) {
            throw new InvalidOperationException($"波次 {id} 的路径参数无效");
        }
        if (motionType == EnemyMotionType.HORIZONTAL_PASS && (exitDirection.y != 0f || Mathf.Sign(exitDirection.x) != direction)) {
            throw new InvalidOperationException($"横穿波次 {id} 必须从前进方向的侧边撤离");
        }
        if (enemyClass == EnemyClass.NORMAL) {
            layout = new EnemyFormationLayoutVO(this);
        } else if (count != 1 || enemy.displaySize.x + 2f * BattleConst.EnemyFormationViewportPadding > BattleConst.BattleViewportWidth ||
            enemy.displaySize.y > -BattleConst.EnemyActivityBottom - BattleConst.EnemyFormationViewportPadding ||
            spawnCenter.x < enemy.displaySize.x * 0.5f + BattleConst.EnemyFormationViewportPadding ||
            spawnCenter.x > BattleConst.BattleViewportWidth - enemy.displaySize.x * 0.5f - BattleConst.EnemyFormationViewportPadding ||
            spawnCenter.y < BattleConst.EnemyActivityBottom + enemy.displaySize.y * 0.5f) {
            throw new InvalidOperationException($"特殊敌机波次 {id} 的数量、尺寸或入场横坐标无效");
        }
    }

    private static bool IsFinite(float value) {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
