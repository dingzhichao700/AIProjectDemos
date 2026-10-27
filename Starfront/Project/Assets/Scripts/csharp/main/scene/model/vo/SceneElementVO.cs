using UnityEngine;

/// <summary>
/// 场景逻辑元素基类。只保存逻辑状态，不持有任何视觉对象。
/// </summary>
public abstract class SceneElementVO {

    public long id { get; }

    public TimerType timerType { get; }

    public Vector2 position { get; protected set; }

    public float rotation { get; protected set; }

    public bool destroyed { get; private set; }

    protected SceneElementVO(long id, TimerType timerType, Vector2 position) {
        this.id = id;
        this.timerType = timerType;
        this.position = position;
    }

    /**根据所属 Timer 的时间增量推进自身逻辑*/
    public abstract void OnTimeUpdate(float deltaTime);

    /**将元素标记为已销毁，等待 SceneModel 清理*/
    public void Destroy() {
        destroyed = true;
    }

}
