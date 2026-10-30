using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 已确认对战HUD的定向Prefab设置和构建入口。
/// </summary>
/// <remarks>请求仅交给指定的空闲编辑器，不替代通用导出队列。</remarks>
[InitializeOnLoad]
public static class BattleInfoSetup {

    private const string RequestPath = "Library/battle-info-setup-request.json";

    private const string ResultPath = "Library/battle-info-setup-result.json";

    [Serializable]
    private class Request {
        public string requestId;
        public int processId;
        public string action;
    }

    [Serializable]
    private class Result {
        public string requestId;
        public string status;
        public string error;
    }

    static BattleInfoSetup() {
        EditorApplication.update += Poll;
    }

    private static void Poll() {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        var result = new Result();
        try {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
            result.requestId = request?.requestId;
            File.Delete(RequestPath);
            if (request == null || string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
                throw new InvalidOperationException("Invalid request identity or editor PID.");
            }
            if (request.action == "configureApproved") {
                ConfigureApproved();
            } else if (request.action == "build") {
                AddressablesAutoBuilder.BuildPlayerContent();
            } else {
                throw new InvalidOperationException("Unknown action.");
            }
            result.status = "passed";
        } catch (Exception exception) {
            result.status = "failed";
            result.error = exception.ToString();
            Debug.LogException(exception);
        }
        File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
    }

    /**补齐本地批准结构的锚点与字体，不重新生成UIBinder成员。*/
    private static void ConfigureApproved() {
        string path = ResourceConst.GetUIPath(MainUIEnum.BATTLE_INFO_PANEL);
        var root = PrefabUtility.LoadPrefabContents(path);
        try {
            if (root.GetComponent<Image>() != null) {
                throw new InvalidOperationException("HUD root must remain transparent.");
            }
            var view = root.GetComponent<BattleInfoView>() ?? root.AddComponent<BattleInfoView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("territoryFullLength").floatValue = 668;
            serialized.FindProperty("territoryInsetX").floatValue = 6;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true)) {
                text.fontStyle = FontStyles.Normal;
                text.enableWordWrapping = false;
                text.raycastTarget = false;
                if (text.name == "txtEnemyTerritory" || text.name == "txtEnemyName") {
                    text.alignment = TextAlignmentOptions.MidlineRight;
                } else if (text.name == "txtFriendlyTerritory" || text.name == "txtFriendlyName") {
                    text.alignment = TextAlignmentOptions.MidlineLeft;
                } else {
                    text.alignment = TextAlignmentOptions.Midline;
                }
                foreach (char character in text.text) {
                    if (!text.font.HasCharacter(character, true, true)) {
                        throw new InvalidOperationException("Missing HUD glyph: " + character);
                    }
                }
            }
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) {
                graphic.raycastTarget = false;
            }
            ConfigureRect(root, "barCurrency", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(201, 69), new Vector2(.5f, 29));
            ConfigureRect(root, "barCountdown", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(153, 53), new Vector2(.5f, -78));
            ConfigureRect(root, "boxEnemy", Vector2.one, Vector2.one, new Vector2(259, 115), new Vector2(-19, -66));
            var track = root.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "barTerritory");
            track.anchorMin = new Vector2(0, 1);
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(.5f, 1);
            track.sizeDelta = new Vector2(-39, 37);
            track.anchoredPosition = new Vector2(.5f, -20);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /**坐标来自已批准720×1280源稿，保证改变根容器时仍靠对应边缘。*/
    private static void ConfigureRect(GameObject root, string name, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position) {
        var rect = root.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == name);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

}
