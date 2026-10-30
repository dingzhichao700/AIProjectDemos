using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using cfg;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>攻击资源的定向导入与隔离表现验证；不修改玩法数据。</summary>
[InitializeOnLoad]
public static class BattleAttackPresentationValidation {
    const string Root = "Assets/Art/frameAnimation/scene/Colonial/Infantry/";
    const string Key = "Starfront.AttackPresentationValidation";
    static readonly string[] Directions = { "Up", "UpRight", "Right", "DownRight", "Down" };
    [Serializable] class Request { public string requestId; public int processId; }
    [Serializable] class Result { public string requestId; public string status; public string details; }
    static BattleAttackPresentationValidation() { EditorApplication.update += Poll; }
    static void Poll() {
        const string requestPath = "Library/battle-attack-setup-request.json";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying) {
            if (SessionState.GetBool(Key, false) && Directions.All(d => FrameAnimationManager.GetRes(Root + "Attack1_" + d) != null)) {
                SessionState.SetBool(Key, false);
                var result = new Result { requestId = SessionState.GetString(Key + ".id", ""), status = "failed" };
                try { ValidateViews(); result.status = "passed"; result.details = "8 directions x 10 frames; full-canvas pivot; offscreen recovery/pool reuse; last-frame hold; movement interruption; event restart; no damage from view"; }
                catch (Exception e) { result.details = e.ToString(); Debug.LogException(e); }
                File.WriteAllText("Library/battle-attack-runtime-result.json", JsonUtility.ToJson(result, true));
            }
            return;
        }
        if (!File.Exists(requestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var req = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath)); File.Delete(requestPath);
        var report = new Result { requestId = req?.requestId, status = "failed" };
        try {
            Require(req != null && !string.IsNullOrEmpty(req.requestId) && req.processId == System.Diagnostics.Process.GetCurrentProcess().Id, "request ownership");
            var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("scene"); Require(group != null, "scene group");
            foreach (string direction in Directions) {
                string path = Root + "Attack1_" + direction;
                AssetDatabase.ImportAsset(path + ".png", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(path + ".json", ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path + ".png");
                Require(!importer.isReadable && !importer.mipmapEnabled, "texture settings");
                foreach (string ext in new[] { ".png", ".json" }) {
                    var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path + ext), group); entry.address = path + ext; entry.SetLabel("Colonial", true, true);
                }
            }
            EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            AddressablesAutoBuilder.BuildPlayerContent();
            SessionState.SetBool(Key, true); SessionState.SetString(Key + ".id", req.requestId);
            report.status = "passed"; report.details = "10 assets imported and registered in scene; Addressables build finished; runtime checks armed for next battle load";
        } catch (Exception e) { report.details = e.ToString(); Debug.LogException(e); }
        File.WriteAllText("Library/battle-attack-setup-result.json", JsonUtility.ToJson(report, true));
    }
    static void Require(bool value, string text) { if (!value) throw new InvalidOperationException("Attack presentation: " + text); }
    static void ValidateViews() {
        BattleUnitActionValidation.Validate();
    }
}
