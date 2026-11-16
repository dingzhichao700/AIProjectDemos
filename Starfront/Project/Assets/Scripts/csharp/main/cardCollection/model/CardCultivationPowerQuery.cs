using System;
using System.Collections.Generic;
using System.Linq;
using cfg;

/// <summary>
/// 仅供培养页功能验证的临时战力投影；不参与伤害、生产或存档。
/// </summary>
/// <remarks>权重来自配置版本1；独立攻击力缺失时不以普攻伤害或0补入。正式算法后续替换此查询。</remarks>
public static class CardCultivationPowerQuery {

    public static double? Read(Tables tables, IReadOnlyList<CultivationAttributeVO> attributes, out int version, out bool temporary) {
        var formula = tables.CultivationPowerFormulaObj.GetOrDefault(1);
        version = formula == null ? 0 : formula.Id;
        temporary = formula != null && formula.IsTemporary;
        if (formula == null) {
            return null;
        }
        var ids = new[] { 1, 3, 4, 5, 6 };
        var values = new Dictionary<int, double>();
        foreach (int id in ids) {
            var matches = attributes.Where(row => row.attributeId == id).ToArray();
            if (matches.Length != 1 || !matches[0].current.HasValue || double.IsNaN(matches[0].current.Value) || double.IsInfinity(matches[0].current.Value)) {
                return null;
            }
            values[id] = matches[0].current.Value;
        }
        if (values[6] <= 0 || formula.UnitHpWeight < 0 || formula.BuildingHpWeight < 0 || formula.RangeWeight < 0 || formula.SpeedWeight < 0 || formula.ProductionRateWeight < 0) {
            return null;
        }
        double score = values[1] * formula.UnitHpWeight + values[5] * formula.BuildingHpWeight + values[3] * formula.RangeWeight + values[4] * formula.SpeedWeight + 1000.0 / values[6] * formula.ProductionRateWeight;
        return double.IsNaN(score) || double.IsInfinity(score) ? (double?)null : Math.Round(Math.Max(0, score), MidpointRounding.AwayFromZero);
    }

}
