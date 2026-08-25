using UnityEngine;

/// <summary>
/// 玩家飞机类型的运行时展示数据
/// </summary>
public sealed class PlayerAircraftVO {

    public readonly int id;
    public readonly string code;
    public readonly string displayName;
    public readonly int maxLevel;
    public readonly int level;
    public readonly bool defaultUnlocked;
    public readonly int unlockStarCost;
    public readonly int basePower;
    public readonly string appearancePath;
    public readonly Vector2 displaySize;

    public PlayerAircraftVO(int id, string code, string displayName, int maxLevel, int level, bool defaultUnlocked, int unlockStarCost, int basePower, string appearancePath, Vector2 displaySize) {
        this.id = id;
        this.code = code;
        this.displayName = displayName;
        this.maxLevel = maxLevel;
        this.level = level;
        this.defaultUnlocked = defaultUnlocked;
        this.unlockStarCost = unlockStarCost;
        this.basePower = basePower;
        this.appearancePath = appearancePath;
        this.displaySize = displaySize;
    }

}
