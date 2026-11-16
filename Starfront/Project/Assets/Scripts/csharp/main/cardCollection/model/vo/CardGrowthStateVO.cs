using System.Collections.Generic;

/// <summary>
/// 卡牌成长查询结果，界面只消费已计算状态。
/// </summary>
/// <remarks>消耗为升到NextLevel的成本；满级没有下一档成本。</remarks>
public sealed class CardGrowthStateVO {

    public int cardId;
    public int level;
    public int maxLevel;
    public int nextLevel;
    public long revision;
    public long fragments;
    public long requiredFragments;
    public bool owned;
    public bool maxed;
    public bool canUpgrade;
    public string unavailableReason;
    public IReadOnlyDictionary<int, long> costs;

}
