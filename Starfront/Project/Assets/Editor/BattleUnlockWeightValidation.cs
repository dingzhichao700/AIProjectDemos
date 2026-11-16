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
            var results = new System.Collections.Generic.List<string>();
            foreach (var camp in new[] { BattleSceneCamp.FRIENDLY, BattleSceneCamp.ENEMY }) {
                var owner = BattleRoster.CreateCamp(tables, start.SceneId, camp);
                foreach (var rule in tables.CellUnlockObj.DataList) {
                    int total = rule.EmptyWeight + rule.CollectorWeight + rule.UnitQualityWeights.Sum(v => v.Weight);
                    int cardRollCount = 1;
                    foreach (var quality in rule.UnitQualityWeights) {
                        int sum = owner.cardIds.Where(id => tables.CardObj.Get(id).Content is UnitCardContent && tables.CardObj.Get(id).Quality == quality.Quality).Sum(id => tables.CardObj.Get(id).UnlockWeight);
                        int gcd = cardRollCount, other = sum;
                        while (other != 0) { int remainder = gcd % other; gcd = other; other = remainder; }
                        cardRollCount = checked(cardRollCount / gcd * sum);
                    }
                    var actual = new System.Collections.Generic.Dictionary<int, int>();
                    for (int outcome = 0; outcome < total; outcome++) {
                        for (int card = 0; card < cardRollCount; card++) {
                            random.SetValue(model, new Rolls { outcome = outcome, card = card });
                            int? result = (int?)method.Invoke(model, new object[] { rule, owner });
                            if (result.HasValue && !owner.cardIds.Contains(result.Value)) { throw new Exception("Result outside current deck"); }
                            int id = result ?? 0;
                            actual[id] = actual.TryGetValue(id, out int count) ? count + 1 : 1;
                        }
                    }
                    Require((actual.TryGetValue(0, out int empty) ? empty : 0) == rule.EmptyWeight * cardRollCount, "Exact empty interval " + rule.Id);
                    foreach (int id in owner.cardIds) {
                        var candidate = tables.CardObj.Get(id);
                        int expected = 0;
                        if (candidate.Content is BuildingCardContent) { expected = rule.CollectorWeight * cardRollCount; }
                        else {
                            var quality = rule.UnitQualityWeights.SingleOrDefault(v => v.Quality == candidate.Quality);
                            if (quality != null) {
                                int sum = owner.cardIds.Where(v => tables.CardObj.Get(v).Content is UnitCardContent && tables.CardObj.Get(v).Quality == candidate.Quality).Sum(v => tables.CardObj.Get(v).UnlockWeight);
                                expected = quality.Weight * (cardRollCount / sum) * candidate.UnlockWeight;
                            }
                        }
                        Require((actual.TryGetValue(id, out int count) ? count : 0) == expected, "Exact card interval " + rule.Id + "/" + id);
                    }
                    results.Add(camp + "/" + rule.Id + ": total=" + total + ", enumerated=" + total * cardRollCount);

                }
            }
            foreach (int badWeight in new[] { 0, -1 }) {
                bool rejected = false;
                try {
                    BattleRoster.CreateCamp(Load(badWeight), start.SceneId, BattleSceneCamp.FRIENDLY);
                } catch (InvalidOperationException) {
                    rejected = true;
                }
                Require(rejected, "Invalid/overflow weight must be rejected: " + badWeight);
            }
            File.WriteAllText("Library/AIUI/unlock-weight-validation.txt", "passed: both camps; exhaustive configured total and within-quality intervals; empty and every card verified; zero/negative rejected; formal generated Tables\n" + string.Join("\n", results));
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
                    if (card["id"].AsInt == 1101) {
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
