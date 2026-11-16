#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SimpleJSON;
using cfg;
using cfg.resource;

/// <summary>独立机甲动画Game预览；复用单位显示/阴影，不写业务表、玩家存档或伤害。</summary>
[InitializeOnLoad]
public static class MarauderGamePreviewValidation
{
    const string Key = "MarauderGamePreview.v1";
    const string AssetRoot = "Assets/Art/frameAnimation/scene/Colonial/MarauderPreview/";
    const string Output = "E:/Codex_Projects/异星军团/outputs/assault-mech-3d-animation-test-v1/game-preview-v1/";
    const string Pending = "Library/AIUI/pending-marauder-preview.json";
    [Serializable] class Request { public string requestId; public int processId; }
    [Serializable] class SceneBackup { public SceneSetup[] scenes; }
    [Serializable] class Result { public string requestId; public string status; public string error; public int animations; public int frames; public int units; public int checkedFrames; }
    static BattleSceneModel model;
    static BattleUnitView view;
    static RectTransform bodies;
    static RectTransform shadows;
    static readonly List<BattleUnitVO> units = new List<BattleUnitVO>();
    static readonly List<BattleSkillCastVO> casts = new List<BattleSkillCastVO>();
    static readonly string[] Names = { "Idle", "Move", "AttackLeft", "AttackRight" };
    static readonly string[] Directions = { "UpRight", "DownRight" };
    static bool ready;
    static double started;
    static Text actionText;
    static readonly HashSet<string> captures = new HashSet<string>();
    static string requestId;
    static int priorPhase = -1;
    static int checkedFrames;
    static MarauderGamePreviewValidation()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlay;
    }
    static void Check(bool value, string message)
    {
        if (!value) { throw new InvalidOperationException("Marauder preview: " + message); }
    }
    static void Set(object target, string property, object value)
    {
        target.GetType().GetProperty(property).SetValue(target, value);
    }
    static void Call(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
    static void Poll()
    {
        if (EditorApplication.isPlaying && !string.IsNullOrEmpty(SessionState.GetString(Key, ""))) { EditorApplication.QueuePlayerLoopUpdate(); }
        if (EditorApplication.isPlaying && ready)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            try { Tick(); } catch (Exception e) { ready = false; Finish("failed", e.ToString()); Debug.LogException(e); }
        }
        if (!File.Exists(Pending) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) { return; }
        Request request = null;
        try
        {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
            Check(request.processId == System.Diagnostics.Process.GetCurrentProcess().Id && !string.IsNullOrEmpty(request.requestId), "request identity");
            File.Delete(Pending);
            for (int i = 0; i < SceneManager.sceneCount; i++) { Check(!SceneManager.GetSceneAt(i).isDirty, "unsaved scene"); }
            foreach (string name in Names)
            {
                foreach (string direction in Directions)
                {
                    string path = AssetRoot + name + "_" + direction;
                    AssetDatabase.ImportAsset(path + ".png", ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path + ".png");
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.maxTextureSize = 2048;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                    var settings = AddressableAssetSettingsDefaultObject.Settings;
                    foreach (string ext in new[] { ".png", ".json" })
                    {
                        AssetDatabase.ImportAsset(path + ext, ImportAssetOptions.ForceSynchronousImport);
                        settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path + ext), settings.FindGroup("scene")).SetAddress(path + ext);
                    }
                }
            }
            AssetDatabase.SaveAssets();
            SessionState.SetString(Key + ".scenes", JsonUtility.ToJson(new SceneBackup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetString(Key, request.requestId);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameType).Show();
            EditorApplication.isPlaying = true;
        }
        catch (Exception e)
        {
            requestId = request == null ? "unreadable" : request.requestId;
            Finish("failed", e.ToString());
            Debug.LogException(e);
        }
    }
    static async void OnPlay(PlayModeStateChange state)
    {
        if (string.IsNullOrEmpty(SessionState.GetString(Key, ""))) { return; }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            ready = false;
            var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + ".scenes", "{}"));
            SessionState.EraseString(Key);
            if (backup != null && backup.scenes != null && backup.scenes.Length > 0) { EditorSceneManager.RestoreSceneManagerSetup(backup.scenes); }
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode) { return; }
        requestId = SessionState.GetString(Key, "");
        try { await StartPreview(); } catch (Exception e) { Finish("failed", e.ToString()); Debug.LogException(e); }
    }
    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }
    static Text Label(string text, Transform parent, Vector2 size, Vector2 position, int fontSize)
    {
        var label = Rect("PreviewLabel", parent, size, position).gameObject.AddComponent<Text>();
        label.font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 24);
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }
    static async Task StartPreview()
    {
        Directory.CreateDirectory(Output);
        Application.runInBackground = true;
        new GameObject("MarauderPreviewDispatcher", typeof(UnityMainThreadDispatcher));
        var tables = new Tables(n => JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + n + ".json")));
        typeof(CfgManager).GetField("_tables", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, tables);
        typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        foreach (string field in new[] { "_timer", "_sceneTimer", "_playerTimer", "_enemyTimer" })
        {
            typeof(RookieEngine).GetField(field, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, new Timer());
        }
        foreach (string name in Names)
        {
            foreach (string direction in Directions)
            {
                string path = AssetRoot + name + "_" + direction;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path + ".png");
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path + ".json");
                var data = await FrameAnimationManager.GenerateAsync(JSON.Parse(text.text), sprite, path);
                Check(data.pivot == new Vector2(.5f, .25f), "pivot");
                Check(data.sprites.Length == (name == "Idle" ? 95 : name == "Move" ? 26 : 65), "frame count");
                FrameAnimationManager.loadedMap[path] = data;
            }
        }
        var canvas = new GameObject("MarauderPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);
        scaler.matchWidthOrHeight = .5f;
        var background = Rect("Background", canvas.transform, new Vector2(720, 1280), Vector2.zero).gameObject.AddComponent<Image>();
        background.color = new Color(.08f, .1f, .14f, 1);
        Label("机甲动作预览", canvas.transform, new Vector2(680, 50), new Vector2(22, -30), 28);
        actionText = Label("Loading...", canvas.transform, new Vector2(680, 70), new Vector2(22, -95), 23);
        Label("原尺寸 · 四方向 · 实时阴影", canvas.transform, new Vector2(680, 35), new Vector2(22, -165), 18);
        var viewport = Rect("BattleViewport", canvas.transform, new Vector2(720, 980), new Vector2(0, -225));
        viewport.gameObject.AddComponent<RectMask2D>();
        var root = Rect("BattleSceneRoot", viewport, new Vector2(2000, 2000), new Vector2(-600, 260));
        foreach (string terrain in new[] { "ground", "ground_grid" })
        {
            var raw = Rect(terrain, root, new Vector2(2000, 2000), Vector2.zero).gameObject.AddComponent<RawImage>();
            raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/unpack/scene/battle/1002/" + terrain + ".png");
        }
        shadows = Rect("GROUND_SHADOW", root, new Vector2(2000, 2000), Vector2.zero);
        bodies = Rect("ABOVE_GROUND", root, new Vector2(2000, 2000), Vector2.zero);
        model = new BattleSceneModel(tables.BattleSceneObj.Get(1002), tables.BattleSceneCellObj.DataList);
        units.Clear(); casts.Clear();
        Vector2[] headings = { new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(-1, 1) };
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                var json = JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_unitobj.json"))[0];
                var appearances = new JSONArray();
                foreach (string direction in Directions)
                {
                    foreach (string name in new[] { "Idle", "Move", row == 0 ? "AttackLeft" : "AttackRight" })
                    {
                        var appearance = new JSONObject();
                        appearance["action"] = name == "Idle" ? 4 : name == "Move" ? 1 : 3;
                        appearance["direction"] = direction == "UpRight" ? 2 : 4;
                        appearance["resource"] = "scene/Colonial/MarauderPreview/" + name + "_" + direction;
                        appearance["loop"] = name == "Idle" || name == "Move";
                        var offset = new JSONObject(); offset["x"] = 0; offset["y"] = 0; appearance["offset"] = offset;
                        appearances.Add(appearance);
                    }
                }
                json["appearances"] = appearances;
                var config = new UnitResource(json);
                var unit = new BattleUnitVO(990000 + row * 4 + col, config, BattleSceneCamp.FRIENDLY, new Vector2(679 + col * 160.5f, 442 + row * 186));
                Call(unit, "Face", unit.position + headings[col]);
                model.AddElement(unit); units.Add(unit);
                casts.Add((BattleSkillCastVO)Activator.CreateInstance(typeof(BattleSkillCastVO), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1L, unit.skills[0], unit.id, 0L, 0d }, null));
            }
        }
        view = new BattleUnitView(model, bodies, shadows);
        CheckFrames();
        Label("左攻击组", canvas.transform, new Vector2(600, 35), new Vector2(24, -280), 20);
        Label("右攻击组", canvas.transform, new Vector2(600, 35), new Vector2(24, -466), 20);
        Label("空闲 → 移动 → 攻击 → 空闲 · 仅预览，不影响存档", canvas.transform, new Vector2(680, 60), new Vector2(22, -1210), 17);
        started = EditorApplication.timeSinceStartup; ready = true;
        Finish("running", null);
    }
    static void SetPhase(int phase, double localMs)
    {
        foreach (var unit in units)
        {
            Set(unit, "activeCast", null); Call(unit, "StopMoving");
            if (phase == 1)
            {
                Call(unit, "SetRoute", new List<Vector2> { unit.position + unit.heading * 1000 });
                Set(unit, "animationMs", localMs);
            }
            else if (phase == 2)
            {
                Set(unit, "activeCast", casts[units.IndexOf(unit)]); Set(unit, "skillTimeMs", localMs);
            }
        }
    }
    static void CheckFrames()
    {
        var full = new Rect(0, 0, 2000, 2000);
        for (int phase = 0; phase < 3; phase++)
        {
            int count = phase == 0 ? 95 : phase == 1 ? 26 : 65;
            for (int frame = 0; frame < count; frame++)
            {
                double ms = frame * (phase == 0 ? 3167d / 95 : phase == 1 ? 867d / 26 : 2167d / 65) + .1;
                RookieEngine.playerTimer.curTime = (float)ms;
                SetPhase(phase, ms);
                if (phase == 0)
                {
                    var starts = (Dictionary<long, float>)typeof(BattleUnitView).GetField("idleStarts", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                    foreach (var unit in units) { starts[unit.id] = 0; }
                }
                view.Refresh(full);
                foreach (var unit in units)
                {
                    var image = bodies.Find("unit_" + unit.id).GetComponent<Image>();
                    var source = unit.config.Appearances.Single(a => a.Action == BattleUnitView.ResolveAction(unit) && a.Direction == BattleUnitView.ResolveDirection(unit.heading));
                    var animation = FrameAnimationManager.GetRes(ResourceConst.GetFrameAnimationPath(source.Resource));
                    int index = animation.GetIndexByTime((float)ms);
                    Check(image.sprite == animation.sprites[index], "actual frame selection");
                    Vector2 pivot = Vector2.Scale(animation.sourceSizes[index], animation.pivot) - animation.frameOffsets[index];
                    Vector3 actual = image.rectTransform.TransformPoint(image.rectTransform.rect.min + pivot);
                    Vector3 expected = bodies.TransformPoint(new Vector3(unit.position.x, -unit.position.y + model.config.GroundThickness, 0));
                    Check(Vector3.Distance(actual, expected) < .01f, "cross-action/mirror ground anchor");
                    var shadow = shadows.Find("shadow_" + unit.id).GetComponent<BattleUnitShadowGraphic>();
                    Check(shadow.mainTexture == image.sprite.texture, "body/shadow shared frame");
                    checkedFrames++;
                }
            }
        }
        view.Refresh(new Rect(-10000, -10000, 1, 1)); Check(view.visibleCount == 0, "offscreen recycle");
        view.Refresh(full); Check(view.visibleCount == 8, "pool restore");
    }
    static void Tick()
    {
        double elapsed = (EditorApplication.timeSinceStartup - started) * 1000;
        double t = elapsed % 8235;
        int phase = t < 3167 ? 0 : t < 4901 ? 1 : t < 7068 ? 2 : 0;
        double local = phase == 1 ? t - 3167 : phase == 2 ? t - 4901 : t;
        RookieEngine.playerTimer.curTime = (float)elapsed;
        SetPhase(phase, local);
        view.Refresh(new Rect(0, 0, 2000, 2000));
        actionText.text = phase == 0 ? "空闲 · 原速" : phase == 1 ? "移动 · 原速" : "攻击 · 原速";
        if (phase != priorPhase) { priorPhase = phase; }
        string capture = phase == 0 ? "idle" : phase == 1 ? "move" : "attack";
        bool captureTime = phase == 0 ? t > 800 && t < 1200 : phase == 1 ? local > 350 && local < 650 : local > 150 && local < 400;
        if (captureTime && captures.Add(capture))
        {
            Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(Output + "runtime-" + capture + ".png");
            if (captures.Count == 3) { Finish("passed_live_preview", null); }
        }
    }
    static void Finish(string status, string error)
    {
        var result = new Result { requestId = requestId, status = status, error = error, animations = 8, frames = 502, units = units.Count, checkedFrames = checkedFrames };
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "unity-result.json", JsonUtility.ToJson(result, true));
        File.WriteAllText("Library/AIUI/marauder-preview-result.json", JsonUtility.ToJson(result, true));
    }
}
#endif




