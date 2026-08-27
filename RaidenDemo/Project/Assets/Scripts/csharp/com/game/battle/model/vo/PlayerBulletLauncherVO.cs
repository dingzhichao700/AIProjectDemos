using cfg;
using UnityEngine;

/// <summary>
/// 玩家子弹发射器配置数据
/// </summary>
/// <remarks>
/// 保存玩家飞机在关卡内使用的单个子弹发射器完整配置。
/// </remarks>
public sealed class PlayerBulletLauncherVO {

    public readonly Vector2 offset;
    public readonly int bulletCount;
    public readonly float fireInterval;
    public readonly int bulletIntervalMs;
    public readonly float direction;
    public readonly BulletSpreadType spreadType;
    public readonly float spreadAngle;
    public readonly string projectilePath;
    public readonly Vector2 projectileSize;
    public readonly Vector2 hitSize;
    public readonly Vector2 hitPivot;
    public readonly float speed;
    public readonly int damage;
    public readonly int hitEffectId;
    public readonly int launchEffectId;
    public readonly BulletMotionType motionType;
    public readonly bool rotate;
    public readonly float rotationSpeed;
    public readonly int trackingDelayMs;
    public readonly float trackingTurnSpeed;

    public PlayerBulletLauncherVO(Vector2 offset, int bulletCount, float fireInterval, int bulletIntervalMs, float direction, BulletSpreadType spreadType, float spreadAngle, string projectilePath, Vector2 projectileSize, Vector2 hitSize, Vector2 hitPivot, float speed, int damage, int hitEffectId, int launchEffectId, BulletMotionType motionType, bool rotate, float rotationSpeed, int trackingDelayMs, float trackingTurnSpeed) {
        this.offset = offset;
        this.bulletCount = bulletCount;
        this.fireInterval = fireInterval;
        this.bulletIntervalMs = bulletIntervalMs;
        this.direction = direction;
        this.spreadType = spreadType;
        this.spreadAngle = spreadAngle;
        this.projectilePath = projectilePath;
        this.projectileSize = projectileSize;
        this.hitSize = hitSize;
        this.hitPivot = hitPivot;
        this.speed = speed;
        this.damage = damage;
        this.hitEffectId = hitEffectId;
        this.launchEffectId = launchEffectId;
        this.motionType = motionType;
        this.rotate = rotate;
        this.rotationSpeed = rotationSpeed;
        this.trackingDelayMs = trackingDelayMs;
        this.trackingTurnSpeed = trackingTurnSpeed;
    }
}
