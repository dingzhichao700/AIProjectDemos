using cfg;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 僚机战斗配置
/// </summary>
/// <remarks>
/// 保存单种僚机的外观、编队槽位、移动速度和发射器配置。
/// </remarks>
public sealed class WingmanConfigVO {

    public readonly int id;
    public readonly string code;
    public readonly string displayName;
    public readonly Vector2 displaySize;
    public readonly int maxCount;
    public readonly WingmanFormationType formationType;
    public readonly IReadOnlyList<Vector2> formationOffsets;
    public readonly IReadOnlyDictionary<AttributeType, int> baseAttributes;
    public readonly string appearancePath;
    public readonly string damagedAppearancePath;
    public readonly IReadOnlyList<BulletLauncherConfigVO> bulletLaunchers;

    /**FlyingUnit 配置的僚机体型*/
    public readonly AircraftSizeType aircraftSizeType;

    /**着火点使用的碰撞区域，不改变僚机当前免碰撞规则*/
    public readonly AircraftCollisionVO collision;

    /**保存已解析的僚机战斗配置*/
    public WingmanConfigVO(int id, string code, string displayName, Vector2 displaySize, int maxCount, WingmanFormationType formationType, IReadOnlyList<Vector2> formationOffsets, IReadOnlyDictionary<AttributeType, int> baseAttributes, string appearancePath, string damagedAppearancePath, IReadOnlyList<BulletLauncherConfigVO> bulletLaunchers, AircraftSizeType aircraftSizeType, AircraftCollisionVO collision) {
        this.id = id;
        this.code = code;
        this.displayName = displayName;
        this.displaySize = displaySize;
        this.maxCount = maxCount;
        this.formationType = formationType;
        this.formationOffsets = formationOffsets;
        this.baseAttributes = baseAttributes;
        this.appearancePath = appearancePath;
        this.damagedAppearancePath = damagedAppearancePath;
        this.bulletLaunchers = bulletLaunchers;
        this.aircraftSizeType = aircraftSizeType;
        this.collision = collision;
    }

}
