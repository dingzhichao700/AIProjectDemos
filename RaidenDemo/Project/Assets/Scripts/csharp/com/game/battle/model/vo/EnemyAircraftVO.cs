using cfg;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌方飞机单位数据
/// </summary>
/// <remarks>
/// 管理敌机身份、碰撞、编队、特殊运动和死亡期间移动。
/// </remarks>
internal sealed class EnemyAircraftVO : AircraftVO {

    /**敌机级别*/
    public readonly EnemyClass enemyClass;

    /**敌机显示尺寸*/
    public readonly Vector2 size;

    /**敌机逻辑碰撞形状*/
    public readonly AircraftCollisionVO collision;

    /**敌机运动方式*/
    public readonly EnemyMotionType motionType;

    /**敌机被击破后提供的分数*/
    public readonly int scoreValue;

    /**普通敌机在编队中的位置序号*/
    public readonly int formationIndex;

    /**普通敌机所属编队的成员总数*/
    public readonly int formationCount;

    /**特殊敌机当前的水平移动方向*/
    public float horizontalDirection = 1f;

    /**特殊敌机当前运动阶段的累计时间*/
    public float motionTime;

    /**特殊敌机巡航运动的水平原点*/
    public float originX;

    /**普通编队路径的移动方向*/
    public float motionDirection;

    /**死亡表现期间是否继续执行移动*/
    public bool deathMovementActive { get; private set; }

    /**是否在公共HUD显示共享生命条*/
    public bool showsSharedHealth => enemyClass != EnemyClass.NORMAL;

    /**是否为Boss敌机*/
    public bool isBoss => enemyClass == EnemyClass.BOSS;

    /**普通敌机编队是否已进入离场阶段*/
    public bool isLeavingFormation => formationPath != null && formationPath.isLeaving;

    /**普通敌机使用的共享编队路径*/
    private readonly EnemyFormationPathVO formationPath;

    /**精英或Boss使用的波次运动参数*/
    private readonly EnemyWaveVO specialMotion;

    /**特殊敌机完整进入视野后的累计时间*/
    private float enemyVisibleTime;

    /// <summary>
    /// 创建敌机单位。
    /// </summary>
    /// <param name="id">实体ID</param>
    /// <param name="position">初始坐标</param>
    /// <param name="enemyClass">敌机级别</param>
    /// <param name="size">显示尺寸</param>
    /// <param name="collision">碰撞形状</param>
    /// <param name="baseAttributes">基础属性</param>
    /// <param name="motionType">运动方式</param>
    /// <param name="scoreValue">击破分数</param>
    /// <param name="formationIndex">编队序号</param>
    /// <param name="formationCount">编队数量</param>
    /// <param name="motionDirection">移动方向</param>
    /// <param name="appearancePath">正常外观路径</param>
    /// <param name="damagedAppearancePath">战损外观路径</param>
    /// <param name="semanticName">语义名称</param>
    /// <param name="formationPath">编队路径</param>
    /// <param name="specialMotion">特殊运动参数</param>
    public EnemyAircraftVO(long id, Vector2 position, EnemyClass enemyClass, Vector2 size, AircraftCollisionVO collision, IReadOnlyDictionary<AttributeType, int> baseAttributes, EnemyMotionType motionType = EnemyMotionType.STATIONARY, int scoreValue = 0, int formationIndex = 0, int formationCount = 1, float motionDirection = 1f, string appearancePath = null, string damagedAppearancePath = null, string semanticName = null, EnemyFormationPathVO formationPath = null, EnemyWaveVO specialMotion = null)
        : base(id, semanticName ?? $"enemyEntity{id}", SceneElementFaction.ENEMY, TimerType.ENEMY, position) {
        ConfigureAppearance(appearancePath, damagedAppearancePath);
        this.enemyClass = enemyClass;
        this.size = size;
        this.collision = collision;
        this.motionType = motionType;
        this.scoreValue = scoreValue;
        this.formationIndex = formationIndex;
        this.formationCount = formationCount;
        this.motionDirection = Mathf.Sign(motionDirection);
        this.formationPath = formationPath;
        this.specialMotion = specialMotion;
        ApplyBaseAttributes(baseAttributes);
        originX = position.x;
        SetFiringEnabled(true);
        rotation = BattleConst.EnemyAircraftVisualRotation;
    }

    /**爆炸解体期间是否继续执行原有移动*/
    protected override bool continueDisintegrateMovement => deathMovementActive;

    /**Boss爆炸解体表现结束后保留机身*/
    public override bool retainBodyAfterDeathPresentation => isBoss && deathType == AircraftDeathType.DISINTEGRATE;

    /**进入死亡状态后停止发射，并按死亡类型决定移动策略*/
    protected override void OnDeathPresentationStarted(AircraftDeathType type) {
        deathMovementActive = type == AircraftDeathType.DISINTEGRATE;
    }

