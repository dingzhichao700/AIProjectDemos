using cfg;

/// <summary>
/// 飞机死亡类型判定
/// </summary>
/// <remarks>
/// 玩家飞机和小型敌机固定爆炸解体，中型以上敌机按近期实际伤害比例判定。
/// </remarks>
internal static class AircraftDeathResolver {

    /**判定一架飞机本次死亡使用的表现类型*/
    public static AircraftDeathType Resolve(AircraftVO aircraft) {
        if (aircraft.faction == SceneElementFaction.PLAYER || aircraft.aircraftSizeType == AircraftSizeType.SMALL) {
            return AircraftDeathType.DISINTEGRATE;
        }
        int threshold = BattleAircraftDeathConst.DisintegrateDamageRate;
        if (threshold < 0 || threshold > 10000) {
            throw new System.InvalidOperationException($"爆炸解体伤害比例阈值超出万分比范围：{threshold}");
        }
        bool disintegrates = aircraft.maxHealth > 0 && aircraft.recentDamage * 10000L >= (long)aircraft.maxHealth * threshold;
        return disintegrates ? AircraftDeathType.DISINTEGRATE : AircraftDeathType.FALLING;
    }

}
