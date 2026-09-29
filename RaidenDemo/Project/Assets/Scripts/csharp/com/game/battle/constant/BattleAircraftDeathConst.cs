using cfg;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 飞机死亡与坠地配置
/// </summary>
/// <remarks>
/// 集中提供近期伤害判定、坠落参数及按体型划分的坠地资源候选。
/// </remarks>
public static class BattleAircraftDeathConst {

    /**正常飞行高度*/
    public const float NormalHeight = 1000f;

    /**触发坠地的最低高度*/
    public const float GroundHeight = 100f;

    /**静止飞机开始坠落时使用的移动速度*/
    public const float StationaryFallingSpeed = 30f;

    /**原速度不为零时的坠落速度比例，单位万分比*/
    public const int FallingVelocityRate = 5000;

    /**残骸超出视窗下沿后的回收距离*/
    public const float WreckageRecycleDistance = 300f;

    /**近期伤害统计时长，单位毫秒*/
    public static int RecentDamageDurationMs => ConfigValueHelper.GetInt(7010);

    /**爆炸解体所需的近期伤害比例，单位万分比*/
    public static int DisintegrateDamageRate => ConfigValueHelper.GetInt(7011);

    /**坠落显示比例下限及地面残骸显示比例*/
    public static float GroundAircraftDisplayScale => ConfigValueHelper.GetFloat(7012);

    /**飞机高度下降加速度，单位高度每二次方秒*/
    public static int FallingHeightAcceleration => ConfigValueHelper.GetInt(7013);

    /**坠落角加速度随机范围，单位度每二次方秒*/
    public static IReadOnlyList<int> FallingAngularAccelerationRange => ConfigValueHelper.GetList<int>(7014);

    /**连续普通爆炸之间的间隔，单位毫秒*/
    public static int NormalExplosionIntervalMs => ConfigValueHelper.GetInt(7015);

    /**大型及以上飞机最后一次爆炸前的间隔，单位毫秒*/
    public static int LastExplosionIntervalMs => ConfigValueHelper.GetInt(7016);

    /**获取指定爆炸规模的特效候选*/
    internal static IReadOnlyList<int> GetExplosionEffectIds(AircraftExplosionSize size) {
        return ConfigValueHelper.GetList<int>(7041 + (int)size);
    }

    /**坠落期间持续冒烟的特效候选*/
    public static IReadOnlyList<int> FallingSmokeEffectIds => ConfigValueHelper.GetList<int>(7061);

    /**获取指定体型的坠地爆炸特效候选*/
    public static IReadOnlyList<int> GetCrashExplosionEffectIds(AircraftSizeType size) {
        return ConfigValueHelper.GetList<int>(GetSizeConfigId(size, 7021, 7022, 7023));
    }

    /**获取指定体型的坠地烟尘特效候选*/
    public static IReadOnlyList<int> GetCrashSmokeEffectIds(AircraftSizeType size) {
        return ConfigValueHelper.GetList<int>(GetSizeConfigId(size, 7025, 7026, 7027));
    }

    /**获取指定体型的撞击坑资源名候选*/
    public static IReadOnlyList<string> GetHitHoleResourceNames(AircraftSizeType size) {
        return ConfigValueHelper.GetList<string>(GetSizeConfigId(size, 7031, 7032, 7033));
    }

    /**从非小型飞机的候选列表中随机选择一个值*/
    public static T GetRandom<T>(IReadOnlyList<T> values, System.Random random, string label) {
        if (values == null || values.Count == 0) {
            throw new InvalidOperationException($"{label}候选不能为空");
        }
        return values[random.Next(values.Count)];
    }

    /**枚举所有会用于坠地的特效ID*/
    public static IEnumerable<int> GetAllCrashEffectIds() {
        foreach (AircraftSizeType size in new[] { AircraftSizeType.MEDIUM, AircraftSizeType.LARGE, AircraftSizeType.GIANT }) {
            foreach (int effectId in GetCrashExplosionEffectIds(size)) {
                yield return effectId;
            }
            foreach (int effectId in GetCrashSmokeEffectIds(size)) {
                yield return effectId;
            }
        }
    }

    /**枚举所有飞机死亡爆炸特效ID*/
    public static IEnumerable<int> GetAllDeathExplosionEffectIds() {
        foreach (AircraftExplosionSize size in Enum.GetValues(typeof(AircraftExplosionSize))) {
            foreach (int effectId in GetExplosionEffectIds(size)) {
                yield return effectId;
            }
        }
    }

    /**枚举所有会用于坠地的撞击坑路径*/
    public static IEnumerable<string> GetAllHitHolePaths() {
        foreach (AircraftSizeType size in new[] { AircraftSizeType.MEDIUM, AircraftSizeType.LARGE, AircraftSizeType.GIANT }) {
            foreach (string resourceName in GetHitHoleResourceNames(size)) {
                yield return BattleConst.GetRaidenUnpackImagePath(resourceName);
            }
        }
    }

    /**将飞机体型映射到对应的中、大、巨型配置ID*/
    private static int GetSizeConfigId(AircraftSizeType size, int mediumId, int largeId, int giantId) {
        switch (size) {
            case AircraftSizeType.MEDIUM:
                return mediumId;
            case AircraftSizeType.LARGE:
                return largeId;
            case AircraftSizeType.GIANT:
                return giantId;
            default:
                throw new ArgumentOutOfRangeException(nameof(size), size, "小型飞机不会产生坠地表现");
        }
    }

}
