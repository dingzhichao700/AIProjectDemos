#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 培养页独立满级状态核查，不读写真实玩家进度。
/// </summary>
/// <remarks>只用正式MaxLevel构造隔离内存夹具；不修改共享验证脚本或UI。</remarks>
[InitializeOnLoad]
public static partial class CultivationMaxStateReviewValidation {

    private const string Key = "Cultivation.MaxReview.20261109";

    private const string Pending = "Library/AIUI/pending-cultivation-max-review.json";

    private const string ResultPath = "Library/AIUI/cultivation-max-review-result.json";

    [Serializable] private sealed class Request { public string requestId; public int processId; public int cardId; public string releaseEvidence; public string state; public string evidenceDirectory; }

    [Serializable] private sealed class Result { public string requestId; public string status; public string error; public string[] checks; public string[] observations; public string[] deviations; }

    [Serializable] private sealed class SceneBackup { public RootState[] roots; }

    [Serializable] private sealed class RootState { public int scene; public int index; public string name; public bool active; }

    private static readonly List<string> checks = new List<string>();

    private static readonly List<string> observations = new List<string>();

    private static readonly List<string> deviations = new List<string>();

    private static string runtimeError;

    private static bool readinessPublished;

    private static Camera camera;

    private static RenderTexture target;

    private static Canvas canvas;

