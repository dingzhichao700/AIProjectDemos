using cfg;
using cfg.resource;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 飞机单位数据基类
/// </summary>
/// <remarks>
/// 管理玩家飞机与敌方飞机共有的生命、高度、近期伤害、死亡流程和着火点。
/// </remarks>
internal abstract class AircraftVO : FlyingUnitVO {

    /**死亡表现使用的爆炸效果序列*/
    public IReadOnlyList<AircraftExplosionVO> deathExplosions { get; private set; }

    /**最终属性决定的最大生命值*/
    public int maxHealth { get; private set; }

    /**飞机当前生命值*/
    public int health { get; private set; }

    /**配置确定的飞机体型*/
    public AircraftSizeType aircraftSizeType { get; private set; } = AircraftSizeType.SMALL;

    /**当前飞行高度*/
    public float height { get; private set; } = BattleAircraftDeathConst.NormalHeight;

    /**当前高度决定的显示比例*/
    public float displayScale => Mathf.Clamp(height / BattleAircraftDeathConst.NormalHeight, BattleAircraftDeathConst.GroundAircraftDisplayScale, 1f);

    /**指定统计窗口内累计的实际伤害*/
    public long recentDamage => recentDamageTracker.value;

    /**飞机是否已经进入死亡流程*/
    public bool isDying { get; private set; }

    /**本次死亡采用的表现类型*/
    public AircraftDeathType deathType { get; private set; }

    /**进入死亡表现时的飞机基础角度*/
    public float deathStartRotation { get; private set; }

    /**死亡表现开始后新增的旋转角度*/
    public float deathRotationOffset => isDying ? Mathf.DeltaAngle(deathStartRotation, rotation) : 0f;

    /**坠落飞机是否已经到达地面高度*/
    public bool hasLanded { get; private set; }

    /**本次坠地使用的爆炸特效ID*/
    public int crashExplosionEffectId { get; private set; }

    /**本次坠地使用的烟尘特效ID*/
    public int crashSmokeEffectId { get; private set; }

    /**坠落期间持续冒烟的局部坐标*/
    public Vector2 fallingSmokeLocalPosition { get; private set; }

    /**坠落期间循环播放的冒烟特效ID*/
    public int fallingSmokeEffectId { get; private set; }

    /**当前血量区间生成的只读着火点列表*/
    public IReadOnlyList<AircraftFirePointVO> firePoints { get; }

    /**着火点重建或销毁时通知表现同步*/
    public event Action<AircraftVO> firePointsChanged;

    /**所属计时器到达烟雾间隔时请求一次表现*/
    public event Action<AircraftVO, AircraftFirePointVO> smokeRequested;

    /**坠落飞机到达地面高度时通知所属战斗流程*/
    public event Action<AircraftVO> landed;

    /**当前着火点的数据集合*/
    private readonly List<AircraftFirePointVO> mutableFirePoints = new List<AircraftFirePointVO>();

    /**着火点及坠落资源使用的随机数来源*/
    private static readonly System.Random random = new System.Random();

    /**指定时间窗口内的伤害统计*/
    private readonly RecentDamageTracker recentDamageTracker = new RecentDamageTracker();

    /**坠落阶段的高度和速度状态*/
    private readonly AircraftFallingState fallingState = new AircraftFallingState();

    /**决定取点实际区域的碰撞组合*/
    private AircraftCollisionVO fireCollision;

    /**当前已应用的血量区间规则*/
    private AircraftFireRuleResource currentFireRule;

    /**死亡期间暂缓应用的体型或碰撞配置变化*/
    private bool fireConfigurationDirty;

    /**烟雾间隔内已累计的所属计时器时间，单位毫秒*/
    private double smokeElapsedMs;

    /// <summary>
    /// 创建飞机单位。
    /// </summary>
    /// <param name="id">实体ID</param>
    /// <param name="semanticName">语义名称</param>
    /// <param name="faction">所属阵营</param>
    /// <param name="timerType">所属计时器类型</param>
    /// <param name="position">初始坐标</param>
    protected AircraftVO(long id, string semanticName, SceneElementFaction faction, TimerType timerType, Vector2 position)
        : base(id, semanticName, faction, timerType, position) {
        firePoints = mutableFirePoints.AsReadOnly();
    }

