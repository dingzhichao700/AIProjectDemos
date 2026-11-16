using System;
using System.IO;
using cfg;
using UnityEngine;

/// <summary>
/// 当前玩家局外模块的组合与共享存档生命周期。
/// </summary>
/// <remarks>背包与卡牌同属一个会话；隔离验证必须整体替换并恢复会话。</remarks>
public sealed class PlayerProgressControl {

    private static PlayerProgressControl instance;

    public static PlayerProgressControl ins {
        get {
            if (instance == null) {
                var store = new PlayerProgressStore(Path.Combine(Application.persistentDataPath, "playerProgress.json"), Path.Combine(Application.persistentDataPath, "cardCollection.json"));
                instance = new PlayerProgressControl(CfgManager.tables, store.Load(), store.Save);
            }
            return instance;
        }
    }

    public PlayerProgressModel model { get; }

    public InventoryControl Inventory { get; }

    public CardCollectionControl Cards { get; }

    public PlayerProgressControl(Tables tables, PlayerProgressSaveVO saved, Action<PlayerProgressSaveVO> save) {
        model = new PlayerProgressModel(tables, saved, save);
        Inventory = new InventoryControl(new InventoryModel(tables, model));
        Cards = new CardCollectionControl(new CardCollectionModel(tables, model, Inventory.model));
    }

#if UNITY_EDITOR
    /**仅替换引用，不读取或写入真实玩家存档；finally恢复返回的旧会话。*/
    public static PlayerProgressControl InstallValidationSession(PlayerProgressControl value) {
        var previous = instance;
        instance = value;
        return previous;
    }
#endif

}
