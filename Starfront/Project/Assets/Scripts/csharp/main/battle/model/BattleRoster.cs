using System;
using System.Collections.Generic;
using System.Linq;
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
                if (card.UnlockWeight <= 0) {
                    throw new InvalidOperationException("兵种卡解锁权重必须为正整数：" + card.Id);
                }
                var unit = tables.UnitObj.Get(unitCard.UnitId);
                var production = tables.UnitProductionObj.Get(unitCard.UnitId);
                var producer = production == null ? null : tables.BuildingObj.Get(production.BuildingId);
                if (unit == null || unit.MoveSpeed <= 0 || float.IsNaN(unit.MoveSpeed) || float.IsInfinity(unit.MoveSpeed) || unit.Race != start.Race || producer == null || producer.Race != start.Race || producer.Type != BuildingType.UNIT_PRODUCER || production.Interval <= 0 || production.Count != 1) {
                    throw new InvalidOperationException("兵种生产引用不合法：" + card.Id);
                }
                var required = unit.SkillIds.Select(id => tables.SkillObj.Get(id).Action).Concat(new[] { UnitActionType.MOVE, UnitActionType.IDLE, UnitActionType.DEATH }).Distinct();
                if (unit.Appearances.GroupBy(a => (a.Action, a.Direction)).Any(g => g.Count() != 1)) {
                    throw new InvalidOperationException("单位动作朝向重复：" + unit.Id);
                }
                foreach (var action in required) {
                    foreach (UnitAppearanceDirection direction in Enum.GetValues(typeof(UnitAppearanceDirection))) {
                        var appearance = unit.Appearances.SingleOrDefault(a => a.Action == action && a.Direction == direction);
                        bool loop = action == UnitActionType.MOVE || action == UnitActionType.IDLE;
                        if (appearance == null || appearance.Loop != loop || string.IsNullOrWhiteSpace(appearance.Resource)) {
                            throw new InvalidOperationException("单位动作缺失或循环配置非法：" + unit.Id + "/" + action + "/" + direction);
                        }
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
        // 同品质总权重必须能被随机数接口表示，进场前拒绝非法卡组。
        foreach (var quality in qualities) {
            long total = ids.Where(id => tables.CardObj.Get(id).Quality == quality && tables.CardObj.Get(id).Content is UnitCardContent).Sum(id => (long)tables.CardObj.Get(id).UnlockWeight);
            if (total > int.MaxValue) {
                throw new InvalidOperationException("同品质卡牌解锁权重溢出：" + quality);
            }
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
