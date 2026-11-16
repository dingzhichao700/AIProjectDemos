using System;
using System.Collections.Generic;
using System.Linq;
using cfg;

/// <summary>
/// 玩家唯一局外背包的查询、数量校验与事务候选计算。
/// </summary>
/// <remarks>不持有第二份余额，不包含局内货币；变更统一经InventoryControl提交。</remarks>
public sealed class InventoryModel {

    private readonly Tables tables;

    internal PlayerProgressModel Progress { get; }

    public event Action Changed;

    /**背包总格数来自常量配置，不限制积分货币余额。*/
    public int SlotCapacity {
        get {
            int capacity = InventoryConst.SlotCapacity;
            if (capacity <= 0) {
                throw new InvalidOperationException("背包格数必须为正整数");
            }
            return capacity;
        }
    }

    /**同种道具自动合堆；存总量，按堆叠规则计算规范化后的占格数。*/
    public long GetUsedSlotCount() {
        lock (Progress.SyncRoot) {
            return CountSlots(Progress.Current);
        }
    }

    public long GetStackCount(int itemId) {
        lock (Progress.SyncRoot) {
            ValidateItem(itemId);
            return StackCount(itemId, Quantity(Progress.Current, itemId));
        }
    }

    private long CountSlots(PlayerProgressSaveVO state) {
        long total = 0;
        foreach (var item in state.items) {
            total = checked(total + StackCount(item.itemId, item.count));
        }
        return total;
    }

    private long StackCount(int itemId, long count) {
        var item = tables.ItemObj.Get(itemId);
        if (item == null || count < 0) {
            throw new InvalidOperationException("无效背包道具或数量：" + itemId);
        }
        switch (item.Type) {
            case ItemType.SCORE:
                return 0;
            case ItemType.EQUIPMENT:
                return count;
            case ItemType.NORMAL:
            case ItemType.CONSUMABLE:
            case ItemType.SPECIAL:
                if (item.StackMax < 0) {
                    throw new InvalidOperationException("单堆上限不能为负数：" + itemId);
                }
                if (count == 0) {
                    return 0;
                }
                // 避免count + stackMax - 1在long上界发生溢出。
                return item.StackMax == 0 ? 1 : (count - 1) / item.StackMax + 1;
            default:
                throw new InvalidOperationException("不支持的道具类型：" + itemId);
        }
    }

    public InventoryModel(Tables config, PlayerProgressModel progress) {
        tables = config;
        Progress = progress;
        progress.Committed += PublishChanged;
    }

    public long GetQuantity(int itemId) {
        ValidateItem(itemId);
        lock (Progress.SyncRoot) {
            return Quantity(Progress.Current, itemId);
        }
    }

    /**返回只读副本，调用方不能通过集合改写实际余额。*/
    public IReadOnlyDictionary<int, long> GetBalances() {
        lock (Progress.SyncRoot) {
            return new System.Collections.ObjectModel.ReadOnlyDictionary<int, long>(Progress.Current.items.ToDictionary(row => row.itemId, row => row.count));
        }
    }

    public bool CanAfford(IReadOnlyDictionary<int, long> costs) {
        ValidateAmounts(costs);
        lock (Progress.SyncRoot) {
            return costs.All(cost => Quantity(Progress.Current, cost.Key) >= cost.Value);
        }
    }

    internal void ValidateAmounts(IReadOnlyDictionary<int, long> amounts) {
        if (amounts == null) {
            throw new ArgumentNullException(nameof(amounts));
        }
        foreach (var item in amounts) {
            ValidateItem(item.Key);
            if (item.Value <= 0) {
                throw new ArgumentOutOfRangeException(nameof(amounts), "道具数量必须为正整数");
            }
        }
    }

    private void ValidateItem(int itemId) {
        if (tables.ItemObj.Get(itemId) == null) {
            throw new InvalidOperationException("道具配置不存在：" + itemId);
        }
    }

    private static long Quantity(PlayerProgressSaveVO state, int itemId) {
        return state.items.FirstOrDefault(row => row.itemId == itemId)?.count ?? 0;
    }

    /**仅修改候选快照，卡牌联合事务复用同一扣费规则。*/
    internal bool TryStageConsume(PlayerProgressSaveVO candidate, IReadOnlyDictionary<int, long> costs) {
        ValidateAmounts(costs);
        if (costs.Any(cost => Quantity(candidate, cost.Key) < cost.Value)) {
            return false;
        }
        foreach (var cost in costs) {
            candidate.items.Single(row => row.itemId == cost.Key).count -= cost.Value;
        }
        return true;
    }

    internal void StageGrant(PlayerProgressSaveVO candidate, int itemId, long count) {
        ValidateAmounts(new Dictionary<int, long> { { itemId, count } });
        var row = candidate.items.FirstOrDefault(item => item.itemId == itemId);
        long next = checked(Quantity(candidate, itemId) + count);
        if (row == null) {
            candidate.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = itemId, count = next });
        } else {
            row.count = next;
        }
        // 只检查候选，容量不足时整笔拒绝；货币不占格，满包仍可入账。
        if (tables.ItemObj.Get(itemId).Type != ItemType.SCORE && CountSlots(candidate) > SlotCapacity) {
            throw new InvalidOperationException("背包格子不足，上限 " + SlotCapacity + " 格，本次未增加道具");
        }
    }

    private void PublishChanged() {
        if (Changed == null) {
            return;
        }
        foreach (Action handler in Changed.GetInvocationList()) {
            try {
                handler();
            } catch (Exception error) {
                UnityEngine.Debug.LogException(error);
            }
        }
    }

}
