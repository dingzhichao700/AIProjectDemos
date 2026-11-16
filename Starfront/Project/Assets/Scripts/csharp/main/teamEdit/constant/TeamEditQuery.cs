using System;
using System.Linq;
using cfg;
using cfg.resource;

/// <summary>
/// 队伍编辑的配置、排序和出战资格查询。
/// </summary>
/// <remarks>职业读取正式CardProfessionResource；回调仅用于显式注入的隔离验证。</remarks>
public sealed class TeamEditQuery {

    private readonly Tables tables;

    private readonly CardCollectionModel cards;

    private readonly Func<int, int?> profession;

    public TeamEditQuery(Tables config, CardCollectionModel collection, Func<int, int?> professionQuery = null) {
        tables = config;
        cards = collection;
        profession = professionQuery;
    }

    public CardResource Card(int id) {
        return tables.CardObj.GetOrDefault(id);
    }

    public int Level(int id) {
        return cards.GetState(id).level;
    }

    public double? FormationPower(int[] savedSlots) {
        double total = 0;
        foreach (int id in savedSlots) {
            if (id == 0) continue;
            if (Card(id) == null) return null;
            int level = Level(id);
            if (level <= 0) return null;
            var power = CardCultivationQuery.ReadCardPower(tables, id, level);
            if (!power.HasValue) return null;
            total += power.Value;
        }
        return total;
    }

    public int? Profession(int id) {
        if (profession != null) {
            return profession(id);
        }
        var entry = tables.CardProfessionObj.GetOrDefault(id);
        return entry == null ? (int?)null : (int)entry.Profession;
    }

    public bool HasMissingProfession => tables.CardObj.DataList.Any(card => card.ShowInLibrary && !Profession(card.Id).HasValue);

    public int Total => tables.CardObj.DataList.Count(card => card.ShowInLibrary);

    public int Owned => tables.CardObj.DataList.Count(card => card.ShowInLibrary && Level(card.Id) > 0);

    /**sort=0配置顺序，1品质，2等级；同值回到配置顺序再按ID稳定排列。*/
    public int[] List(int filter, int sort, bool descending) {
        var rows = tables.CardObj.DataList.Where(card => card.ShowInLibrary).Select((card, order) => new { card, order });
        if (filter > 0) {
            rows = rows.Where(row => Profession(row.card.Id) == filter);
        }
        return rows.OrderBy(row => sort == 1 ? (descending ? -(int)row.card.Quality : (int)row.card.Quality) : sort == 2 ? (descending ? -Level(row.card.Id) : Level(row.card.Id)) : 0).ThenBy(row => row.order).ThenBy(row => row.card.Id).Select(row => row.card.Id).ToArray();
    }

    public string UnavailableReason(int id) {
        var card = Card(id);
        if (card == null || !card.ShowInLibrary || card.MaxLevel <= 0) {
            return "不是可编组兵种卡";
        }
        int level = Level(id);
        if (level <= 0) {
            return "卡牌尚未解锁";
        }
        return "";
    }

    /// <summary>战斗用途的配置完整性独立校验，不参与编组和方案保存。</summary>
    public string BattleUnavailableReason(int id) {
        string reason = UnavailableReason(id);
        if (!string.IsNullOrEmpty(reason)) {
            return reason;
        }
        var card = Card(id);
        int level = Level(id);
        if (!card.BattleEnabled || !(card.Content is UnitCardContent content)) {
            return "该卡牌战斗配置未就绪";
        }
        var production = tables.UnitProductionObj.GetOrDefault(content.UnitId);
        if (tables.UnitObj.GetOrDefault(content.UnitId) == null || production == null || tables.BuildingObj.GetOrDefault(production.BuildingId) == null) {
            return "缺少兵种或生产建筑配置";
        }
        if (!tables.CardUnitLevelObj.DataList.Any(row => row.CardId == id && row.Level == level && row.UnitId == content.UnitId) || !tables.CardBuildingLevelObj.DataList.Any(row => row.CardId == id && row.Level == level && row.BuildingId == production.BuildingId)) {
            return "当前等级缺少战斗配置";
        }
        return "";
    }

    /// <summary>每卡唯一生产建筑的配置费用；旧记录0表示尚未配置。</summary>
    public int? ConstructionCost(int id) {
        var card = Card(id);
        return card != null && card.ConstructionCost > 0 ? (int?)card.ConstructionCost : null;
    }

    public string Cost(int id) {
        return ConstructionCost(id)?.ToString() ?? "—";
    }

}
