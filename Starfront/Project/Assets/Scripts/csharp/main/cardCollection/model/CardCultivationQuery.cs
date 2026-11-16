using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using cfg.bean;
using cfg.resource;

/// <summary>
/// 汇总培养页数据，不拥有第二份成长、背包或存档状态。
/// </summary>
public static class CardCultivationQuery {

    public static CardCultivationStateVO Read(Tables tables, PlayerProgressControl player, int cardId) {
        if (tables == null || player == null) {
            throw new ArgumentNullException(tables == null ? nameof(tables) : nameof(player));
        }
        CardCultivationConfigValidation.Validate(tables);
        lock (player.model.SyncRoot) {
            var growth = player.Cards.model.GetState(cardId);
            var card = tables.CardObj.Get(cardId);
            var missing = new List<string>();
            var art = tables.CardArtObj.Get(card.ArtId);
            var raceArt = tables.CultivationRaceArtObj.GetOrDefault(card.Race);
            var qualityArt = tables.CultivationQualityArtObj.GetOrDefault(card.Quality);
            // 台座覆盖只属于对应美术条目；未批准的其他单位仍沿用品质图片与基准位置。
            string podiumAddress = string.IsNullOrWhiteSpace(art.CultivationPodium) ? qualityArt?.Podium : art.CultivationPodium;
            var profession = tables.CardProfessionObj.GetOrDefault(cardId);
            var materials = new List<CultivationMaterialVO>();
            foreach (var cost in growth.costs.OrderBy(value => value.Key)) {
                var item = tables.ItemObj.Get(cost.Key);
                materials.Add(new CultivationMaterialVO { itemId = item.Id, name = item.Name, iconId = item.IconId, required = cost.Value, owned = player.Inventory.model.GetQuantity(item.Id) });
                if (item.IconId == 0) {
                    missing.Add("材料正式图标：" + item.Id);
                }
            }
            if (raceArt == null || string.IsNullOrWhiteSpace(raceArt.Background)) {
                missing.Add("种族培养背景：" + card.Race);
            }
            if (string.IsNullOrWhiteSpace(podiumAddress)) {
                missing.Add("品质台座：" + card.Quality);
            }
            if (profession == null) {
                missing.Add("卡牌职业与克制关系");
            }
            var skills = CardSkillQuery.ReadAll(tables, cardId, growth.level);
            if (skills.Count == 0) {
                missing.Add("卡牌技能关联");
            }
            foreach (var skill in skills) {
                foreach (var item in skill.missing) {
                    missing.Add("技能" + skill.skillId + "：" + item);
                }
            }
            // 卡牌等级效果独立于技能。只显示正式独立记录，不用技能阶数填补空表。
            var stages = tables.CardGrowthStageObj.DataList
                .Where(value => value.CardId == cardId)
                .OrderBy(value => value.UnlockLevel)
                .ThenBy(value => value.SortOrder)
                .ThenBy(value => value.Id)
                .Select(value => new CultivationStageVO {
                    cardId = cardId, stageId = value.Id, unlockLevel = value.UnlockLevel,
                    description = value.Description, gained = growth.owned && growth.level >= value.UnlockLevel
                }).ToList();
            if (stages.Count == 0) {
                missing.Add("卡牌独立等级效果未配置");
            }
            int previewLevel = growth.owned ? growth.level : 1;
            var attributes = ReadAttributes(tables, cardId, previewLevel, growth.owned && !growth.maxed ? (int?)growth.nextLevel : null, missing).ToList();
            // 固定展示定义与对象数据缺项分离；unconfigured不是虚构的单位或建筑ID。
            foreach (string key in new[] { "unit.unconfigured.hp", "unit.unconfigured.attack", "unit.unconfigured.basicRange", "unit.unconfigured.speed", "building.unconfigured.hp", "building.unconfigured.production" }) {
                string prefix = key.StartsWith("unit.", StringComparison.Ordinal) ? "unit." : "building.";
                string suffix = key.Substring(key.LastIndexOf('.'));
                if (!attributes.Any(row => row.key.StartsWith(prefix, StringComparison.Ordinal) && row.key.EndsWith(suffix, StringComparison.Ordinal))) {
                    attributes.Add(new CultivationAttributeVO { key = key, current = null, target = null, unit = suffix == ".production" ? "毫秒" : null });
                }
            }
            foreach (var attribute in attributes) {
                int id = AttributeId(attribute.key);
                var definition = tables.AttributeDefinitionObj.GetOrDefault(id);
                if (definition == null) {
                    throw new InvalidOperationException("缺少固定属性定义：" + id);
                }
                attribute.attributeId = id;
                attribute.name = definition.Name;
                attribute.valueType = definition.ValueType;
                attribute.iconAddress = string.IsNullOrWhiteSpace(definition.Icon) ? null : "Assets/Art/atlasSource/default/cardCultivation/" + definition.Icon + ".png";
            }
            double? power = CardCultivationPowerQuery.Read(tables, attributes, out int powerVersion, out bool temporaryPower);
            if (!power.HasValue) {
                missing.Add("临时战力所需属性不完整");
            }
            missing.Add("正式战力算法待替换；当前仅为临时验证公式");
            var counters = profession == null ? new List<CardProfessionType>() : tables.CardProfessionCounterObj.DataList.Where(value => value.Profession == profession.Profession).Select(value => value.TargetProfession).Distinct().ToList();
            if (profession != null && counters.Count == 0) {
                missing.Add("职业克制关系未配置；无关系与未配置需正式数据明确");
            }
            if (string.IsNullOrWhiteSpace(art.Portrait)) {
                missing.Add("单位培养形象资源");
            }
            return new CardCultivationStateVO {
                cardId = cardId, name = card.Name, race = card.Race, quality = card.Quality, artId = card.ArtId, constructionCost = card.ConstructionCost, portraitAddress = art.Portrait,
                backgroundAddress = raceArt == null ? null : raceArt.Background, podiumAddress = podiumAddress, podiumOffsetY = art.CultivationPodiumOffsetY ?? 0,
                profession = profession == null ? (CardProfessionType?)null : profession.Profession, counterProfessions = counters.AsReadOnly(), power = power, powerIsTemporary = temporaryPower, powerFormulaVersion = powerVersion,
                growth = growth, materials = materials.AsReadOnly(), attributes = attributes,
                skills = skills, stages = stages.AsReadOnly(), missing = missing.Distinct().ToList().AsReadOnly()
            };
        }
    }

