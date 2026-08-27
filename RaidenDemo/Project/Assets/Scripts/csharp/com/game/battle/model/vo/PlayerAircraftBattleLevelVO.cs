using System.Collections.Generic;
using cfg;
using UnityEngine;
/// <summary>
/// 玩家飞机等级配置数据
/// </summary>
/// <remarks>
/// 保存指定玩家飞机在某一临时等级下使用的外观、碰撞、血量和发射器配置。
/// </remarks>
public sealed class PlayerAircraftBattleLevelVO {

    public readonly int aircraftId;
    public readonly int level;
    public readonly string appearancePath;
    public readonly Vector2 displaySize;
    public readonly AircraftCollisionVO collision;
    public readonly int baseHealth;
    public readonly int baseBulletCount;
    public readonly IReadOnlyList<PlayerBulletLauncherVO> bulletLaunchers;
    public readonly IReadOnlyList<ExplosionEffect> deathExplosions;
    public readonly bool removeAfterDeathPresentation;

    public PlayerAircraftBattleLevelVO(int aircraftId, int level, string appearancePath, Vector2 displaySize, AircraftCollisionVO collision, int baseHealth, int baseBulletCount, IReadOnlyList<PlayerBulletLauncherVO> bulletLaunchers, IReadOnlyList<ExplosionEffect> deathExplosions, bool removeAfterDeathPresentation) {
        this.aircraftId = aircraftId;
        this.level = level;
        this.appearancePath = appearancePath;
        this.displaySize = displaySize;
        this.collision = collision;
        this.baseHealth = baseHealth;
        this.baseBulletCount = baseBulletCount;
        this.bulletLaunchers = bulletLaunchers;
        this.deathExplosions = deathExplosions;
        this.removeAfterDeathPresentation = removeAfterDeathPresentation;
    }

}
