using System;
using System.Collections.Generic;
using cfg;
using cfg.resource;

/// <summary>检查本局开局与卡组引用；不将未确认的生命、速度当作已生效属性。</summary>
public static class BattleRoster {
    public static BattleCampVO CreateCamp(Tables tables, int sceneId, BattleSceneCamp camp) {
        var start = tables.BattleStartObj.Get(sceneId, camp);
        if (start == null || start.InitialCurrency < 0 || start.UnitLimit <= 0 || start.DeckLimit <= 0) {
            throw new InvalidOperationException("开局配置缺失或非法：" + sceneId + "/" + camp);
        }
        var building = tables.BuildingObj.Get(start.BaseBuildingId);
        if (building == null || building.Type != BuildingType.BASE || building.Race != start.Race) {
            throw new InvalidOperationException("开局基地配置不匹配。");
        }
        var deck = tables.BattleDeckObj.Get(start.DeckId);
        if (deck == null || deck.Cards.Count == 0 || deck.Cards.Count > start.DeckLimit) {
            throw new InvalidOperationException("开局卡组缺失或超出容量。");
        }
        var ids = new List<int>();
        var qualities = new HashSet<Quality>();
        int collectors = 0;
        foreach (var slot in deck.Cards) {
            var card = tables.CardObj.Get(slot.CardId);
            if (card == null || ids.Contains(card.Id) || card.Race != start.Race || card.Quality == Quality.NORMAL) {
                throw new InvalidOperationException("卡牌引用、种族、品质或唯一性不合法：" + slot.CardId);
            }
            ids.Add(card.Id);
            if (card.Content is UnitCardContent unitCard) {
                var unit = tables.UnitObj.Get(unitCard.UnitId);
                var production = tables.UnitProductionObj.Get(unitCard.UnitId);
                var producer = production == null ? null : tables.BuildingObj.Get(production.BuildingId);
                if (unit == null || unit.MoveSpeed <= 0 || float.IsNaN(unit.MoveSpeed) || float.IsInfinity(unit.MoveSpeed) || unit.Race != start.Race || producer == null || producer.Race != start.Race || producer.Type != BuildingType.UNIT_PRODUCER || production.Interval <= 0 || production.Count != 1) {
                    throw new InvalidOperationException("兵种生产引用不合法：" + card.Id);
                }
                var directions = new HashSet<UnitAppearanceDirection>();
                foreach (var appearance in unit.Appearances) {
                    if (appearance.Action == UnitActionType.MOVE && (!appearance.Loop || !directions.Add(appearance.Direction))) {
                        throw new InvalidOperationException("移动外观必须循环且朝向唯一：" + unit.Id);
                    }
                }
                foreach (UnitAppearanceDirection direction in Enum.GetValues(typeof(UnitAppearanceDirection))) {
                    if (!directions.Contains(direction)) {
                        throw new InvalidOperationException("缺少移动源朝向：" + unit.Id + "/" + direction);
                    }
                }
                qualities.Add(card.Quality);
            } else if (card.Content is BuildingCardContent buildingCard) {
                var collector = tables.BuildingObj.Get(buildingCard.BuildingId);
                var production = tables.CurrencyProductionObj.Get(buildingCard.BuildingId);
                if (collector == null || collector.Type != BuildingType.COLLECTOR || collector.Race != start.Race || !slot.Locked || production == null || production.Interval <= 0 || production.Count <= 0) {
                    throw new InvalidOperationException("采集站卡或产币配置不合法：" + card.Id);
                }
                collectors++;
            } else {
                throw new InvalidOperationException("不支持的卡牌内容：" + card.Id);
            }
        }
        if (collectors != 1) {
            throw new InvalidOperationException("本局须有唯一固定采集站卡。");
        }
        foreach (var unlock in tables.CellUnlockObj.DataList) {
            foreach (var item in unlock.UnitQualityWeights) {
                if (item.Weight > 0 && !qualities.Contains(item.Quality)) {
                    throw new InvalidOperationException("卡组缺少解锁所需品质：" + item.Quality);
                }
            }
        }
        return new BattleCampVO(start, ids);
    }
}
