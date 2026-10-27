using System;
using System.Collections.Generic;
using cfg;
using cfg.resource;

/// <summary>本局阵营状态；卡组和规则只读，局内余额不进入道具系统。</summary>
public sealed class BattleCampVO {
    public BattleStartResource config { get; }
    public IReadOnlyList<int> cardIds { get; }
    public long currency { get; private set; }
    public BattleSceneCamp camp => config.Camp;
    public int unitLimit => config.UnitLimit;
    public event Action<BattleCampVO> CurrencyChanged;

    internal BattleCampVO(BattleStartResource start, List<int> cards) {
        config = start;
        cardIds = cards.AsReadOnly();
        currency = start.InitialCurrency;
    }

    /**扣费只在最终提交时执行；余额变化事件观察到的格子与建筑已完成提交。*/
    internal void SpendCurrency(int count) {
        if (count < 0 || currency < count) {
            throw new InvalidOperationException("非法扣费或局内余额不足。");
        }
        currency -= count;
        CurrencyChanged?.Invoke(this);
    }

    internal void AddCurrency(long count) {
        if (count <= 0) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        currency = checked(currency + count);
        CurrencyChanged?.Invoke(this);
    }
}