    private static int AttributeId(string key) {
        return key.StartsWith("building.", StringComparison.Ordinal) ? key.EndsWith(".hp", StringComparison.Ordinal) ? 5 : 6 : key.EndsWith(".hp", StringComparison.Ordinal) ? 1 : key.EndsWith(".attack", StringComparison.Ordinal) ? 2 : key.EndsWith(".basicRange", StringComparison.Ordinal) ? 3 : 4;
    }

    public static double? ReadCardPower(Tables tables, int cardId, int level) {
        var attributes = ReadAttributes(tables, cardId, level, null, new List<string>());
        foreach (var attribute in attributes) {
            attribute.attributeId = AttributeId(attribute.key);
        }
        return CardCultivationPowerQuery.Read(tables, attributes, out _, out _);
    }

    private static IReadOnlyList<CultivationAttributeVO> ReadAttributes(Tables tables, int cardId, int level, int? target, List<string> missing) {
        var result = new List<CultivationAttributeVO>();
        if (tables.CardUnitLevelObj.DataList.Where(row => row.CardId == cardId).Select(row => row.UnitId).Distinct().Count() > 1 || tables.CardBuildingLevelObj.DataList.Where(row => row.CardId == cardId).Select(row => row.BuildingId).Distinct().Count() > 1) {
            throw new InvalidOperationException("每张卡只能关联一个单位和一个建筑：" + cardId);
        }
        var units = tables.CardUnitLevelObj.DataList.Where(value => value.CardId == cardId && (value.Level == level || value.Level == target)).ToList();
        foreach (int id in units.Select(value => value.UnitId).Distinct().OrderBy(value => value)) {
            var current = units.FirstOrDefault(value => value.UnitId == id && value.Level == level);
            var next = units.FirstOrDefault(value => value.UnitId == id && value.Level == target);
            result.Add(new CultivationAttributeVO { key = "unit." + id + ".hp", name = "单位生命", current = current == null ? (double?)null : current.MaxHp, target = next == null ? (double?)null : next.MaxHp });
            var currentAttack = BasicSkill(tables, current);
            var targetAttack = BasicSkill(tables, next);
            result.Add(new CultivationAttributeVO { key = "unit." + id + ".basicRange", name = "普攻射程", unit = "像素", current = current == null ? (double?)null : current.AttackRange ?? (currentAttack == null ? (double?)null : currentAttack.CastRange), target = next == null ? (double?)null : next.AttackRange ?? (targetAttack == null ? (double?)null : targetAttack.CastRange) });
            // 独立攻击力读取逐级权威属性；绝不回退为普攻伤害。
            result.Add(new CultivationAttributeVO { key = "unit." + id + ".attack", name = "攻击力", current = current == null ? (double?)null : current.Attack, target = next == null ? (double?)null : next.Attack });
            if (current != null && !current.Attack.HasValue || next != null && !next.Attack.HasValue) {
                missing.Add("单位" + id + "独立攻击力逐级数据");
            }
            result.Add(new CultivationAttributeVO { key = "unit." + id + ".speed", name = "移动速度", unit = "像素/秒", current = current == null ? (double?)null : current.MoveSpeed, target = next == null ? (double?)null : next.MoveSpeed });
        }
        var buildings = tables.CardBuildingLevelObj.DataList.Where(value => value.CardId == cardId && (value.Level == level || value.Level == target)).ToList();
        foreach (int id in buildings.Select(value => value.BuildingId).Distinct().OrderBy(value => value)) {
            var current = buildings.FirstOrDefault(value => value.BuildingId == id && value.Level == level);
            var next = buildings.FirstOrDefault(value => value.BuildingId == id && value.Level == target);
            result.Add(new CultivationAttributeVO { key = "building." + id + ".hp", name = "建筑生命", current = current == null ? (double?)null : current.MaxHp, target = next == null ? (double?)null : next.MaxHp });
            result.Add(new CultivationAttributeVO { key = "building." + id + ".production", name = "产兵间隔", unit = "毫秒", current = current == null ? null : (double?)current.UnitIntervalMs, target = next == null ? null : (double?)next.UnitIntervalMs });
        }
        if (units.Count == 0 || (level > 0 && !units.Any(value => value.Level == level)) || (target.HasValue && !units.Any(value => value.Level == target))) {
            missing.Add("单位当前或目标等级属性：" + level + "→" + target);
        }
        if (buildings.Count == 0 || (level > 0 && !buildings.Any(value => value.Level == level)) || (target.HasValue && !buildings.Any(value => value.Level == target))) {
            missing.Add("建筑当前或目标等级属性：" + level + "→" + target);
        }
        return result.AsReadOnly();
    }

    private static SkillResource BasicSkill(Tables tables, CardUnitLevelResource row) {
        if (row == null) {
            return null;
        }
        var skills = row.SkillIds.Select(id => tables.SkillObj.GetOrDefault(id)).Where(value => value != null && value.Trigger == SkillTriggerType.AUTO_ATTACK).ToList();
        return skills.Count == 1 ? skills[0] : null;
    }

}
