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
public static class MarauderCombatPreviewValidation
{
    const string Key = "MarauderCombatPreview.v1";
    const string AssetRoot = "Assets/Art/frameAnimation/scene/Colonial/MarauderPreview/";
    const string Output = "E:/Codex_Projects/异星军团/outputs/assault-mech-3d-animation-test-v1/combat-preview-v1/";
    const string Pending = "Library/AIUI/pending-marauder-combat.json";
    [Serializable] class Request { public string requestId; public int processId; }
    [Serializable] class SceneBackup { public SceneSetup[] scenes; }
    [Serializable] class Result { public string requestId; public string status; public string error; public int animations; public int frames; public int units; public int checkedFrames; public int castsStarted; public int castsCompleted; public int projectileHits; public int damageEvents; public bool sawMove; public bool sawAttack; public bool sawCooldownIdle; public double maxAttackMs; }
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
    static Tables runtimeTables;
    static RectTransform arenaRoot;
    static BattleProjectileView projectileView;
    static BattleHealthView healthView;
    static Text detailText;
    static PlayerProgressControl previousProgress;
    static double lastTick;
    static int round;
    static bool timeFlow;
    static bool reported;
    const string CapturePath = "E:/Codex_Projects/异星军团/work/assault-mech-3d-animation-test-v1/combat-capture-v1/";
    [Serializable] class CaptureSample { public string file; public double seconds; }
    [Serializable] class CaptureManifest { public List<CaptureSample> samples = new List<CaptureSample>(); }
    static CaptureManifest captureManifest = new CaptureManifest();
    static double lastCapture = -1;
    static bool captureComplete;
    static int castStarted;
    static int castCompleted;
    static int projectileHits;
    static int damageEvents;
    static bool sawMove;
    static bool sawAttack;
    static bool sawCooldownIdle;
    static double maxAttackMs;
    static readonly Dictionary<long, UnitActionType> lastActions = new Dictionary<long, UnitActionType>();
    static readonly List<string> trace = new List<string>();

