#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using System.Linq;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 层效果测试窗口六字段的平台输入与生命周期验收。
/// </summary>
/// <remarks>仅替换内存玩家会话；仅使用内存会话与Library测试存档；结束退出自启Play并恢复原场景。</remarks>
[InitializeOnLoad]
public static class LegacyInputSixReview {

    private const string Pending = "Library/AIUI/pending-legacy-input-six-review.json";

    private const string Key = "LegacyInputSix.Review";

    private static readonly List<string> checks = new List<string>();

    private static string error;

    [Serializable]
    private sealed class Request {

        public string requestId;
        public int processId;
        public string evidenceDirectory;
        public bool compileOnly;

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

    static LegacyInputSixReview() {
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
        RestoreOriginalScene();
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(Pending).TrimStart('\uFEFF'));
        if (request.processId != System.Diagnostics.Process.GetCurrentProcess().Id || string.IsNullOrEmpty(request.requestId)) {
            throw new InvalidOperationException("GM review request identity mismatch");
        }
        File.Delete(Pending);
        Directory.CreateDirectory(request.evidenceDirectory);
        if (request.compileOnly) {
            try {
                var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var before = original.GetRootGameObjects().Select(root => root.activeSelf).ToArray();
                SessionState.SetInt(Key + ".originalScene", original.handle);
                SessionState.SetInt(Key + ".testScene", 0);
                SessionState.SetString(Key + ".activeRoots", "");
                RestoreOriginalScene();
                if (!before.SequenceEqual(original.GetRootGameObjects().Select(root => root.activeSelf))) {
                    throw new InvalidOperationException("Scene restoration changed untouched roots");
                }
                File.WriteAllText(Path.Combine(request.evidenceDirectory, "fixture-cleanup.json"), "{\"status\":\"passed\",\"untouchedSceneRootsPreserved\":true}");
                HybridCLR.Editor.Commands.CompileDllCommand.CompileDll(BuildTarget.WebGL, EditorUserBuildSettings.development);
                foreach (string assembly in new[] { "Main.dll", "Login.dll" }) {
                    File.Copy("HybridCLRData/HotUpdateDlls/WebGL/" + assembly, Path.Combine(request.evidenceDirectory, assembly), true);
                }
                File.WriteAllText(Path.Combine(request.evidenceDirectory, "webgl-compile.json"), "{\"requestId\":\"" + request.requestId + "\",\"status\":\"passed\",\"artifact\":\"WebGL hot-update DLL compile only; no bundles, base, candidate or publication\"}");
            } catch (Exception exception) {
                File.WriteAllText(Path.Combine(request.evidenceDirectory, "webgl-compile.json"), JsonUtility.ToJson(new Result { requestId = request.requestId, status = "failed", error = exception.ToString() }, true));
            }
            return;
        }
        if (!FrameworkPlatformConfig.Read().enableGM) {
            Write(request, "failed", "Development configuration must explicitly enable GM", false);
            return;
        }
        // 保留现有未保存场景及原始活跃状态，在附加场景中隔离运行。
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        SessionState.SetInt(Key + ".originalScene", originalScene.handle);
        if (originalScene.path == "Assets/Scenes/GameEntrance.unity") {
            // Unity编辑器对同路径OpenScene复用原场景；直接运行，保留未保存内容。
            SessionState.SetInt(Key + ".testScene", 0);
            SessionState.SetString(Key + ".activeRoots", "");
        } else {
            var roots = originalScene.GetRootGameObjects();
            SessionState.SetString(Key + ".activeRoots", string.Join(",", roots.Select(root => root.activeSelf ? "1" : "0")));
            foreach (var root in roots) {
                root.SetActive(false);
            }
            var testScene = EditorSceneManager.OpenScene("Assets/Scenes/GameEntrance.unity", OpenSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(testScene);
            SessionState.SetInt(Key + ".testScene", testScene.handle);
        }
        SessionState.SetString(Key, JsonUtility.ToJson(request));
        EditorApplication.isPlaying = true;
    }

