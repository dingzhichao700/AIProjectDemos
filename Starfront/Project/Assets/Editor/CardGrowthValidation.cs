using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cfg;
using SimpleJSON;
using UnityEngine.UI;

/// <summary>
/// 成长、原子扣费、存档及真实牌库确认流程验证。
/// </summary>
/// <remarks>使用独立测试路径和隔离会话，不授予真实玩家道具。</remarks>
public static class CardGrowthValidation {

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException("CardGrowth: " + message);
        }
    }

    private static void Grant(InventoryControl inventory, int itemId, long count) {
        Require(inventory.TryGrantItem(itemId, count, out string reason), reason);
    }

    public static void ValidateCore(Tables tables) {
        string directory = "Library/AIUI/CardGrowthValidation/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(directory);
        string path = directory + "/player.json";
        var store = new PlayerProgressStore(path);
        var session = new PlayerProgressControl(tables, store.Load(), store.Save);
        var model = session.Cards.model;
        Require(model.GetState(1101).level == 1 && tables.CardObj.DataList.Where(c => c.ShowInLibrary && c.Id != 1101).All(c => model.GetState(c.Id).level == 0), "new player owns infantry1 only");
        Require(model.GetState(1101).requiredFragments == 15 && model.GetState(1302).requiredFragments == 20, "target level semantics");
        var locked = model.GetState(1302);
        Require(!model.TryUpgrade(1302, locked.level, locked.revision, out string reason), "insufficient rejected");
        Grant(session.Inventory, 3102, 19);
        Require(!model.GetState(1302).canUpgrade, "one fragment short");
        Grant(session.Inventory, 3102, 1);
        Require(model.GetState(1302).canUpgrade && model.GetState(1302).level == 0, "grant enables but never auto unlocks");
        Require(!model.TryUpgrade(1302, locked.level, locked.revision, out reason), "stale confirmation rejected");
        var ready = model.GetState(1302);
        Require(model.TryUpgrade(1302, 0, ready.revision, out reason), "manual unlock");
        Require(model.GetState(1302).level == 1 && session.Inventory.model.GetQuantity(3102) == 0, "unlock consumes exact per-card fragments");
        Require(!model.TryUpgrade(1302, 0, ready.revision, out reason), "double confirm cannot charge twice");
        session = new PlayerProgressControl(tables, store.Load(), store.Save);
        model = session.Cards.model;
        Require(model.GetState(1302).level == 1 && session.Inventory.model.GetQuantity(3102) == 0, "reloaded level and inventory agree");
        Grant(session.Inventory, 3102, 35);
        ready = model.GetState(1302);
        Require(model.TryUpgrade(1302, ready.level, ready.revision, out reason) && model.GetState(1302).level == 2 && session.Inventory.model.GetQuantity(3102) == 10, "target2 cost and excess fragments");
        foreach (var card in tables.CardObj.DataList.Where(c => c.ShowInLibrary)) {
            while (!model.GetState(card.Id).maxed) {
                var state = model.GetState(card.Id);
                foreach (var cost in state.costs) {
                    long missing = cost.Value - session.Inventory.model.GetQuantity(cost.Key);
                    if (missing > 0) {
                        Grant(session.Inventory, cost.Key, missing);
                    }
                }
                state = model.GetState(card.Id);
                Require(model.TryUpgrade(card.Id, state.level, state.revision, out reason), "every configured target level " + card.Id);
            }
            var max = model.GetState(card.Id);
            Require(max.level == card.MaxLevel && max.requiredFragments == 0 && !model.TryUpgrade(card.Id, max.level, max.revision, out reason), "independent cap " + card.Id);
        }
        bool failSave = false;
        var failureSession = new PlayerProgressControl(tables, null, data => {
            if (failSave) {
                throw new IOException("simulated persistence failure");
            }
        });
        var failure = failureSession.Cards.model;
        Grant(failureSession.Inventory, 3102, 20);
        ready = failure.GetState(1302);
        failSave = true;
        bool failed = false;
        try {
            failure.TryUpgrade(1302, 0, ready.revision, out reason);
        } catch (IOException) {
            failed = true;
        }
        Require(failed && failure.GetState(1302).level == 0 && failureSession.Inventory.model.GetQuantity(3102) == 20 && failure.GetState(1302).revision == ready.revision, "save failure leaves all authoritative state intact");
        var extraTables = new Tables(name => {
            JSONNode json = JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json"));
            if (name == "cfgobj_cardlevelobj") {
                foreach (JSONNode row in json.AsArray) {
                    if (row["cardId"].AsInt == 1302 && row["level"].AsInt == 1) {
                        row["extraCosts"] = JSON.Parse("[{\"itemId\":2002,\"count\":5}]");
                    }
                }
            }
            return json;
        });
        var extraSession = new PlayerProgressControl(extraTables, null, ignored => { });
        var extra = extraSession.Cards.model;
        Grant(extraSession.Inventory, 3102, 20);
        Require(!extra.GetState(1302).canUpgrade, "extra currency shortage blocks whole operation");
        Grant(extraSession.Inventory, 2002, 5);
        ready = extra.GetState(1302);
        Require(extra.TryUpgrade(1302, 0, ready.revision, out reason) && extraSession.Inventory.model.GetQuantity(2002) == 0 && extraSession.Inventory.model.GetQuantity(3102) == 0, "fragment and future currency consumed together");
        var overflowSession = new PlayerProgressControl(tables, null, ignored => { });
        var overflow = overflowSession.Cards.model;
        Grant(overflowSession.Inventory, 3102, long.MaxValue);
        failed = !overflowSession.Inventory.TryGrantItem(3102, 1, out reason);
        Require(failed && overflowSession.Inventory.model.GetQuantity(3102) == long.MaxValue, "inventory overflow rejected");
        File.WriteAllText(directory + "/corrupt.json", "not-json");
        failed = false;
        try {
            new PlayerProgressStore(directory + "/corrupt.json").Load();
        } catch (Exception) {
            failed = true;
        }
        Require(failed && File.ReadAllText(directory + "/corrupt.json") == "not-json", "corruption never silently resets player");
        InventoryValidation.Validate(tables, directory);
        File.WriteAllText("Library/AIUI/card-growth-validation.txt", "PASS: new player infantry1; all90 target levels and per-card caps; insufficient/exact/excess fragments; manual unlock; stale and double confirmation; disk reload; save failure rollback; future currency multi-cost; overflow; corrupt save retained. Test store: " + path);
    }



}
