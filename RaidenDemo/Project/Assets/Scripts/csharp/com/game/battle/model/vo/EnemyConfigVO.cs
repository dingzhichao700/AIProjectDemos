using cfg;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 敌机运行配置数据
/// </summary>
/// <remarks>
/// 保存敌机类型、外观、碰撞、血量、移动、发射和计分配置。
/// </remarks>
public sealed class EnemyConfigVO {

    public readonly int id;
    public readonly EnemyClass enemyClass;
    public readonly IReadOnlyDictionary<AttributeType, int> baseAttributes;
    public readonly string appearancePath;
    public readonly string damagedAppearancePath;
    public readonly Vector2 displaySize;
    public readonly AircraftCollisionVO collision;
    public readonly int score;
    public readonly int poolCapacity;
    public readonly IReadOnlyList<BulletLauncherConfigVO> bulletLaunchers;

    /**FlyingUnit 配置的飞行器体型*/
    public readonly AircraftSizeType aircraftSizeType;

    public float moveSpeed => baseAttributes[AttributeType.SPEED];

    /**保存已解析的敌机战斗配置*/
    public EnemyConfigVO(int id, EnemyClass enemyClass, IReadOnlyDictionary<AttributeType, int> baseAttributes, string appearancePath, string damagedAppearancePath, Vector2 displaySize, AircraftCollisionVO collision, int score, int poolCapacity, IReadOnlyList<BulletLauncherConfigVO> bulletLaunchers, AircraftSizeType aircraftSizeType) {
        this.id = id;
        this.enemyClass = enemyClass;
        this.baseAttributes = baseAttributes;
        this.appearancePath = appearancePath;
        this.damagedAppearancePath = damagedAppearancePath;
        this.displaySize = displaySize;
        this.collision = collision;
        this.score = score;
        this.poolCapacity = poolCapacity;
        this.bulletLaunchers = bulletLaunchers;
        this.aircraftSizeType = aircraftSizeType;
    }

}
