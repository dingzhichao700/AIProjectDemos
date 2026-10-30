using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>整图地表的定向资源导入入口；仅空闲编辑器处理带PID和请求ID的请求。</summary>
[InitializeOnLoad]
public static class BattleGroundSetup {
    [Serializable] private class Request {
        public string requestId;
        public int processId;
        public string action;
    }
    [Serializable] private class Result {
        public string requestId;
        public string status;
        public string error;
    }
    static BattleGroundSetup() { EditorApplication.update += Poll; }
    private static void Poll() {
        const string path = "Library/battle-ground-request.json";
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        var result = new Result { status = "passed" };
        try {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
            File.Delete(path);
            result.requestId = request?.requestId;
            if (request == null || string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) { throw new InvalidOperationException("请求身份或PID无效。"); }
            if (request.action == "install") { Install(); }
            else if (request.action == "build") { AddressablesAutoBuilder.BuildPlayerContent(); }
            else { throw new InvalidOperationException("未知操作。"); }
        } catch (Exception e) {
            result.status = "failed";
            result.error = e.ToString();
            Debug.LogException(e);
        }
        File.WriteAllText("Library/battle-ground-result.json", JsonUtility.ToJson(result, true));
    }

    private static void Install() {
        const string art = "Assets/Art/unpack/scene/battle/1001/";
        foreach (string name in new[] { "ground", "ground_grid", "mountain", "water_single", "water_cluster_7" }) {
            string path = art + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.alphaIsTransparency = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = name == "mountain" ? new Vector2(.5f, 1f - 59f / 108f) : new Vector2(.5f, .5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (name == "ground" && sprite.rect.size != new Vector2(2000, 2000)) { throw new InvalidOperationException("底图导入尺寸被改变。"); }
            if (name == "mountain" && Vector2.Distance(sprite.pivot, new Vector2(56, 49)) > .001f) { throw new InvalidOperationException("山体pivot未保留。"); }
            Register(path);
        }
        const string folder = "Assets/Art/material/scene/";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        const string ownersPath = folder + "battle/1001/owners.png";
        Directory.CreateDirectory(folder + "battle");
        AssetDatabase.Refresh();
        // Data texture lives with its material, outside the display-sprite importer rules.
        File.Copy("Library/battle-ground-owners.png", ownersPath, true);
        AssetDatabase.ImportAsset(ownersPath, ImportAssetOptions.ForceSynchronousImport);
        var dataImporter = (TextureImporter)AssetImporter.GetAtPath(ownersPath);
        dataImporter.textureType = TextureImporterType.Default;
        dataImporter.sRGBTexture = false;
        dataImporter.isReadable = false;
        dataImporter.mipmapEnabled = false;
        dataImporter.npotScale = TextureImporterNPOTScale.None;
        dataImporter.maxTextureSize = 2048;
        dataImporter.textureCompression = TextureImporterCompression.Uncompressed;
        dataImporter.filterMode = FilterMode.Point;
        dataImporter.wrapMode = TextureWrapMode.Clamp;
        dataImporter.alphaIsTransparency = false;
        dataImporter.SaveAndReimport();
        var owners = AssetDatabase.LoadAssetAtPath<Texture2D>(ownersPath);
        const string materialPath = folder + "battle/1001/ground.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/shader/BattleGround.shader");
        if (shader == null || !shader.isSupported) { throw new InvalidOperationException("地表shader缺失或不支持。"); }
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
        material.shader = shader;
        material.SetTexture("_SceneOwners", owners);
        EditorUtility.SetDirty(material);
        const string obsolete = folder + "battle/1001/owners.asset";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(obsolete) != null) {
            AddressableAssetSettingsDefaultObject.Settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(obsolete));
            AssetDatabase.DeleteAsset(obsolete);
        }
        Register(materialPath);
        Register(ownersPath);
        AssetDatabase.SaveAssets();
    }

    private static void Register(string path) {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("scene");
        if (group == null) { throw new InvalidOperationException("scene组缺失。"); }
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
        entry.address = path;
        entry.SetLabel("battle", false);
        entry.SetLabel("scene1001", true, true);
        EditorUtility.SetDirty(settings);
    }
}
