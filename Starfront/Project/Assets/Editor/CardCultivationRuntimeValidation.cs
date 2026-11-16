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
/// 技能详情专项的隔离Play验证，不读写真实玩家进度。
/// </summary>
/// <remarks>阶数样例仅在Editor内构造，正式数值始终从CfgManager加载。</remarks>
[InitializeOnLoad]
public static partial class CardCultivationRuntimeValidation {

    private const string Key = "CardCultivation.Runtime.v1";

    private const string Pending = "Library/AIUI/pending-card-cultivation-runtime.json";

    private const string ResultPath = "Library/AIUI/card-cultivation-runtime-result.json";

    [Serializable] private sealed class Request { public string requestId; public int processId; public bool atlasCleanup2; public bool resourceGovernanceB1; public bool unifiedToast; public bool skillSmoke; public bool rankDisplay; public bool rankProgress; public bool marineBasic; public bool marineExtra; public bool humanSkills; public bool remainingSkills; public bool skillAlignment; public bool renderComparison; public bool normalWindow; public string evidenceDirectory; }

    [Serializable] private sealed class Result { public string requestId; public string status; public string error; public string[] checks; }

    [Serializable] private sealed class SceneBackup { public RootState[] roots; }

    [Serializable] private sealed class RootState { public int scene; public int index; public string name; public bool active; }

    private static readonly List<string> checks = new List<string>();

    private static string runtimeError;

    private static Camera camera;

    private static RenderTexture target;

    private static Canvas canvas;

    static CardCultivationRuntimeValidation() {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnState;
    }

    private static IEnumerable<string> CfgManagerImagePaths() {
        var data = SimpleJSON.JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_attributedefinitionobj.json"));
        foreach (SimpleJSON.JSONNode row in data.AsArray) {
            yield return "Assets/Art/atlasSource/default/cardCultivation/" + row["icon"].Value + ".png";
        }
        // Skill icons are enumerated below from the authoritative configuration, without type-based remapping.
        var skills = SimpleJSON.JSON.Parse(File.ReadAllText("Assets/ConfigBin/cfgobj_cultivationskillobj.json"));
        foreach (SimpleJSON.JSONNode row in skills.AsArray) {
            if (!string.IsNullOrWhiteSpace(row["icon"].Value)) {
                string address = row["icon"].Value;
                yield return address.StartsWith(ResourceConst.PATH_ATLAS, StringComparison.Ordinal)
                    ? address.Substring(0, address.LastIndexOf('/')) + ".png" : address;
            }
        }
    }

