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
/// 微信键盘和GM发放的隔离调用链与存储验收。
/// </summary>
/// <remarks>仅替换内存玩家会话；仅使用内存会话与Library测试存档；结束退出自启Play并恢复原场景。</remarks>
[InitializeOnLoad]
public static class WeChatGMInputReview {

    private const string Pending = "Library/AIUI/pending-wechat-gm-input-review.json";

    private const string Key = "WeChatGM.InputReview";

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

    static WeChatGMInputReview() {
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
                for (int i = 0; i < roots.Length && i < flags.Length; i++) {
                    roots[i].SetActive(flags[i] == "1");
                }
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
            }
            SessionState.EraseInt(Key + ".originalScene");
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
        var pointer = new PointerEventData(EventSystem.current) { pointerId = 72, position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0, "真实射线命中 " + target.name);
        GameObject hit = hits[0].gameObject;
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
            string folder = "Library/AIUI/WeChatGMInput/" + request.requestId;
            Directory.CreateDirectory(folder);
            var store = new PlayerProgressStore(folder + "/playerProgress.json");
            store.Save(saved);
            var isolated = new PlayerProgressControl(CfgManager.tables, store.Load(), store.Save);
            previous = PlayerProgressControl.InstallValidationSession(isolated);
            typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(UnityEngine.Object.FindObjectOfType<LoginPanel>(), new object[] { KeyCode.Return });
            await Wait(() => PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL)?.isOpened == true, "Main ready");
            var main = (MainPanel)PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL);
            main.btnDeck.onClick.Invoke();
            await Task.Delay(300);
            main.GetComponentInChildren<DeckPresentationView>(true).RequestCardInfo(2101);
            await Wait(() => PanelMgr.ins.GetPanel(CardCultivationControl.PanelId)?.isOpened == true, "Cultivation ready");
            var cultivation = (CardCultivationPanel)PanelMgr.ins.GetPanel(CardCultivationControl.PanelId);
            await Task.Delay(1000);
            var material = cultivation.GetComponentsInChildren<CardCultivationMaterialItem>().First(row => row.CurrentItemId == 12101);
            GMControl.ins.OpenPanel();
            await Wait(() => PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL)?.isOpened == true, "GM ready");
            var gm = (GmPanel)PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL);
            GMItem command = null;
            var scroll = gm.listItem.GetComponent<ScrollRect>();
            for (int step = 0; step <= 30 && command == null; step++) {
                scroll.verticalNormalizedPosition = 1 - step / 30f;
                await Task.Delay(80);
                command = gm.GetComponentsInChildren<GMItem>(false).FirstOrDefault(row => ((cfg.resource.GMResource)typeof(GMItem).GetField("cfg", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(row))?.Content == "inventoryGrant");
            }
            Check(command != null, "实际GM列表找到inventoryGrant行");
            var idInput = command.listParam.GetCell(0).GetComponent<TMPro.TMP_InputField>();
            var countInput = command.listParam.GetCell(1).GetComponent<TMPro.TMP_InputField>();
            Check(idInput.GetComponent<PlatformInputFieldKeyboard>() != null && countInput.GetComponent<PlatformInputFieldKeyboard>() != null, "两个GM参数均安装平台键盘适配");
            Check(!new WeChatTextKeyboard().IsSupported, "Editor保持桌面输入且不调用微信原生API");
            var idKeyboard = new FakeKeyboard();
            var countKeyboard = new FakeKeyboard();
            var idBridge = idInput.GetComponent<PlatformInputFieldKeyboard>();
            var countBridge = countInput.GetComponent<PlatformInputFieldKeyboard>();
            idBridge.Bind(idInput, idKeyboard);
            countBridge.Bind(countInput, countKeyboard);
            Click(idInput.GetComponent<RectTransform>());
            await Wait(() => idKeyboard.shown == 1, "Keyboard show on input touch");
            Check(idKeyboard.initial == idInput.text, "点击参数将当前内容传给系统键盘");
            idBridge.OnSelect(new BaseEventData(EventSystem.current));
            Check(idKeyboard.shown == 1, "同次Select/PointerClick不重复弹键盘");
            idKeyboard.changed("12101");
            Check(idInput.text == "12101", "原生Input回调同步可见TMP文字");
            idKeyboard.confirmed("12101");
            await Task.Delay(150);
            Check(isolated.Inventory.model.GetQuantity(12101) == 15, "原生Confirm通过真实GM指令入账12101碎片15");
            Check(isolated.Inventory.model.GetQuantity(3101) == 0, "未误发旧默认3101");
            idKeyboard.completed("12101");
            Check(isolated.Inventory.model.GetQuantity(12101) == 15, "Confirm后Complete不重复提交");
            Check(GMControl.ins.LastResultText.Contains("12101") && GMControl.ins.LastResultText.Contains("15"), "成功反馈含实际道具ID和余额");
            Check(material.txtMaterialOwned.text == "15", "培养材料数量接收实际提交事件立即刷新");
            var loaded = store.Load();
            Check(loaded.items.Single(row => row.itemId == 12101).count == 15, "Library隔离存档重读确认入账");
            Click(countInput.GetComponent<RectTransform>());
            await Wait(() => countKeyboard.shown == 1, "Count keyboard show");
            countKeyboard.changed("7");
            var oldChanged = countKeyboard.changed;
            countBridge.Close();
            oldChanged("99999");
            Check(countInput.text == "7", "失焦迟到Input回调不篡改参数");
            countInput.SetTextWithoutNotify("9");
            Click(command.btnExecute.GetComponent<RectTransform>());
            await Task.Delay(100);
            Check(isolated.Inventory.model.GetQuantity(12101) == 24, "执行按钮读取最终可见文字，未通知尾帧也精确增加9");
            Check(store.Load().items.Single(row => row.itemId == 12101).count == 24, "第二次File.Replace提交可重读");
            idInput.text = "3101";
            countInput.text = "4";
            Click(command.btnExecute.GetComponent<RectTransform>());
            Check(isolated.Inventory.model.GetQuantity(3101) == 4 && isolated.Inventory.model.GetQuantity(12101) == 24, "旧3101与现行12101严格独立入账");
            Check(GMControl.ins.LastResultText.Contains("远征步兵"), "旧道具成功反馈明确名字避免误看当前卡牌");
            idInput.text = "12101";
            countInput.text = "0";
            expectedError = true;
            Click(command.btnExecute.GetComponent<RectTransform>());
            expectedError = false;
            Check(isolated.Inventory.model.GetQuantity(12101) == 24 && GMControl.ins.LastResultText.Contains("正整数"), "无效数量拒绝并展示具体格式原因");
            countInput.text = "1";
            var failing = new PlayerProgressControl(CfgManager.tables, store.Load(), ignored => { throw new IOException("模拟存储失败"); });
            PlayerProgressControl.InstallValidationSession(failing);
            expectedError = true;
            Click(command.btnExecute.GetComponent<RectTransform>());
            expectedError = false;
            Check(failing.Inventory.model.GetQuantity(12101) == 24 && GMControl.ins.LastResultText.Contains("模拟存储失败"), "保存失败保持原数量且具体原因传回GM界面");
            PlayerProgressControl.InstallValidationSession(isolated);
            countInput.text = "2";
            Click(command.btnExecute.GetComponent<RectTransform>());
            Check(isolated.Inventory.model.GetQuantity(12101) == 26 && store.Load().items.Single(row => row.itemId == 12101).count == 26, "失败后可重试且按正式事务提交");
            Click(idInput.GetComponent<RectTransform>());
            await Task.Delay(50);
            int hideBefore = idKeyboard.hidden;
            var staleInput = idKeyboard.changed;
            idInput.gameObject.SetActive(false);
            staleInput("3101");
            Check(idKeyboard.hidden > hideBefore && idInput.text == "12101", "参数隐藏回收关闭键盘且迟到事件失效");
            idInput.gameObject.SetActive(true);
            Click(idInput.GetComponent<RectTransform>());
            await Task.Delay(50);
            PanelMgr.ins.ClosePanelByType(MainUIEnum.GM_PANEL);
            await Wait(() => !PanelMgr.ins.IsPanelVisible(MainUIEnum.GM_PANEL), "GM close");
            Check(idKeyboard.hidden > hideBefore + 1, "GM关闭回池释放原生键盘监听");
            Check(cultivation.isOpened && cultivation.CurrentCardId == 2101, "GM关闭保留培养卡牌");
            Check(isolated.Cards.model.GetState(2101).level == 1, "测试未改变等级或培养规则");
            Check(string.IsNullOrEmpty(error), "除预期失败用例外无Error/Exception");
            File.Copy(folder + "/playerProgress.json", Path.Combine(request.evidenceDirectory, "isolated-final-progress.json"), true);
            Write(request, "passed", null, false);
        } catch (Exception exception) {
            Write(request, "failed", exception.ToString(), false);
            Debug.LogException(exception);
        } finally {
            expectedError = false;
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

