using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 在隔离工程中验证真实启动、登录配置及 Main 加载流程。
/// </summary>
[InitializeOnLoad]
public static class FrameworkTemplateValidation {

    private const string Key = "Framework.TemplateValidation";
    private static double deadline;
    private static bool requestedMain;

    static FrameworkTemplateValidation() {
        EditorApplication.playModeStateChanged += OnStateChanged;
    }

    /// <summary>
    /// 批处理入口，使用已现场构建的资源进入实际游戏流程。
    /// </summary>
    public static void RunPlayMode() {
        SessionState.SetBool(Key + ".interactive", false);
        SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".error", string.Empty);
        SessionState.SetBool(Key + ".passed", false);
        EditorSceneManager.OpenScene("Assets/Scenes/GameEntrance.unity");
        EditorApplication.isPlaying = true;
    }

    /**在当前用户工程验证，不保存场景，不退出编辑器。*/
    public static void RunCurrentScene() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/GameEntrance.unity") {
            throw new InvalidOperationException("Validation requires idle GameEntrance scene.");
        }
        SessionState.SetBool(Key + ".interactive", true);
        SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".error", string.Empty);
        SessionState.SetBool(Key + ".passed", false);
        EditorApplication.isPlaying = true;
    }

    /**跨 Domain Reload 继续检查，结束后关闭本次验证实例*/
    private static void OnStateChanged(PlayModeStateChange state) {
        if (!SessionState.GetBool(Key, false)) {
            return;
        }
        if (state == PlayModeStateChange.EnteredPlayMode) {
            // 自动化运行期间编辑器可能不在前台，避免资源回调及演出停止推进。
            SessionState.SetBool(Key + ".background", Application.runInBackground);
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 90;
            requestedMain = false;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += CheckFlow;
        } else if (state == PlayModeStateChange.EnteredEditMode) {
            Application.runInBackground = SessionState.GetBool(Key + ".background", false);
            SessionState.SetBool(Key, false);
            string error = SessionState.GetString(Key + ".error", string.Empty);
            bool success = SessionState.GetBool(Key + ".passed", false) && string.IsNullOrEmpty(error);
            File.WriteAllText("Library/template-runtime-validation.txt", success ? "ok: Opening -> Login -> Loading -> Main; login keyboard entry invoked; OptionPanel absent; MainPanel root matches parent; navigation/content/background bounds passed" : "failed: " + error);
            if (!SessionState.GetBool(Key + ".interactive", false)) {
                EditorApplication.Exit(success ? 0 : 1);
            }
        }
    }

    /**运行时错误必须使验证失败*/
    private static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            SessionState.SetString(Key + ".error", message + "\n" + stack);
        }
    }

    /**等待真实界面就绪后触发正式内容入口*/
    private static void CheckFlow() {
        EditorApplication.QueuePlayerLoopUpdate();
        if (!string.IsNullOrEmpty(SessionState.GetString(Key + ".error", string.Empty)) || EditorApplication.timeSinceStartup > deadline) {
            if (EditorApplication.timeSinceStartup > deadline) {
                SessionState.SetString(Key + ".error", "Startup timed out.");
            }
            Finish();
            return;
        }
        if (UnityEngine.Object.FindObjectOfType<OptionPanel>() != null) {
            SessionState.SetString(Key + ".error", "Unexpected OptionPanel in startup flow.");
            Finish();
            return;
        }
        var main = UnityEngine.Object.FindObjectOfType<MainPanel>();
        if (requestedMain && main != null && main.isOpened) {
            try {
                ValidateMainPanelBounds(main);
                FunctionUnlockValidation.Run(main);
                SessionState.SetBool(Key + ".passed", true);
            } catch (Exception exception) {
                SessionState.SetString(Key + ".error", exception.ToString());
            }
            Finish();
            return;
        }
        if (requestedMain) {
            return;
        }
        var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
        if (login != null && login.isOpened && login.txt.text == "点击屏幕或按任意键开始") {
            requestedMain = true;
            typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { KeyCode.Return });
        }
    }

    /**验证真实 PanelMgr 挂载后的尺寸，而非替代挂载方式的测试实例。*/
    private static void ValidateMainPanelBounds(MainPanel main) {
        RectTransform root = main.trans;
        RectTransform parent = root.parent as RectTransform;
        Canvas.ForceUpdateCanvases();
        root.ForceUpdateRectTransforms();
        if (parent == null || parent.rect.width <= 0 || parent.rect.height <= 0 || root.anchorMin != Vector2.zero || root.anchorMax != Vector2.one || root.sizeDelta.sqrMagnitude > 0.01f || root.anchoredPosition.sqrMagnitude > 0.01f || (root.rect.size - parent.rect.size).sqrMagnitude > 0.01f) {
            throw new InvalidOperationException("MainPanel root failed full-screen bounds: " + root.rect.size + ", sizeDelta=" + root.sizeDelta);
        }
        MainPanelLayout layout = main.GetComponent<MainPanelLayout>();
        Rect navigation = GetBounds(layout.navigation, root);
        Rect content = GetBounds(layout.contentViewport, root);
        Rect background = GetBounds(layout.background, root);
        Rect expected = root.rect;
        if (Mathf.Abs(navigation.yMin - expected.yMin) > 0.1f || Mathf.Abs(navigation.xMin - expected.xMin) > 0.1f || Mathf.Abs(navigation.xMax - expected.xMax) > 0.1f || Mathf.Abs(content.yMax - expected.yMax) > 0.1f || Mathf.Abs(content.yMin - navigation.yMax) > 0.1f || background.xMin > expected.xMin + 0.1f || background.xMax < expected.xMax - 0.1f || background.yMin > expected.yMin + 0.1f || background.yMax < expected.yMax - 0.1f) {
            throw new InvalidOperationException("MainPanel child bounds failed.");
        }
        File.WriteAllText("Library/AIUI/mainpanel-runtime-bounds.txt", "passed: root=" + root.rect.size + "; parent=" + parent.rect.size + "; sizeDelta=" + root.sizeDelta + "; offsetMin=" + root.offsetMin + "; offsetMax=" + root.offsetMax + "; actual PanelMgr startup path");
    }

    private static Rect GetBounds(RectTransform target, RectTransform root) {
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 minimum = root.InverseTransformPoint(corners[0]);
        Vector3 maximum = root.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }

    /**停止监听并离开 Play，结果在 Domain Reload 后落盘*/
    private static void Finish() {
        EditorApplication.update -= CheckFlow;
        Application.logMessageReceived -= OnLog;
        EditorApplication.isPlaying = false;
    }

}
