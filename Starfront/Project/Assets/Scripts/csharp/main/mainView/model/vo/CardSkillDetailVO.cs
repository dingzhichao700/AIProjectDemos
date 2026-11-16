using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能详情展示数据及独立的数据缺项。
/// </summary>
/// <remarks>当前战斗数值与技能阶数分开，禁止把CardUnitLevel等级映射为阶数。</remarks>
public sealed class CardSkillDetailVO {

    public int cardId;

    public int skillId;

    public CardSkillSlot slot;

    public string cardName;

    public string skillName;

    public string typeName;

    public string battleSummary;

    /**由外部正式图标消费方持有，弹窗只显示，不生成或销毁。*/
    public Sprite icon;

    public string iconAddress;

    public bool? unlocked;

    public int? currentRank;

    public IReadOnlyList<CardSkillStageVO> stages = new CardSkillStageVO[0];

    public IReadOnlyList<string> missing = new string[0];

}