    /// <summary>
    /// 应用着火点所需的体型和碰撞配置。
    /// </summary>
    /// <param name="size">配置体型</param>
    /// <param name="collision">组合碰撞区域，小型飞机允许为空</param>
    /// <remarks>
    /// 仅在体型或碰撞配置变化时重新取点；死亡期间保留已有着火点。
    /// </remarks>
    public void ConfigureFirePoints(AircraftSizeType size, AircraftCollisionVO collision) {
        if (!Enum.IsDefined(typeof(AircraftSizeType), size)) {
            throw new ArgumentOutOfRangeException(nameof(size), size, "未知飞机体型");
        }
        if (size != AircraftSizeType.SMALL && collision == null) {
            throw new ArgumentNullException(nameof(collision), "着火点需要实际碰撞区域");
        }
        bool changed = aircraftSizeType != size || fireCollision != collision;
        aircraftSizeType = size;
        fireCollision = collision;
        RefreshFirePoints(changed);
    }

    /**获取着火点的场景坐标*/
    public Vector2 GetFirePointWorldPosition(AircraftFirePointVO point) {
        return position + RotateDeathLocalPosition(point.localPosition);
    }

    /**获取坠落持续冒烟点的场景坐标*/
    public Vector2 GetFallingSmokeWorldPosition() {
        return position + RotateDeathLocalPosition(fallingSmokeLocalPosition);
    }

    /**获取死亡表现局部坐标对应的场景坐标*/
    public Vector2 GetDeathEffectWorldPosition(Vector2 localPosition) {
        return position + RotateDeathLocalPosition(localPosition);
    }

    /// <summary>
    /// 扣除生命值。
    /// </summary>
    /// <param name="value">伤害值</param>
    /// <returns>生命值是否耗尽</returns>
    public bool TakeDamage(int value) {
        if (isDying) {
            return health <= 0;
        }
        int appliedDamage = Mathf.Min(health, Mathf.Max(0, value));
        health -= appliedDamage;
        recentDamageTracker.Record(appliedDamage, BattleAircraftDeathConst.RecentDamageDurationMs);
        RefreshFirePoints();
        return health <= 0;
    }

    /// <summary>
    /// 恢复生命值。
    /// </summary>
    /// <param name="value">恢复值</param>
    /// <returns>实际恢复值</returns>
    public int Heal(int value) {
        if (isDying) {
            return 0;
        }
        int previous = health;
        health = Mathf.Min(maxHealth, health + Mathf.Max(0, value));
        RefreshFirePoints();
        return health - previous;
    }

    /**恢复全部生命值*/
    public void RestoreFullHealth() {
        health = maxHealth;
        RefreshFirePoints();
    }

    /// <summary>
    /// 开始死亡表现。
    /// </summary>
    /// <param name="type">本次死亡表现类型</param>
    public void BeginDeathPresentation(AircraftDeathType type) {
        if (isDying) {
            return;
        }
        isDying = true;
        deathType = type;
        deathStartRotation = rotation;
        deathExplosions = AircraftExplosionSequenceBuilder.Build(aircraftSizeType, type, fireCollision, random);
        SetFiringEnabled(false);
        if (type == AircraftDeathType.FALLING) {
            Vector2 fallingVelocity = velocity.sqrMagnitude > 0.0001f
                ? velocity * BattleAircraftDeathConst.FallingVelocityRate / 10000f
                : GetRandomFallingVelocity();
            fallingState.Begin(height, fallingVelocity, GetRandomFallingAngularAcceleration());
            fallingSmokeLocalPosition = fireCollision.GetRandomPoint(random);
            fallingSmokeEffectId = BattleAircraftDeathConst.GetRandom(BattleAircraftDeathConst.FallingSmokeEffectIds, random, "坠落持续冒烟特效");
            if (fallingSmokeEffectId <= 0) {
                throw new InvalidOperationException("常量表7061必须配置至少一个有效的持续冒烟特效ID");
            }
            crashExplosionEffectId = BattleAircraftDeathConst.GetRandom(BattleAircraftDeathConst.GetCrashExplosionEffectIds(aircraftSizeType), random, $"{aircraftSizeType}坠地爆炸特效");
            crashSmokeEffectId = BattleAircraftDeathConst.GetRandom(BattleAircraftDeathConst.GetCrashSmokeEffectIds(aircraftSizeType), random, $"{aircraftSizeType}坠地烟尘特效");
        }
        OnDeathPresentationStarted(type);
    }

