using cfg;
using cfg.resource;
using System;
using System.Collections.Generic;

/// <summary>
/// 飞行器着火配置与区间规则
/// </summary>
/// <remarks>
/// 从正式配置读取特效候选和烟雾间隔，按万分比区间查询着火规则。
/// </remarks>
public static class BattleAircraftFireConst {

    /**小规模火焰特效候选*/
    public static IReadOnlyList<int> smallFireEffectIds => ConfigValueHelper.GetList<int>(7001);

    /**中规模火焰特效候选*/
    public static IReadOnlyList<int> mediumFireEffectIds => ConfigValueHelper.GetList<int>(7002);

    /**大规模火焰特效候选*/
    public static IReadOnlyList<int> largeFireEffectIds => ConfigValueHelper.GetList<int>(7003);

    /**小规模烟雾特效候选*/
    public static IReadOnlyList<int> smallSmokeEffectIds => ConfigValueHelper.GetList<int>(7004);

    /**中规模烟雾特效候选*/
    public static IReadOnlyList<int> mediumSmokeEffectIds => ConfigValueHelper.GetList<int>(7005);

    /**大规模烟雾特效候选*/
    public static IReadOnlyList<int> largeSmokeEffectIds => ConfigValueHelper.GetList<int>(7006);

    /**烟雾生成间隔，单位毫秒*/
    public static int smokeIntervalMs => ConfigValueHelper.GetInt(7007);

    /**取得指定规模的火焰候选*/
    public static IReadOnlyList<int> GetFireEffectIds(AircraftFireSize size) {
        switch (size) {
            case AircraftFireSize.SMALL:
                return smallFireEffectIds;
            case AircraftFireSize.MEDIUM:
                return mediumFireEffectIds;
            case AircraftFireSize.LARGE:
                return largeFireEffectIds;
            default:
                throw new ArgumentOutOfRangeException(nameof(size), size, "未知着火规模");
        }
    }

    /**取得指定规模的烟雾候选*/
    public static IReadOnlyList<int> GetSmokeEffectIds(AircraftFireSize size) {
        switch (size) {
            case AircraftFireSize.SMALL:
                return smallSmokeEffectIds;
            case AircraftFireSize.MEDIUM:
                return mediumSmokeEffectIds;
            case AircraftFireSize.LARGE:
                return largeSmokeEffectIds;
            default:
                throw new ArgumentOutOfRangeException(nameof(size), size, "未知着火规模");
        }
    }

    /**枚举关卡需要预加载的全部火焰和烟雾候选*/
    public static IEnumerable<int> GetAllEffectIds() {
        foreach (AircraftFireSize size in Enum.GetValues(typeof(AircraftFireSize))) {
            foreach (int effectId in GetFireEffectIds(size)) {
                yield return effectId;
            }
            foreach (int effectId in GetSmokeEffectIds(size)) {
                yield return effectId;
            }
        }
    }

    /// <summary>
    /// 查询当前生命比例对应的着火规则。
    /// </summary>
    /// <param name="size">飞行器体型</param>
    /// <param name="health">当前生命</param>
    /// <param name="maxHealth">最大生命</param>
    /// <returns>左开右闭区间命中的配置，未命中时为空</returns>
    /// <remarks>
    /// 用整数交叉相乘保留精确边界，避免万分比截断将边界外的生命值误判到低血量区间。
    /// </remarks>
    public static AircraftFireRuleResource FindRule(AircraftSizeType size, int health, int maxHealth) {
        if (size == AircraftSizeType.SMALL || health <= 0 || maxHealth <= 0) {
            return null;
        }
        long scaledHealth = (long)health * 10000;
        AircraftFireRuleResource result = null;
        foreach (AircraftFireRuleResource rule in CfgManager.tables.AircraftFireRuleObj.DataList) {
            if (rule.AircraftSizeType != size || scaledHealth <= (long)rule.HealthRateMin * maxHealth || scaledHealth > (long)rule.HealthRateMax * maxHealth) {
                continue;
            }
            if (result != null) {
                throw new InvalidOperationException($"飞机着火规则区间重叠：{result.Id}、{rule.Id}");
            }
            result = rule;
        }
        return result;
    }

}
