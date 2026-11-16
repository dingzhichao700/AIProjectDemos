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

/// <summary>
/// 将显式开发配置从正常入口运行到可供用户直接操作的GM交验现场。
/// </summary>
/// <remarks>保留工程开发开关及本次Play，不执行GM业务命令、不修改玩家数据。</remarks>
[InitializeOnLoad]
public static class WindowGMDevelopmentReview {

    private const string Pending = "Library/AIUI/pending-gm-development-review.json";

    private const string Key = "WindowGM.DevelopmentReview";

    private static readonly List<string> checks = new List<string>();

    private static string error;

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
        public bool leftPlaying;
        public bool enableGM;

    }

    static WindowGMDevelopmentReview() {
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
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
        if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId)) {
            throw new InvalidOperationException("GM review request identity mismatch");
        }
        File.Delete(Pending);
        Directory.CreateDirectory(request.evidenceDirectory);
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) {
            Write(request, "failed", "Active scene has unsaved changes", false);
            return;
        }
        if (!FrameworkPlatformConfig.Read().enableGM) {
            Write(request, "failed", "Development configuration must explicitly enable GM", false);
            return;
        }
        SessionState.SetString(Key, JsonUtility.ToJson(request));
        EditorSceneManager.OpenScene("Assets/Scenes/GameEntrance.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnState(PlayModeStateChange state) {
        string value = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(value)) {
            return;
        }
        if (state == PlayModeStateChange.EnteredPlayMode) {
            Run(JsonUtility.FromJson<Request>(value));
        } else if (state == PlayModeStateChange.EnteredEditMode) {
            SessionState.EraseString(Key);
        }
    }

    private static async Task Wait(Func<bool> predicate, string label, int timeout = 15000) {
        double deadline = EditorApplication.timeSinceStartup + timeout / 1000d;
        while (!predicate()) {
            if (EditorApplication.timeSinceStartup > deadline) {
                throw new TimeoutException(label);
            }
            await Task.Delay(30);
        }
        await Task.Delay(100);
    }

    private static void Check(bool condition, string label) {
        if (!condition) {
            throw new InvalidOperationException(label);
        }
        checks.Add(label);
    }

    private static async void Run(Request request) {
        checks.Clear();
        error = null;
        Application.logMessageReceived += OnLog;
        bool originalBackground = Application.runInBackground;
        Application.runInBackground = true;
        try {
            await Wait(() => {
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                return login != null && login.isOpened && login.txt.text == "点击屏幕或按任意键开始";
            }, "Normal Login ready", 90000);
            Check(!PanelMgr.ins.IsPanelVisible(GMEntryView.PanelId), "启动登录阶段不显示GM入口");
            var loginPanel = UnityEngine.Object.FindObjectOfType<LoginPanel>();
            typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(loginPanel, new object[] { KeyCode.Return });
            await Wait(() => PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL)?.isOpened == true && PanelMgr.ins.GetPanel(GMEntryView.PanelId)?.isOpened == true, "Main and GM entry ready");
            Check(FrameworkDevelopmentSettings.GMEnabled && FrameworkPlatformConfig.Read().enableGM, "实际有效开关和持久开发配置均开启");
            var entry = PanelMgr.ins.GetPanel(GMEntryView.PanelId);
            var button = entry.transform.Find("btnGM").GetComponent<RectTransform>();
            var group = button.GetComponent<CanvasGroup>();
            Check(group.alpha == 1 && group.blocksRaycasts && group.interactable, "正常主界面GM按钮可见可操作");
            var main = (MainPanel)PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL);
            main.btnDeck.onClick.Invoke();
            await Task.Delay(1500);
            Canvas.ForceUpdateCanvases();
            await Task.Delay(400);
            ScreenCapture.CaptureScreenshot(Path.Combine(request.evidenceDirectory, "development-gm-visible.png"));
            await Task.Delay(300);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 27, position = RectTransformUtility.WorldToScreenPoint(null, button.position) };
            ExecuteEvents.ExecuteHierarchy(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            await Wait(() => PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL)?.isOpened == true, "Actual GM open");
            Check(group.alpha == 0, "点击浮动按钮打开GM并隐藏入口");
            PanelMgr.ins.ClosePanelByType(MainUIEnum.GM_PANEL);
            await Wait(() => !PanelMgr.ins.IsPanelVisible(MainUIEnum.GM_PANEL), "GM closed");
            Check(group.alpha == 1 && group.interactable, "GM关闭完成恢复入口");
            Check(string.IsNullOrEmpty(error), "本次实际入口运行无Error/Exception/Assert");
            Check(FrameworkPlatformConfig.Read().enableGM, "交验结束保留开发GM开启，不恢复false");
            Write(request, "passed", null, true);
        } catch (Exception exception) {
            Write(request, "failed", exception.ToString(), true);
            Debug.LogException(exception);
        } finally {
            Application.logMessageReceived -= OnLog;
            Application.runInBackground = originalBackground;
            SessionState.EraseString(Key);
            // Play留给用户直接验收；本工具不退出或重启编辑器。
        }
    }

    private static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            error = message + "\n" + stack;
        }
    }

    private static void Write(Request request, string status, string failure, bool playing) {
        File.WriteAllText(Path.Combine(request.evidenceDirectory, "result.json"), JsonUtility.ToJson(new Result { requestId = request.requestId, status = status, error = failure, checks = checks.ToArray(), leftPlaying = playing, enableGM = FrameworkPlatformConfig.Read().enableGM }, true));
    }

}
#endif

