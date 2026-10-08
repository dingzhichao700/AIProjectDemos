using UnityEngine;

/// <summary>
/// 场景元素数据基类
/// </summary>
/// <remarks>
/// 保存场景元素的权威逻辑状态、阵营与 Timer 类型，不持有任何视觉对象。
/// </remarks>
public abstract class SceneElementVO {

    public long id { get; }
    public SceneElementFaction faction { get; }
    public TimerType timerType { get; }
    public Vector2 position { get; protected set; }
    public float rotation { get; protected set; }
    public bool destroyed { get; private set; }

    protected SceneElementVO(long id, SceneElementFaction faction, TimerType timerType, Vector2 position) {
        this.id = id;
        this.faction = faction;
        this.timerType = timerType;
        this.position = position;
    }

    /**由所属 Timer 推进元素自身行为*/
    public abstract void OnTimeUpdate(float deltaTime);

    /**补偿元素在当前帧内事件发生后已经经过的逻辑时间。*/
    public void AdvanceFromInFrameEvent(float elapsedTime) {
        if (!destroyed && elapsedTime > 0f) {
            OnTimeUpdate(elapsedTime);
        }
    }

    /// <summary>
    /// 销毁场景元素。
    /// </summary>
    /// <remarks>
    /// 只执行一次清理，派生类在元素从场景移除前释放所属逻辑数据。
    /// </remarks>
    public void Destroy() {
        if (destroyed) {
            return;
        }
        destroyed = true;
        OnDestroy();
    }

    /**清理元素持有的数据和监听*/
    protected virtual void OnDestroy() {
    }

}
