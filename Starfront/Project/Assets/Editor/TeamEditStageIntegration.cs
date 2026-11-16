#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>底座显示资源的常驻增量接入；仅处理带PID及目标哈希的显式请求。</summary>
[InitializeOnLoad]
public static class TeamEditStageIntegration {
    const string Pending = "Library/AIUI/pending-team-edit-stages.json";
    const string Target = "Assets/Prefab/default/mainView/TeamEditPanel.prefab";
    [Serializable] class Request { public string requestId; public int processId; public string expectedHash; }
    [Serializable] class Result { public string requestId; public string status; public string error; }

    static TeamEditStageIntegration() {
        EditorApplication.update += Poll;
    }

    static void Poll() {
        if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        Request request = null;
        GameObject root = null;
        var result = new Result();
        try {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending));
            result.requestId = request.requestId;
            if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId)) {
                throw new InvalidOperationException("底座接入请求身份不匹配");
            }
            using (var sha = System.Security.Cryptography.SHA256.Create()) {
                string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Target))).Replace("-", "").ToLowerInvariant();
                if (hash != request.expectedHash) {
                    throw new InvalidOperationException("目标预制体已发生其他修改，未覆盖");
                }
            }
            root = PrefabUtility.LoadPrefabContents(Target);
            var palette = root.GetComponent<TeamEditStagePalette>() ?? root.AddComponent<TeamEditStagePalette>();
            var atlasSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/atlas/default/mainView.png").OfType<Sprite>().ToDictionary(s => s.name);
            palette.normal = atlasSprites["team-formation-stage"];
            palette.rare = atlasSprites["deck-stage-blue"];
            palette.epic = atlasSprites["deck-stage-purple"];
            palette.legend = atlasSprites["deck-stage-orange"];
            palette.emptyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/material/default/sharedRender/DeckLockedFrame.mat");
            if (palette.normal == null || palette.rare == null || palette.epic == null || palette.legend == null || palette.emptyMaterial == null) {
                throw new InvalidOperationException("底座品质或灰度资源缺失");
            }
            PrefabUtility.SaveAsPrefabAsset(root, Target);
            AssetDatabase.SaveAssets();
            result.status = "passed";
        } catch (Exception error) {
            result.status = "failed";
            result.error = error.ToString();
        } finally {
            if (root != null) {
                PrefabUtility.UnloadPrefabContents(root);
            }
            File.WriteAllText("Library/AIUI/team-edit-stages-result.json", JsonUtility.ToJson(result, true));
            File.Move(Pending, Pending + ".processed-" + DateTime.UtcNow.Ticks);
        }
    }
}
#endif