    /**重置死亡与高度状态，供玩家复活使用*/
    protected void ResetDeathState() {
        isDying = false;
        deathType = default;
        deathStartRotation = rotation;
        height = BattleAircraftDeathConst.NormalHeight;
        crashExplosionEffectId = 0;
        crashSmokeEffectId = 0;
        fallingSmokeLocalPosition = Vector2.zero;
        fallingSmokeEffectId = 0;
        hasLanded = false;
        fallingState.Clear();
        recentDamageTracker.Clear();
        velocity = Vector2.zero;
    }

    /**推进飞机计时状态*/
    protected override void UpdateUnitState(float deltaTime) {
        recentDamageTracker.Update(deltaTime);
        UpdateSmoke(deltaTime);
    }

    /**按生存状态切换正常移动、爆炸移动或坠落移动*/
    protected sealed override void UpdateMovement(float deltaTime) {
        if (!isDying) {
            UpdateAircraftMovement(deltaTime);
            return;
        }
        if (deathType == AircraftDeathType.DISINTEGRATE) {
            if (continueDisintegrateMovement) {
                UpdateAircraftMovement(deltaTime);
            }
            return;
        }
        if (fallingState.landed) {
            return;
        }
        position += fallingState.Update(deltaTime);
        rotation = Mathf.Repeat(rotation + fallingState.rotationDelta, 360f);
        height = fallingState.height;
        if (fallingState.landed) {
            hasLanded = true;
            landed?.Invoke(this);
        }
    }

    /**爆炸解体期间是否继续原有移动*/
    protected virtual bool continueDisintegrateMovement => false;

    /**爆炸解体表现结束后是否保留机身*/
    public virtual bool retainBodyAfterDeathPresentation => false;

    /**更新具体飞机类型的正常移动*/
    protected virtual void UpdateAircraftMovement(float deltaTime) {
    }

    /**响应死亡表现开始*/
    protected virtual void OnDeathPresentationStarted(AircraftDeathType type) {
    }

    /**最大生命属性变化时同步生命状态*/
    protected override void OnAttributeChanged(AttributeType type) {
        if (type == AttributeType.MAX_LIFE) {
            RefreshMaxHealth();
        }
    }

    /**属性全量变化时同步生命状态*/
    protected override void OnAttributesRecalculated() {
        RefreshMaxHealth();
    }

    /// <summary>
    /// 清理飞机持有的数据。
    /// </summary>
    /// <remarks>
    /// 销毁时同步通知表现回收火焰，再注销回调，避免池化视图保留旧实体状态。
    /// </remarks>
    protected override void OnDestroy() {
        mutableFirePoints.Clear();
        currentFireRule = null;
        fireConfigurationDirty = false;
        smokeElapsedMs = 0d;
        recentDamageTracker.Clear();
        fallingState.Clear();
        firePointsChanged?.Invoke(this);
        firePointsChanged = null;
        smokeRequested = null;
        landed = null;
        base.OnDestroy();
    }

    /**刷新生命上限*/
    private void RefreshMaxHealth() {
        int previous = maxHealth;
        maxHealth = Mathf.Max(0, attributeContainer.GetAttr(AttributeType.MAX_LIFE));
        health = previous <= 0 ? maxHealth : Mathf.Clamp(health + maxHealth - previous, 0, maxHealth);
        RefreshFirePoints();
    }

