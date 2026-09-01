using cfg;
using UnityEngine;

/// <summary>
/// 玩家子弹发射器配置数据
/// </summary>
/// <remarks>
/// 保存飞机在关卡内使用的单个子弹发射器配置。
/// </remarks>
public sealed class PlayerBulletLauncherVO {

    public readonly Vector2 offset;
    public readonly int bulletType;
    public readonly int bulletLevel;
    public readonly int bulletCount;
    public readonly float fireInterval;
    public readonly int bulletIntervalMs;
    public readonly float direction;
    public readonly BulletSpreadType spreadType;
    public readonly float spreadAngle;

    public PlayerBulletLauncherVO(Vector2 offset, int bulletType, int bulletLevel, int bulletCount, float fireInterval, int bulletIntervalMs, float direction, BulletSpreadType spreadType, float spreadAngle) {
        this.offset = offset;
        this.bulletType = bulletType;
        this.bulletLevel = bulletLevel;
        this.bulletCount = bulletCount;
        this.fireInterval = fireInterval;
        this.bulletIntervalMs = bulletIntervalMs;
        this.direction = direction;
        this.spreadType = spreadType;
        this.spreadAngle = spreadAngle;
    }
}
