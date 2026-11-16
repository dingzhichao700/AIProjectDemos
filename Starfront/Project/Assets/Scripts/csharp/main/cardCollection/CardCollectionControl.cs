using System;
using UnityEngine;

/// <summary>
/// 卡牌收藏与成长命令入口。
/// </summary>
/// <remarks>道具发放属于InventoryControl；卡牌升级使用共享联合事务。</remarks>
public sealed class CardCollectionControl {

    public static CardCollectionControl ins => PlayerProgressControl.ins.Cards;

    public CardCollectionModel model { get; }

    public CardCollectionControl(CardCollectionModel collection) {
        model = collection;
    }

    public bool TryUpgrade(int cardId, int level, long revision, out string reason) {
        try {
            return model.TryUpgrade(cardId, level, revision, out reason);
        } catch (Exception error) {
            Debug.LogException(error);
            reason = "升级失败，等级和道具均未改变：" + error.Message;
            return false;
        }
    }

}
