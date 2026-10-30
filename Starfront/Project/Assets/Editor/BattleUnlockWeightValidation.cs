using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using cfg;
using SimpleJSON;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 穷举两级抽取区间，验证正式业务抽取方法而非随机频次近似。
/// </summary>
public static class BattleUnlockWeightValidation {

    private sealed class Rolls : System.Random {
        public int outcome;
        public int card;
        private int count;

        public override int Next(int maxValue) {
            return (count++ == 0 ? outcome : card) % maxValue;
        }
    }

    [InitializeOnLoadMethod]
    private static void Schedule() {
        EditorApplication.update += Poll;
    }

    private static void Poll() {
        const string flag = "Library/AIUI/validate-unlock-weights.flag";
        if (!File.Exists(flag) || EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) {
            return;
        }
        File.Delete(flag);
        Run();
    }

    [MenuItem("Tools/Starfront/验证/解锁卡牌权重")]
    public static void Run() {
        try {
            Tables tables = Load();
            var start = tables.BattleStartObj.DataList[0];
            var method = typeof(BattleUnlockModel).GetMethod("DrawCard", BindingFlags.Instance | BindingFlags.NonPublic);
            var random = typeof(BattleUnlockModel).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic);
            // 仅隔离无场景副作用的抽取方法，权威输入仍来自正式生成的Tables和卡组校验。
            var model = (BattleUnlockModel)FormatterServices.GetUninitializedObject(typeof(BattleUnlockModel));
            typeof(BattleUnlockModel).GetField("tables", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, tables);
            int[] expected = { 0, 60, 60, 120, 120 };
            foreach (var camp in new[] { BattleSceneCamp.FRIENDLY, BattleSceneCamp.ENEMY }) {
                var owner = BattleRoster.CreateCamp(tables, start.SceneId, camp);
                foreach (var rule in tables.CellUnlockObj.DataList) {
                    int found = 0;
                    int empty = 0;
                    for (int outcome = 0; outcome < 100; outcome++) {
                        for (int card = 0; card < 4; card++) {
                            random.SetValue(model, new Rolls { outcome = outcome, card = card });
                            int? result = (int?)method.Invoke(model, new object[] { rule, owner });
                            if (result == 1201) {
                                found++;
                            }
                            if (!result.HasValue) {
                                empty++;
                            } else if (!owner.cardIds.Contains(result.Value)) {
                                throw new Exception("Result outside current deck");
                            }
                        }
                    }
                    Require(found == expected[rule.Id - 1], "Exact banshee probability " + rule.Id + ": " + found + "/400");
                    Require(empty == (rule.Id == 2 ? 200 : 0), "Empty probability " + rule.Id);
                }
            }
            foreach (int badWeight in new[] { 0, -1, int.MaxValue }) {
                bool rejected = false;
                try {
                    BattleRoster.CreateCamp(Load(badWeight), start.SceneId, BattleSceneCamp.FRIENDLY);
                } catch (InvalidOperationException) {
                    rejected = true;
                }
                Require(rejected, "Invalid/overflow weight must be rejected: " + badWeight);
            }
            File.WriteAllText("Library/AIUI/unlock-weight-validation.txt", "passed: both camps; exhaustive DrawCard intervals 0%,15%,15%,30%,30%; random empty50%; results stay in deck; zero/negative/overflow rejected; formal generated Tables");
        } catch (Exception error) {
            File.WriteAllText("Library/AIUI/unlock-weight-validation.txt", "failed: " + error);
            Debug.LogException(error);
        }
    }

    private static Tables Load(int? weight = null) {
        return new Tables(name => {
            var node = JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json"));
            if (weight.HasValue && name == "cfgobj_cardobj") {
                foreach (JSONNode card in node.AsArray) {
                    if (card["id"].AsInt == 1201) {
                        card["unlockWeight"] = weight.Value;
                    }
                }
            }
            return node;
        });
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }

}
