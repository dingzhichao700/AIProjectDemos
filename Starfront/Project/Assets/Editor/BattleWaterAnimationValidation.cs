using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using cfg;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>单格水池接入与两类水池定向运行验证；请求驱动，不构建内容。</summary>
[InitializeOnLoad]
public static class BattleWaterAnimationValidation {
    const string Key = "Starfront.WaterValidation";
    const string Asset = "Assets/Art/frameAnimation/scene/battle/1002/water_single";
    const string RequestPath = "Library/water-animation-request.json";
    [Serializable] class Request { public string requestId; public int processId; }
    [Serializable] class Result { public string requestId; public string status; public string error; public int frames; public int duration; }
    static BattleWaterAnimationValidation() {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Key, "") != "") {
                Run();
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Key + ".builder", -1) >= 0) {
                AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".builder", 0);
                SessionState.EraseInt(Key + ".builder");
            }
        };
    }
    static void Check(bool value, string message) {
        if (!value) { throw new InvalidOperationException("Water: " + message); }
    }
    static void Poll() {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) { return; }
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
        File.Delete(RequestPath);
        try {
            Check(request.processId == System.Diagnostics.Process.GetCurrentProcess().Id && !string.IsNullOrEmpty(request.requestId), "request identity");
            Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == "Assets/Scenes/GameEntrance.unity", "GameEntrance required");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("scene");
            Check(group != null, "scene group");
            AssetDatabase.ImportAsset(Asset + ".png", ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Asset + ".png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            foreach (var ext in new[] { ".png", ".json" }) {
                AssetDatabase.ImportAsset(Asset + ext, ImportAssetOptions.ForceSynchronousImport);
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(Asset + ext), group);
                entry.address = Asset + ext;
                entry.SetLabel("scene1002", true, true);
            }
            AssetDatabase.SaveAssets();
            SessionState.SetInt(Key + ".builder", settings.ActivePlayModeDataBuilderIndex);
            int fast = settings.DataBuilders.FindIndex(b => b != null && b.GetType().Name == "BuildScriptFastMode");
            Check(fast >= 0, "asset database play builder");
            settings.ActivePlayModeDataBuilderIndex = fast;
            SessionState.SetString(Key, request.requestId);
            EditorApplication.isPlaying = true;
        } catch (Exception e) {
            File.WriteAllText("Library/water-animation-result.json", JsonUtility.ToJson(new Result { requestId = request.requestId, status = "failed", error = e.ToString() }, true));
        }
    }
    static async Task Until(Func<bool> ready) {
        double end = EditorApplication.timeSinceStartup + 90;
        while (!ready()) {
            Check(EditorApplication.timeSinceStartup < end, "timeout");
            EditorApplication.QueuePlayerLoopUpdate();
            await Task.Delay(100);
        }
    }
    static async void Run() {
        string id = SessionState.GetString(Key, "");
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        string error = null;
        PlayerProgressControl previous = null;
        bool isolated = false;
        try {
            await Until(() => UnityEngine.Object.FindObjectOfType<LoginPanel>() != null && UnityEngine.Object.FindObjectOfType<LoginPanel>().isOpened && UnityEngine.Object.FindObjectOfType<LoginPanel>().txt.text == "点击屏幕或按任意键开始");
            var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
            typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { KeyCode.Return });
            await Until(() => PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL) != null && PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL).isOpened);
            await ResourceLoader.LoadListAsync(new System.Collections.Generic.List<ResLoadInfo> {
                new ResLoadInfo(ResourceConst.GetUIPath(MainUIEnum.BATTLE_SCENE_PANEL), ResType.Prefab),
                new ResLoadInfo(ResourceConst.GetUIPath(MainUIEnum.BATTLE_INFO_PANEL), ResType.Prefab)
            });
            previous = PlayerProgressControl.InstallValidationSession(new PlayerProgressControl(CfgManager.tables, null, _ => { }));
            isolated = true;
            for (int run = 0; run < 2; run++) {
                await BattleControl.ins.PrepareAsync(1002);
                var model = BattleControl.ins.model;
                model.StopTimeFlow();
                Check(model.config.Appearances.Count(a => a.ResourceType == BattleSceneResourceType.FRAME_ANIMATION) == 5, "five animated ponds");
                foreach (var path in BattleSceneResources.Collect(model)) {
                    Check(ResourceManager.HasLoadedUnpackImage(path), "sprite loaded: " + path);
                }
                foreach (var path in BattleSceneResources.CollectAnimations(model)) {
                    var loaded = FrameAnimationManager.GetRes(path);
                    Check(loaded != null && loaded.sprites.Length > 0, "animation loaded: " + path);
                }
                var animation = FrameAnimationManager.GetRes(Asset);
                Check(animation != null && animation.sprites.Length == 48 && animation.totalDuration == 6000, "animation ready");
                Check(animation.pivot == new Vector2(.5f, .5f), "pivot");
                var cluster = FrameAnimationManager.GetRes("Assets/Art/frameAnimation/scene/battle/1002/water_cluster_7");
                Check(cluster != null && cluster.sprites.Length == 48, "seven-cell animation retained");
                PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL)?.Close();
                PanelMgr.ins.OpenPanel(MainUIEnum.BATTLE_SCENE_PANEL);
                await Task.Delay(300);
                model.StopTimeFlow();
                var panel = PanelMgr.ins.GetPanel(MainUIEnum.BATTLE_SCENE_PANEL);
                var view = panel.GetComponent<BattleSceneView>();
                var presentation = (BattleScenePresentation)typeof(BattleSceneView).GetField("presentation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                float clock = RookieEngine.sceneTimer.curTime;
                try {
                    for (int frame = 0; frame < 48; frame++) {
                        RookieEngine.sceneTimer.curTime = frame * 125 + 1;
                        presentation.Refresh(new Rect(0, 0, 2000, 2000));
                        var images = view.layers.SelectMany(l => l.GetComponentsInChildren<Image>()).Where(i => i.name.StartsWith("visual_") && i.sprite == animation.sprites[frame]).ToArray();
                        Check(images.Length == 3, "frame " + frame + " displays three single ponds");
                        Check(view.layers.SelectMany(l => l.GetComponentsInChildren<Image>()).Count(i => i.name.StartsWith("visual_") && i.sprite == cluster.sprites[frame]) == 2, "both seven-cell ponds still animate");
                        foreach (var image in images) {
                            var pivot = Vector2.Scale(animation.sourceSizes[frame], animation.pivot) - animation.frameOffsets[frame];
                            Check(Vector2.Distance(image.rectTransform.pivot * image.rectTransform.sizeDelta, pivot) < .001f, "trim pivot");
                        }
                    }
                    presentation.Refresh(new Rect(10000, 10000, 10, 10));
                    Check(presentation.visibleCount == 0, "offscreen recycle");
                    RookieEngine.sceneTimer.curTime = 3001;
                    presentation.Refresh(new Rect(0, 0, 2000, 2000));
                    Check(presentation.visibleCount == presentation.totalCount, "pool restore");
                    if (run == 0) {
                        Directory.CreateDirectory("Library/water-single-animation-capture");
                        for (int frame = 0; frame < 48; frame++) {
                            RookieEngine.sceneTimer.curTime = frame * 125 + 1;
                            EditorApplication.QueuePlayerLoopUpdate();
                            await Task.Delay(90);
                            ScreenCapture.CaptureScreenshot("Library/water-single-animation-capture/" + frame.ToString("D3") + ".png");
                            await Task.Delay(90);
                        }
                    }
                } finally {
                    RookieEngine.sceneTimer.curTime = clock;
                }
                panel.Close();
                Check(BattleControl.ins.model == null, "close releases scene");
                PanelMgr.ins.OpenPanel(UIEnum.MAIN_PANEL);
                await Task.Delay(200);
            }
        } catch (Exception e) {
            error = e.ToString();
            Debug.LogException(e);
        } finally {
            if (isolated) PlayerProgressControl.InstallValidationSession(previous);
            Application.runInBackground = background;
            SessionState.EraseString(Key);
            File.WriteAllText("Library/water-animation-result.json", JsonUtility.ToJson(new Result { requestId = id, status = error == null ? "passed" : "failed", error = error, frames = 48, duration = 6000 }, true));
            EditorApplication.isPlaying = false;
        }
    }
}