using System;
using System.Linq;
using cfg;

/// <summary>
/// 培养配置外键与结构约束；空表是缺数据，不是伪造默认配置。
/// </summary>
public static class CardCultivationConfigValidation {

    public static void Validate(Tables tables) {
        foreach (var skill in tables.CultivationSkillObj.DataList) {
            if (skill.Id <= 0 || string.IsNullOrWhiteSpace(skill.Name) || !Enum.IsDefined(typeof(SkillKind), skill.Kind) || !Enum.IsDefined(typeof(SkillDisplaySlot), skill.Slot) || (skill.Slot == SkillDisplaySlot.Passive ? skill.Kind != SkillKind.Passive : skill.Kind != SkillKind.Active)) {
                throw new InvalidOperationException("培养技能类别/包装/名称非法：" + skill.Id);
            }
        }
        var entries = tables.CardSkillEntryObj.DataList;
        if (entries.GroupBy(value => new { value.CardId, value.SkillId }).Any(group => group.Count() > 1)) {
            throw new InvalidOperationException("重复卡牌技能关联");
        }
        foreach (var entry in entries) {
            Card(tables, entry.CardId);
            var skill = tables.CultivationSkillObj.GetOrDefault(entry.SkillId);
            if (skill == null) {
                throw new InvalidOperationException("卡牌技能关联外键缺失");
            }
            CardSkillQuery.Read(tables, entry.CardId, 0, skill.Slot, skill.Id);
        }
        foreach (var rank in tables.CardSkillRankObj.DataList) {
            if (!entries.Any(entry => entry.CardId == rank.CardId && entry.SkillId == rank.SkillId)) {
                throw new InvalidOperationException("孤立技能阶数");
            }
        }
        foreach (var value in tables.CardProfessionObj.DataList) {
            Card(tables, value.CardId);
            if (!Enum.IsDefined(typeof(CardProfessionType), value.Profession)) {
                throw new InvalidOperationException("职业枚举非法");
            }
        }
        var counters = tables.CardProfessionCounterObj.DataList;
        if (counters.GroupBy(value => new { value.Profession, value.TargetProfession }).Any(group => group.Count() > 1)) {
            throw new InvalidOperationException("重复职业克制关联");
        }
        foreach (var value in counters) {
            if (!Enum.IsDefined(typeof(CardProfessionType), value.Profession) || !Enum.IsDefined(typeof(CardProfessionType), value.TargetProfession)) {
                throw new InvalidOperationException("克制职业枚举非法");
            }
        }
        foreach (var value in tables.CultivationRaceArtObj.DataList) {
            if (!Enum.IsDefined(typeof(Race), value.Race) || string.IsNullOrWhiteSpace(value.Background)) {
                throw new InvalidOperationException("种族背景配置非法");
            }
        }
        foreach (var value in tables.CultivationQualityArtObj.DataList) {
            if (!Enum.IsDefined(typeof(Quality), value.Quality) || string.IsNullOrWhiteSpace(value.Podium)) {
                throw new InvalidOperationException("品质台座配置非法");
            }
        }
        var stages = tables.CardGrowthStageObj.DataList;
        if (stages.GroupBy(value => new { value.CardId, value.Id }).Any(group => group.Count() > 1)) {
            throw new InvalidOperationException("重复成长阶段");
        }
        foreach (var value in stages) {
            Card(tables, value.CardId);
            if (value.Id <= 0 || value.UnlockLevel < 1 || value.UnlockLevel > tables.CardObj.Get(value.CardId).MaxLevel || string.IsNullOrWhiteSpace(value.Description)) {
                throw new InvalidOperationException("成长阶段条件或说明非法");
            }
        }
    }

    private static void Card(Tables tables, int cardId) {
        var card = tables.CardObj.GetOrDefault(cardId);
        if (card == null || card.MaxLevel < 1) {
            throw new InvalidOperationException("培养配置引用无效卡牌：" + cardId);
        }
    }

}