    static CultivationMaxStateReviewValidation() {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnState;
        EditorApplication.delayCall += () => {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling) {
                // 编译失败可能中断Play申请；恢复本夹具保存的场景状态后再接收新请求。
                if (!string.IsNullOrEmpty(SessionState.GetString(Key, ""))) {
                    OnState(PlayModeStateChange.EnteredEditMode);
                }
                File.WriteAllText("Library/AIUI/cultivation-max-review-ready.json", JsonUtility.ToJson(new Request { requestId = "2101-sample-v1", processId = System.Diagnostics.Process.GetCurrentProcess().Id }));
            }
        };
    }

    private static IEnumerable<string> CfgManagerImagePaths() {
        var data = SimpleJSON.JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_attributedefinitionobj.json"));
        foreach (SimpleJSON.JSONNode row in data.AsArray) {
            yield return "Assets/Art/atlasSource/default/cardCultivation/" + row["icon"].Value + ".png";
        }
    }

    private static void Poll() {
        if (!readinessPublished && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating) {
            if (!string.IsNullOrEmpty(SessionState.GetString(Key, ""))) {
                OnState(PlayModeStateChange.EnteredEditMode);
            }
            File.WriteAllText("Library/AIUI/cultivation-max-review-ready.json", JsonUtility.ToJson(new Request { requestId = "2101-sample-v1", processId = System.Diagnostics.Process.GetCurrentProcess().Id }));
            readinessPublished = true;
        }

        if (EditorApplication.isPlaying && !string.IsNullOrEmpty(SessionState.GetString(Key, ""))) {
            EditorApplication.QueuePlayerLoopUpdate();
        }
        if (!File.Exists(Pending) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) {
            return;
        }
        Request request = null;
        try {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
            File.Delete(Pending);
            if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId) || string.IsNullOrWhiteSpace(request.releaseEvidence)) {
                throw new InvalidOperationException("Request identity mismatch");
            }
            if (request.state == "power-micro-r3-setup") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                ApplyPowerMicroShiftR3(); WriteResult(request.requestId, null); return;
            }
            if (request.state == "power-pixels-r2-setup") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                ApplyPowerPixelAlignmentR2(); WriteResult(request.requestId, null); return;
            }
            if (request.state == "power-position-setup") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                ApplyPowerPositionR1(); WriteResult(request.requestId, null); return;
            }
            if (request.state == "power-number-tuning-r2") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                TunePowerNumbersR2();
                WriteResult(request.requestId, null);
                return;
            }
            if (request.state == "power-shared-setup-r1") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                SetupPowerSharedR1();
                WriteResult(request.requestId, null);
                return;
            }
            if (request.state == "shared-portraits-cleanup-r1") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                SetupSharedPortraitDedupR1();
                WriteResult(request.requestId, null);
                return;
            }
            if (request.state == "refinements-setup-r1") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                SetupRefinementsR1();
                WriteResult(request.requestId, null);
                return;
            }
            if (request.state == "quality-pulses-setup-r1") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                SetupQualityPulsesR1();
                WriteResult(request.requestId, null);
                return;
            }
            if (request.state == "blue-pulse-setup") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                SetupBluePulseTrial();
                WriteResult(request.requestId, null);
                return;
            }
            if (request.state == "skill-press-setup") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                ApplySkillPressFix();
                WriteResult(request.requestId, null);
                return;
            }
            if (request.state == "podium-activation-setup") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                ApplyPodiumActivation15();
                WriteResult(request.requestId, null);
                return;
            }
            if (request.state == "figma143-setup") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                ApplyApprovedFigma143();
                WriteResult(request.requestId, null);
                return;
            }
            if (request.state == "material-icons-setup") {
                checks.Clear(); observations.Clear(); deviations.Clear();
                SessionState.SetString(Key + ".evidence", request.evidenceDirectory);
                SetupMaterialIcons();
                WriteResult(request.requestId, null);
                return;
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (var row in SimpleJSON.JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_cultivationqualityartobj.json")).AsArray) {
                var path = ((SimpleJSON.JSONNode)row)["podium"].Value;
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) {
                    throw new InvalidOperationException("Missing approved stage: " + path);
                }
                if (settings.FindAssetEntry(guid) == null) {
                    settings.CreateOrMoveEntry(guid, settings.FindGroup("Default Local Group")).SetAddress(path);
                }
            }
            // 美术条目的已批准台座覆盖与品质图走同一正式Addressables路径。
            foreach (var row in SimpleJSON.JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_cardartobj.json")).AsArray) {
                string path = ((SimpleJSON.JSONNode)row)["cultivationPodium"].Value;
                if (string.IsNullOrWhiteSpace(path)) { continue; }
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) { throw new InvalidOperationException("Missing podium override: " + path); }
                if (settings.FindAssetEntry(guid) == null) { settings.CreateOrMoveEntry(guid, settings.FindGroup("Default Local Group")).SetAddress(path); }
            }
            AssetDatabase.SaveAssets();
            // 不切换或保存用户场景。记录并临时停用原有根节点，Play结束后逐项恢复。
            var roots = new List<RootState>();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++) {
                foreach (var root in SceneManager.GetSceneAt(sceneIndex).GetRootGameObjects()) {
                    roots.Add(new RootState { scene = sceneIndex, index = root.transform.GetSiblingIndex(), name = root.name, active = root.activeSelf });
                }
            }
            SessionState.SetString(Key + ".scenes", JsonUtility.ToJson(new SceneBackup { roots = roots.ToArray() }));
            foreach (var rootState in roots) {
                var root = SceneManager.GetSceneAt(rootState.scene).GetRootGameObjects().Single(value => value.transform.GetSiblingIndex() == rootState.index);
                root.SetActive(false);
            }
            SessionState.SetInt(Key + ".mode", settings.ActivePlayModeDataBuilderIndex);
            settings.ActivePlayModeDataBuilderIndex = 0;
            SessionState.SetString(Key, request.requestId);
            SessionState.SetInt(Key + ".cardId", request.cardId);
            SessionState.SetString(Key + ".state", request.state ?? "max");
            SessionState.SetString(Key + ".release", request.releaseEvidence);
            SessionState.SetString(Key + ".evidence", request.evidenceDirectory ?? "");

            EditorApplication.isPlaying = true;
        } catch (Exception error) {
            WriteResult(request == null ? "unreadable" : request.requestId, error);
        }
    }

    private static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            runtimeError = message + "\n" + stack;
        }
    }

    private static async void OnState(PlayModeStateChange state) {
        string id = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(id)) {
            return;
        }
        if (state == PlayModeStateChange.EnteredEditMode) {
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".mode", 0);
            var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + ".scenes", ""));
            SessionState.EraseString(Key);
            foreach (var rootState in backup.roots) {
                var root = SceneManager.GetSceneAt(rootState.scene).GetRootGameObjects().Single(value => value.transform.GetSiblingIndex() == rootState.index && value.name == rootState.name);
                root.SetActive(rootState.active);
            }
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode) {
            return;
        }
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        checks.Clear();
        observations.Clear();
        deviations.Clear();
        runtimeError = null;
        var previousRegions = UIScreenRegions.EditorOverride;
        PlayerProgressControl previousSession = null;
        bool installed = false;
        var timerField = typeof(RookieEngine).GetField("_timer", BindingFlags.Static | BindingFlags.NonPublic);
        object previousTimer = timerField.GetValue(null);
        var clockFields = new[] { "_sceneTimer", "_playerTimer", "_enemyTimer" }.Select(name => typeof(RookieEngine).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
        var previousClocks = clockFields.Select(field => field.GetValue(null)).ToArray();
        try {
            new GameObject("CultivationMaxReviewDispatcher", typeof(UnityMainThreadDispatcher));
            ResourcePresentation.Install(new GameResourcePresentationPolicy());
            timerField.SetValue(null, new Timer());
            foreach (var clock in clockFields) {
                clock.SetValue(null, new Timer());
            }
            var loading = CfgManager.EnsureLoadedAsync();
            if (await Task.WhenAny(loading, Task.Delay(45000)) != loading) {
                throw new TimeoutException("Configuration load timed out");
            }
            await loading;
            int writes = 0;
            int cardId = SessionState.GetInt(Key + ".cardId", 2101);
            var card = CfgManager.tables.CardObj.Get(cardId);
            var initial = new PlayerProgressSaveVO { initialized = true };
            string mode = SessionState.GetString(Key + ".state", "max");
            if (mode.StartsWith("podium-cards", StringComparison.Ordinal) || mode == "podium-images" || mode == "material-icons" || mode == "all-card-level-effects" || mode == "blue-pulse-trial" || mode == "quality-pulses-r1" || mode == "power-shared-r1" || mode.StartsWith("power-position-", StringComparison.Ordinal) || mode.StartsWith("power-pixels-r2", StringComparison.Ordinal) || mode == "power-micro-r3" || mode == "item-frame-r2" || mode.StartsWith("shared-portraits-", StringComparison.Ordinal) || mode.StartsWith("refresh-stability-", StringComparison.Ordinal)) {
                foreach (var visible in CfgManager.tables.CardObj.DataList.Where(row => row.ShowInLibrary)) {
                    if ((mode == "blue-pulse-trial" || mode == "quality-pulses-r1" || mode.StartsWith("refresh-stability-", StringComparison.Ordinal)) && visible.Id == 2303) {
                        continue;
                    }
                    initial.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = visible.Id, level = Math.Min(6, visible.MaxLevel) });
                }
            } else if (mode.StartsWith("refinements-", StringComparison.Ordinal)) {
                initial.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = int.Parse(mode.Substring("refinements-".Length)) });
            } else if (mode.StartsWith("skill-press-", StringComparison.Ordinal)) {
                initial.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = int.Parse(mode.Substring("skill-press-".Length)) });
            } else if (mode.StartsWith("skill-order-text-", StringComparison.Ordinal)) {
                initial.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = int.Parse(mode.Substring("skill-order-text-".Length)) });
            } else if (mode == "card-level-effects") {
                initial.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = 6 });
            } else if (mode != "unowned") {
                initial.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = (mode == "insufficient" || mode == "materials" || mode == "complete-costs" || mode == "skill-stages-1" || mode.StartsWith("viewport")) ? 1 : card.MaxLevel });
            }
            if (mode == "materials") {
                initial.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = 2001, count = 73 });
                initial.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = 2002, count = 987 });
            }
            var session = new PlayerProgressControl(CfgManager.tables, initial, ignored => writes++);
            writes = 0; // 新会话的初始种子仅写入内存回调，验证从初始化完成后计数。
            previousSession = PlayerProgressControl.InstallValidationSession(session);
            installed = true;
            BuildStage();
            UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(0, 0, 720, 1280) };
            if (mode == "power-micro-r3") {
                await RunPowerMicroShiftR3(session);
            } else if (mode.StartsWith("power-pixels-r2", StringComparison.Ordinal)) {
                await RunPowerPixelsR2(session, mode == "power-pixels-r2-confirm");
            } else if (mode.StartsWith("power-position-", StringComparison.Ordinal)) {
                await RunPowerPositionR1(session, mode == "power-position-after");
            } else if (mode == "item-frame-r2") {
                await RunItemFrameR2(session);
            } else if (mode == "power-shared-r1") {
                await RunPowerSharedR1(session);
            } else if (mode.StartsWith("shared-portraits-", StringComparison.Ordinal)) {
                await RunSharedPortraitDedupR1(session);
            } else if (mode.StartsWith("refinements-", StringComparison.Ordinal)) {
                await RunRefinementsR1(session);
            } else if (mode == "quality-pulses-r1") {
                await RunQualityPulsesR1(session);
            } else if (mode.StartsWith("refresh-stability-", StringComparison.Ordinal)) {
                await RunRefreshStabilityTrial(session);
            } else if (mode == "blue-pulse-trial") {
                await RunBluePulseTrial(session);
            } else if (mode.StartsWith("skill-press-", StringComparison.Ordinal)) {
                await RunSkillPressReview(session);
            } else if (mode.StartsWith("podium-activation-", StringComparison.Ordinal)) {
                await RunPodiumActivationReview(session);
            } else if (mode == "complete-costs") {
                await RunCompleteCostsReview(session);
            } else if (mode == "all-card-level-effects") {
                await RunAllCardLevelEffectsReview(session);
            } else if (mode == "card-level-effects") {
                await RunCardLevelEffectsReview(session);
            } else if (mode == "material-icons") {
                await RunMaterialIconsReview(session);
            } else if (mode.StartsWith("skill-order-text-", StringComparison.Ordinal)) {
                await RunSkillOrderTextReview(session);
            } else if (mode == "podium-images") {
                await RunPodiumImagesReview(session);
            } else if (mode.StartsWith("podium-cards", StringComparison.Ordinal)) {
                await RunPodiumCardsReview(session);
            } else if (mode.StartsWith("exhibit", StringComparison.Ordinal)) {
                await RunExhibitReview();
            } else if (mode == "viewport" || mode == "viewport-before") {
                await RunCultivationViewportReview(mode == "viewport-before");
            } else if (mode == "viewport-core") {
                await RunViewportCoreReview();
             } else if (mode.StartsWith("skill-stages", StringComparison.Ordinal)) {
                await RunSkillStagesReview(session);
            } else if (mode == "materials") {
                await RunMaterialReview(session);
            } else {
                await RunMaxStateReview(session, cardId);
            }
            Check((mode == "power-shared-r1" || mode == "item-frame-r2") ? writes >= 2 : (mode == "quality-pulses-r1" || mode.StartsWith("refresh-stability-", StringComparison.Ordinal)) ? writes >= 3 : writes == 0, "隔离会话写入计数符合验证路径，未安装真实存档写入器");
            if (!string.IsNullOrEmpty(runtimeError)) {
                throw new InvalidOperationException(runtimeError);
            }
            WriteResult(id, null);
        } catch (Exception error) {
            WriteResult(id, error);
            Debug.LogException(error);
        } finally {
            GlobalToast.Shutdown();
            CardSkillDetailControl.Close();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType("default/mainView/MainPanel");
            if (installed) {
                PlayerProgressControl.InstallValidationSession(previousSession);
            }
            UIScreenRegions.EditorOverride = previousRegions;
            timerField.SetValue(null, previousTimer);
            for (int i = 0; i < clockFields.Length; i++) {
                clockFields[i].SetValue(null, previousClocks[i]);
            }
            Application.logMessageReceived -= OnLog;
            Application.runInBackground = background;
            if (target != null) {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            EditorApplication.isPlaying = false;
        }
    }

    private static void BuildStage() {
        var root = new GameObject("CultivationMaxReviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvas = root.GetComponent<Canvas>();
        camera = new GameObject("CultivationMaxReviewCamera", typeof(Camera)).GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 640;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.015f, .045f, .08f);
        target = new RenderTexture(720, 1280, 24);
        target.Create();
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100;
        canvas.pixelPerfect = true;
        var stage = root.GetComponent<RectTransform>();
        var pool = new GameObject("uiPool", typeof(RectTransform)).GetComponent<RectTransform>();
        pool.SetParent(stage, false);
        pool.gameObject.SetActive(false);
        new GameObject("SkillValidationEventSystem", typeof(EventSystem));
        PanelMgr.ins.SetUIRoot(stage, stage, pool, new Vector2(720, 1280));
        PanelMgr.ins.Init();
        Canvas.ForceUpdateCanvases();
    }

    private static async Task Frames(int count) {
        for (int i = 0; i < count; i++) {
            await Tick();
        }
    }

    private static async Task<CardCultivationPanel> CultivationOpened() {
        for (int i = 0; i < 300; i++) {
            var panel = PanelMgr.ins.GetPanel(CardCultivationControl.PanelId) as CardCultivationPanel;
            if (panel != null && panel.isOpened) {
                await Frames(8);
                return panel;
            }
            await Frames(2);
        }
        throw new TimeoutException("培养页真实入口打开超时");
    }

    private static async Task RunExhibitReview() {
        const string mainId = "default/mainView/MainPanel";
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
        PanelMgr.ins.OpenPanel(mainId);
        await Frames(15);
        var main = PanelMgr.ins.GetPanel(mainId) as MainPanel;
        Click(main.btnDeck.GetComponent<RectTransform>());
        await Task.Delay(350);
        await Frames(8);
        var sourceView = main.GetComponentInChildren<DeckPresentationView>(true);
        var sourceScroll = sourceView.library.GetComponent<ScrollRect>();
        sourceScroll.content.anchoredPosition += Vector2.up * 80;
        sourceScroll.velocity = Vector2.zero;
        await Frames(3);
        Vector2 savedSourcePosition = sourceScroll.content.anchoredPosition;
        var filterField = typeof(DeckPresentationView).GetField("filter", BindingFlags.Instance | BindingFlags.NonPublic);
        var categoryField = typeof(DeckPresentationView).GetField("category", BindingFlags.Instance | BindingFlags.NonPublic);
        int savedFilter = (int)filterField.GetValue(sourceView);
        int savedCategory = (int)categoryField.GetValue(sourceView);
        sourceView.RequestCardInfo(2101);
        var panel = await CultivationOpened();
        var source = (CardCultivationOpenVO)typeof(CardCultivationPanel).GetField("request", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
        Check(source.source == "library", "从实际MainPanel牌库来源打开");
        bool allPortraits = SessionState.GetString(Key + ".state", "") == "exhibit-all";
        int[] reviewIds = allPortraits ? CfgManager.tables.CardObj.DataList.Where(card => card.ShowInLibrary).Select(card => card.Id).OrderBy(id => id).Concat(new[] { 2101 }).ToArray() : new[] { 2101, 2103, 2101 };
        ValidateTemporaryStats(CfgManager.tables);
        var reviewedSizes = new Dictionary<int, Vector2>();
        var reviewedAnchors = new Dictionary<int, Vector2>();
        for (int reviewIndex = 0; reviewIndex < reviewIds.Length; reviewIndex++) {
            int id = reviewIds[reviewIndex];
            for (int frame = 0; frame < 300; frame++) {
                await Frames(2);
                if (panel.imgPortrait.enabled && panel.imgPortrait.sprite != null && panel.imgPodium.enabled && panel.imgPodium.sprite != null) {
                    await Frames(5);
                    break;
                }
            }
            Check(panel.CurrentCardId == id, "切卡实际卡牌=" + id);
            Check(panel.imgPodium.sprite != null && panel.imgPodium.enabled, "品质展台已加载=" + id);
            var sprite = panel.imgPortrait.sprite;
            Check(sprite != null && panel.imgPortrait.rectTransform.pivot == sprite.pivot / sprite.rect.size, "立绘使用导入语义pivot=" + id);
            var art = CfgManager.tables.CardArtObj.Get(CfgManager.tables.CardObj.Get(id).ArtId);
            var expectedOffsets = SimpleJSON.JSON.Parse(File.ReadAllText("Library/AIUI/cultivation-portrait-offsets.json"));
            float expectedOffset = expectedOffsets[id.ToString()].AsInt;
            float expectedOffsetX = SimpleJSON.JSON.Parse(File.ReadAllText("Library/AIUI/cultivation-portrait-offsets-x.json"))[id.ToString()].AsInt;
            Check(art.CultivationOffsetY == expectedOffset && art.CultivationOffsetX == expectedOffsetX && art.CultivationPortraitSource == art.Portrait, "正式展示配置与逐图校准清单一致=" + id);
            Check(Vector2.Distance(panel.imgPortrait.rectTransform.anchoredPosition, new Vector2(360 + expectedOffsetX, -296 - expectedOffset)) < .01f, "逐图落位及切卡重置=" + id);
            Check(panel.imgPodium.transform.GetSiblingIndex() < panel.imgPortrait.transform.GetSiblingIndex(), "连续切卡始终保持台座在角色下方=" + id);
            Check(sprite == ResourceManager.GetUnpackImage(art.Portrait), "异步加载完成后无上张立绘残留=" + id);
            Check(panel.imgRaceBackdrop.enabled && panel.imgRaceBackdrop.sprite != null, "当前种族现有共用背景已加载=" + id);
            Check(CultivationPortraitPlacement.ReadOffsetY(art, art.Portrait + ".changed") == 0 && CultivationPortraitPlacement.ReadOffsetX(art, art.Portrait + ".changed") == 0, "更换素材地址后旧校正不继承=" + id);
            Check(!panel.txtCounter.gameObject.activeSelf && panel.txtCounter.text == "" && !panel.imgPortrait.transform.parent.Find("imgCounter").gameObject.activeSelf, "不向玩家显示克制关系文字及底板=" + id);
            Check(!panel.txtPower.gameObject.activeSelf && panel.txtPower.text == "", "战力区只显示数值，不显示内部临时标记=" + id);
            ValidateV11Presentation(panel);
            var displayState = CardCultivationQuery.Read(CfgManager.tables, PlayerProgressControl.ins, id);
            Check(displayState.power.HasValue && panel.txtPowerValue.text == displayState.power.Value.ToString(), "当前卡真实战力为数值且页面同步=" + id);
            var cardConfig = CfgManager.tables.CardObj.Get(id);
            Check(panel.txtCost.text == cardConfig.ConstructionCost.ToString(), "建造费数字读取当前卡牌权威配置=" + id);
            string expectedQualityHex = cardConfig.Quality == cfg.Quality.NORMAL ? "#62BC78" : cardConfig.Quality == cfg.Quality.RARE ? "#529ED8" : cardConfig.Quality == cfg.Quality.EPIC ? "#AE74D3" : "#E6AA4D";
            ColorUtility.TryParseHtmlString(expectedQualityHex, out Color expectedQualityColor);
            if (panel.imgQualityWordmark != null) {
                string qualityName = cardConfig.Quality == cfg.Quality.NORMAL ? "normal" : cardConfig.Quality == cfg.Quality.RARE ? "rare" : cardConfig.Quality == cfg.Quality.EPIC ? "epic" : "legend";
                Check(!panel.txtQuality.enabled && panel.imgQualityWordmark.enabled && panel.imgQualityWordmark.sprite != null && panel.imgQualityWordmark.sprite.name == "quality-" + qualityName, "保留现有品质艺术字且按当前卡牌刷新=" + id);
            } else {
                Check(panel.txtQuality.color == expectedQualityColor && !panel.txtQuality.enableVertexGradient, "品质字色按四品质配置刷新且无渐变叠色=" + id);
            }
            foreach (SimpleJSON.JSONNode layout in SimpleJSON.JSON.Parse(File.ReadAllText("Library/AIUI/cultivation-static-rects.json")).AsArray) {
                var staticRect = (RectTransform)panel.imgPortrait.transform.parent.Find(layout["prefabName"].Value);
                var approved = layout["bounds"].AsArray;
                if (layout["prefabName"].Value == "imgPodium") {
                    Check(Vector2.Distance(staticRect.anchoredPosition, new Vector2(175, -225.7f)) < .01f && Vector2.Distance(staticRect.sizeDelta, new Vector2(370, 123.39f)) < .01f, "Existing podium geometry preserved from pre-integration snapshot");
                    continue;
                }
                Check(Vector2.Distance(staticRect.anchoredPosition, new Vector2(approved[0].AsFloat, -approved[1].AsFloat)) < .01f && Vector2.Distance(staticRect.sizeDelta, new Vector2(approved[2].AsFloat, approved[3].AsFloat)) < .01f, "运行分区与批准v7静态布局一致=" + id + "/" + layout["prefabName"].Value);
            }
            var chrome = (RectTransform)panel.imgPortrait.transform.parent.Find("imgUpgradeChrome");
            float podiumBottom = -panel.imgPodium.rectTransform.anchoredPosition.y + panel.imgPodium.rectTransform.sizeDelta.y;
            observations.Add("Preserved existing podium/chrome gap=" + (-chrome.anchoredPosition.y - podiumBottom) + "; pre-existing geometry outside v11 scoped fix");
            var expectedScales = SimpleJSON.JSON.Parse(File.ReadAllText("Library/AIUI/cultivation-portrait-scales.json"));
            float expectedScale = expectedScales[id.ToString()].AsFloat;
            float baseScale = Mathf.Min(220f / sprite.rect.width, 270f / sprite.rect.height);
            var rect = panel.imgPortrait.rectTransform;
            Check(Mathf.Abs((float)(art.CultivationScale ?? 1) - expectedScale) < .001f && Vector2.Distance(rect.sizeDelta, sprite.rect.size * baseScale * expectedScale) < .01f, "逐图等比体量符合配置且无拉伸=" + id);
            Check(CultivationPortraitPlacement.ReadScale(art, art.Portrait + ".changed") == 1, "换图后旧缩放倍率不继承=" + id);
            float top = -rect.anchoredPosition.y - (1 - rect.pivot.y) * rect.sizeDelta.y;
            Check(top >= 0 && rect.anchoredPosition.x - rect.pivot.x * rect.sizeDelta.x >= 0 && rect.anchoredPosition.x + (1 - rect.pivot.x) * rect.sizeDelta.x <= 720, "新体量完整处于培养展示范围内=" + id);
            reviewedSizes[id] = rect.sizeDelta;
            reviewedAnchors[id] = rect.anchoredPosition;
            observations.Add("card=" + id + ";sprite=" + sprite.name + ";pivot=" + panel.imgPortrait.rectTransform.pivot + ";anchor=" + panel.imgPortrait.rectTransform.anchoredPosition + ";configuredScale=" + expectedScale + ";size=" + panel.imgPortrait.rectTransform.sizeDelta + ";portraitPath=" + CardArtImage.PathFor(CfgManager.tables.CardArtObj.Get(CfgManager.tables.CardObj.Get(id).ArtId), CardArtImage.Usage.Portrait) + ";podium=" + panel.imgPodium.sprite.name);
            Capture("exhibit-" + id + (id == 2101 && checks.Count > 8 ? "-return" : "") + ".png");
            if (reviewIndex + 1 < reviewIds.Length) {
                int targetId = reviewIds[reviewIndex + 1];
                int targetIndex = Array.IndexOf(source.orderedCardIds, targetId);
                while (panel.CurrentCardId != targetId) {
                    int currentIndex = Array.IndexOf(source.orderedCardIds, panel.CurrentCardId);
                    Click((currentIndex < targetIndex ? panel.btnNext : panel.btnPrevious).GetComponent<RectTransform>());
                    await Frames(3);
                }
            }
        }
        if (allPortraits) {
            Check(reviewedSizes[2105].x > reviewedSizes[2102].x && reviewedSizes[2105].x * reviewedSizes[2105].y > reviewedSizes[2102].x * reviewedSizes[2102].y, "战列巡航舰宽度与展示面积均大于医疗运输机");
            Check(reviewedAnchors[2105].y > reviewedAnchors[2102].y, "战列巡航舰悬浮锚点高于医疗运输机");
            Check(reviewedAnchors[2303].y > -296 && reviewedAnchors[2306].y > -296, "哨兵与高阶圣堂武士显示独立浮空间距");
        }
        Click(panel.btnReturn.GetComponent<RectTransform>());
        await Frames(8);
        Check(Vector2.Distance(sourceScroll.content.anchoredPosition, savedSourcePosition) < .1f && (int)filterField.GetValue(sourceView) == savedFilter && (int)categoryField.GetValue(sourceView) == savedCategory, "返回恢复实际牌库筛选、分类与非零滚动位置");
        var view = main.GetComponentInChildren<DeckPresentationView>(true);
        view.RequestCardInfo(2103);
        panel = await CultivationOpened();
        await Frames(12);
        Check(panel.CurrentCardId == 2103 && panel.imgPortrait.rectTransform.anchoredPosition == new Vector2(360, -312), "关闭后从实际牌库重开坦克，恢复已通过v3落位");
        Check(panel.imgPodium.transform.GetSiblingIndex() < panel.imgPortrait.transform.GetSiblingIndex(), "重开后展台与立绘层级正确");
        Check(((RectTransform)panel.imgPortrait.transform.parent.Find("imgUpgradeChrome")).anchoredPosition.y == -333, "重开仍使用Prefab静态位置，无额外44px位移");
        ValidateV11Presentation(panel);
        Capture("reopened-2103.png");

    }

    /**仅在隔离内存会话把1101设为未拥有；展示值仍来自正式1级配置。*/
    /**从当前正式卡牌上限构造隔离满级，只记录显示差异，不修复界面。*/
    private static async Task RunMaxStateReview(PlayerProgressControl session, int cardId) {
        var saved = new TeamSchemeSaveVO();
        saved.schemes[0] = new[] { cardId, 0, 0, 0 };
        var oldTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, saved, ignored => { }));
        var functionField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldFunctions = functionField.GetValue(null);
        string folder = "Library/AIUI/CultivationMaxReview/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        functionField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        const string mainId = "default/mainView/MainPanel";
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(15);
            var main = PanelMgr.ins.GetPanel(mainId) as MainPanel;
            Click(main.btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            await Frames(8);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            if (SessionState.GetString(Key + ".state", "max") == "unowned") {
                Check(view.library.arraySource.Cast<DeckCardView.DisplayData>().Any(card => card.cardId == cardId), "未拥有卡存在于实际牌库来源序列");
                // 未拥有卡不允许上阵，使用实际牌库入口回调，不伪造上阵资格。
                view.RequestCardInfo(cardId);
            } else {
                Check(view.slots[0].title.text == CfgManager.tables.CardObj.Get(cardId).Name, "当前卡牌实际出现在阵容首位");
                Click(view.slots[0].button.GetComponent<RectTransform>());
            }
            var panel = await CultivationOpened();
            for (int frame = 0; frame < 300 && (!panel.imgPortrait.enabled || panel.imgPortrait.sprite == null); frame++) {
                await Frames(2);
            }
            Check(panel.CurrentCardId == cardId && panel.imgPortrait.enabled && panel.imgPortrait.sprite != null, "实际入口打开满级夹具卡并等待形象完成");
            int maximum = CfgManager.tables.CardObj.Get(cardId).MaxLevel;
            string mode = SessionState.GetString(Key + ".state", "max");
            var snapshot = CardCultivationQuery.Read(CfgManager.tables, session, cardId);
            if (mode == "insufficient") {
                ValidateApprovedSample(CfgManager.tables);
            }
            int level = mode == "unowned" ? 0 : mode == "insufficient" ? 1 : maximum;
            Check(snapshot.growth.level == level && panel.txtLevel.text == (mode == "unowned" ? "1级" : level.ToString()), "裁决：未解锁等级框1级，其余显示实际等级");
            Check(panel.txtUpgrade.text == (mode == "unowned" ? "解锁" : "升级"), "裁决：满级仍保留升级文案");
            if (mode == "max") {
                Check(snapshot.growth.maxed && snapshot.materials.Count == 0 && !panel.listMaterials.gameObject.activeSelf, "满级无下一档材料，原区域留空");
                Check(new[] { panel.txtUnitHpDelta, panel.txtDamageDelta, panel.txtRangeDelta, panel.txtSpeedDelta, panel.txtBuildingHpDelta, panel.txtProductionDelta }.All(text => text.text == ""), "满级无下一档增量");
                var tint = panel.btnUpgrade.GetComponent<Image>().color;
                Check(tint.r < .7f && tint.g < .7f && tint.b < .7f, "裁决：满级按钮灰化");
            } else {
                Check(snapshot.materials.Count > 0 && panel.listMaterials.gameObject.activeSelf, "未满级显示实际材料");
                Check(panel.listMaterials.GetComponentsInChildren<CardCultivationMaterialItem>().All(item => !item.txtMaterialName.gameObject.activeSelf && item.txtMaterialName.text == ""), "裁决：材料旁不显示名称或临时图标签");
            }
            var expectedRow = SimpleJSON.JSON.Parse(File.ReadAllText("Library/AIUI/2101-approved-values.json")).AsArray[mode == "max" ? 9 : 0];
            Check(panel.txtUnitHpCurrent.text == expectedRow["unitHp"].AsDouble.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) && panel.txtDamageCurrent.text == expectedRow["attack"].AsDouble.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) && panel.txtRangeCurrent.text == "180" && panel.txtSpeedCurrent.text == expectedRow["moveSpeed"].AsDouble.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) && panel.txtBuildingHpCurrent.text == expectedRow["buildingHp"].AsDouble.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) && panel.txtProductionCurrent.text == (expectedRow["productionMs"].AsDouble / 1000).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "秒", "2101实际培养页六项读数与批准等级一致");
            if (mode == "insufficient") {
                Check(panel.txtUnitHpDelta.text == "+10" && panel.txtDamageDelta.text == "+2" && panel.txtRangeDelta.text == "" && panel.txtSpeedDelta.text == "+1" && panel.txtBuildingHpDelta.text == "+30" && panel.txtProductionDelta.text == "−0.5秒", "2101一级到二级真实增量读取正确");
            }
            if (mode == "unowned") {
                Check(new[] { panel.txtUnitHpDelta, panel.txtDamageDelta, panel.txtRangeDelta, panel.txtSpeedDelta, panel.txtBuildingHpDelta, panel.txtProductionDelta }.All(text => text.text == "") && new[] { panel.txtUnitHpCurrent, panel.txtDamageCurrent, panel.txtRangeCurrent, panel.txtSpeedCurrent, panel.txtBuildingHpCurrent, panel.txtProductionCurrent }.All(text => text.color.r == 1 && text.color.g == 1 && text.color.b == 1), "2101未解锁只白字显示一级六属性，无增量");
            }
            observations.Add("sixValues=" + string.Join("|", new[] { panel.txtUnitHpCurrent.text, panel.txtDamageCurrent.text, panel.txtRangeCurrent.text, panel.txtSpeedCurrent.text, panel.txtBuildingHpCurrent.text, panel.txtProductionCurrent.text }));
            observations.Add("mode=" + mode + ";card=" + cardId + ";actualLevel=" + level + ";label=" + panel.txtLevel.text + ";materials=" + snapshot.materials.Count + ";button=" + panel.txtUpgrade.text);
            GlobalToast.Clear();
            await Frames(3);
            ValidateV11Presentation(panel);
            // 独立表现夹具只切换item状态，随后还原原查询VO；不写正式技能配置或玩家数据。
            foreach (var item in panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Where(value => value.gameObject.activeInHierarchy && value.imgSkillIcon != null)) {
                var original = (CardSkillStateVO)typeof(CardCultivationSkillItem).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);
                item.SetData(0, new CardSkillStateVO { cardId = cardId, slot = cfg.SkillDisplaySlot.Active, unlocked = false });
                Check(item.imgSkillLock.enabled && item.imgSkillIcon.sprite == item.activeTemporaryIcon, "v11显示夹具：锁标叠在内图上，主动占位正确");
                item.Clear();
                Check(!item.imgSkillLock.enabled && item.txtSkillState.text == "", "v11回池清除锁态和状态文字");
                item.SetData(0, original);
                Check(!item.imgSkillLock.enabled && item.txtSkillState.text == "资料待补齐", "v11复用后还原真实缺配置状态");
            }
            var viewport = panel.listMaterials.GetComponent<ScrollRect>().viewport;
            var materialItem = panel.listMaterials.GetComponentsInChildren<CardCultivationMaterialItem>().FirstOrDefault(value => value.gameObject.activeInHierarchy);
            if (materialItem != null) {
                var corners = new Vector3[4];
                ((RectTransform)materialItem.transform).GetWorldCorners(corners);
                Check(Mathf.Abs(viewport.InverseTransformPoint(corners[0]).x - viewport.rect.xMin) < .1f, "v11真实首个消耗项左边缘贴合列表视窗左边缘");
            }
            Capture("cultivation-decision-" + mode + "-before.png");
            long revision = session.model.Revision;
            var balances = CfgManager.tables.ItemObj.DataList.ToDictionary(item => item.Id, item => session.Inventory.model.GetQuantity(item.Id));
            Check(panel.btnUpgrade.interactable, "灰化按钮仍可点击提示");
            Click(panel.btnUpgrade.GetComponent<RectTransform>());
            await GlobalToast.EnsureReadyAsync();
            await Frames(8);
            var messages = GlobalToast.Host.ActiveItems.Select(item => item.Message).ToArray();
            observations.Add("maxClickToast=" + string.Join("|", messages));
            string expected = mode == "max" ? "已升至最大等级" : "材料不足";
            Check(messages.Contains(expected), "裁决：全局提示精确文案=" + expected);
            Check(session.Cards.model.GetState(cardId).level == level && session.model.Revision == revision && balances.All(pair => session.Inventory.model.GetQuantity(pair.Key) == pair.Value), "拒绝操作不改等级、背包、revision");
            Capture("cultivation-decision-" + mode + "-click.png");
        } finally {
            GlobalToast.Shutdown();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);
            functionField.SetValue(null, oldFunctions);
        }
    }


    /**核对真实来源页的列表靠左和复用技能层，不改变业务数据。*/
    private static void ValidateV11Presentation(CardCultivationPanel panel) {
        var materialViewport = panel.listMaterials.GetComponent<ScrollRect>().viewport;
        Check(Mathf.Abs(materialViewport.anchoredPosition.x) < .01f, "v11材料列表首项保持靠左，无数量居中偏移");
        var items = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Where(item => item.gameObject.activeInHierarchy && item.imgSkillIcon != null).ToArray();
        Check(items.Length == 4, "v11实际池化技能项数量为四");
        foreach (var item in items) {
            var black = item.imgSkillFrame.transform.parent.Find("imgSkillBlackBacking");
            Check(black != null && black.GetComponent<Image>().sprite.name == "skill-v11-black", "v11技能黑底为独立图片");
            Check(black.GetSiblingIndex() < item.imgSkillIcon.transform.GetSiblingIndex() && item.imgSkillIcon.transform.GetSiblingIndex() < item.imgSkillFrame.transform.GetSiblingIndex() && item.imgSkillFrame.transform.GetSiblingIndex() < item.imgSkillLock.transform.GetSiblingIndex(), "v11黑底内图金属框锁标层序正确");
            Check(item.imgSkillIcon.rectTransform.sizeDelta == new Vector2(70, 65) && item.imgSkillIcon.rectTransform.anchoredPosition == new Vector2(27, -39), "v11图标匹配70乘65内开口");
            Check(item.imgSkillIcon.enabled && item.imgSkillIcon.sprite != null && item.imgSkillIcon.sprite.rect.size == new Vector2(280, 260), "v11四种视觉样件载入完整比例母图");
            Check(item.imgSkillFrame.sprite.name == "skill-v11-frame" && item.txtSkillState.text == "资料待补齐" && !item.imgSkillLock.enabled, "v11临时图标不伪造正式技能及解锁数据");
        }
    }

    /**只替换隔离成长模型私有成本行，验证真实查询与列表；正式配置不动。*/
    /**核对共享技能效果逐级投影与真实牌库入口，不修改正式技能或玩家存档。*/
    // Read the camera's completed PlayerLoop frame; do not rebuild canvas during a nested Camera.Render.
    [Serializable] private sealed class PodiumCardRecord {
        public int cardId;
        public string cardName;
        public string quality;
        public float[] frame;
        public float[] beforeDraw;
        public float[] candidateDraw;
        public float[] portraitPivot;
        public float[] portraitRect;
        public string sourcePodium;
        public string originalShot;
        public string candidateShot;
    }

    [Serializable] private sealed class PodiumRecords { public PodiumCardRecord[] records; public bool memoryOnly; }

    private static float PodiumSmooth(float a, float b, float x) {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3 - 2 * t);
    }

    private static Sprite[] CreatePodiumStateSprites() {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Check(texture.LoadImage(File.ReadAllBytes("Assets/Art/atlasSource/default/cardCultivation/podium-blue.png")), "现有蓝座台原PNG载入隔离内存");
        var pixels = texture.GetPixels32();
        var states = new Sprite[4];
        var colors = new[] { new Vector3(.08f, 1, .14f), Vector3.zero, new Vector3(.78f, .10f, 1), new Vector3(1, .64f, .04f) };
        for (int state = 0; state < states.Length; state++) {
            var data = (Color32[])pixels.Clone();
            if (state != 1) {
                for (int i = 0; i < data.Length; i++) {
                    float r = pixels[i].r / 255f, g = pixels[i].g / 255f, b = pixels[i].b / 255f;
                    float x = i % texture.width, y = texture.height - 1 - i / texture.width;
                    float dx = (x - 1085.5f) / 1000, dy = (y - 283) / 165;
                    float spatial = 1 - PodiumSmooth(1, 1.08f, dx * dx + dy * dy);
                    foreach (float lampX in new[] { 600f, 1630f }) {
                        spatial = Mathf.Max(spatial, Mathf.Max(0, 1 - Mathf.Max(Mathf.Abs(x - lampX) / 100, Mathf.Abs(y - 527) / 45)));
                    }
                    if (pixels[i].a < 110 && (y < 140 || y > 600)) {
                        spatial = 1;
                    }
                    float weight = spatial * PodiumSmooth(.22f, .52f, g / Mathf.Max(b, .001f)) * PodiumSmooth(.01f, .08f, b - r) * PodiumSmooth(.12f, .36f, b) * PodiumSmooth(.10f, .22f, g);
                    data[i].r = (byte)Mathf.RoundToInt(Mathf.Lerp(pixels[i].r, Mathf.Clamp01(r + (b - r) * colors[state].x) * 255, weight));
                    data[i].g = (byte)Mathf.RoundToInt(Mathf.Lerp(pixels[i].g, Mathf.Clamp01(r + (b - r) * colors[state].y) * 255, weight));
                    data[i].b = (byte)Mathf.RoundToInt(Mathf.Lerp(pixels[i].b, Mathf.Clamp01(r + (b - r) * colors[state].z) * 255, weight));
                }
            }
            Check(data.Where((pixel, i) => pixel.a != pixels[i].a).Count() == 0, "隔离候选四态Alpha逐像素相同=" + state);
            var colored = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            colored.SetPixels32(data);
            colored.Apply();
            states[state] = Sprite.Create(colored, new Rect(0, 0, colored.width, colored.height), new Vector2(.5f, .5f), 100);
            states[state].name = "podium-common-memory-" + state;
        }
        UnityEngine.Object.Destroy(texture);
        return states;
    }

    private static float[] PodiumRect(RectTransform rect) {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        var last = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return new[] { first.x, target.height - last.y, last.x - first.x, last.y - first.y };
    }

    private static float[] PodiumDraw(float[] frame, Sprite sprite) {
        float scale = Mathf.Min(frame[2] / sprite.rect.width, frame[3] / sprite.rect.height);
        return new[] { frame[0] + (frame[2] - sprite.rect.width * scale) / 2, frame[1] + (frame[3] - sprite.rect.height * scale) / 2, sprite.rect.width * scale, sprite.rect.height * scale };
    }

    private static async Task WaitPodiumCard(CardCultivationPanel panel, int id) {
        var state = CardCultivationQuery.Read(CfgManager.tables, PlayerProgressControl.ins, id);
        for (int i = 0; i < 500; i++) {
            var images = new[] { panel.imgPortrait, panel.imgPodium, panel.imgRaceBackdrop, panel.imgQualityWordmark, panel.imgUnitHpAttribute, panel.imgDamageAttribute };
            if (panel.CurrentCardId == id && images.All(image => image.enabled && image.sprite != null)
                && panel.imgPortrait.sprite == ResourceManager.GetUnpackImage(state.portraitAddress)
                && (panel.GetComponent<CultivationExhibitPalette>() != null ? panel.imgPodium.sprite.name == "podium-metal" : panel.imgPodium.sprite == ResourceManager.GetUnpackImage(state.podiumAddress))) {
                await Task.Delay(150);
                return;
            }
            await Frames(2);
        }
        throw new TimeoutException("Podium source load mismatch=" + id);
    }

    /**仅验证已确认的技能序号/两状态文案，不改Prefab或美术资源。*/
    private static async Task RunSkillOrderTextReview(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var functionField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousFunction = functionField.GetValue(null);
        string folder = "Library/AIUI/SkillOrderText/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        functionField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        var scheme = new TeamSchemeSaveVO();
        scheme.schemes[0] = new[] { 2101, 0, 0, 0 };
        var previousTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, scheme, ignored => { }));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(15);
            var main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            var sourceScroll = view.library.GetComponent<ScrollRect>().content.anchoredPosition;
            view.RequestCardInfo(2101);
            var panel = await CultivationOpened();
            await WaitPodiumCard(panel, 2101);
            await Frames(12);
            var rows = panel.listSkills.arraySource.Cast<CardSkillStateVO>().ToArray();
            Check(rows.Length == 4, "正式陆战固定四技能，未减少或追加第五包装");
            Check(rows.Select(v => v.skillId).SequenceEqual(new[] {210101,210102,210103,210104}), "实际列表按技能序号1/2/3/4");
            var cells = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false).OrderBy(v => v.transform.position.x).ToArray();
            Check(cells.Length == 4, "四个真实可见列表项");
            int level = session.Cards.model.GetState(2101).level;
            for (int i = 0; i < 4; i++) {
                bool unlocked = rows[i].unlocked == true && rows[i].currentRank.HasValue;
                string expected = unlocked ? rows[i].currentRank + "阶" : "未解锁";
                Check(cells[i].txtSkillKind.text == CardSkillQuery.TypeName(rows[i].slot), "序号对应实际包装=" + rows[i].skillId);
                Check(cells[i].txtSkillState.text == expected, "等级" + level + "实际状态=" + rows[i].skillId + ":" + expected);
                Check(cells[i].imgSkillLock.enabled == !unlocked, "独立锁标符合状态=" + rows[i].skillId);
                Check(cells[i].imgSkillFrame.rectTransform.sizeDelta == new Vector2(79,79) && cells[i].imgSkillIcon.rectTransform.sizeDelta == new Vector2(55,55), "已审方形框与内图结构=" + rows[i].skillId);
            }
            Check(cells.All(v => v.txtSkillState.text != "资料待补齐"), "列表无开发缺项文案");
            await PrepareStageCapture(panel);
            Capture("runtime-level-" + level + ".png");
            for (int i = 0; i < 4; i++) {
                Click(cells[i].btnSkill.GetComponent<RectTransform>());
                CardSkillDetailPanel detail = null;
                for (int wait = 0; wait < 300 && detail == null; wait++) {
                    await Frames(2);
                    detail = UnityEngine.Object.FindObjectOfType<CardSkillDetailPanel>();
                }
                Check(detail != null, "序号点击打开技能详情=" + rows[i].skillId);
                var request = (CardSkillDetailOpenVO)typeof(CardSkillDetailPanel).GetField("request", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(detail);
                Check(request.cardId == 2101 && request.skillId == rows[i].skillId, "详情身份正确无串位=" + rows[i].skillId);
                CardSkillDetailControl.Close();
                // 窗口关闭含真实过渡；先等待遮罩退场和关闭回调，不在下一次点击前抢跑。
                await Task.Delay(400);
                await Frames(10);
                Check(panel.CurrentCardId == 2101 && cells[i].txtSkillState.text == (rows[i].unlocked == true ? rows[i].currentRank + "阶" : "未解锁"), "关闭恢复原卡和状态=" + rows[i].skillId);
            }
            Click(panel.btnReturn.GetComponent<RectTransform>());
            await Frames(8);
            Check(Vector2.Distance(sourceScroll, view.library.GetComponent<ScrollRect>().content.anchoredPosition)<.1f, "返回原牌库滚动位置");
            view.RequestCardInfo(2101);
            panel = await CultivationOpened();
            await WaitPodiumCard(panel,2101);
            await Frames(8);
            var reopened = panel.listSkills.arraySource.Cast<CardSkillStateVO>().ToArray();
            Check(reopened.Select(v=>v.skillId).SequenceEqual(new[] {210101,210102,210103,210104}), "回池重开序号不变");
        } finally {
            TeamEditControl.InstallValidationSession(previousTeam);
            functionField.SetValue(null, previousFunction);
        }
    }

    /**核查图片台座的正式加载路径，不注入候选Sprite或材质。*/
    private static async Task RunPodiumImagesReview(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var functionsField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = functionsField.GetValue(null);
        string folder = "Library/AIUI/PodiumImages/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        functionsField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(15);
            var main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            view.RequestCardInfo(2101);
            var panel = await CultivationOpened();
            var source = (CardCultivationOpenVO)typeof(CardCultivationPanel).GetField("request", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            var rows = new List<string>();
            var placement = new List<string>();
            for (int index = 0; index < source.orderedCardIds.Length; index++) {
                int id = source.orderedCardIds[index];
                await WaitPodiumCard(panel, id);
                var snapshot = CardCultivationQuery.Read(CfgManager.tables, session, id);
                string path = snapshot.podiumAddress;
                var expected = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Check(expected != null && panel.imgPodium.sprite != null && panel.imgPodium.sprite.texture == expected.texture, "正式品质图片加载=" + id + ":" + path);
                Check(panel.imgPodium.material == panel.imgPodium.defaultMaterial && panel.imgPodium.material.shader.name == "UI/Default", "普通Image默认UI材质=" + id);
                var rect = PodiumRect(panel.imgPodium.rectTransform);
                placement.Add(id + " | podium=" + string.Join(",", rect) + " | portrait=" + string.Join(",", PodiumRect(panel.imgPortrait.rectTransform)));
                Check(rect.Zip(new[] {175f,225.7f + snapshot.podiumOffsetY,370f,123.39f}, (a,b) => Mathf.Abs(a-b)<.1f).All(v=>v), "台座容器坐标不变=" + id);
                Check(snapshot.podiumOffsetY == (id == 2101 ? 8 : 0), "仅已批准陆战台座下移=" + id);
                if (id == 2101) {
                    var art = CfgManager.tables.CardArtObj.Get(snapshot.artId);
                    Check(path.EndsWith("podium-metal.png", StringComparison.Ordinal) && art.CultivationOffsetY == 3, "R4正式无光图片与立绘净下移3px");
                }
                rows.Add(id + " | " + snapshot.quality + " | " + path + " | UI/Default");
                if (index == 0) { await PrepareStageCapture(panel); Capture("runtime-marine.png"); }
                if (index < source.orderedCardIds.Length - 1) { Click(panel.btnNext.GetComponent<RectTransform>()); await Frames(4); }
            }
            // 回到第一张并重新打开，核对普通卡与R4局部偏移互不串用。
            for (int i = 1; i < source.orderedCardIds.Length; i++) { Click(panel.btnPrevious.GetComponent<RectTransform>()); await Frames(1); }
            await WaitPodiumCard(panel, 2101);
            Check(Mathf.Abs(PodiumRect(panel.imgPodium.rectTransform)[1] - 233.7f) < .1f, "快速返回陆战无位置累计");
            Click(panel.btnReturn.GetComponent<RectTransform>()); await Frames(5);
            view.RequestCardInfo(2101); panel = await CultivationOpened(); await WaitPodiumCard(panel, 2101);
            Check(Mathf.Abs(PodiumRect(panel.imgPodium.rectTransform)[1] - 233.7f) < .1f && panel.imgPodium.material == panel.imgPodium.defaultMaterial, "回池重开恢复R4且仍默认UI材质");
            File.WriteAllLines(Path.Combine(SessionState.GetString(Key + ".evidence", ""), "loaded-images.txt"), rows);
            File.WriteAllLines(Path.Combine(SessionState.GetString(Key + ".evidence", ""), "placement.txt"), placement);
        } finally { functionsField.SetValue(null, previous); }
    }

    private static async Task RunPodiumCardsReview(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        string evidence = SessionState.GetString(Key + ".evidence", "");
        var functionsField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousFunctions = functionsField.GetValue(null);
        string folder = "Library/AIUI/PodiumCards/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        functionsField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        var isolatedTeam = new TeamSchemeSaveVO();
        isolatedTeam.schemes[0] = new[] { 2101, 0, 0, 0 };
        var previousTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, isolatedTeam, ignored => { }));
        var candidates = CreatePodiumStateSprites();
        var records = new List<PodiumCardRecord>();
        bool single = SessionState.GetString(Key + ".state", "").EndsWith("-single", StringComparison.Ordinal);
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(15);
            var main = (MainPanel)PanelMgr.ins.GetPanel(mainId);
            Click(main.btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            var sourceScroll = view.library.GetComponent<ScrollRect>();
            var savedScroll = sourceScroll.content.anchoredPosition;
            view.RequestCardInfo(2101);
            var panel = await CultivationOpened();
            var source = (CardCultivationOpenVO)typeof(CardCultivationPanel).GetField("request", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            var ids = source.orderedCardIds;
            Check(source.source == "library" && ids.Length == 16 && ids[0] == 2101, "正常牌库来源含16卡，首张2101");
            for (int index = 0; index < (single ? 1 : ids.Length); index++) {
                int id = ids[index];
                await WaitPodiumCard(panel, id);
                var state = CardCultivationQuery.Read(CfgManager.tables, session, id);
                var original = panel.imgPodium.sprite;
                var frame = PodiumRect(panel.imgPodium.rectTransform);
                Check(Mathf.Abs(frame[0] - 175) < .1f && Mathf.Abs(frame[1] - 225.7f) < .1f && Mathf.Abs(frame[2] - 370) < .1f && Mathf.Abs(frame[3] - 123.39f) < .1f, "实际台座矩形保持批准坐标=" + id);
                var pivot = RectTransformUtility.WorldToScreenPoint(camera, panel.imgPortrait.rectTransform.position);
                var record = new PodiumCardRecord { cardId = id, cardName = state.name, quality = state.quality.ToString(), frame = frame, beforeDraw = PodiumDraw(frame, original), sourcePodium = state.podiumAddress, portraitRect = PodiumRect(panel.imgPortrait.rectTransform), portraitPivot = new[] { pivot.x, target.height - pivot.y }, originalShot = "before-" + id + ".png", candidateShot = "candidate-" + id + ".png" };
                await PrepareStageCapture(panel);
                if (!single) {
                    Capture(record.originalShot);
                }
                int quality = state.quality == cfg.Quality.RARE ? 1 : state.quality == cfg.Quality.EPIC ? 2 : state.quality == cfg.Quality.LEGEND || state.quality == cfg.Quality.LEGEND_PLUS ? 3 : 0;
                panel.imgPodium.sprite = candidates[quality];
                panel.imgPodium.SetAllDirty();
                await Frames(4);
                record.candidateDraw = PodiumDraw(PodiumRect(panel.imgPodium.rectTransform), panel.imgPodium.sprite);
                Check(record.candidateDraw.Zip(new[] { 175f, 225.7f, 370f, 123.39f }, (actual, expected) => Mathf.Abs(actual - expected) < .01f).All(value => value), "同母版实际绘制矩形不随品质变化=" + id);
                var afterPortrait = PodiumRect(panel.imgPortrait.rectTransform);
                Check(afterPortrait.Zip(record.portraitRect, (a, b) => Mathf.Abs(a - b) < .01f).All(value => value), "候选不改变既有立绘坐标及尺寸=" + id);
                Check(panel.imgPodium.rectTransform.pivot == new Vector2(0, 1) && panel.imgPodium.preserveAspect, "台座pivot与等比显示固定=" + id);
                await PrepareStageCapture(panel);
                Capture(record.candidateShot);
                records.Add(record);
                Check(!panel.btnPrevious.interactable == (index == 0) && !panel.btnNext.interactable == (index == ids.Length - 1), "来源首尾不循环=" + id);
                if (!single && index < ids.Length - 1) {
                    Click(panel.btnNext.GetComponent<RectTransform>());
                    await Frames(4);
                }
            }
            if (!single) {
            for (int n = 0; n < 5; n++) {
                Click(panel.btnPrevious.GetComponent<RectTransform>());
                await Frames(1);
            }
            await WaitPodiumCard(panel, ids[ids.Length - 6]);
            Check(panel.CurrentCardId == ids[ids.Length - 6], "快速连续切卡后的形象与实际卡牌匹配，未残留候选内存图");
            }
            Click(panel.btnReturn.GetComponent<RectTransform>());
            await Frames(8);
            Check(Vector2.Distance(sourceScroll.content.anchoredPosition, savedScroll) < .1f, "返回保持牌库原滚动位置");
            view.RequestCardInfo(2101);
            panel = await CultivationOpened();
            await WaitPodiumCard(panel, 2101);
            Check(panel.imgPodium.sprite == ResourceManager.GetUnpackImage(CardCultivationQuery.Read(CfgManager.tables, session, 2101).podiumAddress), "回池重开恢复正式来源，无内存候选泄漏");
            File.WriteAllText(Path.Combine(evidence, "cards.json"), JsonUtility.ToJson(new PodiumRecords { records = records.ToArray(), memoryOnly = true }, true));
            observations.Add("16 actual library cards; all level6 isolated; before and candidate paired; candidate Sprite injection only; production assets/config/prefab unchanged");
        } finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            functionsField.SetValue(null, previousFunctions);
            TeamEditControl.InstallValidationSession(previousTeam);
            foreach (var sprite in candidates) {
                UnityEngine.Object.Destroy(sprite.texture);
                UnityEngine.Object.Destroy(sprite);
            }
        }
    }

    private static void DumpStageGraphics(CardCultivationPanel panel, string phase) {
        var lines = panel.GetComponentsInChildren<Graphic>(true).Where(g => !g.transform.IsChildOf(panel.listStages.transform)).Select(g => g.name + ";active=" + g.gameObject.activeInHierarchy + ";enabled=" + g.enabled + ";cull=" + g.canvasRenderer.cull + ";rect=" + g.rectTransform.rect + ";position=" + g.transform.position + ";clip=" + g.canvasRenderer.hasRectClipping + ";vertices=" + g.canvasRenderer.GetAlpha());
        File.WriteAllLines(Path.Combine(SessionState.GetString(Key + ".evidence", ""), "graphics-" + phase + ".txt"), lines);
    }
    private static void CaptureRenderedStageFrame(string name) {
        Capture(name);
    }

    private static async Task PrepareStageCapture(CardCultivationPanel panel) {
        await Frames(4);
        await Task.Delay(250);
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>()) {
            if (graphic.enabled) {
                graphic.enabled = false;
                graphic.enabled = true;
            }
            graphic.SetAllDirty();
        }
        Canvas.ForceUpdateCanvases();
        await Task.Delay(500);
    }
    private static async Task RunSkillStagesReview(PlayerProgressControl session) {
        // 历史请求兼容路由；旧“八条技能阶段”断言已由独立等级效果语义验证替代。
        await RunCardLevelEffectsReview(session);
    }

    private static async Task RunMaterialReview(PlayerProgressControl session) {
        const int cardId = 2101;
        const string mainId = "default/mainView/MainPanel";
        var saved = new TeamSchemeSaveVO();
        saved.schemes[0] = new[] { cardId, 0, 0, 0 };
        var oldTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, saved, ignored => { }));
        var functionField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldFunctions = functionField.GetValue(null);
        string folder = "Library/AIUI/CultivationMaterials/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        functionField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        var levels = (Dictionary<string, cfg.resource.CardLevelResource>)typeof(CardCollectionModel).GetField("levels", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session.Cards.model);
        var original = levels["2101:2"];
        var refresh = typeof(CardCultivationPanel).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(15);
            var main = PanelMgr.ins.GetPanel(mainId) as MainPanel;
            Click(main.btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            await Frames(8);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            view.RequestCardInfo(cardId);
            var panel = await CultivationOpened();
            for (int frame = 0; frame < 300 && (!panel.imgPortrait.enabled || panel.imgPortrait.sprite == null); frame++) {
                await Frames(2);
            }
            foreach (int count in new[] { 1, 2, 3, 2, 1 }) {
                string extras = count == 1 ? "[]" : count == 2 ? "[{\"itemId\":2001,\"count\":30}]" : "[{\"itemId\":2001,\"count\":30},{\"itemId\":2002,\"count\":20}]";
                levels["2101:2"] = new cfg.resource.CardLevelResource(SimpleJSON.JSON.Parse("{\"cardId\":2101,\"level\":2,\"fragmentCount\":" + original.FragmentCount + ",\"extraCosts\":" + extras + "}"));
                refresh.Invoke(panel, null);
                await Frames(8);
                CheckMaterialGeometry(panel, session, count);
                Capture("materials-" + count + "-items.png");
                if (count == 3) {
                    Click(panel.btnReturn.GetComponent<RectTransform>());
                    await Frames(8);
                    view.RequestCardInfo(cardId);
                    panel = await CultivationOpened();
                    await Frames(10);
                    CheckMaterialGeometry(panel, session, count);
                    Capture("materials-3-items-reopened.png");
                }
                long revision = session.model.Revision;
                int level = session.Cards.model.GetState(cardId).level;
                var balances = CfgManager.tables.ItemObj.DataList.ToDictionary(item => item.Id, item => session.Inventory.model.GetQuantity(item.Id));
                Click(panel.btnUpgrade.GetComponent<RectTransform>());
                await GlobalToast.EnsureReadyAsync();
                await Frames(8);
                Check(GlobalToast.Host.ActiveItems.Any(item => item.Message == "材料不足"), "材料夹具缺料仍走正式拒绝提示=" + count);
                Check(session.model.Revision == revision && session.Cards.model.GetState(cardId).level == level && balances.All(pair => session.Inventory.model.GetQuantity(pair.Key) == pair.Value), "材料夹具拒绝不改背包、等级或revision=" + count);
                GlobalToast.Clear();
            }
            var podiumRect = panel.imgPodium.rectTransform;
            Check(podiumRect.anchoredPosition == new Vector2(175, -225.7f) && Vector2.Distance(podiumRect.sizeDelta, new Vector2(370, 123.39f)) < .01f, "材料修改不带入Figma台座下移12像素");
            observations.Add("fixture only: extra costs2001/30 and2002/20; inventory73 and987; no production cost table changed");
        } finally {
            levels["2101:2"] = original;
            GlobalToast.Shutdown();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);
            functionField.SetValue(null, oldFunctions);
        }
    }

    private static void CheckMaterialGeometry(CardCultivationPanel panel, PlayerProgressControl session, int count) {
        var state = CardCultivationQuery.Read(CfgManager.tables, session, 2101);
        Check(state.materials.Count == count, "隔离实际查询材料项数=" + count);
        var viewport = panel.listMaterials.GetComponent<ScrollRect>().viewport;
        var items = panel.listMaterials.GetComponentsInChildren<CardCultivationMaterialItem>().Where(item => item.gameObject.activeInHierarchy && item.imgMaterialIcon != null).OrderBy(item => item.transform.position.x).ToArray();
        Check(items.Length == count && Mathf.Abs(viewport.anchoredPosition.x) < .01f, "运行列表项数正确且整体靠左=" + count);
        var current = state.materials.OrderBy(material => material.itemId).ToArray();
        for (int i = 0; i < count; i++) {
            var item = items[i];
            var root = (RectTransform)item.txtMaterialOwned.transform.parent;
            Check(root.anchoredPosition == new Vector2(6, -70) && root.sizeDelta == new Vector2(88, 26), "数字容器静态右界94和顶70=" + count + "/" + i);
            var labels = new[] { item.txtMaterialOwned, item.txtMaterialSlash, item.txtMaterialRequired };
            Check(labels.All(label => label.fontSize == 22 && (label.fontStyle & FontStyles.Bold) != 0 && label.rectTransform.sizeDelta.y == 26), "22号粗体行高26=" + count + "/" + i);
            var required = item.txtMaterialRequired.rectTransform;
            Check(Mathf.Abs(root.anchoredPosition.x + required.anchoredPosition.x + required.sizeDelta.x - 94) < .01f, "按文字测量后的数量组合仍右对齐94=" + count + "/" + i);
            Check(item.txtMaterialOwned.text == current[i].owned.ToString() && item.txtMaterialRequired.text == current[i].required.ToString(), "真实隔离余额及成本显示=" + count + "/" + i);
            Check(!item.txtMaterialName.gameObject.activeSelf, "材料名称继续隐藏=" + count + "/" + i);
            var corners = new Vector3[4];
            ((RectTransform)item.transform).GetWorldCorners(corners);
            float left = viewport.InverseTransformPoint(corners[0]).x - viewport.rect.xMin;
            float right = viewport.InverseTransformPoint(corners[2]).x - viewport.rect.xMin;
            Check(Mathf.Abs(left - i * 118) < .1f && right <= 365.1f, "材料项位置和容量正确，无越过升级分隔线=" + count + "/" + i);
        }
    }

    private static void ResizeViewportStage(int width, int height) {
        camera.targetTexture = null;
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        target = new RenderTexture(width, height, 24);
        target.Create();
        camera.targetTexture = target;
        camera.orthographicSize = height * .5f;
        UIScreenRegions.EditorScreenOverride = new Rect(0, 0, width, height);
        UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(0, 0, width, height) };
        Canvas.ForceUpdateCanvases();
    }

    private static Rect ScreenBounds(RectTransform rect) {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static async Task RunViewportCoreReview() {
        var previousScreen = UIScreenRegions.EditorScreenOverride;
        var previous = UIScreenRegions.EditorOverride;
        GameObject go = null;
        try {
            foreach (var size in new[] { new Vector2Int(720,1280), new Vector2Int(720,1600), new Vector2Int(1280,720) }) {
                ResizeViewportStage(size.x, size.y);
                await Frames(5);
                go = new GameObject("SafeContentAlignmentValidation", typeof(RectTransform));
                var root = go.GetComponent<RectTransform>();
                root.SetParent(canvas.transform, false);
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                var fitter = go.AddComponent<PanelSafeContent>();
                foreach (var design in new[] { new Vector2(720,1280), new Vector2(1024,768) }) {
                    fitter.Initialize(Array.Empty<string>(), design);
                    await Frames(2);
                    var area = UIScreenRegions.ContentArea;
                    var centered = ScreenBounds(fitter.content);
                    Check(Vector2.Distance(centered.center, area.center) < .2f, "共享对齐默认居中不变=" + size + "/" + design);
                    int children = root.childCount;
                    fitter.Initialize(Array.Empty<string>(), design, new Vector2(.5f, 0));
                    await Frames(2);
                    var bottom = ScreenBounds(fitter.content);
                    Check(Mathf.Abs(bottom.yMin - area.yMin) < .2f && Mathf.Abs(bottom.center.x-area.center.x)<.2f, "显式靠底且水平居中=" + size + "/" + design);
                    fitter.Initialize(Array.Empty<string>(), design, new Vector2(.5f, 0));
                    await Frames(2);
                    Check(root.childCount == children && go.GetComponents<PanelSafeContent>().Length == 1 && Mathf.Abs(ScreenBounds(fitter.content).yMin-bottom.yMin)<.1f, "重复初始化不叠容器或位移=" + size + "/" + design);
                    fitter.Initialize(Array.Empty<string>(), design);
                    await Frames(2);
                    Check(Vector2.Distance(ScreenBounds(fitter.content).center, area.center) < .2f, "恢复默认居中无残留=" + size + "/" + design);
                }
                UnityEngine.Object.DestroyImmediate(go);
                go = null;
            }
        } finally {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
            ResizeViewportStage(720,1280);
            UIScreenRegions.EditorScreenOverride = previousScreen;
            UIScreenRegions.EditorOverride = previous;
        }
    }

    private static async Task RunCultivationViewportReview(bool beforeFix) {
        const string mainId = "default/mainView/MainPanel";
        var oldScreen = UIScreenRegions.EditorScreenOverride;
        var oldRegions = UIScreenRegions.EditorOverride;
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(15);
            var main = PanelMgr.ins.GetPanel(mainId) as MainPanel;
            Click(main.btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            await Frames(8);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            var names = new[] { "base", "long", "safe", "capsule", "combined", "compact", "ultra", "restored" };
            var heights = new[] { 1280, 1600, 1600, 1600, 1600, 1136, 1920, 1280 };
            foreach (var name in names) {
                int index = Array.IndexOf(names, name);
                ResizeViewportStage(720, heights[index]);
                if (name == "safe") UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(0,32,720,1504) };
                if (name == "capsule") UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(0,0,720,1600), menu = new Rect(516,1480,180,64) };
                if (name == "combined") UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(24,32,672,1504), menu = new Rect(516,1480,180,64) };
                await Frames(6);
                view.RequestCardInfo(2101);
                var panel = await CultivationOpened();
                for (int frame = 0; frame < 300 && (!panel.imgRaceBackdrop.enabled || panel.imgRaceBackdrop.sprite == null || !panel.imgPortrait.enabled); frame++) await Frames(2);
                await Frames(8);
                var safe = UIScreenRegions.ContentArea;
                var body = ScreenBounds(panel.SafeContentRoot);
                var backdrop = ScreenBounds(panel.imgRaceBackdrop.rectTransform);
                observations.Add(name + ";body=" + body + ";safe=" + safe + ";backdrop=" + backdrop + ";bgSprite=" + panel.imgRaceBackdrop.sprite.rect.size + ";screen=" + target.width + "x" + target.height);
                if (!beforeFix) {
                    Check(Mathf.Abs(body.yMin-safe.yMin)<.3f && Mathf.Abs(body.center.x-safe.center.x)<.3f, "培养主体靠可用区底部且水平居中=" + name);
                    Check(body.xMin>=safe.xMin-.3f && body.xMax<=safe.xMax+.3f && body.yMax<=safe.yMax+.3f, "完整主体位于安全区和胶囊下方=" + name);
                    Check(backdrop.xMin<=.3f && backdrop.xMax>=target.width-.3f && backdrop.yMax>=target.height-.3f, "延展背景覆盖视窗顶部和两侧=" + name);
                    Check(panel.imgRaceBackdrop.transform.parent == panel.transform && panel.imgRaceBackdrop.sprite.rect.height>=862 && panel.imgRaceBackdrop.sprite.border.y==414 && (panel.imgRaceBackdrop.type==Image.Type.Simple || panel.imgRaceBackdrop.type==Image.Type.Sliced), "背景独立全屏且保留底部图像带=" + name);
                    Check(panel.GetComponents<PanelSafeContent>().Length==1 && panel.SafeContentRoot.parent==panel.transform, "公共安全容器单层无重复=" + name);
                    Check(panel.listMaterials.GetComponentsInChildren<CardCultivationMaterialItem>().Where(item=>item.gameObject.activeInHierarchy).All(item=>item.txtMaterialOwned.fontSize==22), "适配不回退材料22字号=" + name);
                    foreach (var button in new[] {panel.btnReturn,panel.btnPrevious,panel.btnNext,panel.btnUpgrade}) {
                        var bounds=ScreenBounds(button.GetComponent<RectTransform>());
                        Check(bounds.xMin>=safe.xMin-.5f && bounds.xMax<=safe.xMax+.5f && bounds.yMin>=safe.yMin-.5f && bounds.yMax<=safe.yMax+.5f, "实际交互按钮完整避让="+name+"/"+button.name);
                    }
                    Click(panel.btnNext.GetComponent<RectTransform>());
                    await Frames(8);
                    Check(panel.CurrentCardId==2102, "适配后的实际切卡命中="+name);
                    Click(panel.btnPrevious.GetComponent<RectTransform>());
                    await Frames(8);
                    Check(panel.CurrentCardId==2101, "适配后的返回上一卡命中="+name);
                }
                for (int frame = 0; frame < 600; frame++) {
                    var images = new[] { panel.imgPortrait,panel.imgPodium,panel.imgQualityWordmark,panel.imgRaceBackdrop,panel.imgUnitHpAttribute,panel.imgDamageAttribute,panel.imgRangeAttribute,panel.imgSpeedAttribute,panel.imgBuildingHpAttribute,panel.imgProductionAttribute };
                    var basic = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().FirstOrDefault(item => item.gameObject.activeInHierarchy && item.txtSkillKind.text == "普攻");
                    if (images.All(image=>image.enabled && image.sprite!=null) && basic!=null && basic.imgSkillIcon.enabled && basic.imgSkillIcon.sprite!=null) break;
                    await Frames(2);
                }
                Check(new[] { panel.imgPortrait,panel.imgPodium,panel.imgQualityWordmark,panel.imgRaceBackdrop,panel.imgUnitHpAttribute }.All(image=>image.enabled && image.sprite!=null), "截图前形象与背景等异步资源已到齐="+name);
                observations.Add(name+";spriteBorder="+panel.imgRaceBackdrop.sprite.border+";imagePPU="+panel.imgRaceBackdrop.pixelsPerUnit+";root="+panel.trans.rect+";bgRect="+panel.imgRaceBackdrop.rectTransform.rect);
                await Frames(3);
                // 异步Tick不等同完整PlayerLoop；离屏拍摄前让真实LateUpdate和Canvas批次完成。
                await Task.Delay(180);
                panel.GetComponent<PanelSafeContent>().Apply();
                if (panel.GetComponent<CultivationViewportLayout>() != null) panel.GetComponent<CultivationViewportLayout>().Apply();
                foreach (var graphic in panel.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                await Task.Delay(80);
                Capture((beforeFix ? "before-" : "after-") + name + ".png");
                Click(panel.btnReturn.GetComponent<RectTransform>());
                await Frames(8);
                Check(PanelMgr.ins.GetPanel(CardCultivationControl.PanelId)==null || !PanelMgr.ins.GetPanel(CardCultivationControl.PanelId).isOpened, "实际返回命中并关闭="+name);
            }
        } finally {
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            ResizeViewportStage(720,1280);
            UIScreenRegions.EditorScreenOverride=oldScreen;
            UIScreenRegions.EditorOverride=oldRegions;
        }
    }

    private static void ValidateTemporaryStats(cfg.Tables tables) {
        var expected = SimpleJSON.JSON.Parse(File.ReadAllText("Library/AIUI/cultivation-temp-values.json")).AsArray;
        var fields = new Dictionary<int, string> { { 1, "unitHp" }, { 2, "attack" }, { 3, "attackRange" }, { 4, "moveSpeed" }, { 5, "buildingHp" }, { 6, "productionMs" } };
        foreach (SimpleJSON.JSONNode row in expected) {
            int cardId = row["cardId"].AsInt;
            int level = row["level"].AsInt;
            var save = new PlayerProgressSaveVO { initialized = true };
            save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = level });
            int writes = 0;
            var player = new PlayerProgressControl(tables, save, ignored => writes++);
            writes = 0;
            var state = CardCultivationQuery.Read(tables, player, cardId);
            foreach (var field in fields) {
                var attribute = state.attributes.Single(value => value.attributeId == field.Key);
                Check(attribute.current.HasValue && Math.Abs(attribute.current.Value - row[field.Value].AsDouble) < .000001, "临时属性查询=" + cardId + "/" + level + "/" + field.Key);
            }
            Check(state.power.HasValue && state.power.Value == row["power"].AsDouble, "临时战力公式结果=" + cardId + "/" + level);
            Check(writes == 0 && !tables.CardObj.Get(cardId).BattleEnabled && tables.CardObj.Get(cardId).Content is cfg.UnconfiguredCardContent, "验证数据只读且战斗未启用=" + cardId + "/" + level);
        }
        Check(expected.Count == 160, "16卡各10级验证记录完整");
    }

    /**逐行核对用户批准值和实际培养查询；不打开战斗，也不写入进度。*/
    private static void ValidateApprovedSample(cfg.Tables tables) {
        var expected = SimpleJSON.JSON.Parse(File.ReadAllText("Library/AIUI/2101-approved-values.json")).AsArray;
        var fields = new Dictionary<int, string> { { 1, "unitHp" }, { 2, "attack" }, { 3, "attackRange" }, { 4, "moveSpeed" }, { 5, "buildingHp" }, { 6, "productionMs" } };
        for (int level = 1; level <= 10; level++) {
            var save = new PlayerProgressSaveVO { initialized = true };
            save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = 2101, level = level });
            int writes = 0;
            var player = new PlayerProgressControl(tables, save, ignored => writes++);
            writes = 0;
            var state = CardCultivationQuery.Read(tables, player, 2101);
            foreach (var field in fields) {
                var value = state.attributes.Single(row => row.attributeId == field.Key);
                Check(value.current.HasValue && Math.Abs(value.current.Value - expected[level - 1][field.Value].AsDouble) < .000001 && (level == 10 ? !value.target.HasValue : value.target.HasValue && Math.Abs(value.target.Value - expected[level][field.Value].AsDouble) < .000001), "2101逐级查询：等级" + level + "属性" + field.Key);
            }
            Check(writes == 0 && !tables.CardObj.Get(2101).BattleEnabled && tables.CardObj.Get(2101).Content is cfg.UnconfiguredCardContent, "2101等级" + level + "只读且维持战斗禁入");
        }
        Check(tables.CardUnitLevelObj.DataList.Where(row => row.CardId == 1101).All(row => !row.Attack.HasValue && !row.AttackRange.HasValue), "旧1101独立攻击力和范围保持未配置，不用普攻伤害补值");
        Check(tables.CardUnitLevelObj.DataList.Count(row => row.CardId == 2101) == 10 && tables.CardBuildingLevelObj.DataList.Count(row => row.CardId == 2101) == 10, "2101单位建筑各10条，无重复或缺级");
    }

    private static async Task Tick() {
        RookieEngine.timer.SyncTime(16);
        Canvas.ForceUpdateCanvases();
        await Task.Yield();
        RookieEngine.timer.SyncTime(16);
        Canvas.ForceUpdateCanvases();
    }

    private static GameObject Click(RectTransform rect) {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0) {
            throw new InvalidOperationException("No raycast hit: " + rect.name);
        }
        var hit = hits[0].gameObject;
        pointer.pointerCurrentRaycast = hits[0];
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
        return hit;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, Vector2 position, Vector2 size, string value, int fontSize) {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.rectTransform.sizeDelta = size;
        text.rectTransform.anchoredPosition = position;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/font/tmpFontAsset/common/fontBodyCommon.asset");
        text.fontSize = fontSize;
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void Capture(string name) {
        Canvas.ForceUpdateCanvases();
        foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>()) {
            text.ForceMeshUpdate();
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        byte[] bytes = image.EncodeToPNG();
        File.WriteAllBytes("Library/AIUI/" + name, bytes);
        string evidence = SessionState.GetString(Key + ".evidence", "");
        if (!string.IsNullOrEmpty(evidence)) { Directory.CreateDirectory(evidence); File.WriteAllBytes(Path.Combine(evidence, name), bytes); }
        UnityEngine.Object.Destroy(image);
        RenderTexture.active = previous;
    }

    private static void Check(bool passed, string message) {
        if (!passed) {
            throw new InvalidOperationException("CultivationMaxReview: " + message);
        }
        checks.Add(message);
    }

    private static void WriteResult(string id, Exception error) {
        Directory.CreateDirectory("Library/AIUI");
        string json = JsonUtility.ToJson(new Result { requestId = id, status = error == null ? "passed" : "failed", error = error?.ToString(), checks = checks.ToArray(), observations = observations.ToArray(), deviations = deviations.ToArray() }, true);
        File.WriteAllText(ResultPath, json);
        string evidence = SessionState.GetString(Key + ".evidence", "");
        if (!string.IsNullOrEmpty(evidence)) { Directory.CreateDirectory(evidence); File.WriteAllText(Path.Combine(evidence, id + ".json"), json); }
    }

}
#endif
