using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>导入本轮两张已审核圆环素材并构建资源的定向请求。</summary>
[InitializeOnLoad]
public static class BattleProductionSetup {
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

    static BattleProductionSetup() {
        EditorApplication.update += Poll;
    }

    private static void Poll() {
        const string path = "Library/battle-production-setup-request.json";
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        var result = new Result();
        try {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
            result.requestId = request?.requestId;
            File.Delete(path);
            if (request == null || string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
                throw new InvalidOperationException("Invalid request or editor PID.");
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("scene");
            foreach (string resource in new[] { BattleConst.ProductionBackgroundResource, BattleConst.ProductionFillResource }) {
                string asset = BattleSceneResources.SpritePath(resource);
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                if (importer == null) {
                    throw new InvalidOperationException("Production image missing: " + asset);
                }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.SaveAndReimport();
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(asset), group);
                entry.address = asset;
                entry.SetLabel("battle", true, true);
            }
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AddressablesAutoBuilder.BuildPlayerContent();
            result.status = "passed";
        } catch (Exception exception) {
            result.status = "failed";
            result.error = exception.ToString();
            Debug.LogException(exception);
        }
        File.WriteAllText("Library/battle-production-setup-result.json", JsonUtility.ToJson(result, true));
    }
}