    /// <summary>
    /// 处理最后一次死亡爆炸。
    /// </summary>
    /// <remarks>
    /// 表现层只报告播放进度，实体根据移除规则决定是否继续移动。
    /// </remarks>
    public void OnLastDeathExplosionStarted() {
        deathMovementActive = retainBodyAfterDeathPresentation;
    }

    /// <summary>
    /// 更新敌机移动。
    /// </summary>
    /// <param name="deltaTime">距上次更新经过的时间（秒）</param>
    protected override void UpdateAircraftMovement(float deltaTime) {
        if (isBoss) {
            UpdateBoss(deltaTime);
        } else if (enemyClass == EnemyClass.ELITE) {
            UpdateElite(deltaTime);
        } else {
            UpdateNormal(deltaTime);
        }
    }

    /// <summary>
    /// 判断敌机是否满足发射条件。
    /// </summary>
    /// <param name="deltaTime">距上次更新经过的时间（秒）</param>
    /// <returns>当前是否允许发射</returns>
    /// <remarks>
    /// 普通敌机跟随编队发射许可，特殊敌机完整入场并等待准备时间后才允许发射。
    /// </remarks>
    protected override bool CanLaunch(float deltaTime) {
        if (formationPath != null) {
            return formationPath.canFire;
        }
        bool fullyVisible = position.y + size.y * 0.5f <= 0f &&
            position.y - size.y * 0.5f >= BattleConst.EnemyActivityBottom &&
            position.x - size.x * 0.5f >= 0f &&
            position.x + size.x * 0.5f <= BattleConst.BattleViewportWidth;
        if (!fullyVisible) {
            enemyVisibleTime = 0f;
            return false;
        }
        enemyVisibleTime += deltaTime;
        return enemyVisibleTime >= specialMotion.prepareDuration;
    }

    /**更新普通敌机*/
    private void UpdateNormal(float deltaTime) {
        if (formationPath == null) {
            throw new System.InvalidOperationException("普通敌机必须使用编队路径。");
        }
        Vector2 previousPosition = position;
        position = formationPath.GetMemberPosition(formationIndex);
        UpdateVisualRotation(position - previousPosition, deltaTime);
    }

    /**更新敌机朝向*/
    private void UpdateVisualRotation(Vector2 movement, float deltaTime) {
        if (movement.sqrMagnitude <= 0.0001f) {
            return;
        }
        float rawRotation = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg - 90f;
        float bank = Mathf.Clamp(Mathf.DeltaAngle(BattleConst.EnemyAircraftVisualRotation, rawRotation), -BattleConst.EnemyFormationBankAngle, BattleConst.EnemyFormationBankAngle);
        float smooth = 1f - Mathf.Exp(-BattleConst.EnemyVisualTurnSmoothness * deltaTime);
        rotation = Mathf.LerpAngle(rotation, BattleConst.EnemyAircraftVisualRotation + bank, smooth);
    }

    /**获取特殊敌机停留高度*/
    private float GetSpecialStationY() {
        return Mathf.Lerp(BattleConst.EnemyActivityBottom + size.y * 0.5f, -BattleConst.EnemyFormationViewportPadding - size.y * 0.5f, specialMotion.stationHeightRatio);
    }

    /**更新精英敌机*/
    private void UpdateElite(float deltaTime) {
        float targetY = GetSpecialStationY();
        Vector2 next = position;
        if (next.y > targetY) {
            next.y = Mathf.MoveTowards(next.y, targetY, moveSpeed * deltaTime);
        } else {
            float minX = size.x * 0.5f + BattleConst.EnemyFormationViewportPadding;
            float maxX = BattleConst.BattleViewportWidth - minX;
            float targetX = horizontalDirection > 0f ? maxX : minX;
            next.x = Mathf.MoveTowards(next.x, targetX, moveSpeed * deltaTime);
            if (Mathf.Approximately(next.x, targetX)) {
                horizontalDirection *= -1f;
            }
        }
        position = next;
    }

    /**更新Boss敌机*/
    private void UpdateBoss(float deltaTime) {
        float targetY = GetSpecialStationY();
        Vector2 next = position;
        if (next.y > targetY) {
            next.y = Mathf.MoveTowards(next.y, targetY, moveSpeed * deltaTime);
            motionTime = 0f;
        } else {
            motionTime += deltaTime;
            float margin = size.x * 0.5f + BattleConst.EnemyFormationViewportPadding;
            float amplitude = Mathf.Max(0f, Mathf.Min(specialMotion.patrolAmplitude, Mathf.Min(originX - margin, BattleConst.BattleViewportWidth - margin - originX)));
            float frequency = amplitude > 0f ? moveSpeed / amplitude : 0f;
            next.x = originX + Mathf.Sin(motionTime * frequency) * amplitude;
        }
        position = next;
    }

}
