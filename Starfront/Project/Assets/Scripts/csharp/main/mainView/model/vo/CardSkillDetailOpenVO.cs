using System;

/// <summary>
/// 培养页打开技能详情时传入的卡牌和技能身份。
/// </summary>
/// <remarks>关闭仅通知来源，不重建或切换培养页，不持有培养页滚动数据。</remarks>
public sealed class CardSkillDetailOpenVO {

    public int cardId;

    public CardSkillSlot slot;

    public int skillId;

    public Action onClosed;

}