    static MarauderCombatPreviewValidation()
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
        if (state == PlayModeStateChange.ExitingPlayMode) { ready = false; DisposeBattle(); PlayerProgressControl.InstallValidationSession(previousProgress); }
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
        Directory.CreateDirectory(CapturePath);
        Application.runInBackground = true;
        new GameObject("MarauderPreviewDispatcher", typeof(UnityMainThreadDispatcher));
        var tables = new Tables(n => {
            var data = JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + n + ".json"));
            if (n == "cfgobj_unitvisualobj") { data[0]["normalHeight"] = 48; }
            return data;
        });
        runtimeTables = tables;
        previousProgress = PlayerProgressControl.InstallValidationSession(new PlayerProgressControl(tables, null, _ => { }));
        ResourcePresentation.Install(new GameResourcePresentationPolicy());
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
        var spriteCache = (Dictionary<string, Sprite>)typeof(ResourceManager).GetField("loadedUnpackImgMap", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var spriteResources = BattleConst.HealthBackgrounds.Values.Concat(BattleConst.FriendlyHealthFills.Values).Concat(BattleConst.EnemyHealthFills.Values).Concat(new[] { tables.ProjectileObj.Get(1101).Resource }).Distinct();
        foreach (string resource in spriteResources)
        {
            string path = BattleSceneResources.SpritePath(resource);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Check(sprite != null, "existing sprite " + path);
            spriteCache[path] = sprite;
        }
        string hitPath = ResourceConst.GetFrameAnimationPath(tables.ProjectileObj.Get(1101).ImpactResource);
        var hitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(hitPath + ".png");
        var hitJson = AssetDatabase.LoadAssetAtPath<TextAsset>(hitPath + ".json");
        FrameAnimationManager.loadedMap[hitPath] = await FrameAnimationManager.GenerateAsync(JSON.Parse(hitJson.text), hitSprite, hitPath);
        var canvas = new GameObject("MarauderPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);
        scaler.matchWidthOrHeight = .5f;
        var background = Rect("Background", canvas.transform, new Vector2(720, 1280), Vector2.zero).gameObject.AddComponent<Image>();
        background.color = new Color(.08f, .1f, .14f, 1);
        Label("机甲实战切换测试", canvas.transform, new Vector2(680, 50), new Vector2(22, -30), 28);
        actionText = Label("Loading...", canvas.transform, new Vector2(680, 70), new Vector2(22, -95), 23);
        Label("真实寻路、索敌、技能与弹道 · 保留现有战斗时序", canvas.transform, new Vector2(680, 35), new Vector2(22, -165), 18);
        var viewport = Rect("BattleViewport", canvas.transform, new Vector2(720, 980), new Vector2(0, -225));
        viewport.gameObject.AddComponent<RectMask2D>();
        arenaRoot = Rect("BattleSceneRoot", viewport, new Vector2(2000, 2000), new Vector2(-600, 260));
        foreach (string terrain in new[] { "ground", "ground_grid" })
        {
            var raw = Rect(terrain, arenaRoot, new Vector2(2000, 2000), Vector2.zero).gameObject.AddComponent<RawImage>();
            raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/unpack/scene/battle/1002/" + terrain + ".png");
        }
        shadows = Rect("GROUND_SHADOW", arenaRoot, new Vector2(2000, 2000), Vector2.zero);
        bodies = Rect("ABOVE_GROUND", arenaRoot, new Vector2(2000, 2000), Vector2.zero);
        detailText = Label("", canvas.transform, new Vector2(680, 280), new Vector2(22, -880), 18);
        Label("上：左攻击样本   下：右攻击样本", canvas.transform, new Vector2(680, 40), new Vector2(22, -1210), 18);
        CreateBattle();
        started = lastTick = EditorApplication.timeSinceStartup;
        ready = true;
        Finish("running_real_combat", null);
    }
    static void DisposeBattle()
    {
        if (projectileView != null) { projectileView.Dispose(); projectileView = null; }
        if (healthView != null) { healthView.Dispose(); healthView = null; }
        if (view != null) { view.Dispose(); view = null; }
        if (model != null) { model.Dispose(); model = null; }
    }
    static void CreateBattle()
    {
        DisposeBattle();
        model = new BattleSceneModel(runtimeTables.BattleSceneObj.Get(1002), runtimeTables.BattleSceneCellObj.DataList);
        model.InitializeBattle(runtimeTables, 7);
        // Sandbox only: disable macro purchases; keep unit combat/skills/navigation/projectiles unchanged.
        model.ai.Dispose();
        units.Clear(); casts.Clear(); lastActions.Clear();
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                var json = JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_unitobj.json"))[0];
                json["maxHp"] = 1000;
                var appearances = new JSONArray();
                foreach (string direction in new[] { "Up", "UpRight", "Right", "DownRight", "Down" })
                {
                    foreach (string name in new[] { "Idle", "Move", row == 0 ? "AttackLeft" : "AttackRight" })
                    {
                        var appearance = new JSONObject();
                        appearance["action"] = name == "Idle" ? 4 : name == "Move" ? 1 : 3;
                        appearance["direction"] = direction == "Up" ? 1 : direction == "UpRight" ? 2 : direction == "Right" ? 3 : direction == "DownRight" ? 4 : 5;
                        appearance["resource"] = "scene/Colonial/MarauderPreview/" + name + "_" + ((direction == "Up" || direction == "UpRight") ? "UpRight" : "DownRight");
                        appearance["loop"] = name == "Idle" || name == "Move";
                        var offset = new JSONObject(); offset["x"] = 0; offset["y"] = 0; appearance["offset"] = offset;
                        appearances.Add(appearance);
                    }
                }
                json["appearances"] = appearances;
                var config = new UnitResource(json);
                var unit = new BattleUnitVO(995000 + (round + 1) * 10 + row * 2 + col, config, col == 0 ? BattleSceneCamp.FRIENDLY : BattleSceneCamp.ENEMY, new Vector2(720 + col * 225, 442 + row * 186));
                model.AddElement(unit); units.Add(unit);
            }
        }
        var foreground = arenaRoot.Find("CombatForeground") as RectTransform;
        if (foreground == null) { foreground = Rect("CombatForeground", arenaRoot, new Vector2(2000, 2000), Vector2.zero); }
        var lod = arenaRoot.Find("CombatHealth") as RectTransform;
        if (lod == null) { lod = Rect("CombatHealth", arenaRoot, new Vector2(2000, 2000), Vector2.zero); }
        view = new BattleUnitView(model, bodies, shadows);
        projectileView = new BattleProjectileView(model, foreground);
        healthView = new BattleHealthView(model, arenaRoot, lod);
        model.combat.skills.Changed += e => {
            if (e.type == BattleSkillEventType.Started) { castStarted++; }
            if (e.type == BattleSkillEventType.Completed) { castCompleted++; }
            trace.Add("SKILL " + e.timeMs.ToString("F1") + " " + e.type + " caster=" + e.casterId + " cast=" + e.castId);
        };
        model.combat.projectiles.Hit += e => { projectileHits++; trace.Add("HIT " + RookieEngine.playerTimer.curTime.ToString("F1") + " projectile=" + e.projectileId); };
        model.combat.Changed += e => {
            if (e.type == BattleCombatEventType.Damaged) { damageEvents++; trace.Add("DAMAGE target=" + e.targetId + " hp=" + e.hp + " amount=" + e.damage); }
        };
        round++; timeFlow = false;
    }
    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float ms = (float)Math.Max(0, Math.Min(100, (now - lastTick) * 1000)); lastTick = now;
        double age = (now - started) * 1000;
        if (age > 16000)
        {
            CreateBattle(); started = now; return;
        }
        if (age > 1000)
        {
            if (!timeFlow) { model.StartTimeFlow(); timeFlow = true; }
            RookieEngine.sceneTimer.SyncTime(ms);
            RookieEngine.playerTimer.SyncTime(ms);
            RookieEngine.enemyTimer.SyncTime(ms);
        }
        var full = new Rect(0, 0, 2000, 2000);
        view.Refresh(full); projectileView.Refresh(full); healthView.Refresh(full, 1);
        // Bases are outside the unit demonstration; do not show detached base health bars.
        foreach (var baseUnit in model.buildings.Values)
        {
            var bar = arenaRoot.Find("CombatHealth/health_" + baseUnit.id);
            if (bar != null) { bar.gameObject.SetActive(false); }
        }
        var lines = new List<string>();
        foreach (var unit in units)
        {
            UnitActionType action = BattleUnitView.ResolveAction(unit);
            if (action == UnitActionType.MOVE) { sawMove = true; }
            if (action == UnitActionType.ATTACK_1) { sawAttack = true; maxAttackMs = Math.Max(maxAttackMs, unit.skillTimeMs - unit.activeCast.startedAtMs); }
            if (action == UnitActionType.IDLE && sawAttack && timeFlow) { sawCooldownIdle = true; }
            if (!lastActions.TryGetValue(unit.id, out var last) || last != action)
            {
                trace.Add("ACTION " + RookieEngine.playerTimer.curTime.ToString("F1") + " unit=" + unit.id + " " + last + ">" + action + " pos=" + unit.position);
                lastActions[unit.id] = action;
            }
            var image = bodies.Find("unit_" + unit.id).GetComponent<Image>();
            var appearance = unit.config.Appearances.Single(a => a.Action == action && a.Direction == BattleUnitView.ResolveDirection(unit.heading));
            var animation = FrameAnimationManager.GetRes(ResourceConst.GetFrameAnimationPath(appearance.Resource));
            int index = Array.IndexOf(animation.sprites, image.sprite);
            Check(index >= 0, "actual combat frame");
            Vector2 pivot = Vector2.Scale(animation.sourceSizes[index], animation.pivot) - animation.frameOffsets[index];
            Vector3 point = image.rectTransform.TransformPoint(image.rectTransform.rect.min + pivot);
            Vector3 expected = bodies.TransformPoint(new Vector3(unit.position.x, -unit.position.y + model.config.GroundThickness, 0));
            Check(Vector3.Distance(point, expected) < .01f, "real movement/switch anchor");
            checkedFrames++;
            string camp = unit.camp == BattleSceneCamp.FRIENDLY ? "友军" : "敌军";
            lines.Add(camp + " " + action + " / 帧" + index + " / HP " + unit.hp.ToString("F0"));
            string shot = action == UnitActionType.MOVE ? "moving" : action == UnitActionType.ATTACK_1 ? "attacking" : sawAttack ? "cooldown-idle" : "ready-idle";
            bool capture = action != UnitActionType.ATTACK_1 || unit.skillTimeMs - unit.activeCast.startedAtMs > 110;
            if (capture && captures.Add(shot)) { Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(Output + "runtime-" + shot + ".png"); }
        }
        actionText.text = "实战运行 · 第" + round + "轮 · " + (age / 1000).ToString("F1") + "秒";
        detailText.text = string.Join("\n", lines) + "\n释放 " + castStarted + " / 命中 " + projectileHits + " / 伤害 " + damageEvents + "\n技能400ms；源攻击2167ms（保留现有时序）";
        if (round == 1 && age < 12000 && age / 1000 - lastCapture >= .05)
        {
            lastCapture = age / 1000;
            string file = captureManifest.samples.Count.ToString("D4") + ".png";
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(CapturePath + file);
            captureManifest.samples.Add(new CaptureSample { file = file, seconds = age / 1000 });
        }
        if (round == 1 && age > 12500 && !captureComplete)
        {
            captureComplete = true;
            File.WriteAllText(Output + "game-capture-timestamps.json", JsonUtility.ToJson(captureManifest, true));
            File.WriteAllLines(Output + "combat-transition-trace.txt", trace);
            Finish("passed_live_real_combat", null);
        }
        if (age > 8000 && sawMove && sawAttack && sawCooldownIdle && projectileHits > 0 && damageEvents > 0 && !reported)
        {
            reported = true; File.WriteAllLines(Output + "combat-transition-trace.txt", trace);
            Finish("passed_live_real_combat", null);
        }
    }
    static void Finish(string status, string error)
    {
        var result = new Result { requestId = requestId, status = status, error = error, animations = 8, frames = 502, units = units.Count, checkedFrames = checkedFrames, castsStarted = castStarted, castsCompleted = castCompleted, projectileHits = projectileHits, damageEvents = damageEvents, sawMove = sawMove, sawAttack = sawAttack, sawCooldownIdle = sawCooldownIdle, maxAttackMs = maxAttackMs };
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "unity-result.json", JsonUtility.ToJson(result, true));
        File.WriteAllText("Library/AIUI/marauder-combat-result.json", JsonUtility.ToJson(result, true));
    }
}
#endif




