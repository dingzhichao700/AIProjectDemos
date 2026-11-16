#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 窗口层级与GM入口的真实启动及非破坏性回归。
/// </summary>
/// <remarks>明确请求后运行，临时开发开关自动恢复；不执行道具指令或构建发布。</remarks>
[InitializeOnLoad]
public static class WindowGMValidation {

    private const string Pending = "Library/AIUI/pending-window-gm-validation.json";

    private const string Key = "WindowGMValidation.R1";

    private const string ConfigPath = "ProjectSettings/FrameworkPlatform.json";

    private static readonly List<string> checks = new List<string>();

    private static bool running;

    private static string runtimeError;

    [Serializable]
    private sealed class Request {

        public string requestId;
        public int processId;
        public string evidenceDirectory;

    }

    [Serializable]
    private sealed class Result {

        public string requestId;
        public string status;
        public string error;
        public string[] checks;

    }

    static WindowGMValidation() {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnState;
    }

    private static void Poll() {
        if (!string.IsNullOrEmpty(SessionState.GetString(Key, "")) && EditorApplication.isPlaying) {
            EditorApplication.QueuePlayerLoopUpdate();
        }
        if (running || !File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        Request request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
        if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId)) {
            throw new InvalidOperationException("WindowGM request identity mismatch");
        }
        File.Delete(Pending);
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) {
            Write(request, "failed", "Active scene has unsaved changes");
            return;
        }
        Directory.CreateDirectory(request.evidenceDirectory);
        SessionState.SetString(Key, JsonUtility.ToJson(request));
        SessionState.SetString(Key + ".config", File.ReadAllText(ConfigPath));
        SessionState.SetString(Key + ".scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key + ".background", Application.runInBackground);
        EditorSceneManager.OpenScene("Assets/Scenes/GameEntrance.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnState(PlayModeStateChange state) {
        string pending = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(pending)) {
            return;
        }
        if (state == PlayModeStateChange.EnteredPlayMode) {
            Run(JsonUtility.FromJson<Request>(pending));
        } else if (state == PlayModeStateChange.EnteredEditMode) {
            File.WriteAllText(ConfigPath, SessionState.GetString(Key + ".config", ""));
            Application.runInBackground = SessionState.GetBool(Key + ".background", false);
            SessionState.EraseString(Key);
            running = false;
        }
    }

    private static void Check(bool valid, string label) {
        if (!valid) {
            throw new InvalidOperationException(label);
        }
        checks.Add(label);
    }

    private static async Task Wait(Func<bool> predicate, string label, int timeout = 15000) {
        double end = EditorApplication.timeSinceStartup + timeout / 1000d;
        while (!predicate()) {
            if (EditorApplication.timeSinceStartup > end) {
                throw new TimeoutException(label);
            }
            await Task.Delay(30);
        }
        await Task.Delay(70);
    }

    private static Task Opened(string id) {
        return Wait(() => PanelMgr.ins.GetPanel(id) != null && PanelMgr.ins.GetPanel(id).isOpened, "Open " + id);
    }

    private static Task Gone(string id) {
        return Wait(() => !PanelMgr.ins.IsPanelVisible(id), "Close " + id);
    }

    private static BasePanel Focus => (BasePanel)typeof(PanelMgr).GetField("topPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(PanelMgr.ins);

    private static void Escape() {
        typeof(PanelMgr).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(PanelMgr.ins, new object[] { KeyCode.Escape });
    }

    private static void Enable(bool value) {
        string source = SessionState.GetString(Key + ".config", "");
        File.WriteAllText(ConfigPath, System.Text.RegularExpressions.Regex.Replace(source, "\\\"enableGM\\\"\\s*:\\s*(true|false)", "\"enableGM\": " + value.ToString().ToLowerInvariant()));
        FrameworkDevelopmentSettings.Reload();
    }

    private static async Task Capture(Request request, string name) {
        var cultivation = PanelMgr.ins.GetPanel(CardCultivationControl.PanelId) as CardCultivationPanel;
        if (cultivation != null) {
            await Wait(() => cultivation.imgPortrait.enabled && cultivation.imgPortrait.sprite != null, "Portrait ready before capture");
            await Wait(() => {
                foreach (var item in cultivation.GetComponentsInChildren<CardCultivationSkillItem>(false)) {
                    if (!item.imgSkillIcon.enabled || item.imgSkillIcon.sprite == null) {
                        return false;
                    }
                }
                return true;
            }, "Skill images ready before capture");
        }
        Canvas.ForceUpdateCanvases();
        await Task.Delay(200);
        ScreenCapture.CaptureScreenshot(Path.Combine(request.evidenceDirectory, name));
        await Task.Delay(250);
    }

    private static async void Run(Request request) {
        running = true;
        checks.Clear();
        runtimeError = null;
        Application.logMessageReceived += OnLog;
        Application.runInBackground = true;
        try {
            await Wait(() => {
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                return login != null && login.isOpened && login.txt.text == "点击屏幕或按任意键开始";
            }, "Normal Login ready", 90000);
            var loginPanel = UnityEngine.Object.FindObjectOfType<LoginPanel>();
            typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(loginPanel, new object[] { KeyCode.Return });
            await Opened(UIEnum.MAIN_PANEL);
            Check(GameObject.Find("canvasConstantPixel") == null, "旧固定像素Canvas已移除");
            var main = PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL);
            Check(main.transform.parent == PanelMgr.ins.viewportScale, "实际启动Main共享缩放根");
            Check(!FrameworkDevelopmentSettings.GMEnabled, "正式配置缺省GM关闭");
            GMControl.ins.OpenPanel();
            PanelMgr.ins.OpenPanel(MainUIEnum.GM_PANEL);
            await Task.Delay(350);
            Check(!PanelMgr.ins.IsPanelVisible(MainUIEnum.GM_PANEL), "关闭配置时Control和管理器API均不可绕过");
            Check(!GMControl.ins.ExecuteGM("log", new List<string> { "unexpected" }), "关闭配置时命令API不可绕过");
            Check(!PanelMgr.ins.IsPanelVisible(GMEntryView.PanelId), "关闭配置无常驻按钮");
            Enable(true);
            await Opened(GMEntryView.PanelId);
            var entry = (GMEntryView)PanelMgr.ins.GetPanel(GMEntryView.PanelId);
            Check(Focus == main && entry.transform.parent == main.transform.parent, "按钮高层绘制但不抢主界面焦点");
            await Capture(request, "main-gm-entry.png");
            var button = entry.transform.Find("btnGM").GetComponent<RectTransform>();
            Vector2 original = button.anchoredPosition;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, button.position);
            var data = new PointerEventData(EventSystem.current) { pointerId = 17, position = screen };
            ExecuteEvents.ExecuteHierarchy(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.position = screen + new Vector2(-140, 100);
            ExecuteEvents.ExecuteHierarchy(button.gameObject, data, ExecuteEvents.dragHandler);
            ExecuteEvents.ExecuteHierarchy(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            await Task.Delay(100);
            Check(button.anchoredPosition != original && !PanelMgr.ins.IsPanelVisible(MainUIEnum.GM_PANEL), "真实事件路由拖动移动且不误开GM");
            Vector2 moved = button.anchoredPosition;
            var region = UIScreenRegions.EditorOverride;
            UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(20, 30, Screen.width - 40, Screen.height - 60), menu = new Rect(Screen.width - 170, Screen.height - 100, 140, 40) };
            await Task.Delay(150);
            Rect safe = UIScreenRegions.LocalContentArea(entry.trans);
            Check(safe.Contains(button.anchoredPosition + button.rect.min) && safe.Contains(button.anchoredPosition + button.rect.max), "按钮完整位于模拟安全区与胶囊下方");
            UIScreenRegions.EditorOverride = region;
            await Task.Delay(100);
            await Capture(request, "entry-dragged.png");
            data.position = RectTransformUtility.WorldToScreenPoint(null, button.position);
            ExecuteEvents.ExecuteHierarchy(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            await Opened(MainUIEnum.GM_PANEL);
            Check(button.GetComponent<CanvasGroup>().alpha == 0, "点击实际按钮打开GM并隐藏按钮");
            var gm = PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL);
            CommonUtil.OpenWarning("窗口层级验证：取消应返回GM", null);
            await Opened(UIEnum.WARNING_PANEL);
            var confirm = PanelMgr.ins.GetPanel(UIEnum.WARNING_PANEL);
            Check(confirm.layer > gm.layer && Focus == confirm && PanelMgr.ins.IsPanelOpen(MainUIEnum.GM_PANEL), "确认独立覆盖并保留GM");
            Escape();
            await Gone(UIEnum.WARNING_PANEL);
            Check(Focus == gm, "确认取消后焦点恢复GM");
            await Capture(request, "gm-tool.png");
            PanelMgr.ins.OpenPanel(UIEnum.SETTING_PANEL);
            await Opened(UIEnum.SETTING_PANEL);
            await Gone(MainUIEnum.GM_PANEL);
            var setting = PanelMgr.ins.GetPanel(UIEnum.SETTING_PANEL);
            Check(setting.layer == gm.layer && Focus == setting, "设置与GM同工具层互斥");
            GMControl.ins.OpenPanel();
            await Opened(MainUIEnum.GM_PANEL);
            await Gone(UIEnum.SETTING_PANEL);
            Check(Focus == PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL), "GM反向替换设置");
            Escape();
            await Gone(MainUIEnum.GM_PANEL);
            Check(button.GetComponent<CanvasGroup>().alpha == 1 && Focus == main, "GM退场完成恢复按钮且返回主界面焦点");
            for (int i = 0; i < 4; i++) {
                GMControl.ins.OpenPanel();
                await Opened(MainUIEnum.GM_PANEL);
                gm = PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL);
                Escape();
                GMControl.ins.OpenPanel();
                await Wait(() => PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL) != null && PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL).isOpened, "Reopen during close");
                Check(PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL) == gm, "退场中重开复用同实例=" + i);
                Escape();
                await Gone(MainUIEnum.GM_PANEL);
            }
            main.GetComponentInChildren<DeckPresentationView>(true).RequestCardInfo(2101);
            await Opened(CardCultivationControl.PanelId);
            var cultivation = PanelMgr.ins.GetPanel(CardCultivationControl.PanelId);
            Check(PanelMgr.ins.GetPanel(GMEntryView.PanelId) == entry && entry.isOpened, "跨培养页保持同一按钮实例");
            Check(Focus == cultivation, "跨页按钮不抢返回");
            await Capture(request, "cultivation-gm-entry.png");
            await CardSkillDetailControl.OpenAsync(2101, (CardSkillSlot)0);
            await Opened(CardSkillDetailControl.PanelId);
            var detail = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId);
            Check(detail.layer == PanelLayer.SCALE_PANEL_THIRD && Focus == detail, "实际技能详情迁移三级且保留模态焦点");
            await Capture(request, "skill-third-modal.png");
            Escape();
            await Gone(CardSkillDetailControl.PanelId);
            Check(Focus == cultivation && cultivation.isOpened, "技能返回一次仅关闭详情保留培养");
            GlobalToast.Warn("窗口层级回归");
            await Task.Delay(150);
            Check(Focus == cultivation, "滚动提示和GM入口都不抢返回");
            Enable(false);
            await Gone(GMEntryView.PanelId);
            GMControl.ins.OpenPanel();
            await Task.Delay(100);
            Check(!PanelMgr.ins.IsPanelVisible(MainUIEnum.GM_PANEL), "配置重载关闭移除入口并封闭API");
            Enable(true);
            await Opened(GMEntryView.PanelId);
            GMControl.ins.OpenPanel();
            Enable(false);
            await Task.Delay(400);
            Check(!PanelMgr.ins.IsPanelVisible(MainUIEnum.GM_PANEL) && !PanelMgr.ins.IsPanelVisible(GMEntryView.PanelId), "异步打开中关闭配置无迟到面板或入口残留");
            Check(PanelLayer.SCALE_TOP_MONITOR > PanelLayer.SCALE_LOADING && PanelLayer.SCALE_LOADING > PanelLayer.SCALE_GM_ENTRY && PanelLayer.SCALE_GM_ENTRY > PanelLayer.SCALE_TOAST && PanelLayer.SCALE_TOAST > PanelLayer.SCALE_CONFIRM && PanelLayer.SCALE_CONFIRM > PanelLayer.SCALE_TOOL && PanelLayer.SCALE_TOOL > PanelLayer.SCALE_TIP && PanelLayer.SCALE_TIP > detail.layer, "完整批准层序映射");
            Enable(true);
            await Opened(GMEntryView.PanelId);
            var held = new TaskCompletionSource<bool>();
            LoadingControl.ins.OpenQueue(new List<Func<Action<float>, Task>> { progress => held.Task }, () => { }, error => { throw error; });
            await Opened(UIEnum.LOADING_PANEL);
            var loading = PanelMgr.ins.GetPanel(UIEnum.LOADING_PANEL);
            Check(PanelMgr.ins.IsLoading && Focus == loading, "真实加载窗口占据交互焦点");
            var blocker = loading.transform.Find("imgLoadingInputBlocker").GetComponent<Image>();
            Check(blocker.raycastTarget && blocker.rectTransform.rect.size == loading.trans.rect.size, "加载屏障覆盖整个实际舞台");
            GMControl.ins.OpenPanel();
            typeof(GMControl).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(GMControl.ins, new object[] { KeyCode.Home });
            Check(!PanelMgr.ins.IsPanelVisible(MainUIEnum.GM_PANEL) && !GMControl.ins.ExecuteGM("log", new List<string> { "unexpected" }), "加载阻止按钮/Home/API和GM命令");
            held.SetResult(true);
            await Gone(UIEnum.LOADING_PANEL);
            typeof(GMControl).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(GMControl.ins, new object[] { KeyCode.Home });
            await Opened(MainUIEnum.GM_PANEL);
            Check(PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL).isOpened, "Home遵循相同开启入口");
            Escape();
            await Gone(MainUIEnum.GM_PANEL);
            var before = (List<BasePanel>)typeof(PanelMgr).GetField("openPanelInsList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(PanelMgr.ins);
            int baseline = before.Count;
            for (int i = 0; i < 8; i++) {
                var probe = new GameObject("TipPoolProbe", typeof(RectTransform)).AddComponent<BaseTipPanel>();
                probe.trans.sizeDelta = new Vector2(80, 40);
                PanelMgr.ins.OpenRuntimePanel("validation/tip/" + i, probe);
                Check(Focus == probe && before.Count == baseline + 1, "单Tips仅登记一次=" + i);
                Escape();
                Check(before.Count == baseline && Focus == cultivation, "Tips关闭回收无普通列表残留=" + i);
            }
            Check(string.IsNullOrEmpty(runtimeError), "本次Play无Error/Exception/Assert");
            Write(request, "passed", null);
        } catch (Exception exception) {
            Write(request, "failed", exception.ToString());
            Debug.LogException(exception);
        } finally {
            Application.logMessageReceived -= OnLog;
            File.WriteAllText(ConfigPath, SessionState.GetString(Key + ".config", ""));
            FrameworkDevelopmentSettings.Reload();
            // 仅结束此验证自己启动的Play，不退出用户的其他Play或编辑器。
            EditorApplication.isPlaying = false;
        }
    }

    private static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            runtimeError = message + "\n" + stack;
        }
    }

    private static void Write(Request request, string status, string error) {
        File.WriteAllText(Path.Combine(request.evidenceDirectory, "validation-result.json"), JsonUtility.ToJson(new Result { requestId = request.requestId, status = status, error = error, checks = checks.ToArray() }, true));
    }

}
#endif
