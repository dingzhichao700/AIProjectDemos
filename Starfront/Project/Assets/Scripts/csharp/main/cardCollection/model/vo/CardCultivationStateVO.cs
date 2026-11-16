using System.Collections.Generic;
using cfg;

/// <summary>
/// 同一共享进度快照上的培养页只读查询结果。
/// </summary>
public sealed class CardCultivationStateVO {

    public int cardId;
    public string name;
    public Race race;
    public Quality quality;
    public int artId;
    public int constructionCost;
    public string portraitAddress;
    public string backgroundAddress;
    public string podiumAddress;
    /**对应美术条目的培养台座下移量，未配置为0。*/
    public int podiumOffsetY;
    public CardProfessionType? profession;
    public IReadOnlyList<CardProfessionType> counterProfessions;
    public double? power;
    public bool powerIsTemporary;
    public int powerFormulaVersion;
    public CardGrowthStateVO growth;
    public IReadOnlyList<CultivationMaterialVO> materials;
    public IReadOnlyList<CultivationAttributeVO> attributes;
    public IReadOnlyList<CardSkillStateVO> skills;
    public IReadOnlyList<CultivationStageVO> stages;
    public IReadOnlyList<string> missing;

}

/// <summary>实际下一目标等级的一项消耗与共享背包余额。</summary>
public sealed class CultivationMaterialVO {

    public int itemId;
    public string name;
    public int iconId;
    public long owned;
    public long required;

}

/// <summary>当前与目标等级的属性；null代表正式数据缺失。</summary>
public sealed class CultivationAttributeVO {

    public int attributeId;
    public AttributeValueType valueType;
    public string iconAddress;

    public string key;
    public string name;
    public string unit;
    public double? current;
    public double? target;

}

/// <summary>等级自动获得的展示阶段，不含领取及发奖状态。</summary>
public sealed class CultivationStageVO {

    public int cardId;
    public int stageId;
    // 以下技能字段仅保留旧接口兼容；独立等级效果不赋值、不参与身份或显示。
    public int skillId;
    public int rank;
    public string skillName;
    public int unlockLevel;
    public string description;
    public bool gained;

}
