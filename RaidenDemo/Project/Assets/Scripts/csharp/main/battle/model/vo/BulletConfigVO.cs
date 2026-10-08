using UnityEngine;
using cfg;

/// <summary>
/// 子弹配置数据
/// </summary>
/// <remarks>
/// 保存各阵营子弹创建时所需的外观、碰撞、行动与伤害配置。
/// </remarks>
public sealed class BulletConfigVO {

    public readonly int id;
    public readonly int type;
    public readonly int level;
    public readonly string appearancePath;
    public readonly int appearanceEffectId;
    public readonly Vector2 displaySize;
    public readonly int collisionRadius;
    public readonly int speed;
    public readonly int damage;
    public readonly int hitEffectId;
    public readonly int launchEffectId;
    public readonly BulletMotionType motionType;
    public readonly int rotationSpeed;
    public readonly int trackingDelayMs;
    public readonly int trackingTurnSpeed;

    public BulletConfigVO(int id, int type, int level, string appearancePath, int appearanceEffectId, Vector2 displaySize, int collisionRadius, int speed, int damage, int hitEffectId, int launchEffectId, BulletMotionType motionType, int rotationSpeed, int trackingDelayMs, int trackingTurnSpeed) {
        this.id = id;
        this.type = type;
        this.level = level;
        this.appearancePath = appearancePath;
        this.appearanceEffectId = appearanceEffectId;
        this.displaySize = displaySize;
        this.collisionRadius = collisionRadius;
        this.speed = speed;
        this.damage = damage;
        this.hitEffectId = hitEffectId;
        this.launchEffectId = launchEffectId;
        this.motionType = motionType;
        this.rotationSpeed = rotationSpeed;
        this.trackingDelayMs = trackingDelayMs;
        this.trackingTurnSpeed = trackingTurnSpeed;
    }
}
