using UnityEngine;

/// <summary>
/// 子弹配置数据
/// </summary>
/// <remarks>
/// 保存各阵营子弹创建时所需的外观、碰撞、行动与伤害配置。
/// </remarks>
public sealed class BulletConfigVO {

    public readonly int id;
    public readonly string appearancePath;
    public readonly Vector2 displaySize;
    public readonly Vector2 hitSize;
    public readonly Vector2 hitPivot;
    public readonly float speed;
    public readonly int damage;
    public readonly int hitEffectId;
    public readonly int launchEffectId;
    public readonly int poolCapacity;

    public BulletConfigVO(int id, string appearancePath, Vector2 displaySize, Vector2 hitSize, Vector2 hitPivot, float speed, int damage, int hitEffectId, int launchEffectId, int poolCapacity) {
        this.id = id;
        this.appearancePath = appearancePath;
        this.displaySize = displaySize;
        this.hitSize = hitSize;
        this.hitPivot = hitPivot;
        this.speed = speed;
        this.damage = damage;
        this.hitEffectId = hitEffectId;
        this.launchEffectId = launchEffectId;
        this.poolCapacity = poolCapacity;
    }
}
