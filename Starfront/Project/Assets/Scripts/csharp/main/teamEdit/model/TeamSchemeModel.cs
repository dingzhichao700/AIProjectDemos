using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 四套方案的权威状态与提交边界。
/// </summary>
/// <remarks>持久化成功后才更新编号、顺序及revision；对外始终返回副本。</remarks>
public sealed class TeamSchemeModel {

    private readonly object sync = new object();

    private readonly Func<int, string> eligibility;

    private readonly Action<TeamSchemeSaveVO> persist;

    private TeamSchemeSaveVO current;

    public event Action Saved;

    public TeamSchemeModel(TeamSchemeSaveVO saved, Func<int, string> canDeploy, Action<TeamSchemeSaveVO> save) {
        eligibility = canDeploy ?? throw new ArgumentNullException(nameof(canDeploy));
        persist = save ?? throw new ArgumentNullException(nameof(save));
        ValidateShape(saved ?? new TeamSchemeSaveVO());
        current = Copy(saved ?? new TeamSchemeSaveVO());
    }

    public TeamSchemeSaveVO Read() {
        lock (sync) {
            return Copy(current);
        }
    }

    public string GetUnavailableReason(int cardId) {
        return cardId == 0 ? "" : eligibility(cardId) ?? "";
    }

    private static TeamSchemeSaveVO Copy(TeamSchemeSaveVO source) {
        return new TeamSchemeSaveVO { schemaVersion = source.schemaVersion, revision = source.revision, selected = source.selected, schemes = source.schemes.Select(CompactSlots).ToArray() };
    }

    /// <summary>保持已有单位相对顺序，空槽只补在队尾；不修改调用方数组。</summary>
    public static int[] CompactSlots(int[] row) {
        if (row == null || row.Length != 4) {
            throw new ArgumentException("每套方案必须有四个槽位", nameof(row));
        }
        var packed = new int[4];
        int next = 0;
        foreach (int id in row) {
            if (id != 0) {
                packed[next++] = id;
            }
        }
        return packed;
    }

    public static void ValidateShape(TeamSchemeSaveVO value) {
        if (value == null || value.schemaVersion != 1 || value.revision < 0 || value.selected < 1 || value.selected > 4 || value.schemes == null || value.schemes.Length != 4) {
            throw new InvalidOperationException("队伍存档格式无效，保留原文件");
        }
        foreach (var row in value.schemes) {
            if (row == null || row.Length != 4 || row.Any(id => id < 0) || row.Where(id => id > 0).Distinct().Count() != row.Count(id => id > 0)) {
                throw new InvalidOperationException("每套方案必须有四个槽位且卡牌不可重复");
            }
        }
    }

    /// <summary>
    /// 重新校验当前资格并原子提交草稿；失败不改变已保存状态。
    /// </summary>
    /// <remarks>监听异常在提交之后隔离处理，不能把成功保存误报为失败。</remarks>
    public bool TrySave(int scheme, IReadOnlyList<int> slots, long expectedRevision, out string reason) {
        lock (sync) {
            if (scheme < 1 || scheme > 4 || slots == null || slots.Count != 4) {
                reason = "方案编号或卡位数量无效";
                return false;
            }
            if (expectedRevision != current.revision) {
                reason = "方案已被其他操作更新，请放弃修改后重新打开";
                return false;
            }
            var candidate = Copy(current);
            candidate.selected = scheme;
            candidate.schemes[scheme - 1] = CompactSlots(slots.ToArray());
            try {
                candidate.revision = checked(current.revision + 1);
                ValidateShape(candidate);
                foreach (int id in slots.Where(id => id != 0)) {
                    reason = GetUnavailableReason(id);
                    if (!string.IsNullOrEmpty(reason)) {
                        return false;
                    }
                }
                persist(Copy(candidate));
            } catch (Exception error) {
                reason = "保存失败，草稿已保留：" + error.Message;
                return false;
            }
            current = candidate;
        }
        reason = "";
        if (Saved != null) {
            foreach (Action handler in Saved.GetInvocationList()) {
                try {
                    handler();
                } catch (Exception error) {
                    UnityEngine.Debug.LogException(error);
                }
            }
        }
        return true;
    }

}
