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
/// 队伍编辑专项的隔离Play验证，不读写真实玩家进度。
/// </summary>
/// <remarks>四卡样例仅用于隔离状态机测试，运行截图读取正式卡牌配置。</remarks>
[InitializeOnLoad]
public static class TeamEditValidation {

    private const string Key = "TeamEdit.Validation.v1";

    private const string Pending = "Library/AIUI/pending-team-edit.json";

    private const string ResultPath = "Library/AIUI/team-edit-result.json";

    [Serializable] private sealed class Request { public string requestId; public int processId; public bool motionOnly; public bool toastCoverage; }

    [Serializable] private sealed class Result { public string requestId; public string status; public string error; public string[] checks; }

    [Serializable] private sealed class SceneBackup { public RootState[] roots; }

    [Serializable] private sealed class RootState { public int scene; public int index; public string name; public bool active; }

    private static readonly List<string> checks = new List<string>();

    private static string runtimeError;

    private static int testCardId;

    private static Camera camera;

    private static RenderTexture target;

    private static Canvas canvas;

    static TeamEditValidation() {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnState;
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
            SessionState.SetBool(Key + ".motionOnly", request.motionOnly);
            SessionState.SetBool(Key + ".toastCoverage", request.toastCoverage);

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
            foreach (var field in clockFields) {
                field.SetValue(null, new Timer());
            }
            var loading = CfgManager.EnsureLoadedAsync();
            if (await Task.WhenAny(loading, Task.Delay(45000)) != loading) {
                throw new TimeoutException("Configuration load timed out");
            }
            await loading;
            int writes = 0;
            testCardId = CfgManager.tables.CardObj.DataList.First(card => card.ShowInLibrary).Id;
            var seed = new PlayerProgressSaveVO { initialized = true };
            seed.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = testCardId, level = 1 });
            if (SessionState.GetBool(Key + ".toastCoverage", false)) {
                foreach (var card in CfgManager.tables.CardObj.DataList.Where(card => card.ShowInLibrary && card.Id != testCardId).Take(4)) {
                    seed.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = card.Id, level = 1 });
                }
            }
            var session = new PlayerProgressControl(CfgManager.tables, seed, ignored => writes++);
            writes = 0; // 新会话的初始种子仅写入内存回调，验证从初始化完成后计数。
            previousSession = PlayerProgressControl.InstallValidationSession(session);
            installed = true;
            BuildStage();
            UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(0, 0, 720, 1280) };
            if (SessionState.GetBool(Key + ".toastCoverage", false)) {
                await ValidateGlobalToasts();
            }
            var validation = SessionState.GetBool(Key + ".motionOnly", false) ? ValidateCommonWindows(session) : Run(session);
            if (await Task.WhenAny(validation, Task.Delay(60000)) != validation) {
                throw new TimeoutException("队伍编辑验证超过60秒，见team-edit-progress.txt");
            }
            await validation;
            Check(writes == 0, "真实玩家存档未访问；隔离成长与背包没有额外写入");
            if (!string.IsNullOrEmpty(runtimeError)) {
                throw new InvalidOperationException(runtimeError);
            }
            WriteResult(id, null);
        } catch (Exception error) {
            WriteResult(id, error);
            Debug.LogException(error);
        } finally {
            GlobalToast.Shutdown();
            PanelMgr.ins.ClosePanelByType(TeamEditControl.PanelId);
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

    /**独立组件验收与真实MainPanel往返分别记录；不安装生产玩家存档。*/
    private static async Task ValidateGlobalToasts() {
        int start = checks.Count;
        try {
            GlobalToast.Shutdown();
            GlobalToast.Warn("卡牌未解锁");
            GlobalToast.Warn("卡牌未解锁");
            await GlobalToast.EnsureReadyAsync();
            var host = GlobalToast.Host;
            Check(host != null && host.ActiveCount == 2 && host.ActiveItems.All(item => item.Message == "卡牌未解锁"), "Toast组件：首次并发调用复用加载且相同文案各显示一条");
            Check(ConfigValueHelper.GetInt(1300) == 3 && GlobalToast.MaxVisible == 3, "Toast组件：正式常量1300按普通整数读取三条上限");
            float previousY = host.ActiveItems[1].Trans.anchoredPosition.y;
            GlobalToast.Warn("卡牌未解锁");
            RookieEngine.timer.SyncTime(250);
            Check(host.ActiveCount == 3 && host.ActiveItems.All(item => item.Message == "卡牌未解锁"), "Toast组件：重复调用三次叠加三条相同提示");
            Check(host.ActiveItems[1].Trans.anchoredPosition.y > previousY, "Toast组件：新提示到来时旧提示向上叠加");
            Capture("global-toast-repeated.png");
            GlobalToast.Clear();
            Check(host.KeepOpenOnCloseAll && !host.ReceivesPanelInput && !host.UsesWindowTransition, "Toast组件：被动常驻层不启用弹窗输入或统一过渡");
            GlobalToast.Show("队伍已保存，已选用方案 1");
            GlobalToast.Warn("队伍已满，请先点击上阵立绘下阵");
            GlobalToast.Warn("队伍已满，请先点击上阵立绘下阵");
            Check(host.ActiveCount == 3 && GlobalToast.PendingCount == 0, "Toast组件：普通与重复警告可同时显示三条");
            RookieEngine.timer.SyncTime(250);
            Check(host.ActiveItems.All(item => item.GetComponent<CanvasGroup>().alpha == 1), "Toast组件：入场后可见并短暂停留");
            for (int i = 0; i < 3; i++) {
                var item = host.ActiveItems[i];
                item.txtMessage.ForceMeshUpdate();
                Check(item.txtMessage.textInfo.lineCount == 1 && !item.txtMessage.enableWordWrapping && !item.txtMessage.richText, "Toast组件：提示保持单行且不解析富文本 " + i);
                Check(item.Trans.rect.height == 44 && item.Trans.rect.width <= 640 && item.Trans.rect.width >= 300, "Toast组件：底板宽度受限且高度44 " + i);
                Check(item.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), "Toast组件：文字及底板射线关闭 " + i);
                var gradient = item.GetComponentInChildren<ToastGradientGraphic>();
                Check(gradient.GetComponent<CanvasRenderer>() != null, "Toast组件：渐变具备实际CanvasRenderer绘制依赖 " + i);
                gradient.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                gradient.Rebuild(CanvasUpdate.PreRender);
                var mesh = gradient.canvasRenderer.GetMesh();
                Check(mesh != null && mesh.vertexCount > 0 && mesh.colors.Any(value => value.a > .7f) && mesh.colors.Any(value => value.a == 0), "Toast组件：底板实际网格具备中间半透明及两端透明Alpha " + i);
                foreach (char letter in item.Message) {
                    Check(item.txtMessage.font.HasCharacter(letter, true, true), "Toast组件：项目字库覆盖 " + letter);
                }
                if (i > 0) {
                    Check(host.ActiveItems[i - 1].Trans.anchoredPosition.y - item.Trans.anchoredPosition.y >= 56, "Toast组件：连续条目12像素以上净间距 " + i);
                }
            }
            Capture("global-toast-component-three.png");
            for (int i = 0; i < 12; i++) {
                GlobalToast.Show("等待提示 " + i);
                if (i == 0) {
                    GlobalToast.Show("等待提示 0");
                }
            }
            GlobalToast.Show("等待提示 0");
            Check(GlobalToast.PendingCount == 10 && host.ActiveCount == 3, "Toast组件：重复内容也进入等待队列且保持十条等待上限");
            RookieEngine.timer.SyncTime(1600);
            Check(host.ActiveItems.Select(item => item.Message).SequenceEqual(new[] { "等待提示 0", "等待提示 0", "等待提示 1" }), "Toast组件：先进先出且退出后立即回收填充");
            for (int i = 0; i < 5; i++) {
                RookieEngine.timer.SyncTime(1600);
            }
            Check(host.ActiveCount == 0 && GlobalToast.PendingCount == 0 && host.PoolCount == 3, "Toast组件：队列播完自动回收且对象池上限三项");
            GlobalToast.Show(new string('长', 80) + "\n单行");
            RookieEngine.timer.SyncTime(250);
            var longItem = host.ActiveItems[0];
            longItem.txtMessage.ForceMeshUpdate();
            Check(longItem.txtMessage.textInfo.lineCount == 1 && !longItem.Message.Contains("\n"), "Toast组件：超长与含换行输入仍单行，TMP缩字并省略");
            Check(host.PoolCount == 2 && longItem.txtMessage.color == Color.white && longItem.Age < .3f, "Toast组件：复用恢复字色和动画时间");
            GlobalToast.Clear();
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(UIEnum.WARNING_PANEL), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(UIEnum.WARNING_PANEL, new object[] { "这是隔离验证确认框，仍保留选择操作。", null });
            await Task.Delay(300);
            await Frames(3);
            var warning = PanelMgr.ins.GetPanel(UIEnum.WARNING_PANEL) as WarningPanel;
            GlobalToast.Warn("确认弹窗上方的全局提示");
            RookieEngine.timer.SyncTime(250);
            Check(host.transform.GetSiblingIndex() > warning.transform.GetSiblingIndex(), "Toast组件：全局提示绘制层高于19040通用确认框");
            Check(typeof(PanelMgr).GetField("topPanel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(PanelMgr.ins) == warning, "Toast组件：弹窗保留键盘焦点");
            Capture("global-toast-above-confirm.png");
            var button = warning.btnCancel.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, button.TransformPoint(button.rect.center)) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits.All(hit => !hit.gameObject.transform.IsChildOf(host.transform)), "Toast组件：弹窗按钮射线命中不被提示层截获");
            typeof(PanelMgr).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(PanelMgr.ins, new object[] { KeyCode.Escape });
            await Task.Delay(240);
            Check(!PanelMgr.ins.IsPanelOpen(UIEnum.WARNING_PANEL) && host.isOpened, "Toast组件：Esc关闭确认框且不关闭全局提示宿主");
            GlobalToast.Clear();
            GlobalToast.Show("页面切换期间继续显示");
            PanelMgr.ins.CloseAllPanel();
            await Frames(2);
            Check(host.isOpened && host.ActiveCount == 1, "Toast组件：CloseAll页面切换保留提示");
            GlobalToast.Shutdown();
            Check(GlobalToast.Host == null && GlobalToast.PendingCount == 0, "Toast组件：系统重置清空消息和宿主引用");
            GlobalToast.Show("重开提示");
            await GlobalToast.EnsureReadyAsync();
            Check(GlobalToast.Host == host && host.ActiveCount == 1, "Toast组件：宿主从现有窗口池复用且不重复创建");
            GlobalToast.Clear();
            Check(host.ActiveCount == 0 && host.PoolCount == 3, "Toast组件：重复调用及重开后无残留动画或消息");
            GlobalToast.Clear();
            GlobalToast.Warn("卡牌尚未解锁");
            RookieEngine.timer.SyncTime(250);
            var oldest = host.ActiveItems[0];
            float startY = oldest.Trans.anchoredPosition.y;
            float startAge = oldest.Age;
            GlobalToast.Warn("卡牌尚未解锁");
            Check(Mathf.Abs(oldest.Trans.anchoredPosition.y - startY) < .01f && oldest.Age == startAge, "Toast挤开：新消息出现时旧条位置与生命周期不瞬跳或重置");
            var motionY = new List<float>();
            for (int frame = 0; frame <= 24; frame++) {
                if (frame == 3) {
                    float beforeRetarget = oldest.Trans.anchoredPosition.y;
                    float beforeAge = oldest.Age;
                    Check(beforeRetarget > startY && beforeRetarget < startY + 56, "Toast挤开：45毫秒时旧条在一格移动途中");
                    GlobalToast.Warn("卡牌尚未解锁");
                    Check(Mathf.Abs(oldest.Trans.anchoredPosition.y - beforeRetarget) < .01f && oldest.Age == beforeAge, "Toast挤开：途中连续点击从当前位置接续且不重置淡出计时");
                }
                for (int i = 1; i < host.ActiveCount; i++) {
                    Check(host.ActiveItems[i - 1].Trans.anchoredPosition.y - host.ActiveItems[i].Trans.anchoredPosition.y >= 55.99f, "Toast挤开：动画逐帧保持12像素净间距 " + frame + ":" + i);
                }
                motionY.Add(oldest.Trans.anchoredPosition.y);
                Capture("global-toast-push-" + frame.ToString("D2") + ".png");
                if (frame < 24) {
                    RookieEngine.timer.SyncTime(15);
                }
            }
            Check(motionY.Zip(motionY.Skip(1), (a, b) => b >= a - .01f).All(value => value), "Toast挤开：连续让位过程中旧条持续向上不回跳");
            Check(Mathf.Abs(oldest.Trans.anchoredPosition.y - 232) < .01f, "Toast挤开：180毫秒OutQuad接续后准确落在第三格");
            Check(Mathf.Abs(oldest.Age - startAge - .36f) < .001f, "Toast挤开：让位只改变槽位运动，原消息年龄保持连续");
            var positions = host.ActiveItems.Select(item => item.Trans.anchoredPosition).ToArray();
            GlobalToast.Warn("满额等待的第四条");
            Check(GlobalToast.PendingCount == 1 && host.ActiveItems.Select(item => item.Trans.anchoredPosition).SequenceEqual(positions), "Toast挤开：满额后仍排队且不无故重启槽位移动");
            GlobalToast.Clear();
            GlobalToast.Show("复用后不残留挤开状态");
            RookieEngine.timer.SyncTime(250);
            Check(host.ActiveCount == 1 && Mathf.Abs(host.ActiveItems[0].Trans.anchoredPosition.y - 120) < .01f, "Toast挤开：回池复用恢复单条基准位置");
            GlobalToast.Clear();
            // 临时配置替身只存在于当前隔离Play会话，finally恢复正式表对象与解析缓存。
            var values = CfgManager.tables.ConfigValueObj.DataMap;
            var originalLimit = values[1300];
            try {
                foreach (int limit in new[] { 2, 4 }) {
                    values[1300] = new cfg.resource.ConfigValueResource(SimpleJSON.JSON.Parse("{\"id\":1300,\"type\":0,\"valueType\":1,\"content\":\"" + limit + "\"}"));
                    typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    GlobalToast.Clear();
                    for (int i = 0; i <= limit; i++) {
                        GlobalToast.Warn("配置容量重复测试");
                    }
                    Check(GlobalToast.MaxVisible == limit && host.ActiveCount == limit && GlobalToast.PendingCount == 1, "Toast组件：读取临时常量容量而非硬编码三条 " + limit);
                    GlobalToast.Clear();
                    Check(host.PoolCount <= limit, "Toast组件：条目池容量随常量上限调整 " + limit);
                }
                values[1300] = new cfg.resource.ConfigValueResource(SimpleJSON.JSON.Parse("{\"id\":1300,\"type\":0,\"valueType\":1,\"content\":\"0\"}"));
                typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                bool rejected = false;
                try {
                    int invalid = GlobalToast.MaxVisible;
                } catch (InvalidOperationException) {
                    rejected = true;
                }
                Check(rejected, "Toast组件：零条配置明确拒绝而非无限排队或静默回退");
            } finally {
                values[1300] = originalLimit;
                typeof(ConfigValueHelper).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                GlobalToast.Clear();
            }
            Check(GlobalToast.MaxVisible == 3 && host.PoolCount <= 3, "Toast组件：配置夹具恢复正式三条上限且不写源表");
            File.WriteAllText("Library/AIUI/global-toast-components-result.json", JsonUtility.ToJson(new Result { requestId = SessionState.GetString(Key, ""), status = "passed", checks = checks.Skip(start).ToArray() }, true));
        } catch (Exception error) {
            File.WriteAllText("Library/AIUI/global-toast-components-result.json", JsonUtility.ToJson(new Result { requestId = SessionState.GetString(Key, ""), status = "failed", error = error.ToString(), checks = checks.Skip(start).ToArray() }, true));
            throw;
        }
    }

    /**全局队列与原来的常驻txtHint不同，失败反馈须等实际展示后再断言和截图。*/
    private static async Task WaitToast(string fragment) {
        await GlobalToast.EnsureReadyAsync();
        for (int i = 0; i < 240; i++) {
            if (GlobalToast.Host.ActiveItems.Any(item => item.Message.Contains(fragment))) {
                RookieEngine.timer.SyncTime(250);
                return;
            }
            await Frames(1);
        }
        throw new TimeoutException("未显示队列提示：" + fragment);
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


    /**当前卡池未开放战斗资格：验证既有方案失效后的清空与提交，不伪造新卡可出战。*/
    private static async Task RunUnavailableCardEntry(PlayerProgressControl session) {
        string directory = "Library/AIUI/TeamEditEntry/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(directory);
        var store = new TeamSchemeStore(directory + "/team.json");
        var legacy = new TeamSchemeSaveVO();
        legacy.schemes[0][0] = testCardId;
        store.Save(legacy); // 仅此隔离测试文件：模拟旧方案中的卡在配置更新后失去资格。
        bool fail = false;
        var control = new TeamEditControl(CfgManager.tables, session.Cards.model, legacy, data => {
            if (fail) throw new IOException("隔离验证：磁盘写入失败");
            store.Save(data);
        });
        var previous = TeamEditControl.InstallValidationSession(control);
        var functionField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        var previousFunction = functionField.GetValue(null);
        functionField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, directory + "/functions.json")));
        const string mainId = "default/mainView/MainPanel";
        try {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(12);
            var main = PanelMgr.ins.GetPanel(mainId);
            var nav = main.GetComponent<MainPanel>().btnDeck;
            Click(nav.GetComponent<RectTransform>());
            await Task.Delay(260); await Frames(8);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            Check(view != null && view.editTeam != null, "正式阵容入口存在");
            Capture("team-edit-entry-main-before.png");
            Click(view.editTeam.GetComponent<RectTransform>());
            var panel = await Opened();
            Check(panel.boxWindow.sizeDelta == new Vector2(661, 967), "实际入口使用新窗口661x967");
            Check(panel.imgWindowBg.sprite.name == "window-frame", "实际入口使用批准金属外框");
            Check(!string.IsNullOrEmpty(control.Query.UnavailableReason(testCardId)), "当前未就绪卡保持资格拒绝，不修改正式配置");
            Click(panel.btnQuality.GetComponent<RectTransform>());
            Check(panel.imgQualityUp.gameObject.activeSelf, "点击当前品质切升序并显示独立箭头");
            Click(panel.btnLevel.GetComponent<RectTransform>());
            Check(panel.imgLevelDown.gameObject.activeSelf && !panel.imgQualityUp.gameObject.activeSelf, "切换等级默认降序且互斥");
            for (int role = 1; role <= 4; role++) {
                panel.Filter(role);
                Check(panel.listCards.arraySource.Count == control.Query.List(role, 2, true).Length, "真实职业筛选数量正确：" + role);
            }
            panel.Filter(0);
            panel.ClearDraft();
            Check(panel.Draft.Dirty && store.Load().schemes[0][0] == testCardId, "一键下阵只清草稿，旧方案未写盘");
            Check(panel.txtSlotEmpty1.gameObject.activeSelf && panel.imgStage1.gameObject.activeSelf, "空上阵显示加号且保留展台");
            panel.SelectCard(testCardId);
            Check(panel.Draft.Slots.All(cardId => cardId == 0), "失去出战资格的旧卡不能重新加入");
            panel.SwitchScheme(2);
            Check(panel.boxConfirm.gameObject.activeSelf, "有修改切方案显示确认");
            Capture("team-edit-entry-dirty-confirm.png");
            Click(panel.btnContinue.GetComponent<RectTransform>());
            Check(!panel.boxConfirm.gameObject.activeSelf, "继续编辑真实点击关闭确认层");
            await Task.Delay(320); await Frames(10);
            Capture("team-edit-entry-window.png");
            fail = true;
            Click(panel.btnSave.GetComponent<RectTransform>());
            await Frames(4);
            Check(PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId) && panel.Draft.Dirty && panel.Draft.Slots.All(cardId => cardId == 0), "保存失败保留窗口与清空草稿");
            await WaitToast("保存失败");
            Check(store.Load().schemes[0][0] == testCardId && GlobalToast.Host != null && GlobalToast.Host.ActiveItems.Any(item => item.Message.Contains("保存失败")), "失败未写原方案且原因可见");
            Capture("team-edit-entry-save-failed.png");
            panel.RequestClose();
            Check(panel.boxConfirm.gameObject.activeSelf, "失败草稿关闭须确认");
            Capture("team-edit-entry-failure-discard-confirm.png");
            Click(panel.btnDiscard.GetComponent<RectTransform>());
            await Task.Delay(250); await Frames(4);
            Check(!PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId) && store.Load().schemes[0][0] == testCardId, "放弃失败草稿不保存");
            fail = false;
            for (int scheme = 1; scheme <= 4; scheme++) {
                Click(view.editTeam.GetComponent<RectTransform>());
                panel = await Opened();
                panel.SwitchScheme(scheme);
                panel.ClearDraft();
                Click(panel.btnSave.GetComponent<RectTransform>());
                await Task.Delay(250); await Frames(4);
                Check(!PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId) && store.Load().selected == scheme && store.Load().schemes[scheme - 1].All(cardId => cardId == 0), "四方案全空保存并返回：" + scheme);
            }
            Capture("team-edit-entry-main-saved.png");
            TeamEditControl.InstallValidationSession(new TeamEditControl(CfgManager.tables, session.Cards.model, store.Load(), store.Save));
            Click(view.editTeam.GetComponent<RectTransform>());
            panel = await Opened();
            Check(panel.Draft.Scheme == 4 && !panel.Draft.Dirty && panel.Draft.Slots.All(cardId => cardId == 0), "重载读取选用第四方案及空队伍");
            Capture("team-edit-entry-reloaded.png");
            panel.RequestClose(); await Task.Delay(250); await Frames(4);
            Capture("team-edit-entry-main-empty.png");
        } finally {
            PanelMgr.ins.ClosePanelByType(TeamEditControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(previous);
            functionField.SetValue(null, previousFunction);
        }
    }

    private static async Task Run(PlayerProgressControl session) {
        ValidateCore(session);
        var gateTeam = new TeamEditControl(CfgManager.tables, session.Cards.model, null, ignored => { });
        var previousGateTeam = TeamEditControl.InstallValidationSession(gateTeam);
        try {
            Check(BattleControl.ins.CanEnterSceneBattle(out _), "空方案可进入，只有固定经济设施");
            var unsupported = gateTeam.Query.List(0, 0, true).FirstOrDefault(id =>
                string.IsNullOrEmpty(gateTeam.Query.UnavailableReason(id)) &&
                !string.IsNullOrEmpty(gateTeam.Query.BattleUnavailableReason(id)));
            if (unsupported != 0) {
                Check(gateTeam.model.TrySave(1, new[] { unsupported, 0, 0, 0 }, gateTeam.model.Read().revision, out _), "未接战斗的已拥有卡仍可编组");
                Check(!BattleControl.ins.CanEnterSceneBattle(out string reason), "场景战斗拒绝未就绪方案");
                bool rejected = false;
                try {
                    await BattleControl.ins.PrepareAsync(BattleConst.InitialSceneId);
                } catch (InvalidOperationException error) {
                    rejected = error.Message == reason;
                }
                Check(rejected && !BattleControl.ins.preparing && BattleControl.ins.model == null, "直接准备也拒绝同一未就绪方案");
            }
        } finally {
            TeamEditControl.InstallValidationSession(previousGateTeam);
        }
        var actualQuery = new TeamEditQuery(CfgManager.tables, session.Cards.model);
        if (!actualQuery.List(0, 0, true).Any(cardId => string.IsNullOrEmpty(actualQuery.UnavailableReason(cardId)))) {
            await RunUnavailableCardEntry(session);
            return;
        }
        testCardId = actualQuery.List(0, 0, true).First(cardId => string.IsNullOrEmpty(actualQuery.UnavailableReason(cardId)));
        string directory = "Library/AIUI/TeamEditEntry/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(directory);
        var store = new TeamSchemeStore(directory + "/team.json");
        bool fail = false;
        var control = new TeamEditControl(CfgManager.tables, session.Cards.model, null, data => {
            if (fail) {
                throw new IOException("隔离验证：磁盘写入失败");
            }
            store.Save(data);
        });
        var previous = TeamEditControl.InstallValidationSession(control);
        var functionField = typeof(FunctionControl).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        var previousFunction = functionField.GetValue(null);
        functionField.SetValue(null, new FunctionControl(new FunctionModel(CfgManager.tables.FunctionObj.DataList, directory + "/functions.json")));
        const string mainId = "default/mainView/MainPanel";
        try {
            File.WriteAllText("Library/AIUI/team-edit-progress.txt", "真实阵容入口");
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
            PanelMgr.ins.OpenPanel(mainId);
            await Frames(12);
            var main = PanelMgr.ins.GetPanel(mainId);
            Check(string.IsNullOrEmpty(runtimeError), "MainPanel完成真实OnOpen初始化");
            await GlobalToast.EnsureReadyAsync();
            FunctionUnlockValidation.Run(main.GetComponent<MainPanel>());
            Check(main.transform.Find("boxFunctionHint") == null, "统一提示：真实主界面锁定入口改用全局提示且保留导航选择");
            GlobalToast.Clear();
            Click(main.GetComponent<MainPanel>().btnDeck.GetComponent<RectTransform>());
            await Task.Delay(350);
            await Task.Delay(240);
            await Frames(8);
            var view = main.GetComponentInChildren<DeckPresentationView>(true);
            Check(view != null && view.editTeam != null, "正式MainPanel存在真实队伍编辑入口");
            await Frames(12);
            Capture("team-edit-entry-main-before.png");
            Click(view.editTeam.GetComponent<RectTransform>());
            var panel = await Opened();
            Check(panel.Draft.Scheme == 1 && !panel.Draft.Dirty, "从实际入口进入已选用方案草稿");
            Check(new[] { panel.imgStage1, panel.imgStage2, panel.imgStage3, panel.imgStage4 }.All(stage => stage.gameObject.activeInHierarchy && stage.sprite != null), "首次打开四个空上阵位均保留底座资源");
            var palette = panel.GetComponent<TeamEditStagePalette>();
            Check(palette != null && palette.emptyMaterial != null, "正式窗口绑定四品质底座和空位灰度材质");
            var stageRect = panel.imgStage1.rectTransform;
            Vector2 stageSize = stageRect.sizeDelta;
            Vector2 stagePosition = stageRect.anchoredPosition;
            var stageQualities = new[] { cfg.Quality.NORMAL, cfg.Quality.RARE, cfg.Quality.EPIC, cfg.Quality.LEGEND };
            var stageSprites = new[] { palette.normal, palette.rare, palette.epic, palette.legend };
            for (int qualityIndex = 0; qualityIndex < stageQualities.Length; qualityIndex++) {
                palette.Apply(panel.imgStage1, stageQualities[qualityIndex]);
                Check(panel.imgStage1.sprite == stageSprites[qualityIndex] && panel.imgStage1.material == Graphic.defaultGraphicMaterial, "品质状态正确且清除空位灰度：" + stageQualities[qualityIndex]);
                Check(stageRect.sizeDelta == stageSize && stageRect.anchoredPosition == stagePosition, "底座切换不改变几何：" + stageQualities[qualityIndex]);
                Capture("team-edit-stage-" + stageQualities[qualityIndex] + ".png");
                palette.Apply(panel.imgStage1, null);
                Check(panel.imgStage1.material == palette.emptyMaterial && panel.imgStage1.gameObject.activeSelf, "下阵转为空位中性灰：" + stageQualities[qualityIndex]);
            }
            Capture("team-edit-stage-empty.png");
            Check(panel.btnQuality.GetComponentInChildren<TextMeshProUGUI>().text == "品质" && panel.imgQualityDown.gameObject.activeSelf && panel.btnLevel.GetComponentInChildren<TextMeshProUGUI>().text == "等级", "默认品质降序，未选项不显示方向");
            Click(panel.btnQuality.GetComponent<RectTransform>());
            Check(panel.btnQuality.GetComponentInChildren<TextMeshProUGUI>().text == "品质" && panel.imgQualityUp.gameObject.activeSelf, "当前排序点击切升序");
            Click(panel.btnLevel.GetComponent<RectTransform>());
            Check(panel.btnLevel.GetComponentInChildren<TextMeshProUGUI>().text == "等级" && panel.imgLevelDown.gameObject.activeSelf, "换排序项首次降序");
            Check(string.IsNullOrEmpty(control.Query.UnavailableReason(testCardId)), "已解锁卡的编组资格不依赖战斗配置");
            Check(!string.IsNullOrEmpty(control.Query.BattleUnavailableReason(testCardId)), "同一卡仍保留独立战斗资格拒绝");
            int lockedCard = control.Query.List(0, 0, true).FirstOrDefault(cardId => control.Query.Level(cardId) == 0);
            if (lockedCard > 0) {
                GlobalToast.Clear();
                for (int i = 0; i < 3; i++) {
                    await ClickLibraryCard(panel, lockedCard);
                }
                Check(panel.Draft.Slots.All(cardId => cardId == 0), "未解锁卡仍禁止加入队伍");
                await GlobalToast.EnsureReadyAsync();
                Check(GlobalToast.Host.ActiveItems.Count(item => item.Message == control.Query.UnavailableReason(lockedCard)) == 3, "真实入口：连续点击同一未解锁卡形成三条重复提示且草稿不变");
                RookieEngine.timer.SyncTime(250);
                Capture("global-toast-team-locked.png");
                GlobalToast.Clear();
            }
            if (SessionState.GetBool(Key + ".toastCoverage", false)) {
                var ownedCards = control.Query.List(0, 0, true).Where(cardId => control.Query.Level(cardId) > 0).Take(5).ToArray();
                Check(ownedCards.Length == 5, "真实入口：使用五张已拥有卡的独立内存夹具");
                foreach (int cardId in ownedCards.Take(4)) {
                    await ClickLibraryCard(panel, cardId);
                }
                var beforeFull = panel.Draft.Slots;
                await ClickLibraryCard(panel, ownedCards[4]);
                await WaitToast("队伍已满");
                Check(panel.Draft.Slots.SequenceEqual(beforeFull), "真实入口：满队点击第五卡不改变四位草稿");
                Capture("global-toast-team-full.png");
                panel.ClearDraft();
                GlobalToast.Clear();
                await Frames(3);
            }
            await ClickLibraryCard(panel, testCardId);
            Check(panel.imgStage1.sprite == palette.GetSprite(control.Query.Card(testCardId).Quality) && panel.imgStage1.material == Graphic.defaultGraphicMaterial, "真实加入按卡牌品质选择底座并移除空位材质");
            Check(panel.Draft.Slots.SequenceEqual(new[] { testCardId, 0, 0, 0 }) && !File.Exists(directory + "/team.json"), "真实点击牌库卡加入队首，仅改草稿不提前写盘");
            await ClickLibraryCard(panel, testCardId);
            Check(panel.Draft.Slots.Count(cardId => cardId == testCardId) == 1, "重复点击已上阵卡保持一个实例且不下阵");
            await WaitArt(panel);
            var portraitHit = Click(panel.imgSlot1.rectTransform);
            Check(portraitHit.GetComponentInParent<Button>(true) == panel.btnSlot1, "真实点击立绘命中对应上阵位");
            Check(panel.Draft.Slots.All(cardId => cardId == 0) && !File.Exists(directory + "/team.json"), "点击上阵立绘直接下阵且不写盘");
            Check(panel.txtSlotEmpty1.gameObject.activeSelf && panel.imgStage1.gameObject.activeSelf, "下阵恢复空位加号且保留底座");
            Check(panel.imgStage1.material == palette.emptyMaterial, "真实点击下阵恢复中性灰底座");
            Click(panel.btnSlot1.GetComponent<RectTransform>());
            Check(!panel.Draft.Dirty, "点击空上阵位不产生修改");
            await Frames(4); // ScrollList在赋值后延后一帧刷新可见单元。
            Check(!panel.listCards.GetCell(0).GetComponent<TeamEditCard>().imgSelected.gameObject.activeSelf, "下阵后牌库勾选状态同步清除");
            Capture("team-edit-click-remove.png");
            await ClickLibraryCard(panel, testCardId);
            Click(panel.btnScheme2.GetComponent<RectTransform>());
            Check(panel.boxConfirm.gameObject.activeSelf && panel.Draft.Scheme == 1, "修改后切方案提示");
            Capture("team-edit-entry-dirty-confirm.png");
            Click(panel.btnContinue.GetComponent<RectTransform>());
            await WaitArt(panel);
            Check(panel.imgSlot1.enabled && panel.imgSlot1.sprite != null && panel.imgSlot1.color.a == 1f, "已加入卡的上阵立绘加载且不残留透明色");
            Capture("team-edit-entry-window.png");
            Check(panel.boxWindow.anchoredPosition == Vector2.zero && panel.boxWindow.GetComponent<CanvasGroup>().alpha == 1f, "打开表现归位且完全可见");
            Check(panel.boxWindow.GetComponent<CanvasGroup>().interactable, "打开完成允许操作");
            Click(panel.btnSave.GetComponent<RectTransform>());
            await Task.Delay(240);
            await Frames(8);
            Check(!PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId), "保存成功关闭编辑并返回阵容");
            Check(control.ReadSelected(out int number).SequenceEqual(new[] { testCardId, 0, 0, 0 }) && number == 1, "成功才提交已选用方案和队伍");
            Check(view.slots[0].title.text == CfgManager.tables.CardObj.Get(testCardId).Name, "原MainPanel回调刷新真实上阵单位");
            Check(GlobalToast.Host.ActiveItems.Any(item => item.Message.StartsWith("队伍已保存")), "真实入口：保存成功返回后全局反馈继续可见");
            Capture("global-toast-team-save-return.png");
            Capture("team-edit-entry-main-saved.png");
            for (int scheme = 2; scheme <= 4; scheme++) {
                Click(view.editTeam.GetComponent<RectTransform>());
                panel = await Opened();
                panel.SwitchScheme(scheme);
                Check(panel.Draft.Scheme == scheme && control.model.Read().selected == scheme - 1, "切换只选草稿，不提交选用编号：" + scheme);
                Click(panel.btnSave.GetComponent<RectTransform>());
                await Task.Delay(240);
                await Frames(4);
                Check(!PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId) && store.Load().selected == scheme, "全空方案保存后返回阵容：" + scheme);
            }
            Check(store.Load().schemes.Length == 4, "固定四套方案磁盘结构");
            var reloaded = new TeamEditControl(CfgManager.tables, session.Cards.model, store.Load(), store.Save);
            TeamEditControl.InstallValidationSession(reloaded);
            Click(view.editTeam.GetComponent<RectTransform>());
            panel = await Opened();
            Check(panel.Draft.Scheme == 4 && panel.Draft.Slots.All(id => id == 0), "独立存档重载选用第四套全空队伍");
            panel.SwitchScheme(1);
            Check(panel.Draft.Slots[0] == testCardId, "重载第一套保留已保存单位");
            panel.RequestClose();
            await Task.Delay(240);
            await Frames(3);
            TeamEditControl.InstallValidationSession(control);
            Click(view.editTeam.GetComponent<RectTransform>());
            panel = await Opened();
            panel.SwitchScheme(1);
            panel.ClearDraft();
            Check(panel.Draft.Dirty && store.Load().schemes[0][0] == testCardId, "一键下阵不清已保存队伍");
            fail = true;
            long revision = control.model.Read().revision;
            Click(panel.btnSave.GetComponent<RectTransform>());
            await Task.Delay(240);
            await Frames(3);
            Check(PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId) && panel.Draft.Dirty && panel.Draft.Slots.All(id => id == 0), "保存失败留在编辑并保留草稿");
            Check(control.model.Read().revision == revision && control.model.Read().selected == 4 && store.Load().schemes[0][0] == testCardId, "失败不改变磁盘、revision和已选方案");
            await WaitToast("保存失败");
            Check(GlobalToast.Host != null && GlobalToast.Host.ActiveItems.Any(item => item.Message.Contains("保存失败")), "失败原因可见");
            Capture("team-edit-entry-save-failed.png");
            Capture("global-toast-team-save-failed.png");
            Click(panel.btnClose.GetComponent<RectTransform>());
            Check(panel.boxConfirm.gameObject.activeSelf, "失败草稿关闭仍需确认");
            Capture("team-edit-entry-failure-discard-confirm.png");
            var discardTarget = panel.btnDiscard;
            var discardHit = Click(discardTarget.GetComponent<RectTransform>());
            Check(discardHit.GetComponentInParent<Button>(true) == discardTarget, "真实点击命中放弃按钮：" + discardHit.name);
            await Task.Delay(240);
            await Frames(3);
            Check(!PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId) && store.Load().selected == 4, "放弃失败草稿不提交；仍打开=" + PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId) + ";磁盘选中=" + store.Load().selected);
            fail = false;
            Click(view.editTeam.GetComponent<RectTransform>());
            panel = await Opened();
            panel.SwitchScheme(1);
            panel.ClearDraft();
            Click(panel.btnSave.GetComponent<RectTransform>());
            await Task.Delay(240);
            await Frames(5);
            Check(control.ReadSelected(out number).All(id => id == 0) && number == 1 && !PanelMgr.ins.IsPanelOpen(TeamEditControl.PanelId), "重试保存全空第一套成功并返回");
            Capture("team-edit-entry-main-empty.png");
        } finally {
            PanelMgr.ins.ClosePanelByType(TeamEditControl.PanelId);
            PanelMgr.ins.ClosePanelByType(mainId);
            TeamEditControl.InstallValidationSession(previous);
            functionField.SetValue(null, previousFunction);
        }
    }

    private static async Task ValidateCommonWindows(PlayerProgressControl session) {
        var team = new TeamEditControl(CfgManager.tables, session.Cards.model, null, _ => { });
        var oldTeam = TeamEditControl.InstallValidationSession(team);
        var settingsField = typeof(SaveModel).GetField("userSettingVO", BindingFlags.Instance | BindingFlags.NonPublic);
        var settingsModel = PersistentDataControl.ins.saveModel;
        var oldSettings = settingsField.GetValue(settingsModel);
        var isolatedSettings = new SaveUserSettingVO {
            settingMap = new Dictionary<cfg.SettingOptionSelection, string> {
                { cfg.SettingOptionSelection.GRAPHIC_RESOLUTION, SettingConst.GetResolutionList().Last() },
                { cfg.SettingOptionSelection.GRAPHIC_DISPLAY_MODE, ((int)cfg.SettingOptionSelectionDisplay.EXCLUSIVE_FULLSCREEN).ToString() },
                { cfg.SettingOptionSelection.GRAPHIC_VSYNC, ((int)cfg.SettingOptionDualitySelection.ON).ToString() },
                { cfg.SettingOptionSelection.AUDIO_TOTAL, "100" },
                { cfg.SettingOptionSelection.AUDIO_MUSIC, "100" },
                { cfg.SettingOptionSelection.AUDIO_EFFECT, "100" },
                { cfg.SettingOptionSelection.LANGUAGE, ((int)cfg.SettingOptionLanguage.CHINESE_SIMPLE).ToString() }
            }
        };
        settingsField.SetValue(settingsModel, isolatedSettings);
        try {
        const string mainId = "default/mainView/MainPanel";
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(mainId), ResType.Prefab) });
        PanelMgr.ins.OpenPanel(mainId);
        var main = PanelMgr.ins.GetPanel(mainId);
        Check(main.isOpened && !main.UsesWindowTransition && main.GetComponent<PanelWindowMotion>() == null, "全屏MainPanel同步打开且不创建过渡组件");
        foreach (string id in new[] { UIEnum.WARNING_PANEL, UIEnum.SETTING_PANEL, "default/gm/GmPanel", TeamEditControl.PanelId, CardSkillDetailControl.PanelId }) {
            await ResourceLoader.LoadListAsync(new List<ResLoadInfo> {
                new ResLoadInfo(ResourcePresentation.Policy.ResolveUI(id), ResType.Prefab)
            });
            for (int cycle = 0; cycle < 2; cycle++) {
                PanelMgr.ins.OpenPanel(id, MotionParameters(id, team));
                var panel = PanelMgr.ins.GetPanel(id);
                Check(panel != null && panel.UsesWindowTransition && panel.isOpening, "局部窗口开始统一动画：" + id);
                var motion = panel.GetComponent<PanelWindowMotion>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var content = (RectTransform)typeof(PanelWindowMotion).GetField("content", flags).GetValue(motion);
                var position = (Vector2)typeof(PanelWindowMotion).GetField("position", flags).GetValue(motion);
                var blocker = (Image)typeof(PanelWindowMotion).GetField("blocker", flags).GetValue(motion);
                var alpha = content.GetComponent<CanvasGroup>();
                Check(Mathf.Abs(content.anchoredPosition.y - position.y - 10f) < .01f && alpha.alpha == 0f && blocker.enabled, "打开从上方10像素透明起步并拦截输入：" + id);
                await Task.Delay(350);
                Check(panel.isOpened && Vector2.Distance(content.anchoredPosition, position) < .01f && alpha.alpha == 1f && !blocker.enabled, "打开归位并开放操作：" + id);
                Capture("window-motion-" + panel.GetType().Name + ".png");
                panel.Close();
                panel.Close();
                Check(panel.isClosing && blocker.enabled, "关闭保持输入遮挡且重复关闭无副作用：" + id);
                await Task.Delay(280);
                Check(!PanelMgr.ins.IsPanelOpen(id) && alpha.alpha == 0f && Vector2.Distance(content.anchoredPosition, position) < .01f, "关闭回收并重置位置：" + id);
            }
            // Interrupt an opening; only the close completion is allowed to run.
            PanelMgr.ins.OpenPanel(id, MotionParameters(id, team));
            var interrupted = PanelMgr.ins.GetPanel(id);
            interrupted.Close();
            await Task.Delay(300);
            Check(interrupted.isClosing && !PanelMgr.ins.IsPanelOpen(id), "打开中关闭不会迟到触发OnOpen：" + id);
        }
        main.Close();
        Check(!PanelMgr.ins.IsPanelOpen(mainId), "全屏MainPanel同步关闭");
        } finally {
            foreach (string id in new[] { UIEnum.WARNING_PANEL, UIEnum.SETTING_PANEL, "default/gm/GmPanel", TeamEditControl.PanelId, CardSkillDetailControl.PanelId, "default/mainView/MainPanel" }) {
                PanelMgr.ins.ClosePanelByType(id);
            }
            await Task.Delay(300);
            TeamEditControl.InstallValidationSession(oldTeam);
            settingsField.SetValue(settingsModel, oldSettings);
        }
    }

    private static object[] MotionParameters(string id, TeamEditControl team) {
        if (id == UIEnum.WARNING_PANEL) {
            return new object[] { "窗口统一动画验证", null };
        }
        if (id == TeamEditControl.PanelId) {
            return new object[] { team, null };
        }
        if (id == CardSkillDetailControl.PanelId) {
            return new object[] { new CardSkillDetailOpenVO { cardId = testCardId, slot = CardSkillSlot.Basic } };
        }
        return null;
    }

    private static async Task Frames(int count) {
        for (int i = 0; i < count; i++) {
            await Tick();
        }
    }

    private static async Task<TeamEditPanel> Opened() {
        for (int i = 0; i < 500; i++) {
            var panel = PanelMgr.ins.GetPanel(TeamEditControl.PanelId) as TeamEditPanel;
            if (panel != null && panel.isOpened) {
                await Task.Delay(240);
                await Frames(4);
                return panel;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("真实入口打开队伍编辑超时");
    }

    private static void ValidateCore(PlayerProgressControl session) {
        var clickModel = new TeamSchemeModel(null, cardId => "", ignored => { });
        var clickDraft = new TeamEditDraft(clickModel);
        for (int cardId = 1; cardId <= 4; cardId++) {
            Check(clickDraft.TryAddCard(cardId, out _), "点击加入草稿样例：" + cardId);
        }
        clickDraft.RemoveAt(1);
        Check(clickDraft.Slots.SequenceEqual(new[] { 1, 3, 4, 0 }), "点击中间立绘下阵后右侧单位稳定左移");
        Check(clickDraft.TryAddCard(3, out _) && clickDraft.Slots.SequenceEqual(new[] { 1, 3, 4, 0 }), "重复加入已存在卡不下阵也不重复");
        clickDraft.RemoveAt(3);
        Check(clickDraft.Slots.SequenceEqual(new[] { 1, 3, 4, 0 }), "点击尾部空位不改变队伍");
        string directory = "Library/AIUI/TeamEditCore/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(directory);
        var store = new TeamSchemeStore(directory + "/schemes.json");
        int writes = 0;
        var model = new TeamSchemeModel(null, id => id >= 1 && id <= 5 ? "" : "资格拒绝", data => { store.Save(data); writes++; });
        var draft = new TeamEditDraft(model);
        for (int i = 1; i <= 4; i++) {
            Check(draft.TryCard(i, out _), "隔离四卡样例加入：" + i);
        }
        Check(!draft.TryCard(5, out _) && writes == 0, "满队必须先选替换目标");
        draft.SelectSlot(1);
        Check(draft.TryCard(5, out _) && draft.Slots.SequenceEqual(new[] { 1, 5, 3, 4 }), "指定替换不改变其他顺序");
        draft.SelectSlot(0);
        draft.SelectSlot(3);
        Check(draft.Slots.SequenceEqual(new[] { 4, 5, 3, 1 }), "两非空槽位换位");
        draft.SelectSlot(0);
        draft.TryCard(3, out _);
        Check(draft.Slots.SequenceEqual(new[] { 3, 5, 4, 1 }), "点击已上阵卡换位不重复");
        Check(draft.TrySave(out _) && writes == 1 && !draft.Dirty, "一次显式保存一次提交");
        var reloaded = new TeamSchemeModel(store.Load(), id => "", store.Save);
        Check(reloaded.Read().schemes[0].SequenceEqual(draft.Slots), "真实独立测试文件重载保持顺序");
        var removal = new TeamEditDraft(reloaded);
        removal.TryCard(5, out _);
        Check(removal.Slots.SequenceEqual(new[] { 3, 4, 1, 0 }), "下阵中间卡按原顺序左移");
        removal.SelectSlot(0);
        removal.SelectSlot(3);
        Check(removal.Slots.SequenceEqual(new[] { 4, 1, 3, 0 }), "单位移到尾部空位后仍连续靠左");
        for (int mask = 0; mask < 16; mask++) {
            var legacy = new TeamSchemeSaveVO();
            legacy.schemes[0] = Enumerable.Range(0, 4).Select(i => (mask & (1 << i)) == 0 ? 0 : i + 1).ToArray();
            var expected = legacy.schemes[0].Where(id => id != 0).Concat(new int[4]).Take(4).ToArray();
            var migrated = new TeamSchemeModel(legacy, id => "", ignored => { });
            Check(migrated.Read().schemes[0].SequenceEqual(expected), "旧带间隙快照稳定左移：" + mask);
            for (int left = 0; left < 4; left++) {
                for (int right = left + 1; right < 4; right++) {
                    var moving = new TeamEditDraft(migrated);
                    moving.SelectSlot(left);
                    moving.SelectSlot(right);
                    Check(moving.Slots.SequenceEqual(TeamSchemeModel.CompactSlots(moving.Slots)), "换位无中间空位：" + mask + ":" + left + ":" + right);
                }
            }
        }
        string legacyPath = directory + "/legacy.json";
        const string legacyJson = "{\"schemaVersion\":1,\"revision\":7,\"selected\":1,\"schemes\":[[0,1,0,2],[0,0,0,0],[0,0,0,0],[0,0,0,0]]}";
        File.WriteAllText(legacyPath, legacyJson);
        var legacyStore = new TeamSchemeStore(legacyPath);
        var loadedLegacy = legacyStore.Load();
        Check(loadedLegacy.schemes[0].SequenceEqual(new[] { 1, 2, 0, 0 }) && loadedLegacy.revision == 7 && File.ReadAllText(legacyPath) == legacyJson, "旧存档归一化读取但不隐式改写磁盘");
        legacyStore.Save(loadedLegacy);
        Check(legacyStore.Load().schemes[0].SequenceEqual(new[] { 1, 2, 0, 0 }), "旧快照显式保存后磁盘连续靠左");
        long revision = model.Read().revision;
        Check(!model.TrySave(2, new[] { 1, 1, 0, 0 }, revision, out _), "拒绝重复卡");
        Check(!model.TrySave(2, new[] { 1, 0, 0 }, revision, out _), "拒绝错误容量");
        Check(!model.TrySave(5, new int[4], revision, out _), "拒绝第五方案");
        Check(!model.TrySave(2, new int[4], revision - 1, out _), "拒绝过期revision");
        Check(!model.TrySave(2, new[] { 99, 0, 0, 0 }, revision, out _), "保存时重新校验资格");
        draft.Clear();
        Check(!draft.TrySwitch(2) && draft.Scheme == 1 && writes == 1, "脏草稿切换不写盘");
        Check(draft.TrySwitch(2, true) && draft.Slots.All(id => id == 0), "放弃后切空方案");
        Check(draft.TrySave(out _) && model.Read().selected == 2, "保存全空方案合法");
        var leaked = model.Read();
        leaked.schemes[1][0] = 99;
        Check(model.Read().schemes[1][0] == 0, "返回值不能修改权威状态");
        Directory.CreateDirectory(directory + "/schemes.json.pending");
        var failure = new TeamEditDraft(model);
        failure.TryCard(1, out _);
        Check(!failure.TrySave(out string reason) && failure.Dirty && model.Read().schemes[1][0] == 0 && reason.Contains("保存失败"), "真实文件写入失败时草稿和原存档保持");
        Check(store.Load().schemes[1][0] == 0, "写入失败后重新加载仍为旧快照");
        File.WriteAllText(directory + "/corrupt.json", "{broken");
        bool rejected = false;
        try {
            new TeamSchemeStore(directory + "/corrupt.json").Load();
        } catch {
            rejected = true;
        }
        Check(rejected && File.ReadAllText(directory + "/corrupt.json") == "{broken", "损坏存档不覆盖");
    }


    /**通过实际ScrollList单元与EventSystem点击，避免直接调用Panel方法冒充输入验证。*/
    private static async Task ClickLibraryCard(TeamEditPanel panel, int cardId) {
        await Frames(4);
        var rows = panel.listCards.arraySource;
        int index = Enumerable.Range(0, rows.Count).First(i => ((TeamEditCard.Row)rows[i]).id == cardId);
        var cell = panel.listCards.GetCell(index);
        Check(cell != null && cell.activeInHierarchy, "待点击卡在实际可见列表中：" + cardId);
        var item = cell.GetComponent<TeamEditCard>();
        var hit = Click(item.btnCard.GetComponent<RectTransform>());
        Check(hit.GetComponentInParent<Button>(true) == item.btnCard, "真实点击命中牌库卡按钮：" + cardId);
        await Frames(4);
    }

    private static async Task WaitArt(TeamEditPanel panel) {
        await Tick();
        File.WriteAllText("Library/AIUI/team-edit-progress.txt", "wait-art " + string.Join(",", panel.GetComponentsInChildren<CardArtImage>(true).Select(loader => loader.name + ":" + loader.artId + ":" + loader.Loading.Status)));
        var art = Task.WhenAll(panel.GetComponentsInChildren<CardArtImage>(true).Select(loader => loader.Loading));
        if (await Task.WhenAny(art, Task.Delay(10000)) != art) {
            throw new TimeoutException("卡面加载超过10秒");
        }
        await art;
        await Tick();
        foreach (var cell in panel.listCards.GetComponentsInChildren<TeamEditCard>()) {
            Check(cell.imgArtwork.enabled && cell.imgArtwork.sprite != null, "可见卡面已加载：" + cell.txtCardTitle.text);
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
        File.WriteAllBytes("Library/AIUI/" + name, image.EncodeToPNG());
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
        File.WriteAllText(ResultPath, JsonUtility.ToJson(new Result { requestId = id, status = error == null ? "passed" : "failed", error = error?.ToString(), checks = checks.ToArray() }, true));
    }

}
#endif
