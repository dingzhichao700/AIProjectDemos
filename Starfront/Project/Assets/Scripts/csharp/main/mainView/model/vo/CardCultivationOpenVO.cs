using System;
using UnityEngine;

/// <summary>培养页来源序列及返回上下文；不保存为玩家成长数据。</summary>
public sealed class CardCultivationOpenVO {

    public int cardId;
    public int[] orderedCardIds;
    public string source;
    public UnityEngine.Object owner;
    public Action onReturn;

}
