using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>正式陆战队员待机的可复现接入检查与用户查看入口。</summary>
/// <remarks>实际Panel与入口配隔离内存玩家/编组，根节点仅临时停用，结束Play恢复，不写真实存档。</remarks>
[InitializeOnLoad]
public static partial class MarineIdleIntegrationReview {

    private const string Pending = "Library/AIUI/pending-marine-idle-review.json";

    private const string Key = "MarineIdle.R1";

    [Serializable]
    private sealed class Request {

        public string requestId;
        public int processId;
        public string action;
        public string evidenceDirectory;

    }

    [Serializable]
    private sealed class RootState {

        public int scene;
        public int index;
        public string name;
        public bool active;

    }

    [Serializable]
    private sealed class RootBackup {

        public RootState[] roots;

    }

    [Serializable]
    private sealed class Result {

        public string requestId;
        public string status;
        public string error;
        public string[] checks;
        public int memoryWriteCallbacks;

    }

    private static readonly List<string> checks = new List<string>();

    private static Camera camera;

    private static RenderTexture target;

    private static Canvas canvas;

    private static double lastTick;

    private static int writes;

    private static string runtimeError;

    static MarineIdleIntegrationReview() {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnState;
    }

    [MenuItem("Tools/开发验收/陆战队员待机/打开阵容与培养")]
    public static void OpenUserReview() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || File.Exists(Pending)) {
            Debug.LogWarning("请在Edit空闲状态打开陆战队员待机验收入口，当前现场保持不变。");
            return;
        }
        var request = new Request { requestId = "marine-user-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), processId = System.Diagnostics.Process.GetCurrentProcess().Id, action = "review", evidenceDirectory = Path.GetFullPath("Library/AIUI/MarineIdleUserReview") };
        string temporary = Pending + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(request));
        File.Move(temporary, Pending);
    }

    private static void Poll() {
        if (EditorApplication.isPlaying && SessionState.GetString(Key, "") != "") {
            double now = EditorApplication.timeSinceStartup;
            if (RookieEngine.timer != null && lastTick > 0) {
                RookieEngine.timer.SyncTime((float)Math.Min(100, (now - lastTick) * 1000));
            }
            lastTick = now;
            EditorApplication.QueuePlayerLoopUpdate();
        }
        if (!File.Exists(Pending) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) {
            return;
        }
        Request request = null;
        try {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending));
            if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId)) {
                throw new InvalidOperationException("Marine review request identity mismatch");
            }
            File.Delete(Pending);
            SessionState.SetString(Key + ".out", request.evidenceDirectory);
            Directory.CreateDirectory(request.evidenceDirectory);
            checks.Clear();
            if (request.action == "setup") {
                MarineIdleAssetSetup.Build();
                Check(true, "骨架/网格/Pivot持久化和唯一Prefab登记完成");
                WriteResult(request.requestId, "passed", null);
                return;
            }
            if (request.action == "flash-setup") { MountedFlashAssetSetup.Build(); WriteResult(request.requestId, "passed", null); return; }
            if (request.action == "zerg-setup") { HydraliskIdleAssetSetup.Build(); WriteResult(request.requestId, "passed", null); return; }
            if (request.action == "fire-setup") {
                MarineBurstAssetSetup.Build();
                Check(true, "拆件四层七骨蒙皮与独立Prefab已保存");
                WriteResult(request.requestId, "passed", null);
                return;
            }
            if (request.action != "test" && request.action != "review" && request.action != "fire-test" && request.action != "fire-review" && request.action != "zerg-test" && request.action != "zerg-review" && request.action != "zerg-roar-review") {
                throw new InvalidOperationException("Unknown marine review action");
            }
            var roots = new List<RootState>();
            for (int si = 0; si < SceneManager.sceneCount; si++) {
                foreach (var root in SceneManager.GetSceneAt(si).GetRootGameObjects()) {
                    roots.Add(new RootState { scene = si, index = root.transform.GetSiblingIndex(), name = root.name, active = root.activeSelf });
                }
            }
            SessionState.SetString(Key + ".roots", JsonUtility.ToJson(new RootBackup { roots = roots.ToArray() }));
            foreach (var root in roots) {
                SceneManager.GetSceneAt(root.scene).GetRootGameObjects().Single(x => x.transform.GetSiblingIndex() == root.index).SetActive(false);
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            SessionState.SetInt(Key + ".builder", settings.ActivePlayModeDataBuilderIndex);
            settings.ActivePlayModeDataBuilderIndex = 0;
            SessionState.SetString(Key, request.requestId);
            SessionState.SetString(Key + ".action", request.action);
            EditorApplication.isPlaying = true;
        } catch (Exception error) {
            WriteResult(request == null ? "unreadable" : request.requestId, "failed", error);
            Debug.LogException(error);
        }
    }

    private static async void OnState(PlayModeStateChange state) {
        string id = SessionState.GetString(Key, "");
        if (id == "") {
            return;
        }
        if (state == PlayModeStateChange.EnteredEditMode) {
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".builder", 0);
            var roots = JsonUtility.FromJson<RootBackup>(SessionState.GetString(Key + ".roots", ""));
            foreach (var record in roots.roots) {
                var root = SceneManager.GetSceneAt(record.scene).GetRootGameObjects().Single(x => x.transform.GetSiblingIndex() == record.index && x.name == record.name);
                root.SetActive(record.active);
            }
            SessionState.EraseString(Key);
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode) {
            return;
        }
        try {
            Application.runInBackground = true;
            runtimeError = null;
            Application.logMessageReceived += OnLog;
            new GameObject("MarineReviewDispatcher", typeof(UnityMainThreadDispatcher));
            ResourcePresentation.Install(new GameResourcePresentationPolicy());
            foreach (string name in new[] { "_timer", "_sceneTimer", "_playerTimer", "_enemyTimer" }) {
                typeof(RookieEngine).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, new Timer());
            }
            lastTick = EditorApplication.timeSinceStartup;
            await CfgManager.EnsureLoadedAsync();
            var seed = new PlayerProgressSaveVO { initialized = true };
            foreach (var card in CfgManager.tables.CardObj.DataList.Where(x => x.ShowInLibrary)) {
                seed.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = card.Id, level = 1 });
            }
            if (SessionState.GetString(Key + ".action", "").StartsWith("zerg-")) {
                seed.cards.Single(x => x.cardId == 2202).level = SessionState.GetString(Key + ".action", "") == "zerg-roar-review" ? 1 : 0;
                seed.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = CfgManager.tables.CardObj.Get(2202).FragmentItemId, count = 20 });
            }
            var progress = new PlayerProgressControl(CfgManager.tables, seed, ignored => writes++);
            writes = 0;
            PlayerProgressControl.InstallValidationSession(progress);
            var teams = new TeamSchemeSaveVO();
            teams.schemes[0] = new[] { 2101, 2102, 2103, 2104 };
            TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, progress.Cards.model, teams, ignored => { }));
            typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, "Library/AIUI/MarineIdleFunctions.json")));
            BuildStage();
            const string mainId = "default/mainView/MainPanel";
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(30);
            var main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
            main.btnDeck.onClick.Invoke();
            await Frames(30);
            var deck = main.GetComponentInChildren<DeckPresentationView>(true);
            await Task.WhenAll(deck.GetComponentsInChildren<CardArtImage>().Select(x => x.Loading));
            await Frames(10);
            var formation = deck.slots[0].artwork;
            Check(formation.GetComponent<CardArtIdleSkin>()?.Rig != null, "正式阵容2101已启用独立骨架");
            foreach (var slot in deck.slots.Skip(1)) {
                Check(slot.artwork.GetComponent<CardArtIdleSkin>()?.Rig == null, "其他上阵角色保持静态");
            }
            foreach (var art in deck.library.GetComponentsInChildren<CardArtImage>()) {
                Check(art.GetComponent<CardArtIdleSkin>()?.Rig == null, "牌库卡面不启用立绘动画");
            }
            if (SessionState.GetString(Key + ".action", "").StartsWith("zerg-")) { if (SessionState.GetString(Key + ".action", "") == "zerg-roar-review") { await RunRoarPreview(id, deck); } else { await RunZerg(id, deck); } return; }
            if (SessionState.GetString(Key + ".action", "").StartsWith("fire-")) {
                await RunFire(id, deck, formation);
                return;
            }
            if (SessionState.GetString(Key + ".action", "") == "review") {
                target.Release();
                UnityEngine.Object.Destroy(target);
                target = null;
                camera.targetTexture = null;
                var game = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                EditorWindow.GetWindow(game).Show();
                EditorWindow.GetWindow(game).Focus();
                WriteResult(id, "ready-for-user", null);
                return;
            }
            await CheckMotion(formation, "formation");
            Vector2 size = formation.rectTransform.sizeDelta;
            Vector2 pivot = formation.rectTransform.pivot;
            Vector3 anchor = formation.rectTransform.position;
            deck.RequestCardInfo(2101);
            var panel = await CultivationOpened(2101);
            await CheckMotion(panel.imgPortrait, "cultivation");
            Check(CardArtIdleSkin.ActiveLeases == 2, "阵容和培养共享Prefab持有计数为2");
            var effect = panel.imgPortrait.GetComponent<CardArtIdleSkin>();
            var color = panel.imgPortrait.color;
            var group = panel.imgPortrait.gameObject.AddComponent<CanvasGroup>();
            group.alpha = .35f;
            await Frames(3);
            Check(Math.Abs(panel.imgPortrait.canvasRenderer.GetInheritedAlpha() - .35f) < .02, "CanvasGroup透明度由原Image保持");
            Capture("cultivation-alpha.png");
            group.alpha = 1;
            UnityEngine.Object.Destroy(group);
            var testMask = new GameObject("marineMaskProbe", typeof(RectTransform), typeof(Image), typeof(Mask));
            testMask.transform.SetParent(panel.imgPortrait.transform.parent, false);
            var maskRect = (RectTransform)testMask.transform;
            maskRect.position = panel.imgPortrait.rectTransform.position;
            maskRect.sizeDelta = new Vector2(400, 400);
            testMask.GetComponent<Mask>().showMaskGraphic = false;
            var oldParent = panel.imgPortrait.transform.parent;
            int oldIndex = panel.imgPortrait.transform.GetSiblingIndex();
            panel.imgPortrait.transform.SetParent(maskRect, true);
            await Frames(3);
            Check(panel.imgPortrait.materialForRendering != null && panel.imgPortrait.GetComponent<CardArtIdleSkin>().Rig == effect.Rig, "原Image的Mask/材质与骨架同时有效");
            Capture("cultivation-mask.png");
            panel.imgPortrait.transform.SetParent(oldParent, true);
            panel.imgPortrait.transform.SetSiblingIndex(oldIndex);
            UnityEngine.Object.Destroy(testMask);
            for (int round = 0; round < 6; round++) {
                panel.btnNext.onClick.Invoke();
                panel = await CultivationOpened(2102);
                Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>()?.Rig == null, "切换其他角色无旧骨架=" + round);
                panel.btnPrevious.onClick.Invoke();
                panel = await CultivationOpened(2101);
                Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>()?.Rig != null, "切回陆战队员骨架恢复=" + round);
            }
            panel.btnReturn.onClick.Invoke();
            await Frames(25);
            Check(CardArtIdleSkin.ActiveLeases == 1, "关闭培养后阵容共享资源仍有效");
            Check(Vector2.Distance(formation.rectTransform.sizeDelta, size) < .01f && Vector2.Distance(formation.rectTransform.pivot, pivot) < .00001f && Vector3.Distance(formation.rectTransform.position, anchor) < .01f, "两页往返后阵容尺寸/锚点不漂移");
            deck.RequestCardInfo(2101);
            panel = await CultivationOpened(2101);
            var cancelled = panel.imgPortrait.GetComponent<CardArtImage>().BindAsync(2101, CardArtImage.Usage.Portrait);
            panel.imgPortrait.GetComponent<CardArtImage>().Clear();
            await cancelled;
            await Frames(3);
            Check(panel.imgPortrait.GetComponent<CardArtIdleSkin>().Rig == null && CardArtIdleSkin.ActiveLeases == 1, "加载途中清理后迟到骨架不回填");
            await panel.imgPortrait.GetComponent<CardArtImage>().BindAsync(2101, CardArtImage.Usage.Portrait);
            await Frames(4);
            Check(CardArtIdleSkin.ActiveLeases == 2, "同帧清理重绑无重复租约");
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            await Frames(30);
            Check(CardArtIdleSkin.ActiveLeases == 0 && UnityEngine.Object.FindObjectsOfType<MarineIdleRig>().Length == 0, "全部关闭后骨架/租约释放");
            var inactiveData = deck.library.arraySource.ToArray();
            deck.library.array = inactiveData;
            await Frames(4);
            Check(!deck.library.isActiveAndEnabled, "回池列表的迟到刷新保持停用");
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(20);
            main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
            main.btnDeck.onClick.Invoke();
            await Frames(20);
            deck = main.GetComponentInChildren<DeckPresentationView>(true);
            Check(deck.library.GetComponentsInChildren<DeckLibraryCard>().Length > 0 && deck.slots[0].artwork.GetComponent<CardArtIdleSkin>()?.Rig != null, "回池后重开牌库和待机恢复");
            PanelMgr.ins.ClosePanelByType(mainId);
            await Frames(20);
            Check(CardArtIdleSkin.ActiveLeases == 0, "重开后再次关闭租约归零");
            Check(writes == 0, "未安装真实存档写入器且内存回调无写入");
            if (!string.IsNullOrEmpty(runtimeError)) {
                throw new InvalidOperationException(runtimeError);
            }
            WriteResult(id, "passed", null);
        } catch (Exception error) {
            WriteResult(id, "failed", error);
            Debug.LogException(error);
        }
        EditorApplication.isPlaying = false;
    }

    private static async Task CheckMotion(Image image, string label) {
        var effect = image.GetComponent<CardArtIdleSkin>();
        bool previousAutomatic = effect.AutomaticSpecialEnabled;
        effect.AutomaticSpecialEnabled = false; effect.StopSpecial(); await Frames(2);
        var rig = effect.Rig;
        var renderer = rig.GetComponent<SpriteRenderer>();
        Check(renderer.forceRenderingOff, label + "不重复绘制场景SpriteRenderer");
        Check(rig.skin.HasCurrentDeformedVertices(), label + "原生变形缓冲有效");
        var first = rig.skin.GetDeformedVertexPositionData().ToArray();
        Vector3 root = rig.skin.rootBone.localPosition;
        float maxDelta = 0;
        float cycleStart = RookieEngine.timer.curTime;
        Directory.CreateDirectory(Path.Combine(SessionState.GetString(Key + ".out", ""), label));
        for (int frame = 0; frame < 48; frame++) {
            await Frames(4);
            var current = rig.skin.GetDeformedVertexPositionData().ToArray();
            for (int i = 0; i < current.Length; i++) {
                Check(!float.IsNaN(current[i].x) && !float.IsInfinity(current[i].x) && !float.IsNaN(current[i].y) && !float.IsInfinity(current[i].y), label + "有限顶点=" + frame + "/" + i);
                maxDelta = Mathf.Max(maxDelta, Vector3.Distance(first[i], current[i]));
            }
            if (!label.StartsWith("hydralisk") || frame % 12 == 0) { Capture(label + "/" + frame.ToString("D3") + ".png"); }
        }
        while ((RookieEngine.timer.curTime - cycleStart) < rig.durationSeconds * 1000f) {
            await Frames(2);
        }
        Check(maxDelta > .001f, label + "完整周期网格实际变化");
        Check((RookieEngine.timer.curTime - cycleStart) >= rig.durationSeconds * 1000f, label + "采样覆盖至少一个完整周期");
        Check(rig.skin.rootBone.localPosition == root, label + "根骨骼原地固定");
        rig.paused = true;
        float elapsed = rig.Elapsed;
        await Frames(8);
        Check(Math.Abs(elapsed - rig.Elapsed) < .00001, label + "暂停保留相位");
        rig.paused = false;
        await Frames(5);
        Check(rig.Elapsed != elapsed, label + "恢复继续推进");
        var mesh = image.canvasRenderer.GetMesh();
        var mouth = rig.GetComponent<HydraliskMouthRig>();
        int expectedVertices = first.Length + (mouth == null ? 0 : mouth.jawSkin.GetDeformedVertexPositionData().ToArray().Length);
        Check(mesh.vertexCount == expectedVertices, label + "Image绘制原生细分顶点而非静态四边形");
        effect.AutomaticSpecialEnabled = previousAutomatic;

    }

    private static async Task<CardCultivationPanel> CultivationOpened(int cardId) {
        for (int frame = 0; frame < 500; frame++) {
            await Frames(1);
            var panel = PanelMgr.ins.GetPanel(CardCultivationControl.PanelId) as CardCultivationPanel;
            if (panel != null && panel.isOpened && panel.CurrentCardId == cardId && panel.imgPortrait.sprite != null) {
                var loader = panel.imgPortrait.GetComponent<CardArtImage>();
                await loader.Loading;
                await Frames(5);
                return panel;
            }
        }
        throw new TimeoutException("培养实际入口未就绪=" + cardId);
    }

    private static async Task Frames(int count) {
        for (int i = 0; i < count; i++) {
            EditorApplication.QueuePlayerLoopUpdate();
            await Task.Delay(20);
            Canvas.ForceUpdateCanvases();
        }
    }

    private static void BuildStage() {
        var root = new GameObject("MarineIdleReviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvas = root.GetComponent<Canvas>();
        camera = new GameObject("MarineIdleReviewCamera", typeof(Camera)).GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 640;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.015f, .045f, .08f);
        target = new RenderTexture(720, 1280, 24);
        target.Create();
        // Screen output stays live; Capture binds the offscreen target only while reading pixels.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100;
        var stage = root.GetComponent<RectTransform>();
        var pool = new GameObject("uiPool", typeof(RectTransform)).GetComponent<RectTransform>();
        pool.SetParent(stage, false);
        pool.gameObject.SetActive(false);
        new GameObject("MarineReviewEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        PanelMgr.ins.SetUIRoot(stage, stage, pool, new Vector2(720, 1280));
        PanelMgr.ins.Init();
        Canvas.ForceUpdateCanvases();
    }

    private static void Capture(string name) {
        Canvas.ForceUpdateCanvases();
        var previous = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        Texture2D image = null;
        try {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(SessionState.GetString(Key + ".out", ""), name), image.EncodeToPNG());
        } finally {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            if (image != null) UnityEngine.Object.Destroy(image);
        }
    }

    private static void OnLog(string message, string trace, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            runtimeError = message + "\n" + trace;
        }
    }

    private static void Check(bool passed, string message) {
        if (!passed) {
            throw new InvalidOperationException(message);
        }
        // 逐顶点有限性需检查，但报告按阶段聚合，避免几十万条重复诊断。
        if (!message.Contains("有限顶点=")) {
            checks.Add(message);
        }
    }

    private static void WriteResult(string id, string status, Exception error) {
        string output = SessionState.GetString(Key + ".out", "");
        var result = new Result { requestId = id, status = status, error = error?.ToString(), checks = checks.ToArray(), memoryWriteCallbacks = writes };
        File.WriteAllText("Library/AIUI/marine-idle-review-result.json", JsonUtility.ToJson(result, true));
        if (!string.IsNullOrEmpty(output)) {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, id + ".json"), JsonUtility.ToJson(result, true));
        }
    }

}