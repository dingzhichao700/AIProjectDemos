using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cfg;
using cfg.bean;
using cfg.resource;

/// <summary>
/// 培养列表与技能详情唯一的技能关联、阶数和解锁查询。
/// </summary>
/// <remarks>只读正式配置；旧普攻仅作为战斗参考，不推导培养阶数。</remarks>
public static class CardSkillQuery {

    public static string TypeName(SkillDisplaySlot slot) {
        switch (slot) {
            case SkillDisplaySlot.Basic: return "普攻";
            case SkillDisplaySlot.Passive: return "被动";
            case SkillDisplaySlot.Active: return "主动";
            case SkillDisplaySlot.Ultimate: return "大招";
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    public static IReadOnlyList<CardSkillStateVO> ReadAll(Tables tables, int cardId, int cardLevel) {
        ValidateCard(tables, cardId, cardLevel);
        var entries = tables.CardSkillEntryObj.DataList.Where(value => value.CardId == cardId).OrderBy(value => value.SortOrder).ThenBy(value => value.SkillId).ToList();
        var result = new List<CardSkillStateVO>();
        foreach (var entry in entries) {
            var definition = tables.CultivationSkillObj.GetOrDefault(entry.SkillId);
            if (definition == null) {
                throw new InvalidOperationException("技能关联引用不存在：" + entry.SkillId);
            }
            result.Add(Read(tables, cardId, cardLevel, definition.Slot, entry.SkillId));
        }
        if (entries.Count == 0) {
            // 无关联不能声称四个正式技能存在；仅保留真实普攻参考供旧详情消费。
            var reference = Read(tables, cardId, cardLevel, SkillDisplaySlot.Basic);
            if (reference.skillId != 0) {
                result.Add(reference);
            }
        }
        return result.OrderBy(skill => skill.ranks.Count > 0 ? skill.ranks.Min(rank => rank.unlockLevel) : skill.slot == SkillDisplaySlot.Basic ? 0 : int.MaxValue).ThenBy(skill => skill.sortOrder).ThenBy(skill => skill.skillId).ToList().AsReadOnly();
    }

    public static CardSkillStateVO Read(Tables tables, int cardId, int cardLevel, SkillDisplaySlot slot, int skillId = 0) {
        ValidateCard(tables, cardId, cardLevel);
        TypeName(slot);
        var entries = tables.CardSkillEntryObj.DataList.Where(value => value.CardId == cardId).ToList();
        var matching = entries.Where(value => {
            var definition = tables.CultivationSkillObj.GetOrDefault(value.SkillId);
            if (definition == null) {
                throw new InvalidOperationException("技能关联引用不存在：" + value.SkillId);
            }
            return definition.Slot == slot && (skillId == 0 || value.SkillId == skillId);
        }).ToList();
        if (matching.Count > 1) {
            throw new ArgumentException("此包装有多个技能，请传入明确skillId");
        }
        if (matching.Count == 0) {
            return ReadBattleReference(tables, cardId, cardLevel, slot, skillId, entries.Count == 0);
        }
        var entry = matching[0];
        var skill = tables.CultivationSkillObj.Get(entry.SkillId);
        var expectedKind = slot == SkillDisplaySlot.Passive ? SkillKind.Passive : SkillKind.Active;
        if (skill.Kind != expectedKind || string.IsNullOrWhiteSpace(skill.Name)) {
            throw new InvalidOperationException("技能底层类别、包装或名称非法：" + skill.Id);
        }
        var missing = new List<string>();
        var ranks = new List<CardSkillRankStateVO>();
        int previousLevel = 0;
        foreach (var row in tables.CardSkillRankObj.DataList.Where(value => value.CardId == cardId && value.SkillId == skill.Id).OrderBy(value => value.Rank)) {
            if (row.Rank != ranks.Count + 1 || row.UnlockLevel < 1 || row.UnlockLevel < previousLevel || row.UnlockLevel > tables.CardObj.Get(cardId).MaxLevel) {
                throw new InvalidOperationException("技能阶数或解锁等级非法：" + skill.Id);
            }
            string effect = DescribeRank(tables, row, skill.Kind);
            previousLevel = row.UnlockLevel;
            ranks.Add(new CardSkillRankStateVO { rank = row.Rank, unlockLevel = row.UnlockLevel, battleSkillId = row.BattleSkillId, unlocked = cardLevel >= row.UnlockLevel, effect = effect });
        }
        var current = ranks.LastOrDefault(value => value.unlocked);
        if (current != null) {
            current.current = true;
        }
        if (ranks.Count == 0) {
            missing.Add("技能阶数、各阶效果与解锁条件");
            missing.Add("当前生效阶数");
        }
        if (string.IsNullOrWhiteSpace(skill.Icon)) {
            missing.Add("技能图标正式配置关联");
        }
        return new CardSkillStateVO {
            cardId = cardId, skillId = skill.Id, slot = slot, kind = skill.Kind, name = skill.Name, iconAddress = skill.Icon,
            sortOrder = entry.SortOrder, unlocked = ranks.Count == 0 ? (bool?)null : current != null,
            currentRank = current == null ? (int?)null : current.rank, ranks = ranks.AsReadOnly(), missing = missing.AsReadOnly(),
            battleSummary = current != null ? current.effect : ranks.Count > 0 ? "尚未解锁；首阶需卡牌" + ranks[0].unlockLevel + "级" : "暂无正式阶数配置"
        };
    }

    /**结构化培养效果只作查询说明；不执行战斗结算、治疗或持续计时。*/
    private static string DescribeRank(Tables tables, CardSkillRankResource row, SkillKind kind) {
        bool attribute = row.MoveSpeedBonus.HasValue || row.AttackSpeedBonus.HasValue || row.AttackBonus.HasValue;
        int families = (attribute ? 1 : 0) + (row.MaxHpBonus.HasValue ? 1 : 0) + (row.HealMaxHpRatio.HasValue ? 1 : 0) + (row.HealingBonus.HasValue ? 1 : 0) + (row.AttackRangeBonus.HasValue ? 1 : 0) + (row.DamageMultiplier.HasValue ? 1 : 0) + (row.BattleSkillId != 0 ? 1 : 0);
        if (families != 1 || (row.TargetCount.HasValue && (row.TargetCount.Value < 1 || (!row.HealMaxHpRatio.HasValue && !row.DamageMultiplier.HasValue))) || (row.DurationSeconds.HasValue && !attribute)) {
            throw new InvalidOperationException("技能效果须为单一类型，持续时间/目标数量不得混配：" + row.SkillId);
        }
        if (row.HealMaxHpRatio.HasValue) {
            RequirePositive(row.HealMaxHpRatio.Value, row.SkillId);
            if (kind != SkillKind.Active || !row.TargetCount.HasValue) throw new InvalidOperationException("治疗须为主动且指定目标数量：" + row.SkillId);
            return (row.TargetCount.Value == 1 ? "为1名友军恢复其最大生命值的" : "为最多" + row.TargetCount.Value + "名友军各恢复其最大生命值的") + Number(row.HealMaxHpRatio.Value * 100) + "%";
        }
        if (row.MaxHpBonus.HasValue || row.HealingBonus.HasValue || row.AttackRangeBonus.HasValue) {
            double value = row.MaxHpBonus ?? row.HealingBonus ?? row.AttackRangeBonus.Value;
            RequirePositive(value, row.SkillId);
            if (kind != SkillKind.Passive) throw new InvalidOperationException("常驻属性效果须为被动：" + row.SkillId);
            string label = row.MaxHpBonus.HasValue ? "生命值" : row.HealingBonus.HasValue ? "治疗效果" : "攻击范围";
            return label + "提升" + Number(value * 100) + "%";
        }
        if (attribute) return DescribeAttributeBonus(row, kind);
        if (row.DamageMultiplier.HasValue) {
            double multiplier = row.DamageMultiplier.Value;
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier < 0 || kind != SkillKind.Active) throw new InvalidOperationException("技能伤害系数非法：" + row.SkillId);
            string prefix = !row.TargetCount.HasValue ? "造成 " : row.TargetCount.Value == 1 ? "对1个敌方目标造成" : "对最多" + row.TargetCount.Value + "个敌方目标各造成";
            return prefix + Number(multiplier * 100) + "%技能伤害";
        }
        var battleSkill = tables.SkillObj.GetOrDefault(row.BattleSkillId);
        if (battleSkill == null) throw new InvalidOperationException("技能阶数引用不存在：" + row.BattleSkillId);
        return Describe(battleSkill);
    }

    private static void RequirePositive(double value, int skillId) {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) throw new InvalidOperationException("技能效果须为有限正数：" + skillId);
    }

