using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using cfg.resource;

/// <summary>本局卡牌等级参数索引；缺行拒绝入场，不回退到其他等级。</summary>
public sealed class BattleCardGrowthModel {
    private readonly Tables tables;
    private readonly Dictionary<(int, int, int), CardBuildingLevelResource> buildings;
    private readonly Dictionary<(int, int, int), CardUnitLevelResource> units;

    public BattleCardGrowthModel(Tables tables) {
        this.tables = tables;
        buildings = tables.CardBuildingLevelObj.DataList.ToDictionary(r => (r.CardId, r.Level, r.BuildingId));
        units = tables.CardUnitLevelObj.DataList.ToDictionary(r => (r.CardId, r.Level, r.UnitId));
        foreach (var row in buildings.Values) {
            if (!tables.CardObj.Get(row.CardId).BattleEnabled) {
                continue;
            }
            ValidateIdentity(row.CardId, row.Level);
            var source = tables.BuildingObj.GetOrDefault(row.BuildingId);
            if (source == null || source.Race != tables.CardObj.Get(row.CardId).Race || !Positive(row.MaxHp)) {
                throw new InvalidOperationException("建筑等级参数非法：" + row.BuildingId);
            }
            if (source.Type == BuildingType.UNIT_PRODUCER) {
                if (!row.UnitIntervalMs.HasValue || row.UnitIntervalMs.Value <= 0 || row.CurrencyIntervalMs.HasValue || row.CurrencyCount.HasValue) {
                    throw new InvalidOperationException("产兵建筑等级周期非法");
                }
            } else if (!row.CurrencyIntervalMs.HasValue || row.CurrencyIntervalMs.Value <= 0 || !row.CurrencyCount.HasValue || row.CurrencyCount.Value <= 0 || row.UnitIntervalMs.HasValue) {
                throw new InvalidOperationException("产币建筑等级参数非法");
            }
            if (row.SkillIds.Count != 0) {
                throw new InvalidOperationException("当前建筑攻击尚未实现，不能配置无效技能引用");
            }
        }
        foreach (var row in units.Values) {
            if (!tables.CardObj.Get(row.CardId).BattleEnabled) {
                continue;
            }
            ValidateIdentity(row.CardId, row.Level);
            var source = tables.UnitObj.GetOrDefault(row.UnitId);
            if (source == null || source.Race != tables.CardObj.Get(row.CardId).Race || !Positive(row.MaxHp) || !Positive(row.MoveSpeed)) {
                throw new InvalidOperationException("单位等级参数非法：" + row.UnitId);
            }
            BattleSkillModel.CreateSkills(source, tables, row.SkillIds);
        }
    }

    private void ValidateIdentity(int cardId, int level) {
        var card = tables.CardObj.GetOrDefault(cardId);
        if (card == null || card.MaxLevel <= 0 || level <= 0 || level > card.MaxLevel) {
            throw new InvalidOperationException("战斗成长行卡牌或等级非法：" + cardId + "/" + level);
        }
    }

    private static bool Positive(double value) {
        return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public CardBuildingLevelResource Building(int cardId, int level, int id) {
        if (!buildings.TryGetValue((cardId, level, id), out var row)) {
            throw new InvalidOperationException("缺少建筑战斗等级配置：卡牌" + cardId + " 等级" + level + " 建筑" + id);
        }
        return row;
    }

    public CardUnitLevelResource Unit(int cardId, int level, int id) {
        if (!units.TryGetValue((cardId, level, id), out var row)) {
            throw new InvalidOperationException("缺少单位战斗等级配置：卡牌" + cardId + " 等级" + level + " 单位" + id);
        }
        return row;
    }
}
