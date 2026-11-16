using System;
using System.Linq;

/// <summary>
/// 队伍编辑草稿和未保存离开的状态机。
/// </summary>
/// <remarks>操作不触碰存档；确认放弃或显式保存才结束当前草稿。</remarks>
public sealed class TeamEditDraft {

    private readonly TeamSchemeModel model;

    private int[] slots;

    private int[] baseline;

    private long revision;

    public int Scheme { get; private set; }

    public int SelectedSlot { get; private set; } = -1;

    public bool Dirty => !slots.SequenceEqual(baseline);

    public int[] Slots => (int[])slots.Clone();

    public TeamEditDraft(TeamSchemeModel source) {
        model = source;
        Load(source.Read().selected);
    }

    private void Load(int scheme) {
        var saved = model.Read();
        Scheme = scheme;
        revision = saved.revision;
        baseline = (int[])saved.schemes[scheme - 1].Clone();
        slots = (int[])baseline.Clone();
        SelectedSlot = -1;
    }

    /**false表示需要确认；确认放弃后discard=true执行原目标操作。*/
    public bool TrySwitch(int scheme, bool discard = false) {
        if (scheme < 1 || scheme > 4) {
            throw new ArgumentOutOfRangeException(nameof(scheme));
        }
        if (scheme == Scheme) {
            return true;
        }
        if (Dirty && !discard) {
            return false;
        }
        Load(scheme);
        return true;
    }

    /**选中一槽再点另一槽换位；再次点同槽取消选择。*/
    public void SelectSlot(int slot) {
        if (slot < 0 || slot >= 4) {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        if (SelectedSlot >= 0 && SelectedSlot != slot) {
            int previous = slots[slot];
            slots[slot] = slots[SelectedSlot];
            slots[SelectedSlot] = previous;
            slots = TeamSchemeModel.CompactSlots(slots);
            SelectedSlot = -1;
        } else {
            SelectedSlot = SelectedSlot == slot ? -1 : slot;
        }
    }

    /// <summary>牌库点击仅加入；重复点击已上阵卡不重复加入，也不下阵。</summary>
    public bool TryAddCard(int id, out string reason) {
        if (id > 0 && Array.IndexOf(slots, id) >= 0) {
            SelectedSlot = -1;
            reason = "";
            return true;
        }
        return TryCard(id, out reason);
    }

    /// <summary>点击已有上阵位直接下阵，剩余单位稳定左移；空位不产生修改。</summary>
    public void RemoveAt(int index) {
        if (index < 0 || index >= slots.Length) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        slots[index] = 0;
        slots = TeamSchemeModel.CompactSlots(slots);
        SelectedSlot = -1;
    }

    public bool TryCard(int id, out string reason) {
        if (id <= 0) {
            reason = "卡牌无效";
            return false;
        }
        int existing = Array.IndexOf(slots, id);
        if (existing >= 0) {
            if (SelectedSlot >= 0 && SelectedSlot != existing) {
                int previous = slots[SelectedSlot];
                slots[SelectedSlot] = id;
                slots[existing] = previous;
            } else {
                slots[existing] = 0;
            }
            slots = TeamSchemeModel.CompactSlots(slots);
            SelectedSlot = -1;
            reason = "";
            return true;
        }
        reason = model.GetUnavailableReason(id);
        if (!string.IsNullOrEmpty(reason)) {
            return false;
        }
        int target = SelectedSlot >= 0 ? SelectedSlot : Array.IndexOf(slots, 0);
        if (target < 0) {
            reason = "队伍已满，请先点击上阵立绘下阵";
            return false;
        }
        slots[target] = id;
        slots = TeamSchemeModel.CompactSlots(slots);
        SelectedSlot = -1;
        return true;
    }

    public void Clear() {
        slots = new int[4];
        SelectedSlot = -1;
    }

    public bool TrySave(out string reason) {
        if (!model.TrySave(Scheme, slots, revision, out reason)) {
            return false;
        }
        Load(Scheme);
        return true;
    }

}
