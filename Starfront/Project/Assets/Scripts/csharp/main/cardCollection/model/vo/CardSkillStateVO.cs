using System.Collections.Generic;
using cfg;

/// <summary>
/// 培养页与技能详情共用的技能配置及等级状态快照。
/// </summary>
/// <remarks>null解锁状态表示正式条件缺失，不等同于已锁定。</remarks>
public sealed class CardSkillStateVO {

    public int cardId;
    public int skillId;
    public SkillDisplaySlot slot;
    public SkillKind? kind;
    public string name;
    public string iconAddress;
    public int sortOrder;
    public bool? unlocked;
    public int? currentRank;
    public string battleSummary;
    public IReadOnlyList<CardSkillRankStateVO> ranks;
    public IReadOnlyList<string> missing;

}

/// <summary>
/// 一阶完整技能效果；高阶替换低阶，不累加。
/// </summary>
public sealed class CardSkillRankStateVO {

    public int rank;
    public int unlockLevel;
    public int battleSkillId;
    public bool unlocked;
    public bool current;
    public string effect;

}
