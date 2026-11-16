using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using cfg.resource;

/// <summary>
/// 管理卡牌拥有等级、升级资格与成长规则。
/// </summary>
/// <remarks>通过共享背包计算成本，通过玩家进度层一次提交等级与扣费。</remarks>
public sealed class CardCollectionModel {

    private readonly Tables tables;
    private readonly PlayerProgressModel progress;

    private readonly InventoryModel inventory;

    private object transactionLock => progress.SyncRoot;
    private readonly Dictionary<int, CardResource> cards;
    private readonly Dictionary<string, CardLevelResource> levels;
    private int activeBattles;

    public IDisposable BeginBattle() {
        lock (transactionLock) {
            activeBattles++;
        }
        PublishChanged();
        return new BattleLease(this);
    }

    private sealed class BattleLease : IDisposable {
        private CardCollectionModel owner;
        public BattleLease(CardCollectionModel owner) {
            this.owner = owner;
        }
        public void Dispose() {
            if (owner == null) {
                return;
            }
            var previous = owner;
            lock (previous.transactionLock) {
                previous.activeBattles--;
                owner = null;
            }
            previous.PublishChanged();
        }
    }

    public event Action Changed;

    public CardCollectionModel(Tables config, PlayerProgressModel sharedProgress, InventoryModel sharedInventory) {
        tables = config;
        progress = sharedProgress;
        inventory = sharedInventory;
        if (!ReferenceEquals(inventory.Progress, progress)) {
            throw new ArgumentException("卡牌与背包必须使用同一玩家事务");
        }
        cards = tables.CardObj.DataList.Where(card => card.MaxLevel > 0).ToDictionary(card => card.Id);
        levels = tables.CardLevelObj.DataList.ToDictionary(row => Key(row.CardId, row.Level));
        ValidateConfiguration();
        progress.Committed += PublishChanged;
    }

    private static string Key(int id, int level) {
        return id + ":" + level;
    }

    public void ValidateConfiguration() {
        foreach (var initial in tables.PlayerInitialCardObj.DataList) {
            if (!cards.TryGetValue(initial.CardId, out CardResource initialCard) || initial.Level < 1 || initial.Level > initialCard.MaxLevel) {
                throw new InvalidOperationException("初始拥有卡牌或等级非法");
            }
        }
        foreach (var card in cards.Values) {
            var fragment = tables.ItemObj.Get(card.FragmentItemId);
            if ((int)card.Quality < (int)Quality.NORMAL || fragment == null || fragment.Type != ItemType.NORMAL || fragment.Quality != card.Quality) {
                throw new InvalidOperationException("卡牌碎片类型或品质不匹配：" + card.Id);
            }
            if (card.MaxLevel < 1 || card.FragmentItemId <= 0 || tables.ItemObj.Get(card.FragmentItemId) == null || tables.CardArtObj.Get(card.ArtId) == null || tables.CardTagObj.Get(card.TagId) == null) {
                throw new InvalidOperationException("卡牌成长基础配置不完整：" + card.Id);
            }
            for (int level = 1; level <= card.MaxLevel; level++) {
                if (!levels.TryGetValue(Key(card.Id, level), out CardLevelResource row) || row.FragmentCount <= 0) {
                    throw new InvalidOperationException("卡牌目标等级配置缺失：" + card.Id + "/" + level);
                }
                Costs(card, row);
            }
        }
        foreach (var row in levels.Values) {
            if (!cards.TryGetValue(row.CardId, out CardResource card) || row.Level < 1 || row.Level > card.MaxLevel) {
                throw new InvalidOperationException("等级行不属于有效卡牌或超出上限");
            }
        }
        if (cards.Values.Select(card => card.FragmentItemId).Distinct().Count() != cards.Count) {
            throw new InvalidOperationException("每张卡须有独立碎片道具");
        }
    }

    private Dictionary<int, long> Costs(CardResource card, CardLevelResource target) {
        var costs = new Dictionary<int, long> { { card.FragmentItemId, target.FragmentCount } };
        foreach (var extra in target.ExtraCosts) {
            if (tables.ItemObj.Get(extra.ItemId) == null || double.IsNaN(extra.Count) || double.IsInfinity(extra.Count) || extra.Count <= 0 || extra.Count >= long.MaxValue || extra.Count != Math.Truncate(extra.Count)) {
                throw new InvalidOperationException("升级道具消耗必须引用有效道具和正整数数量");
            }
            costs[extra.ItemId] = checked((costs.TryGetValue(extra.ItemId, out long previous) ? previous : 0) + (long)extra.Count);
        }
        return costs;
    }

    public CardGrowthStateVO GetState(int cardId) {
        lock (transactionLock) {
            if (!cards.TryGetValue(cardId, out CardResource card)) {
                throw new InvalidOperationException("不可成长的卡牌：" + cardId);
            }
            int level = progress.Current.cards.FirstOrDefault(row => row.cardId == cardId)?.level ?? 0;
            bool maxed = level >= card.MaxLevel;
            var costs = maxed ? new Dictionary<int, long>() : Costs(card, levels[Key(cardId, level + 1)]);
            bool enough = !maxed && inventory.CanAfford(costs);
            return new CardGrowthStateVO {
                cardId = cardId, level = level, maxLevel = card.MaxLevel, nextLevel = maxed ? level : level + 1,
                revision = progress.Revision, owned = level > 0, maxed = maxed, canUpgrade = enough && activeBattles == 0,
                fragments = inventory.GetQuantity(card.FragmentItemId),
                requiredFragments = maxed ? 0 : levels[Key(cardId, level + 1)].FragmentCount,
                unavailableReason = activeBattles > 0 ? "战斗中不能升级卡牌" : maxed ? "已满级" : enough ? "" : "道具不足", costs = costs
            };
        }
    }

    public bool TryUpgrade(int cardId, int expectedLevel, long expectedRevision, out string reason) {
        lock (transactionLock) {
            CardGrowthStateVO state = GetState(cardId);
            if (state.level != expectedLevel || state.revision != expectedRevision) {
                reason = "数据已变化，请重新确认";
                return false;
            }
            if (!state.canUpgrade) {
                reason = state.unavailableReason;
                return false;
            }
            var candidate = progress.CreateCandidate();
            if (!inventory.TryStageConsume(candidate, state.costs)) {
                reason = "道具不足";
                return false;
            }
            var row = candidate.cards.FirstOrDefault(card => card.cardId == cardId);
            if (row == null) {
                candidate.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = state.nextLevel });
            } else {
                row.level = state.nextLevel;
            }
            // 只有一次持久化，失败时等级、全部余额与revision均保持原值。
            progress.Commit(candidate);
        }
        reason = "";
        progress.PublishCommitted();
        return true;
    }

    /**已提交事务不能因某个界面监听异常而被误报为扣费失败。*/
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
