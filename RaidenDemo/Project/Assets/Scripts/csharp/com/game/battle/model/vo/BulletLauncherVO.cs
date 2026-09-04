/// <summary>
/// 子弹发射器运行状态与发射行为
/// </summary>
/// <remarks>
/// 独立维护生效状态、升级修正和发射时序，按配置输出完整发射请求，不负责场景登记。
/// </remarks>
internal sealed class BulletLauncherVO {

    /**发射规格的唯一配置来源*/
    public readonly BulletLauncherConfigVO config;

    /**配置查询由组装入口注入，发射器不依赖具体模块单例*/
    private readonly System.Func<int, int, int, BulletConfigVO> resolveBullet;

    /**独立开关；关闭时冻结整轮与轮内冷却，不产生补发时间债*/
    public bool isActive { get; set; } = true;

    /**升级发生时更新的发射器修正，不在子弹生成环节按阵营推断*/
    public int additionalLevel { get; private set; }

    /**已解析的有效子弹配置，可供同配置子弹共用*/
    public BulletConfigVO effectiveBullet { get; private set; }

    /**距离下一次生成事件的时间；轮内为子弹间隔，轮末才切换为开火间隔*/
    private double emissionCooldown;

    /**当前轮下一颗子弹的序号*/
    private int nextProjectileIndex;

    /**当前轮尚未发射的数量*/
    private int pendingProjectileCount;

    /**创建独立发射器并解析初始有效配置*/
    public BulletLauncherVO(BulletLauncherConfigVO config, System.Func<int, int, int, BulletConfigVO> resolveBullet, int additionalLevel = 0) {
        this.config = config ?? throw new System.ArgumentNullException(nameof(config));
        this.resolveBullet = resolveBullet ?? throw new System.ArgumentNullException(nameof(resolveBullet));
        SetAdditionalLevel(additionalLevel);
    }

    /**更新有效配置；不会重置冷却，也不会修改已经生成的子弹*/
    public void SetAdditionalLevel(int level) {
        additionalLevel = UnityEngine.Mathf.Max(0, level);
        effectiveBullet = resolveBullet(config.bulletType, config.bulletLevel, additionalLevel);
        if (effectiveBullet == null) {
            UnityEngine.Debug.LogError($"发射器子弹配置缺失：type={config.bulletType}, level={config.bulletLevel}, additionalLevel={additionalLevel}");
        }
    }

    /// <summary>
    /// 顺序推进轮内连发和轮后等待；关闭时冻结当前阶段，不积累跨轮冷却债。
    /// </summary>
    public void Update(float deltaTime, AircraftVO owner, System.Action<BulletLaunchVO> emit) {
        if (!isActive || effectiveBullet == null || emit == null || deltaTime <= 0f) {
            return;
        }
        double remainingTime = deltaTime;
        while (isActive) {
            if (emissionCooldown > remainingTime) {
                emissionCooldown -= remainingTime;
                return;
            }
            remainingTime -= emissionCooldown;
            if (pendingProjectileCount == 0) {
                nextProjectileIndex = 0;
                pendingProjectileCount = UnityEngine.Mathf.Max(1, config.bulletCount);
            }
            float direction = GetProjectileDirection(nextProjectileIndex++);
            pendingProjectileCount--;
            // 只有最后一颗子弹生成后才进入轮后等待；剩余帧时间按顺序消耗，不跨阶段重复扣减。
            emissionCooldown = pendingProjectileCount == 0
                ? System.Math.Max(0.001, config.fireInterval)
                : System.Math.Max(0, config.bulletIntervalMs) / 1000.0;
            emit(new BulletLaunchVO(owner, owner.position + config.offset, config.offset, effectiveBullet, direction, (float)remainingTime));
        }
    }

    /// <summary>
    /// 按发射器配置计算散射方向；现有配置的方向和偏移使用战场坐标，不隐式跟随机身旋转。
    /// </summary>
    private float GetProjectileDirection(int index) {
        if (config.bulletCount <= 1 || config.spreadAngle <= 0f) {
            return config.direction;
        }
        float ratio = index / (float)(config.bulletCount - 1);
        switch (config.spreadType) {
            case cfg.BulletSpreadType.LEFT:
                return config.direction + config.spreadAngle * ratio;
            case cfg.BulletSpreadType.RIGHT:
                return config.direction - config.spreadAngle * ratio;
            default:
                return config.direction - config.spreadAngle * 0.5f + config.spreadAngle * ratio;
        }
    }

    /**重置发射会话，保留已应用的升级配置；全新实体通过构造函数建立初始修正*/
    public void Reset() {
        isActive = true;
        emissionCooldown = 0.0;
        nextProjectileIndex = 0;
        pendingProjectileCount = 0;
    }

}
