#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using SimpleJSON;
using UnityEditor;
using UnityEngine;

/// <summary>培养配置与共用查询隔离验证，不读取玩家存档。</summary>
[InitializeOnLoad]
public static class CardCultivationValidation {

    private const string Pending = "Library/AIUI/pending-card-cultivation-business.json";
    [Serializable] private sealed class Request { public string requestId; public int processId; }
    [Serializable] private sealed class Result { public string requestId; public string status; public string error; public string[] checks; }
    [Serializable] private sealed class MissingCard { public int cardId; public string[] missing; }
    [Serializable] private sealed class MissingReport { public MissingCard[] cards; }
    private static readonly List<string> checks = new List<string>();

    static CardCultivationValidation() {
        EditorApplication.update += Poll;
    }

    private static Tables Load(Dictionary<string, string> overrides = null) {
        return new Tables(name => JSON.Parse(overrides != null && overrides.ContainsKey(name) ? overrides[name] : File.ReadAllText("Assets/ConfigBin/" + name + ".json")));
    }

    private static void Check(bool valid, string label) {
        if (!valid) {
            throw new InvalidOperationException(label);
        }
        checks.Add(label);
    }

    private static void Reject(Action action, string label) {
        bool rejected = false;
        try {
            action();
        } catch (ArgumentException) {
            rejected = true;
        } catch (InvalidOperationException) {
            rejected = true;
        }
        Check(rejected, label);
    }

