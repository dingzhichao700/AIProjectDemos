using System;
using System.Collections.Generic;

/// <summary>
/// 所有局外业务共用的道具发放和消耗入口。
/// </summary>
/// <remarks>保存成功才发布余额与事件，失败返回原因并保留原状态。</remarks>
public sealed class InventoryControl {

    public static InventoryControl ins => PlayerProgressControl.ins.Inventory;

    public InventoryModel model { get; }

    public InventoryControl(InventoryModel inventory) {
        model = inventory ?? throw new ArgumentNullException(nameof(inventory));
    }

    public bool TryGrantItem(int itemId, long count, out string reason) {
        return TryChange(candidate => {
            model.StageGrant(candidate, itemId, count);
            return true;
        }, out reason);
    }

    /**清空永久背包道具，保留卡牌等级及其他进度；保存失败全部保留。*/
    public bool TryClear(out string reason) {
        return TryChange(candidate => {
            candidate.items.Clear();
            return true;
        }, out reason);
    }

    public bool TryConsume(int itemId, long count, out string reason) {
        return TryConsume(new Dictionary<int, long> { { itemId, count } }, out reason);
    }

    public bool TryConsume(IReadOnlyDictionary<int, long> costs, out string reason) {
        if (costs == null || costs.Count == 0) {
            reason = "消耗清单不能为空";
            return false;
        }
        return TryChange(candidate => model.TryStageConsume(candidate, costs), out reason);
    }

    private bool TryChange(Func<PlayerProgressSaveVO, bool> change, out string reason) {
        try {
            lock (model.Progress.SyncRoot) {
                var candidate = model.Progress.CreateCandidate();
                if (!change(candidate)) {
                    reason = "道具不足";
                    return false;
                }
                model.Progress.Commit(candidate);
            }
        } catch (Exception error) {
            reason = "道具变更失败，原数量已保留：" + error.Message;
            return false;
        }
        reason = "";
        model.Progress.PublishCommitted();
        return true;
    }

}
