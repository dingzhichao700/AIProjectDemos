using System;
using System.Collections.Generic;

/// <summary>
/// 同一提交中的卡牌等级与背包余额。
/// </summary>
/// <remarks>0级表示未拥有，不保存可由配置推导的状态。</remarks>
[Serializable]
public sealed class PlayerProgressSaveVO {


    [Newtonsoft.Json.JsonRequired]
    public int schemaVersion = 2;

    [Newtonsoft.Json.JsonRequired]
    public bool initialized;

    [Newtonsoft.Json.JsonRequired]
    public long revision;

    [Newtonsoft.Json.JsonRequired]
    public List<CardLevelEntry> cards = new List<CardLevelEntry>();

    [Newtonsoft.Json.JsonRequired]
    public List<ItemEntry> items = new List<ItemEntry>();

    /**退役卡与碎片的原值档案，不参与当前成长或余额。*/
    public List<CardLevelEntry> retiredCards = new List<CardLevelEntry>();

    public List<ItemEntry> retiredItems = new List<ItemEntry>();

    /**保留旧快照中的扩展数据，迁移不丢弃其他业务字段。*/
    [Newtonsoft.Json.JsonExtensionData]
    public Dictionary<string, Newtonsoft.Json.Linq.JToken> extensions;

    [Serializable]
    public sealed class CardLevelEntry {

        [Newtonsoft.Json.JsonRequired]
        public int cardId;

        [Newtonsoft.Json.JsonRequired]
        public int level;

    }

    [Serializable]
    public sealed class ItemEntry {

        [Newtonsoft.Json.JsonRequired]
        public int itemId;

        [Newtonsoft.Json.JsonRequired]
        public long count;

    }

}

