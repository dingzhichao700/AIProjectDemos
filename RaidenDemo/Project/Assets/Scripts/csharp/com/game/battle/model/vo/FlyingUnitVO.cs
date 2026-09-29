using cfg;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 飞行单位数据基类
/// </summary>
/// <remarks>
/// 管理飞机与僚机共有的属性、发射器、外观和瞬时速度，不表达具体单位类型的生命周期。
/// </remarks>
internal abstract class FlyingUnitVO : SceneElementVO {

    /**飞行单位持有的全部子弹发射器*/
    public readonly List<BulletLauncherVO> bulletLaunchers = new List<BulletLauncherVO>();

    /**汇总全部来源的最终属性容器*/
    public readonly AttributeContainer attributeContainer = new AttributeContainer(true);

    /**保存配置提供的基础属性*/
    public readonly AttributeContainer baseAttributeContainer = new AttributeContainer();

    /**汇总非战斗属性影响*/
    public readonly AttributeContainer nonBattleAttributeContainer = new AttributeContainer();

    /**汇总战斗内属性影响*/
    public readonly AttributeContainer battleAttributeContainer = new AttributeContainer();

    /**用于日志和对象识别的语义名称*/
    public readonly string semanticName;

    /**正常外观资源路径*/
    public string appearancePath { get; private set; }

    /**战损外观资源路径*/
    public string damagedAppearancePath { get; private set; }

    /**最终属性决定的实际移动速度*/
    public float moveSpeed => attributeContainer.GetAttr(AttributeType.SPEED);

    /**上一帧实际位移计算出的当前速度向量*/
    public Vector2 velocity { get; protected set; }

    /**发射子弹时调用的创建回调*/
    private Action<BulletLaunchVO> projectileRequested;

    /**当前是否允许全部发射器工作*/
    private bool firingEnabled;

    /**批量替换基础属性期间暂停响应逐项变化*/
    private bool applyingBaseAttributes;

    /// <summary>
    /// 创建飞行单位。
    /// </summary>
    /// <param name="id">实体ID</param>
    /// <param name="semanticName">语义名称</param>
    /// <param name="faction">所属阵营</param>
    /// <param name="timerType">所属计时器类型</param>
    /// <param name="position">初始坐标</param>
    protected FlyingUnitVO(long id, string semanticName, SceneElementFaction faction, TimerType timerType, Vector2 position)
        : base(id, faction, timerType, position) {
        this.semanticName = semanticName;
        InitializeAttributeTree();
    }

    /// <summary>
    /// 更新飞行单位。
    /// </summary>
    /// <param name="deltaTime">距上次更新经过的时间（秒）</param>
    public override void OnTimeUpdate(float deltaTime) {
        if (destroyed) {
            return;
        }
        UpdateUnitState(deltaTime);
        Vector2 previousPosition = position;
        UpdateMovement(deltaTime);
        velocity = deltaTime > 0f ? (position - previousPosition) / deltaTime : Vector2.zero;
        UpdateLaunchers(deltaTime);
    }

    /**设置正常外观和战损外观*/
    public void ConfigureAppearance(string normalPath, string damagedPath) {
        appearancePath = normalPath;
        damagedAppearancePath = damagedPath;
    }

    /**设置子弹创建回调*/
    public void ConfigureFiring(Action<BulletLaunchVO> handler) {
        projectileRequested = handler;
    }

    /**设置发射器开关*/
    public void SetFiringEnabled(bool enabled) {
        firingEnabled = enabled;
    }

    /**重置发射器*/
    public void ResetLaunchers() {
        foreach (BulletLauncherVO launcher in bulletLaunchers) {
            launcher.Reset();
        }
    }

    /**设置全部发射器修正*/
    public void SetAllLauncherModifiers(BulletLauncherModifierZone zone, BulletLauncherModifierVO modifier) {
        foreach (BulletLauncherVO launcher in bulletLaunchers) {
            launcher.SetModifier(zone, modifier);
        }
    }

    /**移除全部发射器修正*/
    public void RemoveAllLauncherModifiers(BulletLauncherModifierZone zone, int sourceId) {
        foreach (BulletLauncherVO launcher in bulletLaunchers) {
            launcher.RemoveModifier(zone, sourceId);
        }
    }

    /**清空指定乘区的发射器修正*/
    public void ClearAllLauncherModifiers(BulletLauncherModifierZone zone) {
        foreach (BulletLauncherVO launcher in bulletLaunchers) {
            launcher.ClearModifiers(zone);
        }
    }

    /**设置逻辑坐标*/
    public void SetPosition(Vector2 value) {
        position = value;
        velocity = Vector2.zero;
    }

    /// <summary>
    /// 应用基础属性。
    /// </summary>
    /// <param name="attributes">新的基础属性</param>
    /// <remarks>
    /// 整体替换配置属性，并在完成汇总后统一通知派生类型同步状态。
    /// </remarks>
    public void ApplyBaseAttributes(IReadOnlyDictionary<AttributeType, int> attributes) {
        applyingBaseAttributes = true;
        baseAttributeContainer.InitByCfg(attributes);
        applyingBaseAttributes = false;
        OnAttributesRecalculated();
    }

    /**更新飞行单位自身状态*/
    protected virtual void UpdateUnitState(float deltaTime) {
    }

    /**更新类型特有的移动*/
    protected virtual void UpdateMovement(float deltaTime) {
    }

    /**判断是否满足发射条件*/
    protected virtual bool CanLaunch(float deltaTime) {
        return true;
    }

    /**响应单项最终属性变化*/
    protected virtual void OnAttributeChanged(AttributeType type) {
    }

    /**响应最终属性全量重算*/
    protected virtual void OnAttributesRecalculated() {
    }

    /**清理飞行单位持有的数据*/
    protected override void OnDestroy() {
        projectileRequested = null;
        attributeContainer.Off<AttributeType>(AttributeContainerEvent.ATTR_CHANGED, HandleAttributeChanged);
        attributeContainer.Off(AttributeContainerEvent.RECALCULATED, HandleAttributesRecalculated);
        base.OnDestroy();
    }

    /**初始化固定的三层属性树*/
    private void InitializeAttributeTree() {
        attributeContainer.On<AttributeType>(AttributeContainerEvent.ATTR_CHANGED, HandleAttributeChanged);
        attributeContainer.On(AttributeContainerEvent.RECALCULATED, HandleAttributesRecalculated);
        attributeContainer.AddChild(baseAttributeContainer);
        attributeContainer.AddChild(nonBattleAttributeContainer);
        attributeContainer.AddChild(battleAttributeContainer);
    }

    /**转发单项属性变化*/
    private void HandleAttributeChanged(AttributeType type) {
        if (!applyingBaseAttributes) {
            OnAttributeChanged(type);
        }
    }

    /**转发属性全量重算*/
    private void HandleAttributesRecalculated() {
        if (!applyingBaseAttributes) {
            OnAttributesRecalculated();
        }
    }

    /**更新发射器*/
    private void UpdateLaunchers(float deltaTime) {
        if (!firingEnabled || projectileRequested == null || destroyed || !CanLaunch(deltaTime)) {
            return;
        }
        foreach (BulletLauncherVO launcher in bulletLaunchers) {
            launcher.Update(deltaTime, this, projectileRequested);
        }
    }

}