    private static UnityEngine.SceneManagement.Scene FindScene(int handle) {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.handle == handle) {
                return scene;
            }
        }
        return default;
    }

    private static void RestoreOriginalScene() {
        if (SessionState.GetInt(Key + ".originalScene", 0) != 0) {
            var original = FindScene(SessionState.GetInt(Key + ".originalScene", 0));
            var test = FindScene(SessionState.GetInt(Key + ".testScene", 0));
            if (test.IsValid() && test.isLoaded && test.handle != original.handle) {
                EditorSceneManager.CloseScene(test, true);
            }
            if (original.IsValid() && original.isLoaded) {
                var flags = SessionState.GetString(Key + ".activeRoots", "").Split(',');
                var roots = original.GetRootGameObjects();
                for (int i = 0; i < roots.Length && i < flags.Length && !string.IsNullOrEmpty(flags[0]); i++) {
                    roots[i].SetActive(flags[i] == "1");
                }
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
            }
            SessionState.EraseInt(Key + ".originalScene");
            SessionState.EraseString(Key + ".activeRoots");
        }
    }

    private static void OnState(PlayModeStateChange state) {
        if (state == PlayModeStateChange.EnteredEditMode) {
            RestoreOriginalScene();
        }
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

    private static void Click(RectTransform target) {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current) { pointerId = 0, position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0, "真实射线命中 " + target.name);
        GameObject hit = hits[0].gameObject;
        Check(hit.transform.IsChildOf(target) || hit == target.gameObject, "射线实际目标 " + target.name + " -> " + hit.name);
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
    }

    private sealed class FakeKeyboard : ITextKeyboard {
        public bool IsSupported => true;
        public int shown;
        public int hidden;
        public string initial;
        public Action<string> changed;
        public Action<string> confirmed;
        public Action<string> completed;
        public Action<string> failed;

        public void Show(string value, int limit, bool multiline, Action<string> onChanged, Action<string> onConfirmed, Action<string> onCompleted, Action<string> onFailed) {
            shown++;
            initial = value;
            changed = onChanged;
            confirmed = onConfirmed;
            completed = onCompleted;
            failed = onFailed;
        }

        public void Hide() {
            hidden++;
            completed?.Invoke(initial);
        }
    }

    private static bool expectedError;

    private static LayerEffectTestSettings Settings(LayerEffectTestPanel panel) {
        return (LayerEffectTestSettings)typeof(LayerEffectTestPanel).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
    }

    private static async Task<LayerEffectTestPanel> OpenPanel() {
        Check(GMControl.ins.ExecuteGM(LayerEffectTestGM.Command), "正式GM指令打开层效果测试窗口");
        await Wait(() => PanelMgr.ins.GetPanel(MainUIEnum.LAYER_EFFECT_TEST_PANEL)?.isOpened == true, "Layer effect panel ready");
        return (LayerEffectTestPanel)PanelMgr.ins.GetPanel(MainUIEnum.LAYER_EFFECT_TEST_PANEL);
    }

    private static async void Run(Request request) {
        checks.Clear();
        error = null;
        Application.logMessageReceived += OnLog;
        Application.runInBackground = true;
        PlayerProgressControl previous = null;
        try {
            await Wait(() => {
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                return login != null && login.isOpened && login.txt.text == "点击屏幕或按任意键开始";
            }, "Normal Login ready", 90000);
            var saved = new PlayerProgressSaveVO { initialized = true };
            saved.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = 2101, level = 1 });
            previous = PlayerProgressControl.InstallValidationSession(new PlayerProgressControl(CfgManager.tables, saved, ignored => { }));
            typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(UnityEngine.Object.FindObjectOfType<LoginPanel>(), new object[] { KeyCode.Return });
            await Wait(() => PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL)?.isOpened == true, "Main ready");
            var panel = await OpenPanel();
            var inputs = new[] { panel.inpRedHex, panel.inpBlueHex, panel.inpGridWidth, panel.inpGridHeight, panel.inpGridOriginX, panel.inpGridOriginY };
            var drivers = new FakeKeyboard[inputs.Length];
            for (int i = 0; i < inputs.Length; i++) {
                Check(inputs[i].GetComponents<PlatformInputFieldKeyboard>().Length == 1, "静态/隐藏字段安装一份适配器 " + inputs[i].name);
                Check(ReferenceEquals(PlatformInputFieldKeyboard.Install(inputs[i]), inputs[i].GetComponent<PlatformInputFieldKeyboard>()), "重复安装复用组件 " + inputs[i].name);
                drivers[i] = new FakeKeyboard();
                inputs[i].GetComponent<PlatformInputFieldKeyboard>().Bind(inputs[i], drivers[i]);
            }
            Check(!new WeChatTextKeyboard().IsSupported, "Editor保留桌面路径，不调用微信原生SDK");
            Click((RectTransform)inputs[0].transform);
            await Wait(() => drivers[0].shown == 1, "Red keyboard ready");
            drivers[0].changed("#12ab34");
            drivers[0].confirmed("#12ab34");
            Check(ColorUtility.ToHtmlStringRGB(Settings(panel).red) == "12AB34" && inputs[0].text == "#12AB34", "红色色值原生Confirm即时应用并规范大小写");
            drivers[0].completed("#FFFFFF");
            Check(ColorUtility.ToHtmlStringRGB(Settings(panel).red) == "12AB34", "Confirm之后Complete不覆盖颜色");
            Click((RectTransform)inputs[1].transform);
            await Wait(() => drivers[1].shown == 1, "Blue keyboard ready");
            drivers[1].changed("#654321");
            drivers[1].confirmed("#654321");
            Check(ColorUtility.ToHtmlStringRGB(Settings(panel).blue) == "654321", "蓝色色值原生Confirm即时应用");
            panel.inpBlueHex.onEndEdit.Invoke("#AABBCC");
            Check(ColorUtility.ToHtmlStringRGB(Settings(panel).blue) == "AABBCC", "桌面失焦提交保持有效");
            panel.inpRedHex.onSubmit.Invoke("#ZZZZZZ");
            Check(ColorUtility.ToHtmlStringRGB(Settings(panel).red) == "12AB34" && panel.inpRedHex.text == "#12AB34", "非法颜色恢复最后有效值");
            Click((RectTransform)inputs[0].transform);
            await Task.Delay(100);
            var resetStale = drivers[0].confirmed;
            drivers[0].changed("#FF00FF");
            panel.btnReset.onClick.Invoke();
            resetStale("#FF00FF");
            Check(panel.inpRedHex.text == "#B84A33" && ColorUtility.ToHtmlStringRGB(Settings(panel).red) == "B84A33", "重置废弃活跃原生会话和迟到Confirm");
            int readonlyShows = drivers[0].shown;
            inputs[0].readOnly = true;
            inputs[0].GetComponent<PlatformInputFieldKeyboard>().OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            Check(drivers[0].shown == readonlyShows, "只读颜色字段不唤起原生键盘");
            inputs[0].readOnly = false;
            int disabledShows = drivers[1].shown;
            inputs[1].interactable = false;
            inputs[1].GetComponent<PlatformInputFieldKeyboard>().OnSelect(new BaseEventData(EventSystem.current));
            Check(drivers[1].shown == disabledShows, "禁用颜色字段不唤起原生键盘");
            inputs[1].interactable = true;
            Click((RectTransform)inputs[1].transform);
            await Task.Delay(100);
            string beforeFailure = inputs[1].text;
            int failureHide = drivers[1].hidden;
            var failedStale = drivers[1].changed;
            drivers[1].failed("模拟键盘拒绝");
            failedStale("#000000");
            Check(drivers[1].hidden > failureHide && inputs[1].text == beforeFailure, "键盘打开失败释放会话并拒绝迟到文本");
            ScreenCapture.CaptureScreenshot(Path.Combine(request.evidenceDirectory, "actual-ground-panel.png"));
            await Task.Delay(200);
            var renderer = panel.GetComponent<LayerEffectTestRenderer>();
            var ownersField = typeof(LayerEffectTestRenderer).GetField("sceneOwnersData", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalOwners = ownersField.GetValue(renderer);
            Check(renderer.IsGroundScene, "正式GM入口保持原场景预览模式");
            // 四个网格字段属保留的通用预览分支，仅在Play隔离实例解除场景数据，不改Prefab/渲染源。
            ownersField.SetValue(renderer, null);
            renderer.Initialize();
            panel.btnReset.onClick.Invoke();
            panel.btnGridSettings.onClick.Invoke();
            // 新激活的Graphic须进入一次实际Canvas重建后才有有效射线深度。
            await Task.Delay(150);
            Check(panel.boxGridSettings.gameObject.activeSelf, "隔离通用预览分支显示四个网格字段");
            string[] values = { "121.5", "104", "-25.5", "40.25" };
            Vector2 priorSize = Settings(panel).hexSize;
            Vector2 priorOrigin = Settings(panel).origin;
            for (int i = 2; i < inputs.Length; i++) {
                Click((RectTransform)inputs[i].transform);
                int index = i;
                await Wait(() => drivers[index].shown > 0, "Grid keyboard ready " + index);
                drivers[i].changed(values[i - 2]);
                drivers[i].confirmed(values[i - 2]);
                Check(inputs[i].text == values[i - 2], "网格原生文本同步 " + inputs[i].name);
                Check(Settings(panel).hexSize == priorSize && Settings(panel).origin == priorOrigin, "单字段确认不自动应用整个网格 " + inputs[i].name);
            }
            panel.inpGridWidth.SetTextWithoutNotify("123.5");
            Click(panel.btnGridApply.GetComponent<RectTransform>());
            Check(Settings(panel).hexSize == new Vector2(123.5f, 104) && Settings(panel).origin == new Vector2(-25.5f, 40.25f), "应用按钮读取四字段最终快照并一次提交");
            Check(!panel.boxGridSettings.gameObject.activeSelf, "应用后关闭网格面板");
            panel.btnGridSettings.onClick.Invoke();
            // 新激活的Graphic须进入一次实际Canvas重建后才有有效射线深度。
            await Task.Delay(150);
            Vector2 sizeBeforeInvalid = Settings(panel).hexSize;
            Vector2 originBeforeInvalid = Settings(panel).origin;
            panel.inpGridWidth.SetTextWithoutNotify("0");
            panel.inpGridOriginX.SetTextWithoutNotify("300");
            panel.btnGridApply.onClick.Invoke();
            Check(panel.boxGridSettings.gameObject.activeSelf && Settings(panel).hexSize == sizeBeforeInvalid && Settings(panel).origin == originBeforeInvalid, "任一非法字段拒绝整组网格提交");
            panel.inpGridWidth.SetTextWithoutNotify("NaN");
            panel.btnGridApply.onClick.Invoke();
            Check(Settings(panel).hexSize == sizeBeforeInvalid, "非有限数值不写入本地网格状态");
            Click((RectTransform)inputs[4].transform);
            await Task.Delay(80);
            var cancelStale = drivers[4].changed;
            drivers[4].changed("55");
            int hidden = drivers[4].hidden;
            panel.btnGridCancel.onClick.Invoke();
            cancelStale("999");
            Check(drivers[4].hidden > hidden && inputs[4].text == "55", "取消隐藏字段关闭键盘并拒绝迟到回调");
            Check(Settings(panel).origin == originBeforeInvalid, "取消保留之前有效网格");
            panel.btnGridSettings.onClick.Invoke();
            // 新激活的Graphic须进入一次实际Canvas重建后才有有效射线深度。
            await Task.Delay(150);
            Check(inputs[4].text == "-25.5", "网格重开恢复已提交值");
            ScreenCapture.CaptureScreenshot(Path.Combine(request.evidenceDirectory, "isolated-grid-fields.png"));
            await Task.Delay(200);
            Click((RectTransform)inputs[3].transform);
            await Task.Delay(80);
            int oldHidden = drivers[3].hidden;
            var closeStale = drivers[3].changed;
            ownersField.SetValue(renderer, originalOwners);
            var oldTexture = (RenderTexture)panel.rawResultPreview.texture;
            panel.Close();
            await Wait(() => !PanelMgr.ins.IsPanelVisible(MainUIEnum.LAYER_EFFECT_TEST_PANEL), "Layer panel closed");
            closeStale("333");
            Check(drivers[3].hidden > oldHidden && inputs[3].text == "104", "窗口关闭会话失效，旧输入不能回写");
            Check(oldTexture == null || !oldTexture.IsCreated(), "窗口关闭保持原GPU资源释放契约");
            var reopened = await OpenPanel();
            Check(ReferenceEquals(reopened, panel), "实际池化重用同一窗口实例");
            Check(reopened.GetComponent<LayerEffectTestRenderer>().IsGroundScene && reopened.inpRedHex.text == "#B84A33", "重开恢复正式场景模式与默认颜色");
            for (int i = 0; i < inputs.Length; i++) {
                Check(inputs[i].GetComponents<PlatformInputFieldKeyboard>().Length == 1, "回池重开适配器不重复 " + inputs[i].name);
            }
            Click((RectTransform)inputs[0].transform);
            await Task.Delay(80);
            int submits = 0;
            UnityEngine.Events.UnityAction<string> count = value => submits++;
            inputs[0].onSubmit.AddListener(count);
            drivers[0].confirmed("#334455");
            drivers[0].completed("#000000");
            inputs[0].onSubmit.RemoveListener(count);
            Check(submits == 1 && ColorUtility.ToHtmlStringRGB(Settings(panel).red) == "334455", "池化后Confirm/Complete仅一次提交且颜色正确");
            panel.Close();
            await Wait(() => !PanelMgr.ins.IsPanelVisible(MainUIEnum.LAYER_EFFECT_TEST_PANEL), "Final layer panel closed");
            Check(string.IsNullOrEmpty(error), "完整六字段测试无Error/Exception");
            Write(request, "passed", null, false);
        } catch (Exception exception) {
            ScreenCapture.CaptureScreenshot(Path.Combine(request.evidenceDirectory, "failure-game.png"));
            await Task.Delay(250);
            Write(request, "failed", exception.ToString(), false);
            Debug.LogException(exception);
        } finally {
            Application.logMessageReceived -= OnLog;
            PlayerProgressControl.InstallValidationSession(previous);
            SessionState.EraseString(Key);
            EditorApplication.isPlaying = false;
        }
    }

    private static void OnLog(string message, string stack, LogType type) {
        if (!expectedError && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) {
            error = message + "\n" + stack;
        }
    }

    private static void Write(Request request, string status, string failure, bool playing) {
        File.WriteAllText(Path.Combine(request.evidenceDirectory, "result.json"), JsonUtility.ToJson(new Result { requestId = request.requestId, status = status, error = failure, checks = checks.ToArray(), leftPlaying = playing, enableGM = FrameworkPlatformConfig.Read().enableGM }, true));
    }

}
#endif