    private static string DescribeAttributeBonus(CardSkillRankResource row, SkillKind kind) {
        double?[] bonuses = { row.MoveSpeedBonus, row.AttackSpeedBonus, row.AttackBonus };
        foreach (var bonus in bonuses.Where(value => value.HasValue)) RequirePositive(bonus.Value, row.SkillId);
        if ((kind == SkillKind.Active && (!row.DurationSeconds.HasValue || row.DurationSeconds.Value <= 0)) || (kind == SkillKind.Passive && row.DurationSeconds.HasValue)) {
            throw new InvalidOperationException("主动属性加成须指定正持续秒数，被动不得混入持续时间：" + row.SkillId);
        }
        var lines = new List<string>();
        if (row.MoveSpeedBonus.HasValue && row.AttackSpeedBonus.HasValue && row.MoveSpeedBonus.Value == row.AttackSpeedBonus.Value) {
            lines.Add("移动速度和攻击速度提升" + Number(row.MoveSpeedBonus.Value * 100) + "%");
        } else {
            if (row.MoveSpeedBonus.HasValue) lines.Add("移动速度提升" + Number(row.MoveSpeedBonus.Value * 100) + "%");
            if (row.AttackSpeedBonus.HasValue) lines.Add("攻击速度提升" + Number(row.AttackSpeedBonus.Value * 100) + "%");
        }
        if (row.AttackBonus.HasValue) lines.Add("攻击力提升" + Number(row.AttackBonus.Value * 100) + "%");
        if (row.DurationSeconds.HasValue) lines.Add("持续" + row.DurationSeconds.Value + "秒");
        return string.Join("\n", lines);
    }

