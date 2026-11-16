using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using cfg;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// 独立背包、共享事务、旧存档迁移与失败边界的隔离验证。
/// </summary>
/// <remarks>所有文件限定在调用方的Library测试目录，绝不使用真实玩家路径。</remarks>
public static class InventoryValidation {

    private static void Check(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("Inventory: " + message);
        }
    }

    private static void Reject(Action action, string message) {
        bool rejected = false;
        try {
            action();
        } catch (Exception) {
            rejected = true;
        }
        Check(rejected, message);
    }

    public static void Validate(Tables tables, string directory) {
        ValidateCapacity(directory);
        string legacyPath = Path.Combine(directory, "cardCollection.json");
        string path = Path.Combine(directory, "playerProgress.json");
        var legacy = new PlayerProgressSaveVO { schemaVersion = 1, initialized = true, revision = 7 };
        legacy.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = 1101, level = 2 });
        legacy.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = 3101, count = 75 });
        legacy.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = 2002, count = 11 });
        legacy.extensions = new Dictionary<string, JToken> { { "playerLevel", new JValue(9) } };
        string original = JsonConvert.SerializeObject(legacy);
        File.WriteAllText(legacyPath, original);
        var store = new PlayerProgressStore(path, legacyPath);
        int saves = 0;
        bool fail = false;
        Action<PlayerProgressSaveVO> persist = data => {
            if (fail) {
                throw new IOException("injected failure before atomic replace");
            }
            store.Save(data);
            saves++;
        };
        var session = new PlayerProgressControl(tables, store.Load(), persist);
        var inventory = session.Inventory;
        var cards = session.Cards.model;
        Check(saves == 1 && session.model.Revision == 7 && cards.GetState(1101).level == 2 && inventory.model.GetQuantity(3101) == 75, "migration preserves level/balance/revision and saves once");
        Check(File.ReadAllText(legacyPath) == original && store.Load().extensions["playerLevel"].Value<int>() == 9, "legacy bytes and unrelated player level retained");
        var previous = PlayerProgressControl.InstallValidationSession(session);
        try {
            Check(ReferenceEquals(InventoryControl.ins, inventory) && ReferenceEquals(CardCollectionControl.ins.model, cards), "all modules resolve one player and inventory");
            int inventoryEvents = 0;
            int cardEvents = 0;
            bool eventStateConsistent = true;
            Action balanceListener = () => {
                inventoryEvents++;
                var disk = store.Load();
                eventStateConsistent &= disk.revision == session.model.Revision && disk.items.Single(row => row.itemId == 3101).count == inventory.model.GetQuantity(3101);
            };
            inventory.model.Changed += balanceListener;
            cards.Changed += () => cardEvents++;
            Check(inventory.TryGrantItem(3102, 20, out string reason), reason);
            Check(cards.GetState(1302).canUpgrade && inventoryEvents == 1 && cardEvents == 1, "shared grant refreshes card availability once");
            var state = cards.GetState(1302);
            int beforeSaves = saves;
            Check(cards.TryUpgrade(1302, state.level, state.revision, out reason), reason);
            Check(saves == beforeSaves + 1 && cards.GetState(1302).level == 1 && inventory.model.GetQuantity(3102) == 0, "unlock is one joint save");
            Check(!cards.TryUpgrade(1302, state.level, state.revision, out reason) && saves == beforeSaves + 1, "repeat confirmation no write");
            var both = new Dictionary<int, long> { { 3101, 5 }, { 2002, 12 } };
            long revision = session.model.Revision;
            Check(!inventory.model.CanAfford(both) && !inventory.TryConsume(both, out reason), "multi-item shortage rejected");
            Check(inventory.model.GetQuantity(3101) == 75 && session.model.Revision == revision, "shortage consumes nothing");
            both[2002] = 3;
            Check(inventory.model.CanAfford(both) && inventory.TryConsume(both, out reason), "shared multi-item consume");
            Check(inventory.model.GetQuantity(3101) == 70 && inventory.model.GetQuantity(2002) == 8, "all costs committed");
            Check(!inventory.TryGrantItem(-1, 1, out reason) && !inventory.TryGrantItem(3101, 0, out reason) && !inventory.TryConsume(3101, -1, out reason), "unknown and non-positive rejected");
            int eventsBeforeFailure = inventoryEvents;
            revision = session.model.Revision;
            string diskBeforeFailure = File.ReadAllText(path);
            fail = true;
            Check(!inventory.TryGrantItem(3101, 1, out reason) && !inventory.TryConsume(3101, 1, out reason), "grant and consume save failures rejected");
            state = cards.GetState(1101);
            Reject(() => cards.TryUpgrade(1101, state.level, state.revision, out _), "upgrade persistence failure reported");
            Check(cards.GetState(1101).level == 2 && inventory.model.GetQuantity(3101) == 70 && session.model.Revision == revision && inventoryEvents == eventsBeforeFailure && File.ReadAllText(path) == diskBeforeFailure, "failed joint transaction preserves memory/disk/revision and sends no event");
            fail = false;
            Check(cards.TryUpgrade(1101, state.level, state.revision, out reason), "same confirmation can retry after failed save");
            Check(eventStateConsistent, "all events observe durable state");
            inventory.model.Changed -= balanceListener;
            int detachedEvents = inventoryEvents;
            Check(inventory.TryGrantItem(3101, 1, out reason) && inventoryEvents == detachedEvents, "listener teardown");
            var reload = new PlayerProgressControl(tables, store.Load(), persist);
            Check(reload.Cards.model.GetState(1101).level == 3 && reload.Cards.model.GetState(1302).level == 1 && reload.Inventory.model.GetQuantity(3101) == inventory.model.GetQuantity(3101), "reload uses current save, not unchanged legacy");
            int reloadWrites = saves;
            new PlayerProgressControl(tables, store.Load(), persist);
            Check(saves == reloadWrites && File.ReadAllText(legacyPath) == original, "reload never regrants or overwrites old file");
            File.WriteAllText(path, "corrupt-new-save");
            Reject(() => store.Load(), "corrupt new save never falls back to legacy");
            Check(File.ReadAllText(path) == "corrupt-new-save", "corrupt new save preserved");
            File.WriteAllText(path, diskBeforeFailure);
        } finally {
            PlayerProgressControl.InstallValidationSession(previous);
        }
        string failedMigrationPath = Path.Combine(directory, "migration-failure.json");
        var migrationStore = new PlayerProgressStore(failedMigrationPath, legacyPath);
        Reject(() => new PlayerProgressControl(tables, migrationStore.Load(), data => { throw new IOException("migration write failure"); }), "migration fails closed");
        Check(!File.Exists(failedMigrationPath) && File.ReadAllText(legacyPath) == original, "failed migration preserves source and no new snapshot");
        foreach (var mutation in new Action<PlayerProgressSaveVO>[] {
            data => data.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = 3101, count = 1 }),
            data => data.items[0].count = -1,
            data => data.items[0].itemId = -1,
            data => data.cards[0].level = 999,
            data => data.cards.Add(null),
            data => data.schemaVersion = 99
        }) {
            var invalid = JsonConvert.DeserializeObject<PlayerProgressSaveVO>(original);
            mutation(invalid);
            int attempts = 0;
            Reject(() => new PlayerProgressControl(tables, invalid, data => attempts++), "invalid data rejected");
            Check(attempts == 0, "invalid migration never saved");
        }
        string emptyLegacy = Path.Combine(directory, "empty-legacy.json");
        var empty = new PlayerProgressSaveVO { schemaVersion = 1, initialized = true, revision = 4 };
        File.WriteAllText(emptyLegacy, JsonConvert.SerializeObject(empty));
        var emptyStore = new PlayerProgressStore(Path.Combine(directory, "empty-new.json"), emptyLegacy);
        var emptySession = new PlayerProgressControl(tables, emptyStore.Load(), emptyStore.Save);
        Check(emptySession.Cards.model.GetState(1101).level == 0 && emptySession.Inventory.model.GetBalances().Count == 0, "initialized empty legacy receives no initial rewards");
        File.WriteAllText("Library/AIUI/inventory-validation.txt", "PASS: independent shared inventory; grant/consume/query; multi-cost atomicity; one-write card upgrade; stale confirmation; events after persistence and teardown; failure preserves disk/memory/revision; migration retains original bytes and extension fields; no reward replay; reload precedence; corrupt/invalid migration and failed migration. Independent stores only: " + directory);
    }

    /**使用独立配置副本制造有限堆叠；正式碎片表保持不变。*/
    private static void ValidateCapacity(string directory) {
        var tables = new Tables(name => {
            var json = SimpleJSON.JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json"));
            if (name == "cfgobj_itemobj") {
                foreach (SimpleJSON.JSONNode row in json.AsArray) {
                    if (row["id"].AsInt == 3101) {
                        row["stackMax"] = 10;
                    }
                    if (row["id"].AsInt == 2003) {
                        row["type"] = (int)ItemType.EQUIPMENT;
                    }
                }
            }
            return json;
        });
        var store = new PlayerProgressStore(Path.Combine(directory, "capacity.json"));
        bool fail = false;
        int saves = 0;
        var session = new PlayerProgressControl(tables, null, data => {
            if (fail) {
                throw new IOException("clear save failure");
            }
            store.Save(data);
            saves++;
        });
        var inventory = session.Inventory;
        Check(inventory.model.SlotCapacity == 100, "configured 100 slots");
        int events = 0;
        inventory.model.Changed += () => events++;
        Check(inventory.TryGrantItem(3101, 995, out string reason), reason);
        Check(inventory.model.GetStackCount(3101) == 100 && inventory.model.GetUsedSlotCount() == 100, "995 items occupy 100 stacks of10");
        Check(inventory.TryGrantItem(3101, 5, out reason) && inventory.model.GetQuantity(3101) == 1000, "full bag can fill existing partial stack");
        long revision = session.model.Revision;
        int priorSaves = saves;
        int priorEvents = events;
        string priorDisk = File.ReadAllText(Path.Combine(directory, "capacity.json"));
        Check(!inventory.TryGrantItem(3101, 1, out reason) && !inventory.TryGrantItem(3102, 1, out reason), "full bag rejects extra stack and new type");
        Check(inventory.model.GetQuantity(3101) == 1000 && inventory.model.GetQuantity(3102) == 0 && session.model.Revision == revision && saves == priorSaves && events == priorEvents && File.ReadAllText(Path.Combine(directory, "capacity.json")) == priorDisk, "capacity rejection changes no memory/disk/revision/events");
        Check(inventory.TryConsume(3101, 10, out reason), reason);
        Check(!inventory.TryGrantItem(3101, 11, out reason) && inventory.model.GetQuantity(3101) == 990, "insufficient capacity rejects entire amount, no partial grant");
        Check(inventory.TryGrantItem(3102, long.MaxValue, out reason), reason);
        Check(inventory.model.GetStackCount(3102) == 1 && inventory.model.GetUsedSlotCount() == 100, "stackMax0 occupies one slot");
        Check(inventory.TryGrantItem(2002, 20, out reason) && inventory.model.GetStackCount(2002) == 0 && inventory.model.GetUsedSlotCount() == 100, "currency still accepted when full and consumes no slot");
        var state = session.Cards.model.GetState(1101);
        priorSaves = saves;
        priorEvents = events;
        fail = true;
        Check(!inventory.TryClear(out reason) && inventory.model.GetQuantity(3101) == 990 && inventory.model.GetQuantity(2002) == 20 && saves == priorSaves && events == priorEvents, "failed clear preserves all balances and emits no event");
        fail = false;
        Check(inventory.TryClear(out reason) && inventory.model.GetBalances().Count == 0 && inventory.model.GetUsedSlotCount() == 0 && saves == priorSaves + 1 && events == priorEvents + 1, "clear commits once and releases all slots including currency balances");
        Check(session.Cards.model.GetState(1101).level == state.level, "clear preserves owned cards and levels");
        var reload = new PlayerProgressControl(tables, store.Load(), store.Save);
        Check(reload.Inventory.model.GetBalances().Count == 0 && reload.Cards.model.GetState(1101).level == state.level, "clear survives reload without initial reward replay");
        Check(inventory.TryGrantItem(2003, 100, out reason) && inventory.model.GetUsedSlotCount() == 100 && !inventory.TryGrantItem(2003, 1, out reason), "nonstacking equipment takes one slot each");
        Check(inventory.TryClear(out reason) && inventory.TryClear(out reason), "empty clear remains safe");
        var oversized = store.Load();
        oversized.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = 3101, count = 1010 });
        var legacy = new PlayerProgressControl(tables, oversized, store.Save);
        Check(legacy.Inventory.model.GetUsedSlotCount() == 101 && legacy.Inventory.model.GetQuantity(3101) == 1010, "preexisting overcapacity keeps player items");
        Check(!legacy.Inventory.TryGrantItem(3102, 1, out reason) && legacy.Inventory.TryConsume(3101, 20, out reason) && legacy.Inventory.TryGrantItem(3102, 1, out reason), "overcapacity blocks additions until consumption frees capacity");
        File.WriteAllText("Library/AIUI/inventory-capacity-validation.txt", "PASS: config100; stack10 splits/fills; full bag and insufficient slots reject entire grant; unchanged disk/revision/events; stackMax0 one slot; currency no slots and accepted when full; consume releases slots; clear all including currency with one save/event; failed clear rollback; clear reload/card levels retained; equipment count slots; old overcapacity retained and recoverable. Isolated store: " + directory);
    }

}