    /// <summary>
    /// 按当前血量区间同步着火点。
    /// </summary>
    /// <param name="force">配置变化时强制重新取点</param>
    /// <remarks>
    /// 血量跨区间时按着火规模保留已有点，只补充或移除数量差额；生命耗尽时保留现状进入死亡表现。
    /// </remarks>
    private void RefreshFirePoints(bool force = false) {
        fireConfigurationDirty |= force;
        if (destroyed || health <= 0 || maxHealth <= 0) {
            return;
        }
        AircraftFireRuleResource rule = BattleAircraftFireConst.FindRule(aircraftSizeType, health, maxHealth);
        if (!fireConfigurationDirty && currentFireRule == rule) {
            return;
        }

        bool rebuildAll = fireConfigurationDirty;
        int previousCount = mutableFirePoints.Count;
        Dictionary<AircraftFireSize, int> desiredCounts = new Dictionary<AircraftFireSize, int>();
        if (rule != null) {
            foreach (AircraftFirePointCount entry in rule.FirePointList) {
                if (entry.Count <= 0) {
                    throw new InvalidOperationException($"飞机着火规则 {rule.Id} 的着火点数量必须大于 0");
                }
                desiredCounts.TryGetValue(entry.FireSize, out int count);
                desiredCounts[entry.FireSize] = checked(count + entry.Count);
            }
        }

        List<AircraftFirePointVO> nextPoints = new List<AircraftFirePointVO>();
        Dictionary<AircraftFireSize, int> currentCounts = new Dictionary<AircraftFireSize, int>();
        if (!rebuildAll) {
            foreach (AircraftFirePointVO point in mutableFirePoints) {
                if (!desiredCounts.TryGetValue(point.fireSize, out int desiredCount)) {
                    continue;
                }
                currentCounts.TryGetValue(point.fireSize, out int currentCount);
                if (currentCount >= desiredCount) {
                    continue;
                }
                nextPoints.Add(point);
                currentCounts[point.fireSize] = currentCount + 1;
            }
        }

        if (rule != null) {
            foreach (AircraftFirePointCount entry in rule.FirePointList) {
                IReadOnlyList<int> fireIds = BattleAircraftFireConst.GetFireEffectIds(entry.FireSize);
                IReadOnlyList<int> smokeIds = BattleAircraftFireConst.GetSmokeEffectIds(entry.FireSize);
                if (fireIds.Count == 0 || smokeIds.Count == 0) {
                    throw new InvalidOperationException($"着火规模 {entry.FireSize} 的火焰和烟雾候选不能为空");
                }
                int desiredCount = desiredCounts[entry.FireSize];
                currentCounts.TryGetValue(entry.FireSize, out int currentCount);
                while (currentCount < desiredCount) {
                    nextPoints.Add(new AircraftFirePointVO(fireCollision.GetRandomPoint(random), entry.FireSize, fireIds[random.Next(fireIds.Count)], smokeIds[random.Next(smokeIds.Count)]));
                    currentCount++;
                }
                currentCounts[entry.FireSize] = currentCount;
            }
        }

        mutableFirePoints.Clear();
        mutableFirePoints.AddRange(nextPoints);
        currentFireRule = rule;
        fireConfigurationDirty = false;
        if (rebuildAll || previousCount == 0 || mutableFirePoints.Count == 0) {
            smokeElapsedMs = 0d;
        }
        firePointsChanged?.Invoke(this);
    }

    /**推进着火点的烟雾生成间隔*/
    private void UpdateSmoke(float deltaTime) {
        if (deltaTime <= 0f || mutableFirePoints.Count == 0 || destroyed) {
            return;
        }
        int intervalMs = BattleAircraftFireConst.smokeIntervalMs;
        if (intervalMs <= 0) {
            throw new InvalidOperationException("烟雾生成间隔必须大于 0 毫秒");
        }
        smokeElapsedMs += deltaTime * 1000d;
        while (smokeElapsedMs >= intervalMs && !destroyed && mutableFirePoints.Count > 0) {
            smokeElapsedMs -= intervalMs;
            foreach (AircraftFirePointVO point in mutableFirePoints) {
                smokeRequested?.Invoke(this, point);
            }
        }
    }

    /**生成静止飞机开始坠落时使用的随机速度*/
    private static Vector2 GetRandomFallingVelocity() {
        float angle = (float)(random.NextDouble() * 360d) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BattleAircraftDeathConst.StationaryFallingSpeed;
    }

    /**从配置范围内生成坠落旋转角加速度*/
    private static int GetRandomFallingAngularAcceleration() {
        IReadOnlyList<int> range = BattleAircraftDeathConst.FallingAngularAccelerationRange;
        if (range == null || range.Count != 2 || range[0] > range[1]) {
            throw new InvalidOperationException("坠落角加速度范围必须由两个递增整数构成");
        }
        return random.Next(range[0], checked(range[1] + 1));
    }

    /**按高度比例和死亡旋转增量转换局部坐标*/
    private Vector2 RotateDeathLocalPosition(Vector2 localPosition) {
        Vector2 scaledPosition = localPosition * displayScale;
        float radians = deathRotationOffset * Mathf.Deg2Rad;
        return new Vector2(scaledPosition.x * Mathf.Cos(radians) - scaledPosition.y * Mathf.Sin(radians), scaledPosition.x * Mathf.Sin(radians) + scaledPosition.y * Mathf.Cos(radians));
    }

}
