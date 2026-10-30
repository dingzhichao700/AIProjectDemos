using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>
/// 解锁功能已有资源与字体引用的定向安装请求。
/// </summary>
/// <remarks>不改动通用AIUI队列，不重建已确认场景骨架。</remarks>
[InitializeOnLoad]
public static class BattleUnlockSetup {

    [Serializable]
    private class Request {
        public string requestId;
        public int processId;
    }

    [Serializable]
    private class Result {
        public string requestId;
        public string status;
        public string error;
    }

    static BattleUnlockSetup() {
        EditorApplication.update += Poll;
    }

    /**编辑器未运行时使用本轮自有实例，真实入口回归负责完成后正常退出。*/
    public static void RunBatch() {
        try {
            Setup();
            BattleSceneRuntimeValidation.RunBatch();
        } catch (Exception exception) {
            Debug.LogException(exception);
            File.WriteAllText("Library/battle-unlock-batch-result.txt", "failed: " + exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Poll() {
        const string path = "Library/battle-unlock-setup-request.json";
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        var result = new Result();
        try {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
            result.requestId = request?.requestId;
            File.Delete(path);
            if (request == null || string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
                throw new InvalidOperationException("Wrong editor identity.");
            }
            Setup();
            result.status = "passed";
        } catch (Exception error) {
            result.status = "failed";
            result.error = error.ToString();
            Debug.LogException(error);
        }
        File.WriteAllText("Library/battle-unlock-setup-result.json", JsonUtility.ToJson(result, true));
    }

    private static void Setup() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("scene");
        foreach (string name in new[] { "UI_unlock-glow", "UI_helmet", "UI_random", "UI_mineral" }) {
            string path = "Assets/Art/unpack/scene/battle/effects/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
            entry.address = path;
            entry.SetLabel("battle", true, true);
        }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/font/tmpFontAsset/common/fontDynamicCommon.asset");
        foreach (char value in "货币不足只能解锁己方格子该不能请先相邻对战已结束尚未就绪不存在0123456789") {
            if (!font.HasCharacter(value, true, true)) {
                throw new InvalidOperationException("Missing interaction glyph: " + value);
            }
        }
        string prefabPath = ResourceConst.GetUIPath(MainUIEnum.BATTLE_SCENE_PANEL);
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try {
            root.GetComponent<BattleSceneView>().hintFont = font;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(group);
        AssetDatabase.SaveAssets();
        AddressablesPrefabDependencyChecker.ValidateOrThrow();
    }

}
