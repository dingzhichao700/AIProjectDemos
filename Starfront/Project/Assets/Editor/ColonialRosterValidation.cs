using System;
using System.IO;
using System.Linq;
using cfg;
using SimpleJSON;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>参考图十单位展示名册接入；未设定条目不进入战斗。</summary>
public static class ColonialRosterValidation {
    private const string Main = "Assets/Prefab/default/mainView/MainPanel.prefab";
    private const string Pending = "Library/AIUI/pending-colonial-roster.json";
    [Serializable] private class Request { public string requestId; public int processId; public string expectedMainHash; }
    [Serializable] private class Result { public string requestId; public string status; public string error; public int displayCount; }
    public static void ValidateTables(Tables tables) {
        string[] names = { "远征步兵", "突击机甲", "攻城步行者", "先锋统帅", "旋翼蜂鸟", "雷隼攻击机", "工程卫队", "疾行斥候", "轨道炮兵", "泰坦指挥机甲" };
        int[] qualities = { 2, 3, 4, 5, 2, 4, 2, 5, 5, 6 };
        if (tables.CardObj.DataList.Count(c => c.ShowInLibrary) != 10 || tables.CardArtObj.DataList.Count != 10 || tables.UnitObj.DataList.Count != 1 || tables.CardObj.DataList.Any(c => c.Id == 1201) || tables.BuildingObj.DataList.Any(c => c.Id == 1201) || tables.UnitProductionObj.DataList.Any(c => c.UnitId == 1201) || tables.SkillObj.DataList.Any(c => c.Id == 1201) || tables.ProjectileObj.DataList.Any(c => c.Id == 1201) || tables.UnitVisualObj.DataList.Any(c => c.Id == 1201)) {
            throw new InvalidOperationException("Roster count or retired gunship reference");
        }
        for (int id = 1; id <= 10; id++) {
            var entry = tables.CardObj.DataList.Single(c => c.ShowInLibrary && c.ArtId == id);
            var art = tables.CardArtObj.Get(entry.ArtId);
            if (art.Name != names[id - 1] || (int)entry.Quality != qualities[id - 1] || entry.BattleEnabled != (id == 1)) {
                throw new InvalidOperationException("Reference roster mapping " + id);
            }
            foreach (var file in new[] { art.Portrait, art.Formation, art.CardImage, art.RevealImage }) {
                if (!File.Exists(file)) { throw new FileNotFoundException(file); }
            }
        }
        foreach (var start in tables.BattleStartObj.DataList) {
            var camp = BattleRoster.CreateCamp(tables, start.SceneId, start.Camp);
            if (camp.cardIds.Any(id => id != 1001 && id != 1101)) { throw new InvalidOperationException("Unconfigured unit entered battle deck"); }
        }
    }
    private static string Hash() {
        using (var sha = System.Security.Cryptography.SHA256.Create()) {
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Main))).Replace("-", "").ToLowerInvariant();
        }
    }

}