    private static void ValidateCard(Tables tables, int cardId, int level) {
        if (tables == null) {
            throw new ArgumentNullException(nameof(tables));
        }
        var card = tables.CardObj.GetOrDefault(cardId);
        if (card == null || level < 0 || level > card.MaxLevel) {
            throw new ArgumentException("卡牌或等级非法：" + cardId);
        }
    }

    private static CardSkillStateVO ReadBattleReference(Tables tables, int cardId, int level, SkillDisplaySlot slot, int skillId, bool allowReference) {
        SkillResource skill = null;
        var row = tables.CardUnitLevelObj.DataList.FirstOrDefault(value => value.CardId == cardId && value.Level == Math.Max(1, level));
        var allIds = tables.CardUnitLevelObj.DataList.Where(value => value.CardId == cardId).SelectMany(value => value.SkillIds).ToList();
        if (skillId != 0) {
            if (!allowReference || slot != SkillDisplaySlot.Basic || !allIds.Contains(skillId)) {
                throw new ArgumentException("所选技能不属于当前卡牌或包装");
            }
            skill = tables.SkillObj.GetOrDefault(skillId);
            if (skill == null || skill.Trigger != SkillTriggerType.AUTO_ATTACK) {
                throw new ArgumentException("技能不存在或包装不匹配");
            }
        } else if (allowReference && slot == SkillDisplaySlot.Basic && row != null) {
            skill = row.SkillIds.Select(id => tables.SkillObj.GetOrDefault(id)).FirstOrDefault(value => value != null && value.Trigger == SkillTriggerType.AUTO_ATTACK);
        }
        var missing = new List<string> { "技能阶数、各阶效果与解锁条件", "当前生效阶数", "技能图标正式配置关联", "卡牌培养技能正式关联" };
        if (skill == null) {
            missing.Add(TypeName(slot) + "名称、类型与卡牌关联");
        }
        if (row == null) {
            missing.Add("当前卡牌等级的战斗配置");
        }
        return new CardSkillStateVO {
            cardId = cardId, skillId = skill == null ? 0 : skill.Id, slot = slot, name = skill == null ? TypeName(slot) + " · 暂无配置" : skill.Name,
            kind = null, unlocked = null, currentRank = null, iconAddress = null, ranks = Array.AsReadOnly(new CardSkillRankStateVO[0]), missing = missing.AsReadOnly(),
            battleSummary = skill == null ? "暂无正式技能配置\n补齐配置后显示各阶效果" : (level == 0 ? "未拥有卡牌：以下为1级战斗配置参考" : "战斗配置参考；培养关联及阶数未配置") + "\n\n" + Describe(skill)
        };
    }

    public static string Describe(SkillResource skill) {
        var lines = new List<string>();
        FormatEffects(skill.Effects, lines);
        lines.Add("射程 " + Number(skill.CastRange) + " 像素   冷却 " + skill.CooldownMs + " 毫秒");
        lines.Add("前摇 " + skill.WindupMs + " 毫秒   后摇 " + skill.RecoveryMs + " 毫秒");
        return string.Join("\n", lines);
    }

    private static string Number(double value) {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void FormatEffects(IEnumerable<SkillEffect> effects, List<string> lines) {
        foreach (var effect in effects) {
            if (effect is FixedDamageEffect damage) {
                lines.Add("造成 " + Number(damage.Amount) + " 点伤害");
            } else if (effect is LaunchProjectileEffect projectile) {
                lines.Add("发射弹道后命中生效");
                FormatEffects(projectile.HitEffects, lines);
            } else {
                lines.Add("效果类型暂不支持展示：" + effect.GetType().Name);
            }
        }
    }

}
