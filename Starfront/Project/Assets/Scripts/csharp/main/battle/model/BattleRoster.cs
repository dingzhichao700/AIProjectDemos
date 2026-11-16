using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using cfg.resource;

/// <summary>检查本局开局与卡组引用；不将未确认的生命、速度当作已生效属性。</summary>
public static class BattleRoster {
    public static BattleCampVO CreateCamp(Tables tables, int sceneId, BattleSceneCamp camp, BattlePlayerDeckVO player = null, Func<ResLoadInfo, bool> resourceExists = null) {
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
        player = player ?? BattlePlayerDeckVO.CaptureCurrent();
        int limit = BattleConst.DeckCardLimit;
        if (limit <= 0 || player.cardLevels.Count > limit)
        {
            throw new InvalidOperationException("出战卡牌数量超过常量配置上限。");
        }
        var ignored = new Dictionary<int, string>();
        var playable = new List<int>();
        foreach (var pair in player.cardLevels)
        {
            string missing = BattleCardResources.UnavailableReason(tables, pair.Key, pair.Value, resourceExists);
            if (string.IsNullOrEmpty(missing))
            {
                playable.Add(pair.Key);
            }
            else
            {
                ignored.Add(pair.Key, missing);
            }
        }
        var ids = new List<int>();
        var qualities = new HashSet<Quality>();
        int collectors = 0;
        var fixedCards = new HashSet<int>(deck.Cards.Where(slot => slot.Locked).Select(slot => slot.CardId));
        var roster = fixedCards.Concat(playable).ToArray();
        foreach (int id in roster) {
            var card = tables.CardObj.GetOrDefault(id);
            if (card == null || !card.BattleEnabled) {
                throw new InvalidOperationException("卡牌尚无可用战斗配置：" + id);
            }
            if (ids.Contains(card.Id)) {
                throw new InvalidOperationException("卡牌引用、种族或唯一性不合法：" + id);
            }
            ids.Add(card.Id);
            if (card.Content is UnitCardContent unitCard) {
                if (card.UnlockWeight <= 0) {
                    throw new InvalidOperationException("兵种卡解锁权重必须为正整数：" + card.Id);
                }
                var unit = tables.UnitObj.Get(unitCard.UnitId);
                var production = tables.UnitProductionObj.Get(unitCard.UnitId);
                var producer = production == null ? null : tables.BuildingObj.Get(production.BuildingId);
                if (unit == null || unit.MoveSpeed <= 0 || float.IsNaN(unit.MoveSpeed) || float.IsInfinity(unit.MoveSpeed) || unit.Race != card.Race || producer == null || producer.Race != card.Race || producer.Type != BuildingType.UNIT_PRODUCER || production.Interval <= 0 || production.Count != 1) {
                    throw new InvalidOperationException("兵种生产引用不合法：" + card.Id);
                }
                var required = unit.SkillIds.Select(id => tables.SkillObj.Get(id).Action).Concat(new[] { UnitActionType.MOVE, UnitActionType.IDLE, UnitActionType.DEATH }).Distinct();
                if (unit.Appearances.GroupBy(a => (a.Action, a.Direction)).Any(g => g.Count() != 1)) {
                    throw new InvalidOperationException("单位动作朝向重复：" + unit.Id);
                }
                foreach (var action in required) {
                    foreach (UnitAppearanceDirection direction in BattleAim.SourceDirections) {
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
                if (collector == null || collector.Type != BuildingType.COLLECTOR || collector.Race != start.Race || !fixedCards.Contains(id) || production == null || production.Interval <= 0 || production.Count <= 0) {
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
        var levels = new Dictionary<int, int>();
        var growth = new BattleCardGrowthModel(tables);
        foreach (int id in ids) {
            var card = tables.CardObj.Get(id);
            if (card.MaxLevel <= 0) {
                continue;
            }
            int level = player.cardLevels[id];
            if (level <= 0 || level > card.MaxLevel) {
                throw new InvalidOperationException("卡牌未拥有或本局等级非法：" + id);
            }
            if (card.Content is UnitCardContent unit) {
                growth.Unit(id, level, unit.UnitId);
                growth.Building(id, level, tables.UnitProductionObj.Get(unit.UnitId).BuildingId);
            } else if (card.Content is BuildingCardContent buildingCard) {
                growth.Building(id, level, buildingCard.BuildingId);
            }
            levels.Add(id, level);
        }
        return new BattleCampVO(start, ids, levels, ignored);
    }
}

