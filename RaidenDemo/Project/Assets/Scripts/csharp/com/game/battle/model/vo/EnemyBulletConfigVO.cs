using UnityEngine;

/// <summary>
/// 敌机子弹配置数据
/// </summary>
/// <remarks>
/// 保存敌方子弹运行时所需的外观、尺寸、碰撞、伤害和命中特效配置。
/// </remarks>
public sealed class EnemyBulletConfigVO {

    public readonly int id;
    public readonly string appearancePath;
    public readonly Vector2 displaySize;
    public readonly Vector2 hitSize;
    public readonly float speed;
    public readonly int damage;
    public readonly int hitEffectId;
    public readonly int poolCapacity;

    public EnemyBulletConfigVO(int id, string appearancePath, Vector2 displaySize,
        Vector2 hitSize, float speed, int damage, int hitEffectId, int poolCapacity) {
        this.id = id;
        this.appearancePath = appearancePath;
        this.displaySize = displaySize;
        this.hitSize = hitSize;
        this.speed = speed;
        this.damage = damage;
        this.hitEffectId = hitEffectId;
        this.poolCapacity = poolCapacity;
    }

}
