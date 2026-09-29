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
    public readonly string damagedAppearancePath;
    public readonly Vector2 displaySize;
    public readonly AircraftCollisionVO collision;
    public readonly IReadOnlyDictionary<AttributeType, int> baseAttributes;
    public readonly int baseBulletCount;
    public readonly IReadOnlyList<BulletLauncherConfigVO> bulletLaunchers;

    /**当前等级 FlyingUnit 配置的飞行器体型*/
    public readonly AircraftSizeType aircraftSizeType;

    /**保存已解析的飞机等级战斗配置*/
    public PlayerAircraftBattleLevelVO(int aircraftId, int level, string appearancePath, string damagedAppearancePath, Vector2 displaySize, AircraftCollisionVO collision, IReadOnlyDictionary<AttributeType, int> baseAttributes, int baseBulletCount, IReadOnlyList<BulletLauncherConfigVO> bulletLaunchers, AircraftSizeType aircraftSizeType) {
        this.aircraftId = aircraftId;
        this.level = level;
        this.appearancePath = appearancePath;
        this.damagedAppearancePath = damagedAppearancePath;
        this.displaySize = displaySize;
        this.collision = collision;
        this.baseAttributes = baseAttributes;
        this.baseBulletCount = baseBulletCount;
        this.bulletLaunchers = bulletLaunchers;
        this.aircraftSizeType = aircraftSizeType;
    }

}
