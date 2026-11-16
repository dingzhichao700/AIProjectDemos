using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>退役十卡的原值保留迁移；不折算等级、碎片或补发新卡。</summary>
public static class RetiredCardProgressMigration {

    private static readonly HashSet<int> CardIds = new HashSet<int> { 1101, 1302, 1303, 1304, 1305, 1306, 1307, 1308, 1309, 1310 };
    private static readonly HashSet<int> ItemIds = new HashSet<int> { 3101, 3102, 3103, 3104, 3105, 3106, 3107, 3108, 3109, 3110 };

    public static bool Apply(PlayerProgressSaveVO saved) {
        ValidateShape(saved);
        var retiredCards = saved.retiredCards.ToDictionary(row => row.cardId, row => row.level);
        var retiredItems = saved.retiredItems.ToDictionary(row => row.itemId, row => row.count);
        // 先检查所有冲突，再修改调用方提供的候选副本。
        foreach (var row in saved.cards.Where(row => CardIds.Contains(row.cardId))) {
            if (retiredCards.TryGetValue(row.cardId, out int level) && level != row.level) {
                throw new InvalidOperationException("退役卡牌迁移冲突，保留原存档：" + row.cardId);
            }
        }
        foreach (var row in saved.items.Where(row => ItemIds.Contains(row.itemId))) {
            if (retiredItems.TryGetValue(row.itemId, out long count) && count != row.count) {
                throw new InvalidOperationException("退役碎片迁移冲突，保留原存档：" + row.itemId);
            }
        }
        bool changed = false;
        foreach (var row in saved.cards.Where(row => CardIds.Contains(row.cardId)).ToArray()) {
            if (!retiredCards.ContainsKey(row.cardId)) {
                saved.retiredCards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = row.cardId, level = row.level });
            }
            saved.cards.Remove(row);
            changed = true;
        }
        foreach (var row in saved.items.Where(row => ItemIds.Contains(row.itemId)).ToArray()) {
            if (!retiredItems.ContainsKey(row.itemId)) {
                saved.retiredItems.Add(new PlayerProgressSaveVO.ItemEntry { itemId = row.itemId, count = row.count });
            }
            saved.items.Remove(row);
            changed = true;
        }
        return changed;
    }

    public static void ValidateShape(PlayerProgressSaveVO saved) {
        if (saved == null || (saved.schemaVersion != 1 && saved.schemaVersion != 2) || !saved.initialized || saved.revision < 0 || saved.cards == null || saved.items == null || saved.retiredCards == null || saved.retiredItems == null) {
            throw new InvalidOperationException("共享存档格式无效，禁止迁移覆盖");
        }
        if (saved.cards.Any(row => row == null || row.cardId <= 0 || row.level < 0) || saved.items.Any(row => row == null || row.itemId <= 0 || row.count < 0) || saved.retiredCards.Any(row => row == null || !CardIds.Contains(row.cardId) || row.level < 0) || saved.retiredItems.Any(row => row == null || !ItemIds.Contains(row.itemId) || row.count < 0)) {
            throw new InvalidOperationException("共享存档包含非法记录，禁止迁移覆盖");
        }
        if (saved.cards.Select(row => row.cardId).Distinct().Count() != saved.cards.Count || saved.items.Select(row => row.itemId).Distinct().Count() != saved.items.Count || saved.retiredCards.Select(row => row.cardId).Distinct().Count() != saved.retiredCards.Count || saved.retiredItems.Select(row => row.itemId).Distinct().Count() != saved.retiredItems.Count) {
            throw new InvalidOperationException("共享存档包含重复记录，禁止迁移覆盖");
        }
    }
}
