using System;
using System.Linq;
using cfg;

/// <summary>
/// 将共用技能业务快照转换为技能详情显示数据。
/// </summary>
/// <remarks>关联、解锁与最高生效阶均由CardSkillQuery提供，详情不重复实现规则。</remarks>
public static class CardSkillDetailQuery {

    /**兼容现有技能入口枚举；配置权威使用生成的SkillDisplaySlot。*/
    public static SkillDisplaySlot ToDisplaySlot(CardSkillSlot slot) {
        switch (slot) {
            case CardSkillSlot.Basic: return SkillDisplaySlot.Basic;
            case CardSkillSlot.Passive: return SkillDisplaySlot.Passive;
            case CardSkillSlot.Active: return SkillDisplaySlot.Active;
            case CardSkillSlot.Ultimate: return SkillDisplaySlot.Ultimate;
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    public static string TypeName(CardSkillSlot slot) {
        switch (slot) {
            case CardSkillSlot.Passive: return "被动";
            case CardSkillSlot.Basic:
            case CardSkillSlot.Active:
            case CardSkillSlot.Ultimate: return "主动";
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    public static CardSkillDetailVO Read(Tables tables, int cardId, int cardLevel, CardSkillSlot slot, int skillId = 0) {
        var state = CardSkillQuery.Read(tables, cardId, cardLevel, ToDisplaySlot(slot), skillId);
        return new CardSkillDetailVO {
            cardId = cardId, skillId = state.skillId, slot = slot, cardName = tables.CardObj.Get(cardId).Name,
            skillName = state.name, typeName = TypeName(slot), battleSummary = state.battleSummary,
            iconAddress = state.iconAddress, unlocked = state.unlocked, currentRank = state.currentRank,
            stages = state.ranks.Select(rank => new CardSkillStageVO { rank = rank.rank, effect = rank.effect, unlockCondition = rank.unlockLevel + "级解锁", unlocked = rank.unlocked, current = rank.current }).ToList().AsReadOnly(),
            missing = state.missing
        };
    }

}