    private static void Poll() {
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
            if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId)) {
                throw new InvalidOperationException("Request identity mismatch");
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (string id in new[] { "default/mainView/MainPanel", CardCultivationControl.PanelId, CardSkillDetailControl.PanelId }) {
                string path = "Assets/Prefab/" + id + ".prefab";
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.FindGroup("Default Local Group"));
                entry.SetAddress(path);
            }
            // 配置驱动的散图按实际加载地址登记，供Editor真实入口联验；不构建Player Content。
            var imagePaths = new[] { "Assets/Art/unpack/default/cardCultivation/temp-hero-background.png", "Assets/Art/unpack/default/cardCultivation/temp-podium-ring.png" }.Concat(CfgManagerImagePaths());
            foreach (string path in imagePaths) {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) {
                    throw new InvalidOperationException("培养图标资源缺失：" + path);
                }
                settings.CreateOrMoveEntry(guid, settings.FindGroup("Default Local Group")).SetAddress(path);
            }
            EditorUtility.SetDirty(settings);
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
                if (!request.normalWindow) {
                    root.SetActive(false);
                }
            }
            SessionState.SetInt(Key + ".mode", settings.ActivePlayModeDataBuilderIndex);
            settings.ActivePlayModeDataBuilderIndex = 0;
            SessionState.SetString(Key, request.requestId);
            SessionState.SetBool(Key + ".atlasCleanup2", request.atlasCleanup2);
            SessionState.SetBool(Key + ".resourceGovernanceB1", request.resourceGovernanceB1);
            SessionState.SetBool(Key + ".unifiedToast", request.unifiedToast);
            SessionState.SetBool(Key + ".skillSmoke", request.skillSmoke);
            SessionState.SetBool(Key + ".rankDisplay", request.rankDisplay);
            SessionState.SetBool(Key + ".rankProgress", request.rankProgress);
            SessionState.SetBool(Key + ".marineBasic", request.marineBasic);
            SessionState.SetBool(Key + ".marineExtra", request.marineExtra);
            SessionState.SetBool(Key + ".humanSkills", request.humanSkills);
            SessionState.SetBool(Key + ".remainingSkills", request.remainingSkills);
            SessionState.SetBool(Key + ".skillAlignment", request.skillAlignment);
            SessionState.SetBool(Key + ".renderComparison", request.renderComparison);
            SessionState.SetBool(Key + ".normalWindow", request.normalWindow);
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
        if (SessionState.GetBool(Key + ".normalWindow", false)) {
            await RunNormalWindowComparison(id);
            return;
        }
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        checks.Clear();
        runtimeError = null;
        var previousRegions = UIScreenRegions.EditorOverride;
        PlayerProgressControl previousSession = null;
        bool installed = false;
        var timerField = typeof(RookieEngine).GetField("_timer", BindingFlags.Static | BindingFlags.NonPublic);
        object previousTimer = timerField.GetValue(null);
        var clockFields = new[] { "_sceneTimer", "_playerTimer", "_enemyTimer" }.Select(name => typeof(RookieEngine).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
        var previousClocks = clockFields.Select(field => field.GetValue(null)).ToArray();
        try {
            new GameObject("SkillValidationDispatcher", typeof(UnityMainThreadDispatcher));
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
            PlayerProgressSaveVO initial = null;
            if (SessionState.GetBool(Key + ".unifiedToast", false) || SessionState.GetBool(Key + ".skillSmoke", false) || SessionState.GetBool(Key + ".rankDisplay", false)) {
                int cardId = CfgManager.tables.CardObj.DataList.First(card => card.ShowInLibrary && card.MaxLevel > 1).Id;
                initial = new PlayerProgressSaveVO { initialized = true };
                initial.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = 1 });
            }
            var session = new PlayerProgressControl(CfgManager.tables, initial, ignored => writes++);
            writes = 0; // 新会话的初始种子仅写入内存回调，验证从初始化完成后计数。
            previousSession = PlayerProgressControl.InstallValidationSession(session);
            installed = true;
            BuildStage();
            UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(0, 0, 720, 1280) };
            if (SessionState.GetBool(Key + ".resourceGovernanceB1", false)) await RunGovernanceB1DetailImages();
            if (SessionState.GetBool(Key + ".atlasCleanup2", false)) {
                await RunAtlasCleanup2();
            } else if (SessionState.GetBool(Key + ".renderComparison", false)) {
                await RunComparisonOnInstalledStage(false);
            } else if (SessionState.GetBool(Key + ".remainingSkills", false)) {
                await RunRemainingSkills();
            } else if (SessionState.GetBool(Key + ".humanSkills", false)) {
                await RunHumanSkills();
            } else if (SessionState.GetBool(Key + ".marineExtra", false)) {
                await RunMarineExtraSkills();
            } else if (SessionState.GetBool(Key + ".marineBasic", false)) {
                await RunMarineBasic();
            } else if (SessionState.GetBool(Key + ".rankProgress", false)) {
                await RunIsolatedRankProgression();
            } else if (SessionState.GetBool(Key + ".rankDisplay", false)) {
                await RunIsolatedRankDisplay(session);
            } else if (SessionState.GetBool(Key + ".skillSmoke", false)) {
                await RunCurrentSkillSmoke(session);
            } else if (SessionState.GetBool(Key + ".unifiedToast", false)) {
                await RunUnifiedToastMigration(session);
            } else {
                await Run(session);
                await ValidateUnownedPreview();
            }
            if (SessionState.GetBool(Key + ".atlasCleanup2", false) || SessionState.GetBool(Key + ".remainingSkills", false) || SessionState.GetBool(Key + ".humanSkills", false) || SessionState.GetBool(Key + ".renderComparison", false) || SessionState.GetBool(Key + ".skillSmoke", false) || SessionState.GetBool(Key + ".rankDisplay", false) || SessionState.GetBool(Key + ".rankProgress", false) || SessionState.GetBool(Key + ".marineBasic", false) || SessionState.GetBool(Key + ".marineExtra", false)) {
                Check(writes == 0, "技能入口试跑未写入隔离进度或真实存档");
            } else {
                Check(writes > 0, "成长变更仅写入隔离内存回调，不访问真实玩家进度");
            }
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
        var root = new GameObject("SkillDetailValidationCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvas = root.GetComponent<Canvas>();
        camera = new GameObject("SkillDetailValidationCamera", typeof(Camera)).GetComponent<Camera>();
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

    /**仅在隔离内存会话把1101设为未拥有；展示值仍来自正式1级配置。*/
    private static async Task ValidateUnownedPreview() {
        int writes = 0;
        var preview = new PlayerProgressControl(CfgManager.tables, new PlayerProgressSaveVO { initialized = true }, ignored => writes++);
        writes = 0;
        var previous = PlayerProgressControl.InstallValidationSession(preview);
        try {
            await CardCultivationControl.OpenAsync(new CardCultivationOpenVO { cardId = 1101, orderedCardIds = new[] { 1101 }, owner = canvas, source = "editor-unowned-preview" });
            var panel = await CultivationOpened();
            for (int frame = 0; frame < 300 && (!panel.imgRaceBackdrop.enabled || !panel.imgPortrait.enabled || !panel.imgUnitHpAttribute.enabled); frame++) {
                await Frames(2);
            }
            Check(panel.imgRaceBackdrop.enabled && panel.imgRaceBackdrop.sprite != null && panel.imgPortrait.enabled && panel.imgPortrait.sprite != null && panel.imgUnitHpAttribute.enabled, "v7未拥有预览：等待动态资源完成后再截图");
            Check(panel.txtUpgrade.text == "解锁" && panel.txtUnitHpCurrent.text == "100" && panel.txtBuildingHpCurrent.text == "300" && panel.txtProductionCurrent.text == "10秒", "v7未拥有预览：只读真实1级值及解锁按钮");
            var current = new[] { panel.txtUnitHpCurrent, panel.txtDamageCurrent, panel.txtRangeCurrent, panel.txtSpeedCurrent, panel.txtBuildingHpCurrent, panel.txtProductionCurrent };
            var deltas = new[] { panel.txtUnitHpDelta, panel.txtDamageDelta, panel.txtRangeDelta, panel.txtSpeedDelta, panel.txtBuildingHpDelta, panel.txtProductionDelta };
            Check(current.All(label => label.color.r == 1 && label.color.g == 1 && label.color.b == 1) && deltas.All(label => label.text == ""), "v7未拥有预览：六项白字且没有升级增量");
            Check(preview.Cards.model.GetState(1101).level == 0 && writes == 0, "v7未拥有预览：无解锁、扣料或存档写入");
            Capture("cultivation-unowned-preview.png");
        } finally {
            GlobalToast.Shutdown();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PlayerProgressControl.InstallValidationSession(previous);
        }
    }

    private static async Task ValidateSkillStates() {
        var tablesField = typeof(CfgManager).GetField("_tables", BindingFlags.Static | BindingFlags.NonPublic);
        object previousTables = tablesField.GetValue(null);
        var fixture = new cfg.Tables(name => {
            var json = SimpleJSON.JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json"));
            if (name == "cfgobj_cultivationskillobj") {
                return SimpleJSON.JSON.Parse("[{\"id\":90001,\"name\":\"Editor已解锁技能夹具\",\"kind\":1,\"slot\":0,\"icon\":\"\"},{\"id\":90002,\"name\":\"Editor锁定技能夹具\",\"kind\":2,\"slot\":1,\"icon\":\"\"}]");
            }
            if (name == "cfgobj_cardskillentryobj") {
                return SimpleJSON.JSON.Parse("[{\"cardId\":1101,\"skillId\":90001,\"sortOrder\":1},{\"cardId\":1101,\"skillId\":90002,\"sortOrder\":2}]");
            }
            if (name == "cfgobj_cardskillrankobj") {
                return SimpleJSON.JSON.Parse("[{\"cardId\":1101,\"skillId\":90001,\"rank\":1,\"unlockLevel\":1,\"battleSkillId\":1101},{\"cardId\":1101,\"skillId\":90002,\"rank\":1,\"unlockLevel\":5,\"battleSkillId\":1101}]");
            }
            if (name == "cfgobj_cardlevelobj") {
                foreach (SimpleJSON.JSONNode row in json.AsArray) {
                    if (row["cardId"].AsInt == 1101 && row["level"].AsInt == 2) {
                        row["extraCosts"] = SimpleJSON.JSON.Parse("[{\"itemId\":3102,\"count\":1},{\"itemId\":3103,\"count\":1},{\"itemId\":3104,\"count\":1}]");
                    }
                }
                return json;
            }
            if (name == "cfgobj_cardgrowthstageobj") {
                var stages = new SimpleJSON.JSONArray();
                for (int i = 1; i <= 8; i++) {
                    var row = new SimpleJSON.JSONObject();
                    row["cardId"] = 1101;
                    row["id"] = i;
                    row["unlockLevel"] = i;
                    row["description"] = "Editor滚动与两态测试，不是正式奖励";
                    row["sortOrder"] = i;
                    stages.Add(row);
                }
                return stages;
            }
            return json;
        });
        var fixturePlayer = new PlayerProgressControl(fixture, null, ignored => { });
        var previousPlayer = PlayerProgressControl.InstallValidationSession(fixturePlayer);
        tablesField.SetValue(null, fixture);
        typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        try {
            await CardCultivationControl.OpenAsync(new CardCultivationOpenVO { cardId = 1101, orderedCardIds = new[] { 1101 }, owner = canvas, source = "editor-fixture" });
            var panel = await CultivationOpened();

            Check(!panel.btnUpgrade.interactable && !panel.listMaterials.gameObject.activeSelf && panel.txtHint.text.Contains("超出页面容量"), "Editor夹具：超3项成本明确拒绝培养，不隐藏扣费");
            var items = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>();
            var unlocked = items.Single(item => item.txtSkillKind.text == "普攻");
            var locked = items.Single(item => item.txtSkillKind.text == "被动");
            Check(!unlocked.imgSkillLock.enabled && locked.imgSkillLock.enabled && unlocked.imgSkillFrame.color != locked.imgSkillFrame.color, "Editor夹具：技能两态底框及独立锁图");
            var stageScroll = panel.listStages.GetComponent<ScrollRect>();
            stageScroll.verticalNormalizedPosition = 0;
            await Frames(6);
            Vector2 position = stageScroll.content.anchoredPosition;
            Click(locked.btnSkill.GetComponent<RectTransform>());
            locked.btnSkill.onClick.Invoke();
            await Frames(10);
            var detail = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
            Check(detail != null && detail.isOpened && detail.txtTitle.text == "Editor锁定技能夹具", "Editor夹具：锁定技能可以打开正式详情窗口，重复请求无重复窗口");
            Check(detail.listStages.GetComponentsInChildren<CardSkillStageItem>().Single().txtCondition.text == "5级解锁", "Editor夹具：解锁条件使用适配已有详情框的短文案");
            Capture("cultivation-editor-fixture-locked.png");
            Click(panel.btnUpgrade.GetComponent<RectTransform>());
            await Frames(6);
            Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId) && panel.CurrentCardId == 1101 && (stageScroll.content.anchoredPosition - position).sqrMagnitude < .1f, "Editor夹具：遮罩关闭保留卡牌与阶段滚动");
            panel.btnReturn.onClick.Invoke();
            await Frames(6);
        } finally {
            CardSkillDetailControl.Close();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PlayerProgressControl.InstallValidationSession(previousPlayer);
            tablesField.SetValue(null, previousTables);
            typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
    }

    /**统一提示专项使用当前可见卡池；旧1101培养视觉测试保留为其原版本入口。*/
    private static async Task RunUnifiedToastMigration(PlayerProgressControl session) {
        int cardId = CfgManager.tables.CardObj.DataList.First(card => card.ShowInLibrary && card.MaxLevel > 1).Id;
        string folder = "Library/AIUI/UnifiedToast/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);
        var saved = new TeamSchemeSaveVO();
        saved.schemes[0] = new[] { cardId, 0, 0, 0 };
        var oldTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, saved, ignored => { }));
        var functionField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldFunctions = functionField.GetValue(null);
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
            Check(view.slots[0].title.text == CfgManager.tables.CardObj.Get(cardId).Name, "统一提示：真实阵容入口显示当前可见卡池的独立已拥有卡");
            Click(view.slots[0].button.GetComponent<RectTransform>());
            var panel = await CultivationOpened();
            Check(panel.CurrentCardId == cardId, "统一提示：当前卡池真实上阵位打开培养页");
            string status = panel.txtHint.text;
            GlobalToast.Clear();
            long revision = session.model.Revision;
            Click(panel.btnUpgrade.GetComponent<RectTransform>());
            await GlobalToast.EnsureReadyAsync();
            await Frames(8);
            var warning = GlobalToast.Host.ActiveItems.FirstOrDefault(item => item.Message == "材料不足" && item.Warning);
            Check(warning != null, "统一提示：培养材料不足使用指定的全局警告文案");
            Check(warning != null && warning.txtMessage.font != null &&
                warning.txtMessage.fontSharedMaterial == warning.txtMessage.font.material,
                "统一提示：材料不足使用提示字体自带的默认材质");
            Check(session.Cards.model.GetState(cardId).level == 1 && session.model.Revision == revision, "统一提示：失败不扣料不改等级或进度版本");
            Check(panel.txtHint.text == status, "统一提示：瞬时失败不覆盖培养页持续状态说明");
            Capture("unified-toast-cultivation-insufficient.png");
            foreach (var cost in session.Cards.model.GetState(cardId).costs) {
                Check(session.Inventory.TryGrantItem(cost.Key, cost.Value, out _), "统一提示：升级材料只准备在隔离内存会话");
            }
            await Frames(5);
            GlobalToast.Clear();
            Click(panel.btnUpgrade.GetComponent<RectTransform>());
            await Frames(8);
            Check(session.Cards.model.GetState(cardId).level == 2 && GlobalToast.Contains("升级成功", false), "统一提示：培养成功仍提交原业务事务并给出全局反馈");
            Check(panel.txtHint.text == status, "统一提示：成功反馈不写入页面持续状态标签");
            Capture("unified-toast-cultivation-success.png");
            Click(panel.btnReturn.GetComponent<RectTransform>());
            await Frames(8);
            Check(view.slots[0].level.text.Contains("2"), "统一提示：培养返回仍刷新原上阵卡等级");
            GlobalToast.Clear();
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(MainUIEnum.LAYER_EFFECT_TEST_PANEL), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(MainUIEnum.LAYER_EFFECT_TEST_PANEL);
            await Task.Delay(300);
            await Frames(6);
            var debug = PanelMgr.ins.GetPanel(MainUIEnum.LAYER_EFFECT_TEST_PANEL) as LayerEffectTestPanel;
            string instructions = debug.txtHint.text;
            debug.inpRedHex.onEndEdit.Invoke("invalid");
            Check(GlobalToast.Contains("颜色格式应为 #RRGGBB，已保留原颜色。"), "统一提示：调试颜色错误使用公共警告");
            Check(debug.txtHint.text == instructions, "统一提示：调试操作说明保持原内容");
            debug.btnGridSettings.onClick.Invoke();
            bool gridWasOpen = debug.boxGridSettings.gameObject.activeSelf;
            string gridInstructions = debug.txtHint.text;
            string gridInfo = debug.txtGridInfo.text;
            debug.inpGridWidth.SetTextWithoutNotify("0");
            debug.btnGridApply.onClick.Invoke();
            Check(GlobalToast.Contains("网格参数无效：宽8–720、高8–1280；基点超出范围。"), "统一提示：调试网格错误使用公共警告");
            Check(debug.boxGridSettings.gameObject.activeSelf == gridWasOpen && debug.txtHint.text == gridInstructions && debug.txtGridInfo.text == gridInfo, "统一提示：非法网格不提交且保持原模式、弹层状态和说明");
            await Frames(8);
            Capture("unified-toast-debug-errors.png");
            debug.Close();
            await Frames(8);
            await ValidateRemainingToastEntrances(view, cardId, session);
        } finally {
            GlobalToast.Shutdown();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(MainUIEnum.LAYER_EFFECT_TEST_PANEL);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);
            functionField.SetValue(null, oldFunctions);
        }
    }

    /**故障只注入资源映射与临时GM处理器，玩家进度始终是外层隔离会话。*/
    private static async Task ValidateRemainingToastEntrances(DeckPresentationView view, int cardId, PlayerProgressControl session) {
        var previous = ResourcePresentation.Policy;
        long revision = session.model.Revision;
        Application.logMessageReceived -= OnLog;
        try {
            GlobalToast.Clear();
            ResourcePresentation.Install(new RejectedToastUI(previous, TeamEditControl.PanelId));
            Click(view.editTeam.GetComponent<RectTransform>());
            await Frames(4);
            Check(GlobalToast.Contains("队伍编辑打开失败，请重试") && !PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId), "补漏：真实阵容编辑按钮资源失败显示全局警告");
            ResourcePresentation.Install(new RejectedToastUI(previous, CardCultivationControl.PanelId));
            GlobalToast.Clear();
            Click(view.slots[0].button.GetComponent<RectTransform>());
            await Frames(4);
            Check(GlobalToast.Contains("卡牌培养打开失败，请重试") && !PanelMgr.ins.IsPanelOpen(CardCultivationControl.PanelId), "补漏：真实上阵卡培养入口资源失败显示全局警告");
        } finally {
            ResourcePresentation.Install(previous);
            Application.logMessageReceived += OnLog;
        }
        Check(session.model.Revision == revision && view.slots[0].title.text == CfgManager.tables.CardObj.Get(cardId).Name, "补漏：入口失败保留阵容页面与进度");
        Capture("unified-toast-entry-failure.png");
        var probe = new GameObject("ToastBattleRejectionProbe", typeof(RectTransform), typeof(BattleScenePanel));
        try {
            var panel = probe.GetComponent<BattleScenePanel>();
            var rejected = typeof(BattleScenePanel).GetMethod("OnUnlockRejected", BindingFlags.Instance | BindingFlags.NonPublic);
            GlobalToast.Clear();
            rejected.Invoke(panel, new object[] { 123L, "货币不足" });
            rejected.Invoke(panel, new object[] { 123L, "货币不足" });
            await Frames(4);
            Check(panel.lastInteractionHint == "货币不足" && GlobalToast.Host.ActiveItems.Count(item => item.Message == "货币不足") == 2, "补漏组件：战斗拒绝事件重复触发形成两条全局警告");
            Check(probe.transform.Find("unlockMessage") == null, "补漏组件：战斗拒绝不再创建地块短时文字");
        } finally { UnityEngine.Object.Destroy(probe); }
        // 使用真实GM窗口和按钮，临时替换一条处理器，不执行它原来的业务写入。
        const string gmId = "default/gm/GmPanel";
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(previous.ResolveUI(gmId), ResType.Prefab) });
        PanelMgr.ins.OpenPanel(gmId);
        await Task.Delay(450);
        await Frames(12);
        var gm = PanelMgr.ins.GetPanel(gmId) as GmPanel;
        var item = gm.listItem.GetCell(0).GetComponent<GMItem>();
        var cfg = (cfg.resource.GMResource)typeof(GMItem).GetField("cfg", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);
        var commands = (Dictionary<string, Func<IReadOnlyList<string>, bool>>)typeof(GMControl).GetField("commands", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GMControl.ins);
        commands.TryGetValue(cfg.Content, out var oldHandler);
        try {
            commands[cfg.Content] = ignored => true;
            GlobalToast.Clear();
            File.WriteAllLines("Library/AIUI/gm-toast-layout.txt", gm.GetComponentsInChildren<RectTransform>(true).Select(rect => rect.name + " active=" + rect.gameObject.activeInHierarchy + " local=" + rect.localPosition + " world=" + rect.position + " size=" + rect.rect.size + " scale=" + rect.lossyScale + " sibling=" + rect.GetSiblingIndex()));
            Capture("unified-toast-gm-before.png");
            var gmHit = Click(item.btnExecute.GetComponent<RectTransform>());
            Check(gmHit == item.btnExecute.gameObject || gmHit.transform.IsChildOf(item.btnExecute.transform), "GM按钮命中：" + gmHit.name);
            await Frames(3);
            Check(GlobalToast.Contains("GM执行成功：" + cfg.Desc, false), "补漏：真实GM执行按钮成功使用普通提示");
            commands[cfg.Content] = ignored => false;
            GlobalToast.Clear();
            Application.logMessageReceived -= OnLog;
            try { Click(item.btnExecute.GetComponent<RectTransform>()); } finally { Application.logMessageReceived += OnLog; }
            await Frames(3);
            Check(GlobalToast.Contains("GM执行失败，请检查参数"), "补漏：真实GM执行按钮失败使用全局警告");
            Capture("unified-toast-gm-failure.png");
        } finally {
            if (oldHandler != null) commands[cfg.Content] = oldHandler; else GMControl.ins.UnregisterCommand(cfg.Content);
            gm.Close();
        }
        Check(session.model.Revision == revision, "补漏：GM夹具未执行原业务或修改进度");
        ValidateStartupToastCallbacks();
    }

    /**启动错误分支组件验证；复用的已打开占位面板仅阻止二次启动真实登录流程。*/
    private static void ValidateStartupToastCallbacks() {
        var root = new GameObject("StartupToastCallbackProbe", typeof(RectTransform));
        var login = root.AddComponent<LoginPanel>();
        login.txt = new GameObject("retryPrompt", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        login.txt.transform.SetParent(root.transform, false);
        var optionRoot = new GameObject("OptionToastCallbackProbe", typeof(RectTransform));
        var option = optionRoot.AddComponent<OptionPanel>();
        typeof(BasePanel).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(option, PanelState.STATE_OPEN);
        var map = (Dictionary<string, BasePanel>)typeof(PanelMgr).GetField("openPanelMap", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(PanelMgr.ins);
        map.TryGetValue(UIEnum.LOGIN_PANEL, out var previousLogin);
        map[UIEnum.LOGIN_PANEL] = option;
        Application.logMessageReceived -= OnLog;
        try {
            typeof(LoginPanel).GetField("openVersion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(login, 2);
            var failConfigs = typeof(LoginPanel).GetMethod("OnConfigsFailed", BindingFlags.Instance | BindingFlags.NonPublic);
            login.txt.text = "new prompt";
            GlobalToast.Clear();
            failConfigs.Invoke(login, new object[] { 1 });
            Check(login.txt.text == "new prompt" && GlobalToast.Host.ActiveItems.Count == 0, "启动组件：过期配置失败不覆盖新状态或发出提示");
            failConfigs.Invoke(login, new object[] { 2 });
            Check(login.txt.text.Contains("重试") && GlobalToast.Contains("配置加载失败，请点击重试"), "启动组件：当前配置失败显示滚动警告，保留持续重试入口");
            GlobalToast.Clear();
            typeof(LoginPanel).GetMethod("OnEnterMainFailed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { new InvalidOperationException("Editor callback fixture") });
            Check(GlobalToast.Contains("进入主界面失败，请重试"), "启动组件：主内容失败回调使用全局警告");
            GlobalToast.Clear();
            var entering = typeof(OptionPanel).GetField("enteringGame", BindingFlags.Instance | BindingFlags.NonPublic);
            entering.SetValue(option, true);
            typeof(OptionPanel).GetMethod("OnEnterGameFailed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(option, new object[] { new InvalidOperationException("Editor callback fixture") });
            Check(!(bool)entering.GetValue(option) && GlobalToast.Contains("进入游戏失败，请重试"), "启动组件：新游戏失败解除忙态并给出全局警告");
        } finally {
            if (previousLogin == null) map.Remove(UIEnum.LOGIN_PANEL); else map[UIEnum.LOGIN_PANEL] = previousLogin;
            Application.logMessageReceived += OnLog;
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(optionRoot);
        }
    }

    private sealed class RejectedToastUI : IResourcePresentationPolicy {
        private readonly IResourcePresentationPolicy inner;
        private readonly string rejected;
        public RejectedToastUI(IResourcePresentationPolicy inner, string rejected) { this.inner = inner; this.rejected = rejected; }
        public string ResolveUI(string id) { if (id == rejected) throw new InvalidOperationException("Editor toast fault injection"); return inner.ResolveUI(id); }
        public ImageResource ResolveImage(string id) => inner.ResolveImage(id);
        public string ResolveAudio(int id) => inner.ResolveAudio(id);
        public int ResolveButtonSound(ButtonSoundType type, bool interactable) => inner.ResolveButtonSound(type, interactable);
        public Material GetFrameMaterial() => inner.GetFrameMaterial();
    }

    /**当前可见卡池的真实入口试跑；无正式技能时仅核对缺项分支，不伪造已解锁/锁定数据。*/
    /**只用正式2101普攻配置；拥有、解锁、升级和背包使用隔离内存存档。*/
    private static async Task RunMarineBasic() {
        const int cardId = 2101;
        const int skillId = 210101;
        CardCultivationConfigValidation.Validate(CfgManager.tables);
        Check(CfgManager.tables.CultivationSkillObj.Get(skillId).Name == "步枪射击", "正式源表已生成步枪射击且不使用测试技能");
        int writes = 0;
        var seed = new PlayerProgressSaveVO { initialized = true };
        var player = new PlayerProgressControl(CfgManager.tables, seed, ignored => writes++);
        var previous = PlayerProgressControl.InstallValidationSession(player);
        try {
            await CardCultivationControl.OpenAsync(new CardCultivationOpenVO { cardId = cardId, orderedCardIds = new[] { cardId }, owner = canvas, source = "formal-marine-basic-isolated-save" });
            var source = await CultivationOpened();
            for (int level = 0; level <= 10; level++) {
                Check(player.Cards.model.GetState(cardId).level == level, "陆战队员隔离等级" + level);
                var query = CardSkillQuery.Read(CfgManager.tables, cardId, level, cfg.SkillDisplaySlot.Basic, skillId);
                int expectedRank = level >= 10 ? 3 : level >= 5 ? 2 : level >= 1 ? 1 : 0;
                int[] unlockLevels = { 1, 5, 10 };
                string[] effects = { "造成 100%技能伤害", "造成 120%技能伤害", "造成 140%技能伤害" };
                Check(query.ranks.Count == 3 && query.ranks.Select(rank => rank.unlockLevel).SequenceEqual(unlockLevels) && query.unlocked == (level > 0) && query.currentRank == (expectedRank > 0 ? (int?)expectedRank : null), "等级" + level + "：1/5/10级自动解锁最高阶");
                Check(query.ranks.Count(rank => rank.current) == (expectedRank > 0 ? 1 : 0) && query.ranks.All(rank => rank.unlocked == (level >= rank.unlockLevel)), "等级" + level + "：各阶锁定正确且当前阶唯一");
                if (expectedRank > 0) {
                    Check(query.battleSummary == effects[expectedRank - 1], "等级" + level + "：当前阶完整替换低阶，不累加");
                }
                Check(query.ranks.Select(rank => rank.effect).SequenceEqual(effects) && query.iconAddress.EndsWith("/210101.png") && query.missing.Count == 0, "等级" + level + "：三阶100%/120%/140%与图标关联齐备");
                await Frames(8);
                var item = source.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Single(value => value.txtSkillKind.text == "普攻");
                for (int i = 0; i < 100 && !item.imgSkillIcon.enabled; i++) await Frames(2);
                Check(item.imgSkillIcon.enabled && item.imgSkillIcon.sprite != null && item.imgSkillLock.enabled == (level == 0), "等级" + level + "：培养普攻显示正式图标与解锁状态");
                bool capture = new[] { 0, 1, 4, 5, 9, 10 }.Contains(level);
                if (capture) {
                    Capture("marine-cultivation-level-" + level + ".png");
                }
                Click(item.btnSkill.GetComponent<RectTransform>());
                for (int i = 0; i < 100 && !PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId); i++) await Frames(2);
                await Task.Delay(350);
                await Frames(8);
                var detail = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
                for (int i = 0; i < 100 && !detail.imgSkillIcon.enabled; i++) await Frames(2);
                Check(detail.txtTitle.text == "步枪射击" && detail.txtType.text == "主动" && detail.imgSkillIcon.enabled && detail.imgSkillIcon.sprite != null && detail.imgSkillIcon.sprite.name == "210101", "等级" + level + "：实际普攻入口到名称/主动类型/同一瞄准图标");
                var rows = detail.listStages.GetComponentsInChildren<CardSkillStageItem>(true).Where(value => value.gameObject.activeInHierarchy).ToArray();
                Check(rows.Length == 3, "等级" + level + "：实际详情显示三阶");
                for (int rank = 1; rank <= 3; rank++) {
                    var row = rows.Single(value => value.txtRank.text == rank + "阶");
                    string condition = level < unlockLevels[rank - 1] ? unlockLevels[rank - 1] + "级解锁" : rank == expectedRank ? "当前" : "已解锁";
                    Check(row.txtEffect.text == effects[rank - 1] && row.imgLock.enabled == (level < unlockLevels[rank - 1]) && row.imgStageCurrent.enabled == (rank == expectedRank) && row.txtCondition.text == condition, "等级" + level + "：" + rank + "阶效果/锁/当前状态正确");
                }
                Check(!detail.txtMissing.gameObject.activeSelf, "等级" + level + "：已配置的技能/阶数/图标缺项提示消除");
                GlobalToast.Clear();
                foreach (var text in detail.GetComponentsInChildren<TextMeshProUGUI>()) { text.SetVerticesDirty(); text.SetMaterialDirty(); }
                Canvas.ForceUpdateCanvases();
                await Frames(12);
                await Task.Delay(150);
                if (capture) {
                    Capture("marine-skill-detail-level-" + level + ".png");
                }
                if (level == 5) {
                    long revision = player.model.Revision;
                    var hit = Click(source.btnUpgrade.GetComponent<RectTransform>());
                    Check(hit != null && hit.transform.IsChildOf(detail.transform) && player.model.Revision == revision, "三阶正式详情遮罩拦截底层升级且进度不变");
                } else {
                    Click(detail.btnClose.GetComponent<RectTransform>());
                }
                await Task.Delay(300);
                await Frames(4);
                Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId) && ReferenceEquals(PanelMgr.ins.GetPanel(CardCultivationControl.PanelId), source) && source.CurrentCardId == cardId && item.imgSkillIcon.enabled && item.imgSkillIcon.sprite != null, "等级" + level + "：关闭回原卡且不提前释放培养页共用图标");
                if (level < 10) {
                    foreach (var cost in player.Cards.model.GetState(cardId).costs) {
                        Check(player.Inventory.TryGrantItem(cost.Key, cost.Value, out _), "材料仅补入隔离存档");
                    }
                    Click(source.btnUpgrade.GetComponent<RectTransform>());
                    await Task.Delay(450);
                    await Frames(8);
                    Check(player.Cards.model.GetState(cardId).level == level + 1, "实际培养按钮" + level + "→" + (level + 1) + "，技能随等级自动刷新");
                    GlobalToast.Clear();
                }
            }
            Check(writes > 0, "解锁/升级事务仅保存到隔离内存回调");
        } finally {
            CardSkillDetailControl.Close();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PlayerProgressControl.InstallValidationSession(previous);
        }
    }

    /**正式强化剂/弹群配置，实际培养按钮逐级解锁；进度只保存至隔离内存。*/
    private static async Task RunMarineExtraSkills() {
        const int cardId = 2101;
        CardCultivationConfigValidation.Validate(CfgManager.tables);
        int writes = 0;
        bool alignmentOnly = SessionState.GetBool(Key + ".skillAlignment", false);
        int firstLevel = alignmentOnly ? 6 : 0;
        int lastLevel = alignmentOnly ? 6 : 10;
        var save = new PlayerProgressSaveVO { initialized = true };
        if (alignmentOnly) {
            save.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = firstLevel });
        }
        var player = new PlayerProgressControl(CfgManager.tables, save, ignored => writes++);
        var previous = PlayerProgressControl.InstallValidationSession(player);
        try {
            await CardCultivationControl.OpenAsync(new CardCultivationOpenVO { cardId = cardId, orderedCardIds = new[] { cardId }, owner = canvas, source = "formal-marine-extra-isolated-save" });
            var source = await CultivationOpened();
            for (int level = firstLevel; level <= lastLevel; level++) {
                Check(player.Cards.model.GetState(cardId).level == level, "陆战队员实际培养等级" + level);
                Check(CardSkillQuery.ReadAll(CfgManager.tables, cardId, level).Select(skill => skill.skillId).SequenceEqual(new[] { 210101, 210102, 210103, 210104 }), "等级" + level + "：四项正式技能按首阶解锁顺序稳定");
                foreach (int skillId in new[] { 210102, 210103, 210104 }) {
                    bool passive = skillId == 210102;
                    bool stimulant = skillId == 210103;
                    string name = passive ? "防暴护盾" : stimulant ? "强化剂" : "弹群";
                    string slotName = passive ? "被动" : stimulant ? "主动" : "大招";
                    var slot = passive ? cfg.SkillDisplaySlot.Passive : stimulant ? cfg.SkillDisplaySlot.Active : cfg.SkillDisplaySlot.Ultimate;
                    int[] unlockLevels = passive ? new[] { 2, 6, 9 } : stimulant ? new[] { 3, 7, 10 } : new[] { 4, 8 };
                    string[] effects = passive ? new[] { "生命值提升10%", "生命值提升20%", "生命值提升30%" } : stimulant ? new[] { "移动速度和攻击速度提升50%\n持续3秒", "移动速度和攻击速度提升50%\n持续4秒", "移动速度和攻击速度提升50%\n持续5秒" } : new[] { "攻击力提升25%\n持续5秒", "攻击力提升50%\n持续5秒" };
                    int expectedRank = unlockLevels.Count(unlockLevel => level >= unlockLevel);
                    var query = CardSkillQuery.Read(CfgManager.tables, cardId, level, slot, skillId);
                    Check(query.ranks.Count == unlockLevels.Length && query.currentRank == (expectedRank > 0 ? (int?)expectedRank : null) && query.unlocked == (expectedRank > 0) && query.ranks.All(rank => rank.unlocked == (level >= rank.unlockLevel)), name + "等级" + level + "：自动解锁各阶且当前阶正确");
                    Check(query.ranks.Select(rank => rank.effect).SequenceEqual(effects) && query.missing.Count == 0 && (expectedRank == 0 || query.battleSummary == effects[expectedRank - 1]), name + "等级" + level + "：正式效果替换低阶不累加");
                    await Frames(8);
                    var item = source.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Single(value => value.txtSkillKind.text == slotName);
                    for (int i = 0; i < 100 && !item.imgSkillIcon.enabled; i++) {
                        await Frames(2);
                    }
                    Check(item.imgSkillIcon.enabled && item.imgSkillIcon.sprite != null && item.imgSkillLock.enabled == (expectedRank == 0), name + "等级" + level + "：培养图标及锁定状态自动刷新");
                    if (skillId == 210103 && new[] { 0, 3, 4, 7, 8, 10 }.Contains(level)) {
                        Capture("marine-cultivation-level-" + level + ".png");
                    }
                    Click(item.btnSkill.GetComponent<RectTransform>());
                    for (int i = 0; i < 100 && !PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId); i++) {
                        await Frames(2);
                    }
                    await Task.Delay(350);
                    await Frames(8);
                    var detail = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
                    for (int i = 0; i < 100 && !detail.imgSkillIcon.enabled; i++) {
                        await Frames(2);
                    }
                    Check(detail.txtTitle.text == name && detail.txtType.text == (passive ? "被动" : "主动") && detail.imgSkillIcon.enabled && detail.imgSkillIcon.sprite != null && detail.imgSkillIcon.sprite.name == (passive ? "210102" : stimulant ? "210103" : "210104") && !detail.txtMissing.gameObject.activeSelf, name + "等级" + level + "：真实入口、类型、匹配清边显示图且无缺项");
                    var opening = detail.boxWindow.Find("imgSkillOpening").GetComponent<Mask>();
                    var rim = detail.boxWindow.Find("imgSkillFrame").GetComponent<Image>();
                    Check(opening.enabled && !opening.showMaskGraphic && detail.imgSkillIcon.transform.parent == opening.transform && opening.transform.Find("imgSkillBlackBacking") != null && rim.rectTransform.sizeDelta == new Vector2(79, 79) && detail.imgSkillIcon.rectTransform.sizeDelta == new Vector2(55, 55) && rim.rectTransform.anchoredPosition == new Vector2(29, -22), name + "：正方形框/居中/共用Mask装配正确");
                    var rows = detail.listStages.GetComponentsInChildren<CardSkillStageItem>(true).Where(value => value.gameObject.activeInHierarchy).ToArray();
                    Check(rows.Length == effects.Length, name + "等级" + level + "：实际详情阶数正确");
                    for (int rank = 1; rank <= effects.Length; rank++) {
                        var row = rows.Single(value => value.txtRank.text == rank + "阶");
                        string condition = level < unlockLevels[rank - 1] ? unlockLevels[rank - 1] + "级解锁" : rank == expectedRank ? "当前" : "已解锁";
                        if (alignmentOnly) {
                            Check(row.txtCondition.alignment == TextAlignmentOptions.Center && row.txtCondition.margin == new Vector4(6, 0, 6, 0) && row.txtCondition.rectTransform.sizeDelta == new Vector2(112, 36), name + "：状态文字居中、对称内边距及112×36框");
                            Check(row.txtCondition.rectTransform.anchoredPosition == new Vector2(356, level >= unlockLevels[rank - 1] ? -34 : -58) && row.imgLock.rectTransform.anchoredPosition == new Vector2(393.5f, -17) && row.imgLock.rectTransform.sizeDelta == new Vector2(37, 42), name + "：锁与文本框中心X=412，原纵坐标与尺寸保持");
                            float expectedMax = rank == expectedRank ? 17 : level >= unlockLevels[rank - 1] ? 14 : 15;
                            Check(row.txtCondition.enableAutoSizing && row.txtCondition.fontSizeMin == 14 && row.txtCondition.fontSizeMax == expectedMax, name + "：原各状态字号策略保持");
                        }
                        row.txtEffect.ForceMeshUpdate();
                        Check(row.txtEffect.text == effects[rank - 1] && row.imgLock.enabled == (level < unlockLevels[rank - 1]) && row.imgStageCurrent.enabled == (rank == expectedRank) && row.txtCondition.text == condition && !row.txtEffect.isTextOverflowing, name + "等级" + level + "：" + rank + "阶文案/锁/当前标记且不溢出");
                    }
                    GlobalToast.Clear();
                    await Frames(8);
                    if (new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.Contains(level)) {
                        Capture((passive ? "shield" : stimulant ? "stimulant" : "barrage") + "-level-" + level + ".png");
                    }
                    if (level == 7 && stimulant) {
                        long revision = player.model.Revision;
                        var hit = Click(source.btnUpgrade.GetComponent<RectTransform>());
                        Check(hit != null && hit.transform.IsChildOf(detail.transform) && player.model.Revision == revision, "强化剂遮罩拦截底层升级且进度不变");
                    } else {
                        Click(detail.btnClose.GetComponent<RectTransform>());
                    }
                    await Task.Delay(300);
                    await Frames(4);
                    Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId) && ReferenceEquals(PanelMgr.ins.GetPanel(CardCultivationControl.PanelId), source) && source.CurrentCardId == cardId && item.imgSkillIcon.enabled && item.imgSkillIcon.sprite != null, name + "等级" + level + "：关闭返回原卡并保留图标");
                }
                if (level < lastLevel) {
                    foreach (var cost in player.Cards.model.GetState(cardId).costs) {
                        Check(player.Inventory.TryGrantItem(cost.Key, cost.Value, out _), "升级材料仅补入隔离存档");
                    }
                    long revision = player.model.Revision;
                    Click(source.btnUpgrade.GetComponent<RectTransform>());
                    await Task.Delay(450);
                    await Frames(8);
                    Check(player.Cards.model.GetState(cardId).level == level + 1 && player.model.Revision == revision + 1, "培养按钮" + level + "→" + (level + 1) + "：一次事务，无额外技能解锁扣费");
                    GlobalToast.Clear();
                }
            }
            Check(alignmentOnly ? writes == 0 : writes > 0, alignmentOnly ? "对齐验证只读隔离6级卡牌，无存档写入" : "新技能解锁及升阶只写隔离内存回调");
        } finally {
            CardSkillDetailControl.Close();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PlayerProgressControl.InstallValidationSession(previous);
        }
    }

    /**内存配置驱动真实成长事务及正式详情查询；验证2/4/6级阈值，不写配置文件。*/
    private static async Task RunIsolatedRankProgression() {
        const int fixtureSkillId = 990100;
        int cardId = CfgManager.tables.CardObj.DataList.First(card => card.ShowInLibrary && card.MaxLevel >= 6).Id;
        var tablesField = typeof(CfgManager).GetField("_tables", BindingFlags.Static | BindingFlags.NonPublic);
        object oldTables = tablesField.GetValue(null);
        var fixture = new cfg.Tables(name => {
            var json = SimpleJSON.JSON.Parse(File.ReadAllText("Assets/ConfigBin/" + name + ".json"));
            if (name == "cfgobj_cultivationskillobj") {
                return SimpleJSON.JSON.Parse("[{\"id\":990100,\"name\":\"隔离测试·等级解锁\",\"kind\":2,\"slot\":1,\"icon\":\"\"}]");
            }
            if (name == "cfgobj_cardskillentryobj") {
                return SimpleJSON.JSON.Parse("[{\"cardId\":" + cardId + ",\"skillId\":990100,\"sortOrder\":1}]");
            }
            if (name == "cfgobj_cardskillrankobj") {
                var ranks = new SimpleJSON.JSONArray();
                for (int rank = 1; rank <= 3; rank++) {
                    ranks.Add(SimpleJSON.JSON.Parse("{\"cardId\":" + cardId + ",\"skillId\":990100,\"rank\":" + rank + ",\"unlockLevel\":" + rank * 2 + ",\"battleSkillId\":" + (990100 + rank) + "}"));
                }
                return ranks;
            }
            if (name == "cfgobj_skillobj") {
                string source = json.AsArray[0].ToString();
                for (int rank = 1; rank <= 3; rank++) {
                    var row = SimpleJSON.JSON.Parse(source);
                    row["id"] = 990100 + rank;
                    row["name"] = "隔离测试" + rank + "阶效果";
                    row["effects"] = SimpleJSON.JSON.Parse("[{\"$type\":\"FixedDamageEffect\",\"amount\":" + rank * 10 + "}]");
                    json.AsArray.Add(row);
                }
                return json;
            }
            return json;
        });
        var seed = new PlayerProgressSaveVO { initialized = true };
        seed.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = cardId, level = 1 });
        int fixtureWrites = 0;
        var player = new PlayerProgressControl(fixture, seed, ignored => fixtureWrites++);
        fixtureWrites = 0;
        var oldPlayer = PlayerProgressControl.InstallValidationSession(player);
        tablesField.SetValue(null, fixture);
        typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        try {
            await CardCultivationControl.OpenAsync(new CardCultivationOpenVO { cardId = cardId, orderedCardIds = new[] { cardId }, owner = canvas, source = "editor-isolated-rank-progression" });
            var sourcePanel = await CultivationOpened();
            for (int level = 1; level <= 6; level++) {
                var state = CardSkillQuery.Read(fixture, cardId, level, cfg.SkillDisplaySlot.Passive, fixtureSkillId);
                int expectedRank = level / 2;
                Check(player.Cards.model.GetState(cardId).level == level && state.currentRank == (expectedRank == 0 ? (int?)null : expectedRank), "等级" + level + "：真实成长等级与最高解锁阶一致");
                Check(state.unlocked == (expectedRank > 0) && state.ranks.Count(rank => rank.current) == (expectedRank == 0 ? 0 : 1), "等级" + level + "：阈值解锁自动发生且当前阶唯一");
                Check(state.ranks.All(rank => rank.unlocked == (level >= rank.unlockLevel)), "等级" + level + "：2/4/6级解锁边界正确");
                if (expectedRank > 0) {
                    var active = state.ranks.Single(rank => rank.current);
                    Check(state.battleSummary == active.effect && active.effect.StartsWith("造成 " + expectedRank * 10 + " 点伤害"), "等级" + level + "：当前效果替换低阶，不拼接累加");
                }
                GlobalToast.Clear();
                var item = sourcePanel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Single(value => value.txtSkillKind.text == "被动");
                Check(item.imgSkillLock.enabled == (expectedRank == 0), "等级" + level + "：培养页技能锁状态已自动刷新");
                Click(item.btnSkill.GetComponent<RectTransform>());
                for (int i = 0; i < 100 && !PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId); i++) await Frames(2);
                await Task.Delay(350);
                await Frames(8);
                var detail = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
                Check(detail != null && detail.isOpened && detail.txtTitle.text == "隔离测试·等级解锁" && detail.txtType.text == "被动", "等级" + level + "：从培养技能按钮重开到正确测试技能");
                var rows = detail.listStages.GetComponentsInChildren<CardSkillStageItem>(true).Where(row => row.gameObject.activeInHierarchy && !string.IsNullOrEmpty(row.txtRank.text)).ToArray();
                Check(rows.Count(row => row.imgStageCurrent.enabled) == (expectedRank == 0 ? 0 : 1), "等级" + level + "：重开高亮已刷新且无残留");
                foreach (var row in rows) {
                    int rank = int.Parse(row.txtRank.text.Replace("阶", ""));
                    string expected = rank == expectedRank ? "当前" : rank < expectedRank ? "已解锁" : rank * 2 + "级解锁";
                    Check(row.txtCondition.text == expected && row.imgLock.enabled == (rank > expectedRank), "等级" + level + "：" + rank + "阶条件及锁图正确");
                }
                if (expectedRank == 3) detail.listStages.GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
                await Frames(6);
                GlobalToast.Clear();
                // 等待池化条目的材质与遮罩刷新后再采集，不改变运行时展示数据。
                foreach (var text in detail.GetComponentsInChildren<TextMeshProUGUI>()) { text.SetVerticesDirty(); text.SetMaterialDirty(); }
                Canvas.ForceUpdateCanvases();
                await Frames(6);
                Capture("progression-level-" + level + ".png");
                Click(detail.btnClose.GetComponent<RectTransform>());
                await Task.Delay(300);
                await Frames(4);
                Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId) && ReferenceEquals(PanelMgr.ins.GetPanel(CardCultivationControl.PanelId), sourcePanel) && sourcePanel.CurrentCardId == cardId, "等级" + level + "：关闭保留原培养页和原卡");
                if (level < 6) {
                    foreach (var cost in player.Cards.model.GetState(cardId).costs) {
                        Check(player.Inventory.TryGrantItem(cost.Key, cost.Value, out _), "等级" + level + "：升级材料仅补入隔离内存");
                    }
                    long revision = player.model.Revision;
                    Click(sourcePanel.btnUpgrade.GetComponent<RectTransform>());
                    await Frames(10);
                    await Task.Delay(400);
                    GlobalToast.Clear();
                    Check(player.Cards.model.GetState(cardId).level == level + 1 && player.model.Revision == revision + 1, "实际培养升级" + level + "→" + (level + 1) + "：仅一次事务，无单独技能解锁操作");
                }
            }
            Check(fixtureWrites > 0, "所有升级和材料写入仅到隔离内存回调");
        } finally {
            CardSkillDetailControl.Close();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PlayerProgressControl.InstallValidationSession(oldPlayer);
            tablesField.SetValue(null, oldTables);
            typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
        Check(ReferenceEquals(CfgManager.tables, oldTables), "测试结束恢复正式配置实例");
    }

    /**只向正式窗口注入Editor展示快照；不替换CfgManager表或写入正式配置。*/
    private static async Task RunIsolatedRankDisplay(PlayerProgressControl session) {
        int cardId = CfgManager.tables.CardObj.DataList.First(card => card.ShowInLibrary && card.MaxLevel > 1).Id;
        long revision = session.model.Revision;
        await CardCultivationControl.OpenAsync(new CardCultivationOpenVO { cardId = cardId, orderedCardIds = new[] { cardId }, owner = canvas, source = "editor-isolated-rank-display" });
        var source = await CultivationOpened();
        Check(source.CurrentCardId == cardId, "隔离展示来源为正式培养页及当前可见卡" + cardId);
        Check(CardSkillDetailQuery.TypeName(CardSkillSlot.Basic) == "主动" && CardSkillDetailQuery.TypeName(CardSkillSlot.Active) == "主动" && CardSkillDetailQuery.TypeName(CardSkillSlot.Ultimate) == "主动" && CardSkillDetailQuery.TypeName(CardSkillSlot.Passive) == "被动", "四入口在详情映射为主动/被动两类，培养入口包装不变");
        foreach (int current in new[] { 2, 1, 0 }) {
            await CardSkillDetailControl.OpenAsync(cardId, CardSkillSlot.Passive);
            await Task.Delay(350);
            await Frames(4);
            var panel = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
            var value = new CardSkillDetailVO {
                cardId = cardId, cardName = CfgManager.tables.CardObj.Get(cardId).Name,
                slot = current == 1 ? CardSkillSlot.Active : CardSkillSlot.Passive, typeName = current == 1 ? "主动" : "被动",
                skillName = current == 0 ? "隔离测试 · 全部未解锁" : "隔离测试 · " + current + "阶生效",
                missing = new string[0],
                stages = Enumerable.Range(1, 3).Select(rank => new CardSkillStageVO {
                    rank = rank, effect = "测试效果：造成" + rank * 10 + "点伤害\n本阶独立生效，不累加",
                    unlockCondition = rank * 4 + "级解锁", unlocked = rank <= current, current = rank == current
                }).ToArray()
            };
            panel.Render(value);
            await Frames(8);
            var items = panel.listStages.GetComponentsInChildren<CardSkillStageItem>(true).Where(item => item.gameObject.activeInHierarchy && !string.IsNullOrEmpty(item.txtRank.text)).ToArray();
            Check(items.Length == 3, "当前阶" + current + "：三个阶数均可见");
            Check(items.Count(item => item.imgStageCurrent.enabled) == (current == 0 ? 0 : 1), "当前阶" + current + "：唯一当前高亮或全锁无高亮");
            foreach (var item in items) {
                int rank = int.Parse(item.txtRank.text.Replace("阶", ""));
                var expected = value.stages.Single(stage => stage.rank == rank);
                bool active = expected.current && expected.unlocked;
                Check(item.imgLock.enabled == !expected.unlocked && item.imgStageCurrent.enabled == active && item.imgStageNormal.enabled == !active, "当前阶" + current + "：" + rank + "阶锁图和互斥底板正确");
                Check(item.txtCondition.text == (active ? "当前" : expected.unlocked ? "已解锁" : expected.unlockCondition), "当前阶" + current + "：" + rank + "阶状态/解锁条件正确");
                item.txtEffect.ForceMeshUpdate(); item.txtCondition.ForceMeshUpdate();
                Check(item.txtEffect.text == expected.effect && !item.txtEffect.isTextOverflowing && !item.txtCondition.isTextOverflowing, "当前阶" + current + "：" + rank + "阶效果独立且文本不溢出");
            }
            var badge = panel.boxWindow.Find("imgTypeBg").GetComponent<Image>();
            Check(badge.type == Image.Type.Sliced && badge.sprite.border.x == 22 && badge.sprite.border.z == 22, "类型底板使用九宫格并保护左右端帽");
            string expectedType = current == 1 ? "主动" : "被动";
            Check(panel.txtType.text == expectedType, "详情类型只显示" + expectedType + "两个字");
            Check(badge.material.shader.name == (current == 1 ? "UI/Default" : "Starfront/UI/PassiveSkillBadge"), "类型标签按主动蓝色/被动紫色切换且重开无残留");
            int textWidth = Mathf.CeilToInt(panel.txtType.GetPreferredValues(expectedType).x);
            Check(Mathf.Abs(badge.rectTransform.rect.width - (textWidth + 44)) < .1f && !panel.txtType.isTextOverflowing, "主动/被动底板仍按文本实测宽度适配且无溢出");
            foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>()) { text.SetVerticesDirty(); text.SetMaterialDirty(); }
            Canvas.ForceUpdateCanvases();
            await Frames(2);
            Capture("rank-display-current-" + current + ".png");
            Click(panel.btnClose.GetComponent<RectTransform>());
            await Task.Delay(300);
            await Frames(4);
            Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId) && ReferenceEquals(PanelMgr.ins.GetPanel(CardCultivationControl.PanelId), source) && source.CurrentCardId == cardId, "当前阶" + current + "：关闭后保留原培养页和原卡");
        }
        Check(session.model.Revision == revision && session.Cards.model.GetState(cardId).level == 1, "测试阶数未改变来源卡等级及进度版本");
        PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
    }

    private static async Task RunCurrentSkillSmoke(PlayerProgressControl session) {
        int cardId = CfgManager.tables.CardObj.DataList.First(card => card.ShowInLibrary && card.MaxLevel > 1).Id;
        var saved = new TeamSchemeSaveVO();
        saved.schemes[0] = new[] { cardId, 0, 0, 0 };
        var oldTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, saved, ignored => { }));
        var functionField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object oldFunctions = functionField.GetValue(null);
        string folder = "Library/AIUI/SkillSmoke/" + SessionState.GetString(Key, "");
        Directory.CreateDirectory(folder);
        functionField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, folder + "/functions.json")));
        const string mainId = "default/mainView/MainPanel";
        try {
            var visible = CfgManager.tables.CardObj.DataList.Where(card => card.ShowInLibrary).ToArray();
            int entries = CfgManager.tables.CardSkillEntryObj.DataList.Count(entry => visible.Any(card => card.Id == entry.CardId));
            Check(true, "正式可见卡" + visible.Length + "张；培养技能关联" + entries + "条；当前卡" + cardId);
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(15);
            var main = PanelMgr.ins.GetPanel(mainId) as MainPanel;
            Click(main.btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            await Frames(8);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            Check(view.slots[0].title.text == CfgManager.tables.CardObj.Get(cardId).Name, "实际阵容上阵位显示当前可见卡" + cardId);
            Capture("skill-smoke-formation.png");
            Click(view.slots[0].button.GetComponent<RectTransform>());
            var panel = await CultivationOpened();
            Check(panel.CurrentCardId == cardId, "实际阵容点击进入培养页当前卡" + cardId);
            Capture("skill-smoke-cultivation-before.png");
            long revision = session.model.Revision;
            int level = session.Cards.model.GetState(cardId).level;
            foreach (cfg.SkillDisplaySlot slot in Enum.GetValues(typeof(cfg.SkillDisplaySlot))) {
                string label = CardSkillQuery.TypeName(slot);
                var selected = CardSkillQuery.Read(CfgManager.tables, cardId, level, slot);
                var item = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Single(value => value.txtSkillKind.text == label);
                var scroll = panel.listSkills.GetComponent<ScrollRect>().content.anchoredPosition;
                Click(item.btnSkill.GetComponent<RectTransform>());
                for (int i = 0; i < 100 && !PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId); i++) await Frames(2);
                await Task.Delay(350);
                await Frames(4);
                var detail = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
                Check(detail != null && detail.isOpened && detail.txtTitle.text == selected.name && detail.txtType.text == (slot == cfg.SkillDisplaySlot.Passive ? "被动" : "主动"), "所点" + label + "入口与弹窗标题/类型一致，skillId=" + selected.skillId);
                Check(detail.txtMissing.text.Length > 0 && selected.missing.Count > 0, label + "真实缺项单列，不冒充正式技能阶数");
                Capture("skill-smoke-detail-" + slot + ".png");
                if (slot == cfg.SkillDisplaySlot.Basic) {
                    var hit = Click(panel.btnUpgrade.GetComponent<RectTransform>());
                    Check(hit != null && hit.transform.IsChildOf(detail.transform) && session.model.Revision == revision, "弹层遮罩拦截底层升级，未修改进度");
                }
                if (PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId)) Click(detail.btnClose.GetComponent<RectTransform>());
                await Task.Delay(300);
                await Frames(4);
                Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId) && ReferenceEquals(PanelMgr.ins.GetPanel(CardCultivationControl.PanelId), panel) && panel.CurrentCardId == cardId && panel.listSkills.GetComponent<ScrollRect>().content.anchoredPosition == scroll, label + "关闭返回同一培养页、原卡及技能滚动");
                Capture("skill-smoke-return-" + slot + ".png");
            }
            Check(session.model.Revision == revision && session.Cards.model.GetState(cardId).level == level, "试跑前后卡牌等级和进度版本不变");
        } finally {
            CardSkillDetailControl.Close();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(oldTeam);
            functionField.SetValue(null, oldFunctions);
        }
    }

    private static async Task Run(PlayerProgressControl session) {
        const string mainId = "default/mainView/MainPanel";
        var saved = new TeamSchemeSaveVO();
        saved.schemes[0] = new[] { 1101, 0, 0, 0 };
        var team = new TeamEditControl(CfgManager.tables, session.Cards.model, saved, ignored => { }, id => 1);
        var previousTeam = TeamEditControl.InstallValidationSession(team);
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(25);
            var main = PanelMgr.ins.GetPanel(mainId) as MainPanel;
            main.btnDeck.onClick.Invoke();
            await Frames(20);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            Check(view != null && view.gameObject.activeInHierarchy, "真实MainPanel阵容视图已打开");
            Capture("cultivation-entry-main.png");
            Click(view.slots[0].button.GetComponent<RectTransform>());
            var panel = await CultivationOpened();
            Check(panel.txtPowerValue.text == "179" && panel.txtPower.text == "临时战力" && panel.txtDamageCurrent.text == "—", "临时战力179明确标注且不伪造独立攻击力");
            Check(panel.txtLevel.text == "1" && panel.txtQuality.text == CfgManager.tables.CardObj.Get(1101).QualityLabel, "批准v6：等级与品质读取正式卡牌");
            Check(panel.txtUnitHpCurrent.text == "100" && panel.txtUnitHpDelta.text == "+10" && panel.txtBuildingHpCurrent.text == "300", "批准v6：单位及所属建筑属性读取现有查询");
            Check(panel.txtProductionCurrent.text == "10秒" && panel.txtProductionDelta.text == "−0.5秒" && panel.txtProductionDelta.color.g > panel.txtProductionDelta.color.r, "批准v6：生产间隔降低仍显示绿色收益");
            var materialView = panel.listMaterials.GetComponentInChildren<CardCultivationMaterialItem>();
            Check(materialView != null && materialView.txtMaterialOwned.text == "0" && materialView.txtMaterialRequired.text == "15", "批准v6：实际材料拥有和需求数量分色显示");
            var numberRect = materialView.txtMaterialRequired.rectTransform;
            Check(Mathf.Abs(numberRect.anchoredPosition.x + numberRect.rect.width - 56) < .1f && materialView.txtMaterialRequired.alignment == TextAlignmentOptions.MidlineRight, "批准v6：材料数量右端固定在图标右下角");
            Check(panel.listMaterials.GetComponent<ScrollRect>().viewport.anchoredPosition.x > 120, "批准v6：单个实际材料居中，无虚构槽位");
            Check(panel.CurrentCardId == 1101 && panel.Source == "formation", "真实上阵位进入点击的卡牌");
            Check(!panel.btnPrevious.interactable && !panel.btnNext.interactable, "单张非空上阵序列首尾均禁用");
            Check(panel.btnReturn.GetComponentInChildren<TextMeshProUGUI>().text == "返回阵容" && panel.listFunctionTabs.GetComponentInChildren<CardCultivationTabItem>().btnTab.GetComponentInChildren<TextMeshProUGUI>().text == "升级", "底部返回与升级标签文字可见");
            var backgroundImage = panel.imgRaceBackdrop;
            Check(backgroundImage.enabled && backgroundImage.sprite != null && Mathf.Abs(backgroundImage.rectTransform.rect.height - 414) < .1f, "v7：配置背景加载到批准顶部区域，不拉伸整屏");
            Check(panel.imgPodium.enabled && panel.imgPodium.sprite.name == "temp-podium-ring", "v7：台座仅用批准顶环，未恢复前置铭牌");
            Check(panel.txtRangeCurrent.transform.parent.localPosition.y > panel.txtBuildingHpCurrent.transform.parent.localPosition.y && panel.txtSpeedCurrent.transform.parent.localPosition.y > panel.txtProductionCurrent.transform.parent.localPosition.y, "v7：上四项单位、下两项建筑的整页分组");
            Check(panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().Any(item => item.imgSkillIcon.enabled && item.imgSkillIcon.sprite != null), "v7：临时技能符号保留，正式未知状态不伪造");
            Check(materialView.imgMaterialIcon.enabled && materialView.imgMaterialIcon.sprite.name == "temp-material-symbol", "v7：实际成本使用批准临时材料图，数量仍固定右下角");
            Check(panel.imgUnitHpAttribute.enabled && panel.imgUnitHpAttribute.sprite.name == "attr-unit-hp", "v7：属性定义表驱动独立图标加载");
            Capture("cultivation-entry-formation.png");
            Click(panel.btnReturn.GetComponent<RectTransform>());
            await Frames(8);
            Check(!PanelMgr.ins.IsPanelOpen(CardCultivationControl.PanelId) && view.gameObject.activeInHierarchy, "返回原MainPanel实例");
            if (view.filters != null && view.filters.Length > 1) {
                Click(view.filters[1].GetComponent<RectTransform>());
            }
            await Frames(10);
            var scroll = view.library.GetComponent<ScrollRect>();
            scroll.verticalNormalizedPosition = 0;
            await Frames(10);
            Vector2 position = scroll.content.anchoredPosition;
            int[] order = view.library.arraySource.Cast<DeckCardView.DisplayData>().Select(c => c.cardId).ToArray();
            Check(order.Length > 1, "进入时取得实际筛选牌库序列");
            scroll.verticalNormalizedPosition = 1;
            await Frames(8);
            position = scroll.content.anchoredPosition;
            var libraryCell = view.library.GetCell(1).GetComponent<DeckLibraryCard>();
            Click(libraryCell.button.GetComponent<RectTransform>());
            panel = await CultivationOpened();
            Check(panel.CurrentCardId == order[1] && panel.Source == "library", "真实筛选牌库非首张点击定位正确");
            Capture("cultivation-entry-library.png");
            Check(panel.txtUpgrade.text == "解锁", "未拥有卡培养按钮显示解锁");
            var unlock = session.Cards.model.GetState(order[1]);
            foreach (var cost in unlock.costs) {
                Check(session.Inventory.TryGrantItem(cost.Key, cost.Value, out string unlockReason), "隔离准备实际解锁材料");
            }
            await Frames(6);
            Click(panel.btnUpgrade.GetComponent<RectTransform>());
            await Frames(6);
            Check(session.Cards.model.GetState(order[1]).level == 1 && session.Inventory.model.GetQuantity(CfgManager.tables.CardObj.Get(order[1]).FragmentItemId) == 0, "真实解锁按钮一次提交并联合扣料");
            Check(view.transform.Find("CardGrowthPopup") == null, "培养入口不再显示旧二次确认窗");
            Capture("cultivation-unlocked.png");
            Click(panel.btnPrevious.GetComponent<RectTransform>());
            await Frames(10);
            Check(panel.CurrentCardId == order[0] && !panel.btnPrevious.interactable, "牌库向前按来源顺序并在首张禁用");
            // Disabled buttons do not dispatch pointer events; direct repeated invocation additionally tests endpoint guards.
            panel.btnPrevious.onClick.Invoke();
            Check(panel.CurrentCardId == order[0], "首张不循环到尾部");
            Capture("cultivation-card-upgrade.png");
            var skill = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().First(item => item.txtSkillKind.text == "普攻");
            Vector2 skillScroll = panel.listSkills.GetComponent<ScrollRect>().content.anchoredPosition;
            Click(skill.btnSkill.GetComponent<RectTransform>());
            for (int i = 0; i < 200 && !PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId); i++) {
                await Frames(2);
            }
            await Frames(8);
            var detail = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
            Check(detail != null && detail.isOpened && detail.txtTitle.text == CfgManager.tables.SkillObj.Get(1101).Name, "培养页实际普攻入口打开当前卡技能详情");
            Capture("cultivation-skill-detail.png");
            var hit = Click(panel.btnUpgrade.GetComponent<RectTransform>());
            Check(hit.transform.IsChildOf(detail.transform) && session.Cards.model.GetState(1101).level == 1, "技能弹层遮挡培养升级操作");
            if (PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId)) {
                Click(detail.btnClose.GetComponent<RectTransform>());
            }
            await Frames(8);
            Check(panel.CurrentCardId == 1101 && panel.listSkills.GetComponent<ScrollRect>().content.anchoredPosition == skillScroll, "弹层关闭恢复原卡及技能滚动");
            skill = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>().First(item => item.txtSkillKind.text == "普攻");
            Click(skill.btnSkill.GetComponent<RectTransform>());
            await Frames(10);
            detail = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
            Click(detail.btnClose.GetComponent<RectTransform>());
            await Frames(8);
            Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId) && panel.CurrentCardId == 1101, "详情内部关闭按钮返回同一培养页");
            // Insufficient then exact-cost submit; second click while processing cannot consume a second level.
            Click(panel.btnUpgrade.GetComponent<RectTransform>());
            await Frames(4);
            await GlobalToast.EnsureReadyAsync();
            for (int i = 0; i < 160 && !GlobalToast.Host.ActiveItems.Any(item => item.Message.Contains("不足")); i++) {
                await Frames(1);
            }
            Check(session.Cards.model.GetState(1101).level == 1 && GlobalToast.Host != null && GlobalToast.Host.ActiveItems.Any(item => item.Message.Contains("不足")), "材料不足提示且不改变等级");
            var growth = session.Cards.model.GetState(1101);
            foreach (var cost in growth.costs) {
                Check(session.Inventory.TryGrantItem(cost.Key, cost.Value, out string reason), "隔离会话准备实际所需材料");
            }
            await Frames(8);
            long revision = session.model.Revision;
            panel.btnUpgrade.onClick.Invoke();
            panel.btnUpgrade.onClick.Invoke();
            await Frames(8);
            Check(session.Cards.model.GetState(1101).level == 2 && session.model.Revision == revision + 1 && session.Inventory.model.GetQuantity(3101) == 0, "一次升级联合扣料保存，重复点击只提交一次");
            Check(GlobalToast.Contains("升级成功", false), "培养成功反馈进入全局提示，页面不承载瞬时txtHint");
            Capture("cultivation-upgraded.png");
            for (int i = 1; i < order.Length; i++) {
                Click(panel.btnNext.GetComponent<RectTransform>());
                await Frames(3);
                Check(panel.CurrentCardId == order[i], "牌库来源序列顺序：" + order[i]);
            }
            Check(!panel.btnNext.interactable, "牌库末张禁用下一张");
            panel.btnNext.onClick.Invoke();
            Check(panel.CurrentCardId == order[order.Length - 1], "末张不循环");
            Click(panel.btnReturn.GetComponent<RectTransform>());
            await Frames(10);
            Check(view.library.arraySource.Cast<DeckCardView.DisplayData>().Select(c => c.cardId).SequenceEqual(order) && (scroll.content.anchoredPosition - position).sqrMagnitude < .1f, "返回恢复筛选序列及滚动位置");
            Check(view.slots[0].level.text.Contains("2"), "返回刷新上阵卡真实新等级");
            Capture("cultivation-return-main.png");
            // Nonzero scroll restoration is verified from an actual visible cell after scrolling.
            scroll.verticalNormalizedPosition = 0;
            await Frames(10);
            position = scroll.content.anchoredPosition;
            var visible = view.library.GetComponentsInChildren<DeckLibraryCard>().First(c => c.gameObject.activeInHierarchy);
            Click(visible.button.GetComponent<RectTransform>());
            panel = await CultivationOpened();
            panel.OnPanelOperate(PanelOperateEnum.ESC);
            await Frames(10);
            Check((scroll.content.anchoredPosition - position).sqrMagnitude < .1f, "非零滚动来源进入及ESC返回恢复");
            // Open then leave source immediately; pending load or open page cannot survive source teardown.
            view.RequestCardInfo(order[0]);
            CardCultivationControl.CloseForSource(view);
            await Frames(10);
            Check(!PanelMgr.ins.IsPanelOpen(CardCultivationControl.PanelId), "来源离开取消在途/已开培养页");
            PanelMgr.ins.ClosePanelByType(mainId);
            await ValidateSkillStates();
        } finally {
            CardSkillDetailControl.Close();
            PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(previousTeam);
        }
    }

    private static CardSkillDetailVO Fixture(int current) {
        return new CardSkillDetailVO {
            cardId = 1101, slot = CardSkillSlot.Passive, typeName = "被动", skillName = "测试技能 · 隔离样例",
            missing = new string[0],
            stages = Enumerable.Range(1, 3).Select(rank => new CardSkillStageVO { rank = rank, effect = "测试效果：对应阶数内容\n不叠加其他阶数效果", unlockCondition = rank * 4 + "级解锁", unlocked = rank <= current, current = rank == current }).ToArray()
        };
    }

    private static void ValidateBindings(CardSkillDetailPanel panel) {
        var binders = panel.GetComponentsInChildren<UIBinder>(true);
        Check(panel.GetComponent<UIBinder>().uiList.Count == 10, "弹窗10个必要动态成员由项目生成器导出");
        foreach (var binder in binders) {
            Check(binder.uiList.All(value => value.go != null), "绑定引用完整：" + binder.name);
        }
        Check(panel.imgWindowBg.rectTransform.sizeDelta == new Vector2(516, 436), "语义窗口命中与柔光画布分离");
        var visual = panel.boxWindow.Find("imgBg").GetComponent<Image>();
        Check(visual.sprite != null && visual.rectTransform.sizeDelta == new Vector2(529, 450) && !visual.raycastTarget, "完整主框及阴影按529×450原样显示，不扩大命中区");
        Check(panel.btnClose.image.sprite.name == "skillClose" && panel.btnClose.GetComponentsInChildren<Image>(true).Length == 1, "关闭按钮只使用一张合成素材");
        var list = panel.listStages;
        Check(list.itemTemplate.GetComponent<UIBinder>().uiList.Count == 6, "复用条目6个必要成员由既有生成器导出");
        Check(list.itemTemplate.GetComponent<RectTransform>().sizeDelta == new Vector2(480, 105) && list.vertGap == -8, "105高光晕画布与97像素条目节距匹配");
        var scroll = list.GetComponent<ScrollRect>();
        Check(scroll.viewport.parent == list.transform && scroll.content.parent == scroll.viewport && list.itemTemplate.transform.parent == scroll.content && scroll.viewport.GetComponent<Mask>() != null, "标准列表层级、遮罩和引用正确");
        foreach (RectTransform rect in panel.GetComponentsInChildren<RectTransform>(true)) {
            Check(Mathf.Approximately(rect.sizeDelta.x, Mathf.Round(rect.sizeDelta.x)) && Mathf.Approximately(rect.sizeDelta.y, Mathf.Round(rect.sizeDelta.y)), "整数尺寸：" + rect.name);
        }
        foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>(true)) {
            foreach (char character in text.text) {
                Check(char.IsWhiteSpace(character) || text.font.HasCharacter(character, true, true), "字体覆盖：" + character);
            }
        }
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

    private static void Resize(int width, int height) {
        var previous = target;
        target = new RenderTexture(width, height, 24);
        target.Create();
        camera.targetTexture = target;
        camera.orthographicSize = height / 2f;
        previous.Release();
        UnityEngine.Object.Destroy(previous);
        Canvas.ForceUpdateCanvases();
    }

    private static void Capture(string name) {
        // 普攻试跑沿用Canvas正常重建后的材质状态，不在拍摄时强制重建每个TMP。
        Canvas.ForceUpdateCanvases();
        if (!SessionState.GetBool(Key + ".marineBasic", false)) {
            foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>()) {
                text.ForceMeshUpdate();
            }
            Canvas.ForceUpdateCanvases();
        }
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
            throw new InvalidOperationException("SkillDetail: " + message);
        }
        checks.Add(message);
    }

    private static void WriteResult(string id, Exception error) {
        Directory.CreateDirectory("Library/AIUI");
        string json = JsonUtility.ToJson(new Result { requestId = id, status = error == null ? "passed" : "failed", error = error?.ToString(), checks = checks.ToArray() }, true);
        File.WriteAllText(ResultPath, json);
        string evidence = SessionState.GetString(Key + ".evidence", "");
        if (!string.IsNullOrEmpty(evidence)) { Directory.CreateDirectory(evidence); File.WriteAllText(Path.Combine(evidence, id + ".json"), json); }
    }

    private static async Task RunAtlasCleanup2() {
        var ids = CfgManager.tables.CardObj.DataList.Where(c => c.ShowInLibrary).Select(c => c.Id).OrderBy(id => id).ToArray();
        var savedPlayer = new PlayerProgressSaveVO { initialized = true };
        foreach (int id in ids) savedPlayer.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = id, level = 1 });
        int writes = 0;
        var player = new PlayerProgressControl(CfgManager.tables, savedPlayer, ignored => writes++);
        writes = 0;
        var previousPlayer = PlayerProgressControl.InstallValidationSession(player);
        var teams = new TeamSchemeSaveVO(); teams.schemes[0] = ids.Take(4).ToArray();
        var previousTeam = TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, player.Cards.model, teams, ignored => writes++));
        var functionField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        var previousFunctions = functionField.GetValue(null);
        string functionPath = "Library/AIUI/atlas-cleanup-02/runtime/functions.json";
        functionField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, functionPath)));
        try {
            const string mainId = "default/mainView/MainPanel";
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId); await Frames(20);
            var main = (MainPanel)PanelMgr.ins.GetPanel(mainId); Click(main.btnDeck.GetComponent<RectTransform>()); await Task.Delay(350); await Frames(12);
            var deck = main.GetComponentInChildren<DeckPresentationView>(true);
            Check(deck != null && deck.isActiveAndEnabled, "散图统一：真实MainPanel阵容入口可用"); Capture("atlas-main-runtime.png");
            await TeamEditControl.OpenAsync(); await Frames(20);
            Check(PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId), "散图统一：真实队伍编辑入口可用"); Capture("atlas-team-runtime.png");
            var teamPanel=(TeamEditPanel)PanelMgr.ins.GetPanel(TeamEditControl.PanelId);
            for(int openWait=0; openWait<180 && !teamPanel.isOpened; openWait++){ await Task.Delay(16); await Frames(1); }
            Check(teamPanel.isOpened,"散图统一：队伍编辑实际打开动画完成");
            Check(!teamPanel.Draft.Dirty,"散图统一：队伍编辑初始草稿未变");
            TeamEditControl.RequestClose();
            for(int closeWait=0; closeWait<180 && PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId); closeWait++){ await Task.Delay(16); await Frames(1); }
            Check(!PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId), "散图统一：队伍编辑原样关闭无草稿写入");
            var currentField = typeof(CardCultivationSkillItem).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (int id in ids) {
                deck.RequestCardInfo(id); var panel = await CultivationOpened();
                for(int i=0;i<120 && (panel.CurrentCardId != id || panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false).Any(c => !c.imgSkillIcon.enabled || c.imgSkillIcon.sprite == null));i++) await Frames(2);
                Check(panel.CurrentCardId == id, "散图统一：切卡=" + id);
                var cells = panel.listSkills.GetComponentsInChildren<CardCultivationSkillItem>(false);
                Check(cells.Length == 4, "散图统一：四技能单元=" + id);
                foreach(var cell in cells) {
                    var state = (CardSkillStateVO)currentField.GetValue(cell);
                    bool atlas = state.iconAddress.StartsWith(ResourceConst.PATH_ATLAS, StringComparison.Ordinal);
                    Check(!atlas,"散图统一：所有技能使用配置中的独立图片="+state.iconAddress);
                    Check(cell.imgSkillIcon.sprite!=null && cell.imgSkillIcon.sprite==ResourceManager.GetUnpackImage(state.iconAddress),"散图统一：培养列表真实散图Sprite="+state.iconAddress);
                }
                if(id==2102 || id==2201 || id==2301) {
                    Capture("atlas-cultivation-"+id+"-runtime.png");
                    var first=cells[0];var state=(CardSkillStateVO)currentField.GetValue(first);
                    Click(first.btnSkill.GetComponent<RectTransform>());await Frames(20);
                    var detail=(CardSkillDetailPanel)PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId);
                    for(int i=0;i<100&&!detail.imgSkillIcon.enabled;i++)await Frames(2);
                    Check(detail!=null&&detail.imgSkillIcon.sprite==first.imgSkillIcon.sprite,"散图统一：详情复用同一图集Sprite="+id);Capture("atlas-detail-"+id+"-runtime.png");
                    CardSkillDetailControl.Close();await Frames(15);
                    Check(first.imgSkillIcon.enabled&&first.imgSkillIcon.sprite!=null,"散图统一：详情关闭不释放培养图标="+id);
                }
                Click(panel.btnReturn.GetComponent<RectTransform>());await Task.Delay(250);await Frames(8);
            }
            Check(writes==0,"散图统一：隔离进度及队伍无写入，真实存档未使用");
        } finally {
            CardSkillDetailControl.Close();PanelMgr.ins.ClosePanelByType(CardCultivationControl.PanelId);PanelMgr.ins.ClosePanelByType(TeamEditControl.PanelId);PanelMgr.ins.ClosePanelByType("default/mainView/MainPanel");
            TeamEditControl.InstallValidationSession(previousTeam);PlayerProgressControl.InstallValidationSession(previousPlayer);functionField.SetValue(null,previousFunctions);
        }
    }

}
#endif

