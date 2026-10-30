using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>场景1001/1002资源目录迁移及定向加载验证；仅显式PID请求触发。</summary>
[InitializeOnLoad]
public static class SceneResourceLayoutMigration
{
    private const string RequestPath = "Library/scene-resource-layout-request.json";
    private const string ResultPath = "Library/scene-resource-layout-result.json";
    private const string Key = "Starfront.SceneResourceLayoutMigration";
    private static bool busy;
    private static readonly string[] Names = { "ground", "ground_grid", "mountain", "water_single", "water_cluster_7", "owners", "material" };
    [Serializable] private class Request { public string requestId; public int processId; public string action; }
    [Serializable] private class Result { public string requestId; public string status; public string action; public string error; }

    static SceneResourceLayoutMigration()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlay;
    }

    private static string OldPath(string name)
    {
        if (name == "owners") { return "Assets/Art/material/scene/battle/ground1001Owners.png"; }
        if (name == "material") { return "Assets/Art/material/scene/battle/ground1001.mat"; }
        return "Assets/Art/unpack/scene/battle/ground/" + name + ".png";
    }

    private static string NewPath(int id, string name)
    {
        if (name == "owners") { return "Assets/Art/material/scene/battle/" + id + "/owners.png"; }
        if (name == "material") { return "Assets/Art/material/scene/battle/" + id + "/ground.mat"; }
        return "Assets/Art/unpack/scene/battle/" + id + "/" + name + ".png";
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) { return; }
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void Register(string path, int id)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("scene");
        if (group == null) { throw new InvalidOperationException("Missing scene group"); }
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
        entry.address = path;
        entry.SetLabel("battle", false);
        entry.SetLabel("scene" + id, true, true);
        EditorUtility.SetDirty(group);
        EditorUtility.SetDirty(settings);
    }

    private static void Migrate()
    {
        foreach (string name in Names)
        {
            if (AssetDatabase.LoadMainAssetAtPath(OldPath(name)) == null || File.Exists(NewPath(1001, name)) || File.Exists(NewPath(1002, name)))
            { throw new InvalidOperationException("Migration preflight: " + name); }
        }
        var moved = new List<string>();
        var copied = new List<string>();
        try
        {
            foreach (string name in Names)
            {
                string target = NewPath(1001, name);
                Folder(Path.GetDirectoryName(target).Replace('\\', '/'));
                string guid = AssetDatabase.AssetPathToGUID(OldPath(name));
                string error = AssetDatabase.MoveAsset(OldPath(name), target);
                if (!string.IsNullOrEmpty(error)) { throw new InvalidOperationException(error); }
                moved.Add(name);
                if (AssetDatabase.AssetPathToGUID(target) != guid) { throw new InvalidOperationException("GUID changed"); }
                Register(target, 1001);
            }
            foreach (string name in Names)
            {
                string target = NewPath(1002, name);
                Folder(Path.GetDirectoryName(target).Replace('\\', '/'));
                if (!AssetDatabase.CopyAsset(NewPath(1001, name), target)) { throw new InvalidOperationException("Copy failed: " + name); }
                copied.Add(name);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                Register(target, 1002);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(NewPath(1002, "material"));
            material.SetTexture("_SceneOwners", AssetDatabase.LoadAssetAtPath<Texture2D>(NewPath(1002, "owners")));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            VerifyAssets();
        }
        catch
        {
            foreach (string name in copied.AsEnumerable().Reverse())
            {
                AddressableAssetSettingsDefaultObject.Settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(NewPath(1002, name)));
                AssetDatabase.DeleteAsset(NewPath(1002, name));
            }
            foreach (string name in moved.AsEnumerable().Reverse())
            {
                AssetDatabase.MoveAsset(NewPath(1001, name), OldPath(name));
                var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(OldPath(name)));
                entry.address = OldPath(name);
                entry.SetLabel("scene1001", false);
                entry.SetLabel("battle", true, true);
            }
            AssetDatabase.SaveAssets();
            throw;
        }
    }

    private static void VerifyAssets()
    {
        foreach (int id in new[] { 1001, 1002 })
        {
            foreach (string name in Names)
            {
                string path = NewPath(id, name);
                string guid = AssetDatabase.AssetPathToGUID(path);
                var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid);
                if (entry == null || entry.address != path || !entry.labels.Contains("scene" + id)) { throw new InvalidOperationException("Address mismatch: " + path); }
                if (AssetDatabase.AssetPathToGUID(NewPath(1001, name)) == AssetDatabase.AssetPathToGUID(NewPath(1002, name))) { throw new InvalidOperationException("Scene assets share GUID"); }
                if (name != "material" && name != "owners")
                {
                    var a = AssetDatabase.LoadAssetAtPath<Sprite>(NewPath(1001, name));
                    var b = AssetDatabase.LoadAssetAtPath<Sprite>(NewPath(1002, name));
                    if (a == null || b == null || a.pixelsPerUnit != b.pixelsPerUnit || a.border != b.border || (name == "mountain" ? (a.rect.size != new Vector2(112,108) || Vector2.Distance(a.pivot,new Vector2(56,49)) > .001f || b.rect.size != new Vector2(130,130) || Vector2.Distance(b.pivot,new Vector2(67.68f,73.12f)) > .01f) : (a.rect != b.rect || a.pivot != b.pivot))) { throw new InvalidOperationException("Sprite settings differ: " + name); }
                }
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(NewPath(id, "material"));
            if (AssetDatabase.GetAssetPath(material.GetTexture("_SceneOwners")) != NewPath(id, "owners")) { throw new InvalidOperationException("Material references other scene ownership mask"); }
        }
    }

    private static void Poll()
    {
        if (busy || !File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        var result = new Result { status = "passed" };
        try
        {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
            File.Delete(RequestPath);
            result.requestId = request.requestId;
            result.action = request.action;
            if (string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) { throw new InvalidOperationException("Request identity mismatch"); }
            if (request.action == "migrate") { Migrate(); }
            else if (request.action == "verify") { VerifyAssets(); }
            else if (request.action == "build") { VerifyAssets(); AddressablesAutoBuilder.BuildPlayerContent(); }
            else if (request.action == "runtime")
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/GameEntrance.unity") { throw new InvalidOperationException("Runtime check requires idle GameEntrance"); }
                SessionState.SetString(Key, request.requestId);
                EditorApplication.isPlaying = true;
                return;
            }
            else { throw new InvalidOperationException("Unknown action"); }
        }
        catch (Exception e) { result.status = "failed"; result.error = e.ToString(); Debug.LogException(e); }
        File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
    }

    private static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && !string.IsNullOrEmpty(SessionState.GetString(Key, ""))) { RunRuntime(); }
    }

    private static async void RunRuntime()
    {
        busy = true;
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        var result = new Result { requestId = SessionState.GetString(Key, ""), action = "runtime", status = "passed" };
        try
        {
            double deadline = EditorApplication.timeSinceStartup + 120;
            while (true)
            {
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                if (login != null && login.isOpened && login.txt.text == "点击屏幕或按任意键开始")
                {
                    typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { KeyCode.Return });
                    break;
                }
                if (EditorApplication.timeSinceStartup > deadline) { throw new TimeoutException("Login"); }
                await Task.Delay(100);
            }
            while (PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL) == null || !PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL).isOpened)
            {
                if (EditorApplication.timeSinceStartup > deadline) { throw new TimeoutException("Main"); }
                await Task.Delay(100);
            }
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourceConst.GetUIPath(MainUIEnum.BATTLE_SCENE_PANEL), ResType.Prefab), new ResLoadInfo(ResourceConst.GetUIPath(MainUIEnum.BATTLE_INFO_PANEL), ResType.Prefab) });
            foreach (int id in new[] { 1001, 1002, 1001, 1002 })
            {
                await BattleControl.ins.PrepareAsync(id);
                var model = BattleControl.ins.model;
                if (model.cells.Count != 171 || model.buildings.Count != 2 || model.config.GroundResource != "scene/battle/" + id + "/ground") { throw new InvalidOperationException("Runtime config or initialization mismatch: " + id); }
                PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL)?.Close();
                PanelMgr.ins.OpenPanel(MainUIEnum.BATTLE_SCENE_PANEL);
                await Task.Delay(300);
                var panel = PanelMgr.ins.GetPanel(MainUIEnum.BATTLE_SCENE_PANEL) as BattleScenePanel;
                if (panel == null || !panel.isOpened) { throw new InvalidOperationException("Scene panel not open"); }
                BattleGroundValidation.Validate(panel.GetComponent<BattleSceneView>(), model);
                GMControl.ins.OpenPanel();
                await Task.Delay(200);
                var gm = (GmPanel)PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL);
                var text = gm.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Single(t => t.name == "txtDiagnostics");
                if (!text.text.Contains("Scene: " + id) || !text.text.Contains("editor (local)")) {
                    throw new InvalidOperationException("GM actual scene/editor diagnostic mismatch");
                }
                string originalVersion = LoadTiming.version;
                try {
                    LoadTiming.version = "base-test/content-test";
                    if (!GMControl.ins.GetDiagnosticsText().Contains("Base: base-test\nContent: content-test")) {
                        throw new InvalidOperationException("GM active version parsing mismatch");
                    }
                } finally {
                    LoadTiming.version = originalVersion;
                }
                text.ForceMeshUpdate();
                if (text.isTextOverflowing || text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c => c.character != '\n' && c.character != ' ' && c.textElement == null)) {
                    ScreenCapture.CaptureScreenshot("Library/gm-diagnostics-failure.png");
                    await Task.Delay(300);
                    throw new InvalidOperationException("GM diagnostic text: overflow=" + text.isTextOverflowing + ", preferred=" + text.preferredWidth + "x" + text.preferredHeight + ", rect=" + text.rectTransform.rect + ", nullGlyphs=" + string.Join(",", text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.textElement == null).Select(c => ((int)c.character).ToString())));
                }
                var listRect = (RectTransform)gm.listItem.transform;
                Vector2 originalPosition = listRect.anchoredPosition;
                gm.Close();
                GMControl.ins.OpenPanel();
                await Task.Delay(100);
                if (listRect.anchoredPosition != originalPosition || gm.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Count(t => t.name == "txtDiagnostics") != 1) {
                    throw new InvalidOperationException("GM diagnostic reopen drift/duplicate");
                }
                if (id == 1002) {
                    ScreenCapture.CaptureScreenshot("Library/gm-diagnostics-review.png");
                    await Task.Delay(200);
                }
                gm.Close();
                panel.Close();
                if (BattleControl.ins.model != null) { throw new InvalidOperationException("Scene not released"); }
                if (GMControl.ins.SceneIdProvider != null || !GMControl.ins.GetDiagnosticsText().EndsWith("Scene: none")) {
                    throw new InvalidOperationException("GM stale scene provider after exit");
                }
                PanelMgr.ins.OpenPanel(UIEnum.MAIN_PANEL);
                await Task.Delay(100);
            }
        }
        catch (Exception e) { result.status = "failed"; result.error = e.ToString(); Debug.LogException(e); }
        finally
        {
            Application.runInBackground = background;
            SessionState.EraseString(Key);
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
            busy = false;
            EditorApplication.isPlaying = false;
        }
    }
}