    private static void Poll() {
        if (!File.Exists(Pending) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) {
            return;
        }
        Request request = null;
        var field = typeof(CfgManager).GetField("_tables", BindingFlags.NonPublic | BindingFlags.Static);
        var previous = field.GetValue(null);
        try {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
            File.Delete(Pending);
            if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId)) {
                throw new InvalidOperationException("Request identity mismatch");
            }
            checks.Clear();
            var tables = Load();
            field.SetValue(null, tables);
            typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Run(tables);
            File.WriteAllText("Library/AIUI/card-cultivation-business-result.json", JsonUtility.ToJson(new Result { requestId = request.requestId, status = "passed", checks = checks.ToArray() }, true));
        } catch (Exception error) {
            File.WriteAllText("Library/AIUI/card-cultivation-business-result.json", JsonUtility.ToJson(new Result { requestId = request == null ? "unreadable" : request.requestId, status = "failed", error = error.ToString(), checks = checks.ToArray() }, true));
        } finally {
            field.SetValue(null, previous);
            typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
    }

    private static void Run(Tables tables) {
        CardCultivationConfigValidation.Validate(tables);
        Check(!tables.CultivationSkillObj.DataList.Any(skill => skill.Name.Contains("Editor") || skill.Name.Contains("隔离测试")), "正式表不含Editor隔离技能夹具");
        var marine = CardSkillQuery.Read(tables, 2101, 1, cfg.SkillDisplaySlot.Basic, 210101);
        Check(marine.name == "步枪射击" && marine.currentRank == 1 && marine.ranks.Count == 3 && marine.ranks.Select(rank => rank.unlockLevel).SequenceEqual(new[] { 1, 5, 10 }) && marine.ranks.Select(rank => rank.effect).SequenceEqual(new[] { "造成 100%技能伤害", "造成 120%技能伤害", "造成 140%技能伤害" }), "陆战队员正式三阶普攻1/5/10级及100%/120%/140%系数");
        foreach (int level in new[] { 0, 2, 3, 4, 6, 7, 8, 9, 10 }) {
            var stimulant = CardSkillQuery.Read(tables, 2101, level, cfg.SkillDisplaySlot.Active, 210103);
            var barrage = CardSkillQuery.Read(tables, 2101, level, cfg.SkillDisplaySlot.Ultimate, 210104);
            int stimulantRank = level >= 10 ? 3 : level >= 7 ? 2 : level >= 3 ? 1 : 0;
            int barrageRank = level >= 8 ? 2 : level >= 4 ? 1 : 0;
            Check(stimulant.name == "强化剂" && stimulant.ranks.Count == 3 && stimulant.currentRank == (stimulantRank == 0 ? (int?)null : stimulantRank) && stimulant.missing.Count == 0 && stimulant.ranks.Select(rank => rank.unlockLevel).SequenceEqual(new[] { 3, 7, 10 }), "强化剂等级" + level + "：3/7/10级自动解锁");
            Check(barrage.name == "弹群" && barrage.ranks.Count == 2 && barrage.currentRank == (barrageRank == 0 ? (int?)null : barrageRank) && barrage.missing.Count == 0 && barrage.ranks.Select(rank => rank.unlockLevel).SequenceEqual(new[] { 4, 8 }), "弹群等级" + level + "：4/8级自动解锁");
            Check(stimulant.ranks.Select(rank => rank.effect).SequenceEqual(new[] { "移动速度和攻击速度提升50%\n持续3秒", "移动速度和攻击速度提升50%\n持续4秒", "移动速度和攻击速度提升50%\n持续5秒" }) && barrage.ranks.Select(rank => rank.effect).SequenceEqual(new[] { "攻击力提升25%\n持续5秒", "攻击力提升50%\n持续5秒" }), "等级" + level + "：加成和持续时间完整读取正式字段");
            if (stimulantRank > 0 && barrageRank > 0) {
                Check(stimulant.battleSummary == stimulant.ranks[stimulantRank - 1].effect && barrage.battleSummary == barrage.ranks[barrageRank - 1].effect, "等级" + level + "：新技能最高阶完整替换不累加");
            }
        }
        foreach (string fieldName in new[] { "durationSeconds", "moveSpeedBonus", "attackSpeedBonus", "damageMultiplier", "battleSkillId" }) {
            var invalidRanks = JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_cardskillrankobj.json"));
            var invalidRank = invalidRanks.AsArray.Children.First(rank => rank["skillId"].AsInt == 210103);
            invalidRank[fieldName] = fieldName == "damageMultiplier" || fieldName == "battleSkillId" ? 1 : -1;
            var invalidTables = Load(new Dictionary<string, string> { { "cfgobj_cardskillrankobj", invalidRanks.ToString() } });
            Reject(() => CardCultivationConfigValidation.Validate(invalidTables), "拒绝属性加成非法或混配字段：" + fieldName);
        }
        int saves = 0;
        var player = new PlayerProgressControl(tables, null, ignored => { saves++; });
        int before = saves;
        long revision = player.model.Revision;
        var state = CardCultivationQuery.Read(tables, player, 1101);
        Check(state.growth.level == 1 && state.materials.Count == 1 && state.materials[0].owned == 0 && state.materials[0].required == 15, "真实成长、共享背包与目标成本");
        Check(saves == before && player.model.Revision == revision, "只读查询不保存不改变revision");
        Check(!state.skills[0].unlocked.HasValue && !state.skills[0].currentRank.HasValue, "缺条件不伪装未解锁/当前阶");
        Check(state.power == 179 && state.powerIsTemporary && state.powerFormulaVersion == 1 && state.profession == null && state.backgroundAddress != null && state.backgroundAddress.EndsWith("temp-hero-background.png") && state.missing.Count > 0, "临时公式179且仍保留职业/资源缺项");
        Check(state.attributes.Any(value => value.key.EndsWith(".hp") && value.current == 100 && value.target == 110), "实际1到2级生命比较");
        Check(state.attributes.Any(value => value.attributeId == 2 && value.key.EndsWith(".attack") && !value.current.HasValue && !value.target.HasValue), "独立攻击力缺失不复用普攻伤害");
        var report = new List<MissingCard>();
        foreach (var card in tables.CardObj.DataList.Where(value => value.MaxLevel > 0)) {
            var snapshot = CardCultivationQuery.Read(tables, player, card.Id);
            Check(snapshot.cardId == card.Id, "逐卡只读查询：" + card.Id);
            report.Add(new MissingCard { cardId = card.Id, missing = snapshot.missing.ToArray() });
        }
        Reject(() => CardSkillQuery.Read(tables, 1302, 0, SkillDisplaySlot.Basic, 1101), "拒绝跨卡技能");
        Reject(() => CardSkillQuery.Read(tables, 1101, 1, SkillDisplaySlot.Passive, 1101), "拒绝包装错配");
        var fixtures = new Dictionary<string, string> {
            { "cfgobj_cultivationskillobj", "[{\"id\":90001,\"name\":\"Editor测试技能\",\"kind\":1,\"slot\":0,\"icon\":\"\"}]" },
            { "cfgobj_cardskillentryobj", "[{\"cardId\":1101,\"skillId\":90001,\"sortOrder\":1}]" },
            { "cfgobj_cardskillrankobj", "[{\"cardId\":1101,\"skillId\":90001,\"rank\":1,\"unlockLevel\":1,\"battleSkillId\":1101},{\"cardId\":1101,\"skillId\":90001,\"rank\":2,\"unlockLevel\":5,\"battleSkillId\":110102},{\"cardId\":1101,\"skillId\":90001,\"rank\":3,\"unlockLevel\":10,\"battleSkillId\":110103}]" }
        };
        var test = Load(fixtures);
        CardCultivationConfigValidation.Validate(test);
        var locked = CardSkillQuery.Read(test, 1101, 0, SkillDisplaySlot.Basic);
        Check(locked.unlocked == false && locked.currentRank == null && locked.ranks.All(value => !value.current), "未拥有卡锁定、可查看全部阶数");
        var rank2 = CardSkillQuery.Read(test, 1101, 5, SkillDisplaySlot.Basic, 90001);
        Check(rank2.currentRank == 2 && rank2.ranks.Count(value => value.current) == 1 && rank2.ranks[0].unlocked && !rank2.ranks[2].unlocked && rank2.battleSummary == rank2.ranks[1].effect, "最高阶唯一生效，完整替换不累加");
        var detail = CardSkillDetailQuery.Read(test, 1101, 5, CardSkillSlot.Basic, 90001);
        Check(detail.currentRank == rank2.currentRank && detail.stages[1].effect == rank2.ranks[1].effect && detail.stages[1].current, "③与④共用技能查询");
        Check(CardSkillQuery.ReadAll(test, 1101, 5).Single().skillId == 90001, "列表与详情稳定技能身份一致");
        foreach (var slot in new[] { SkillDisplaySlot.Passive, SkillDisplaySlot.Active, SkillDisplaySlot.Ultimate }) {
            var copy = new Dictionary<string, string>(fixtures);
            copy["cfgobj_cultivationskillobj"] = "[{\"id\":90001,\"name\":\"Editor包装夹具\",\"kind\":" + (slot == SkillDisplaySlot.Passive ? "2" : "1") + ",\"slot\":" + (int)slot + ",\"icon\":\"\"}]";
            var wrapperTables = Load(copy);
            CardCultivationConfigValidation.Validate(wrapperTables);
            Check(CardSkillQuery.Read(wrapperTables, 1101, 5, slot, 90001).currentRank == 2, "主动/被动底层与包装查询：" + slot);
            copy["cfgobj_cultivationskillobj"] = "[{\"id\":90001,\"name\":\"非法包装夹具\",\"kind\":" + (slot == SkillDisplaySlot.Passive ? "1" : "2") + ",\"slot\":" + (int)slot + ",\"icon\":\"\"}]";
            Reject(() => CardCultivationConfigValidation.Validate(Load(copy)), "拒绝底层类别与包装错配：" + slot);
        }
        var invalid = new Dictionary<string, string>(fixtures);
        invalid["cfgobj_cardskillentryobj"] = "[{\"cardId\":1101,\"skillId\":999999,\"sortOrder\":1}]";
        Reject(() => CardCultivationConfigValidation.Validate(Load(invalid)), "拒绝不存在的技能外键");
        var maxSaved = new PlayerProgressSaveVO { initialized = true };
        maxSaved.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = 1101, level = tables.CardObj.Get(1101).MaxLevel });
        var maxPlayer = new PlayerProgressControl(tables, maxSaved, ignored => { });
        var maxState = CardCultivationQuery.Read(tables, maxPlayer, 1101);
        Check(maxState.growth.maxed && maxState.materials.Count == 0 && maxState.attributes.All(value => value.target == null), "满级没有下一档材料或虚构目标属性");
        var unowned = CardCultivationQuery.Read(tables, player, 1302);
        Check(unowned.growth.level == 0 && unowned.growth.nextLevel == 1 && unowned.attributes.All(value => value.current == null), "未拥有卡仍读取实际解锁成本，缺一级数据不补造");
        fixtures["cfgobj_cardskillrankobj"] = "[{\"cardId\":1101,\"skillId\":90001,\"rank\":2,\"unlockLevel\":1,\"battleSkillId\":1101}]";
        Reject(() => CardCultivationConfigValidation.Validate(Load(fixtures)), "拒绝缺首阶");
        fixtures["cfgobj_cardskillrankobj"] = "[{\"cardId\":1302,\"skillId\":90001,\"rank\":1,\"unlockLevel\":1,\"battleSkillId\":1101}]";
        Reject(() => CardCultivationConfigValidation.Validate(Load(fixtures)), "拒绝孤立阶数");
        Check(tables.AttributeDefinitionObj.DataList.Count == 6 && state.attributes.All(row => row.attributeId > 0 && row.valueType == AttributeValueType.Absolute && !string.IsNullOrWhiteSpace(row.iconAddress)), "六项属性定义及类型图标来自配置");
        var level2Save = new PlayerProgressSaveVO { initialized = true };
        level2Save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = 1101, level = 2 });
        var level2 = CardCultivationQuery.Read(tables, new PlayerProgressControl(tables, level2Save, ignored => { }), 1101);
        Check(level2.power == 193, "升级后临时战力由同一卡牌单位和建筑属性重算193");
        var invalidAttributes = state.attributes.Select(row => new CultivationAttributeVO { attributeId = row.attributeId, current = row.attributeId == 6 ? 0 : row.current }).ToArray();
        Check(CardCultivationPowerQuery.Read(tables, invalidAttributes, out int formulaVersion, out bool temporaryFormula) == null, "生产间隔为0拒绝临时战力除零");
        var previewSave = new PlayerProgressSaveVO { initialized = true };
        var previewPlayer = new PlayerProgressControl(tables, previewSave, ignored => { });
        var preview = CardCultivationQuery.Read(tables, previewPlayer, 1101);
        Check(!preview.growth.owned && preview.attributes.Any(row => row.attributeId == 1 && row.current == 100) && preview.attributes.All(row => row.target == null), "未解锁只读1级预览，无下一档增量且不变更拥有状态");
        var formatter = typeof(CardCultivationPanel).GetMethod("AttributeNumber", BindingFlags.Static | BindingFlags.NonPublic);
        var percentage = new CultivationAttributeVO { valueType = AttributeValueType.Percentage };
        Check((string)formatter.Invoke(null, new object[] { percentage, (double?).25 }) == "25%", "百分比0.25显示25%，不改权威值");
        var skillOrderFixture = new Dictionary<string, string> {
            { "cfgobj_cultivationskillobj", "[{\"id\":91001,\"name\":\"先解锁夹具\",\"kind\":2,\"slot\":1,\"icon\":\"\"},{\"id\":91002,\"name\":\"后解锁夹具\",\"kind\":1,\"slot\":2,\"icon\":\"\"}]" },
            { "cfgobj_cardskillentryobj", "[{\"cardId\":1101,\"skillId\":91001,\"sortOrder\":99},{\"cardId\":1101,\"skillId\":91002,\"sortOrder\":1}]" },
            { "cfgobj_cardskillrankobj", "[{\"cardId\":1101,\"skillId\":91001,\"rank\":1,\"unlockLevel\":2,\"battleSkillId\":1101},{\"cardId\":1101,\"skillId\":91001,\"rank\":2,\"unlockLevel\":10,\"battleSkillId\":1101},{\"cardId\":1101,\"skillId\":91002,\"rank\":1,\"unlockLevel\":5,\"battleSkillId\":1101}]" }
        };
        var orderedTables = Load(skillOrderFixture);
        Check(CardSkillQuery.ReadAll(orderedTables, 1101, 1)[0].skillId == 91001 && CardSkillQuery.ReadAll(orderedTables, 1101, 10)[0].skillId == 91001, "技能首阶解锁顺序优先于sortOrder，升阶不重排");
        CardGrowthValidation.ValidateCore(tables);
        Check(true, "现有联合事务回归：升级/拒绝/过期/重复/重载/保存失败不变");
        File.WriteAllText("Library/AIUI/card-cultivation-missing.json", JsonUtility.ToJson(new MissingReport { cards = report.ToArray() }, true));
    }

}
#endif
