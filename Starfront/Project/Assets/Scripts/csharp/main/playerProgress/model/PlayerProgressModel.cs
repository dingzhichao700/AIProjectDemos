using System;
using System.Linq;
using cfg;
using Newtonsoft.Json;

/// <summary>
/// 同一玩家局外状态的事务与持久化边界。
/// </summary>
/// <remarks>卡牌与背包共享一个锁、revision和快照；候选只在保存成功后发布。</remarks>
public sealed class PlayerProgressModel {

    private readonly Tables tables;

    private readonly Action<PlayerProgressSaveVO> persist;

    internal object SyncRoot { get; } = new object();

    internal PlayerProgressSaveVO Current { get; private set; }

    public long Revision => Current.revision;

    internal event Action Committed;

    public PlayerProgressModel(Tables config, PlayerProgressSaveVO saved, Action<PlayerProgressSaveVO> save) {
        tables = config ?? throw new ArgumentNullException(nameof(config));
        persist = save ?? throw new ArgumentNullException(nameof(save));
        bool needsSave = saved == null || saved.schemaVersion == 1;
        var candidate = saved == null ? CreateInitial() : Copy(saved);
        bool migrated = RetiredCardProgressMigration.Apply(candidate);
        if (migrated) {
            candidate.revision = checked(candidate.revision + 1);
            needsSave = true;
        }
        Validate(candidate);
        candidate.schemaVersion = 2;
        if (needsSave) {
            persist(Copy(candidate));
        }
        Current = candidate;
    }

    private PlayerProgressSaveVO CreateInitial() {
        var initial = new PlayerProgressSaveVO { initialized = true };
        foreach (var row in tables.PlayerInitialCardObj.DataList) {
            initial.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = row.CardId, level = row.Level });
        }
        foreach (var row in tables.PlayerInitialItemObj.DataList) {
            initial.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = row.ItemId, count = row.Count });
        }
        return initial;
    }

    private void Validate(PlayerProgressSaveVO saved) {
        RetiredCardProgressMigration.ValidateShape(saved);
        if ((saved.schemaVersion != 1 && saved.schemaVersion != 2) || !saved.initialized || saved.revision < 0 || saved.cards == null || saved.items == null || saved.cards.Any(row => row == null) || saved.items.Any(row => row == null)) {
            throw new InvalidOperationException("共享存档格式无效，禁止重置覆盖");
        }
        if (saved.cards.Select(row => row.cardId).Distinct().Count() != saved.cards.Count || saved.items.Select(row => row.itemId).Distinct().Count() != saved.items.Count) {
            throw new InvalidOperationException("存档包含重复记录");
        }
        foreach (var row in saved.cards) {
            var card = tables.CardObj.Get(row.cardId);
            if (card == null || card.MaxLevel <= 0 || row.level < 0 || row.level > card.MaxLevel) {
                throw new InvalidOperationException("存档卡牌等级不合法：" + row.cardId);
            }
        }
        foreach (var row in saved.items) {
            if (tables.ItemObj.Get(row.itemId) == null || row.count < 0) {
                throw new InvalidOperationException("存档道具余额不合法：" + row.itemId);
            }
        }
    }

    private static PlayerProgressSaveVO Copy(PlayerProgressSaveVO value) {
        return JsonConvert.DeserializeObject<PlayerProgressSaveVO>(JsonConvert.SerializeObject(value));
    }

    /**仅向模块内部提供候选；调用方须持有SyncRoot完成业务校验。*/
    internal PlayerProgressSaveVO CreateCandidate() {
        return Copy(Current);
    }

    /**持锁提交；不执行第二次独立保存，不允许监听器在提交途中重入。*/
    internal void Commit(PlayerProgressSaveVO candidate) {
        candidate.revision = checked(Current.revision + 1);
        Validate(candidate);
        var committed = Copy(candidate);
        persist(Copy(committed));
        Current = committed;
    }

    /**所有模型均从已提交快照读取，监听异常不改变事务成功结果。*/
    internal void PublishCommitted() {
        if (Committed == null) {
            return;
        }
        foreach (Action handler in Committed.GetInvocationList()) {
            try {
                handler();
            } catch (Exception error) {
                UnityEngine.Debug.LogException(error);
            }
        }
    }

}

