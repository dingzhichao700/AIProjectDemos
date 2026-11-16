#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using cfg;
using SimpleJSON;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>新版三种族牌库的查询与实际筛选入口回归，不写玩家存档。</summary>
[InitializeOnLoad]
internal static class FormationRosterValidation {
    private const string Queue = "Library/AIUI/";
    [Serializable] private class Request { public string requestId; public int processId; }
    [Serializable] private class Result { public string requestId, status, error; public int visibleCards; public int[] professionCounts; }
    static FormationRosterValidation() { EditorApplication.update += Poll; }
    private static void Require(bool value, string label) {
        if (!value) { throw new InvalidOperationException(label); }
    }
    private static void Poll() {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { return; }
        var file = Queue + "pending-formation-roster.json";
        if (!File.Exists(file)) { return; }
        Request request = null;
        var result = new Result();
        try {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(file));
            Require(request.processId == System.Diagnostics.Process.GetCurrentProcess().Id, "Editor PID mismatch");
            if (EditorApplication.isPlaying) {
                var view = UnityEngine.Object.FindObjectOfType<DeckPresentationView>();
                if (view == null || view.previewCards == null || view.previewCards.Length != 16) { return; }
                for (int i = 1; i <= 4; i++) {
                    view.filters[i].onClick.Invoke();
                    Require(view.library.arraySource.Count == 4, "Actual profession button " + i);
                    foreach (var item in view.library.arraySource) {
                        var card = (DeckCardView.DisplayData)item;
                        Require(TeamEditControl.ins.Query.Profession(card.cardId) == i, "Wrong filtered profession");
                        Require(card.cellUnlockCost > 0, "Display cost not assigned");
                    }
                }
                view.filters[0].onClick.Invoke();
                Require(view.library.arraySource.Count == 16, "All button restore");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Queue + request.requestId + ".png"));
            } else {
                PrepareIcons();
                var tables = new Tables(name => JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json")));
                int writes = 0;
                var player = new PlayerProgressControl(tables, null, ignored => writes++);
                int before = writes;
                var query = new TeamEditQuery(tables, player.Cards.model);
                Require(query.Total == 16 && !query.HasMissingProfession, "16 complete professions");
                result.professionCounts = Enumerable.Range(1, 4).Select(i => query.List(i, 0, false).Length).ToArray();
                Require(result.professionCounts.All(n => n == 4), "Each profession has four cards");
                foreach (var id in query.List(0, 0, false)) {
                    Require(query.ConstructionCost(id) > 0 && query.Cost(id) != "—", "Configured cost");
                    Require(player.Cards.model.GetState(id).level == 0, "No ownership transfer");
                    Require(!query.Card(id).BattleEnabled, "Incomplete combat stays disabled");
                    Require(tables.CardLevelObj.DataList.Count(x => x.CardId == id) == 10, "Full initial growth schedule");
                }
                Require(tables.CardObj.Get(1101) != null && !tables.CardObj.Get(1101).ShowInLibrary, "Legacy identity retained");
                Require(writes == before, "Read-only queries do not save");
            }
            result.visibleCards = 16;
            result.status = "passed";
        } catch (Exception error) {
            result.status = "failed"; result.error = error.ToString();
        }
        result.requestId = request == null ? "unreadable" : request.requestId;
        File.Delete(file);
        File.WriteAllText(Queue + "formation-roster-result.json", JsonUtility.ToJson(result, true));
    }

    private static void PrepareIcons() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var source = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID("Assets/Art/unpack/default/card/tags/01.png"));
        Require(source != null, "Existing icon group missing");
        for (int id = 101; id <= 104; id++) {
            string path = "Assets/Art/unpack/default/card/professions/" + id + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Require(importer != null, "Icon was not imported");
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), source.parentGroup);
            entry.address = path;
        }
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}
#endif
