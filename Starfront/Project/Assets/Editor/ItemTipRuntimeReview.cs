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
/// 材料Tips的正常入口隔离验收与可重复运行证据。
/// </summary>
/// <remarks>仅替换内存玩家会话；功能测试结束保留游戏效果供用户查看，不写真实存档。</remarks>
[InitializeOnLoad]
public static class ItemTipRuntimeReview {

    private const string Pending = "Library/AIUI/pending-item-tip-review.json";

    private const string Key = "ItemTip.RuntimeReview";

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

    static ItemTipRuntimeReview() {
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
        if (!FrameworkPlatformConfig.Read().enableGM) {
            Write(request, "failed", "Development configuration must explicitly enable GM", false);
            return;
        }
        string path = "Assets/Prefab/default/itemTip/ItemTipPanel.prefab";
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("Default Local Group");
        if (group == null || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) {
            Write(request, "failed", "Tips prefab/default group missing", false);
            return;
        }
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
        entry.address = path;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
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

    private static async Task<ItemTipPanel> Tip() {
        await Wait(() => PanelMgr.ins.GetPanel(ItemTipControl.PanelId)?.isOpened == true, "Tips open");
        return (ItemTipPanel)PanelMgr.ins.GetPanel(ItemTipControl.PanelId);
    }

    private static async Task Closed() {
        await Wait(() => !PanelMgr.ins.IsPanelVisible(ItemTipControl.PanelId), "Tips recycled");
    }

    private static async Task Capture(Request request, string name) {
        Canvas.ForceUpdateCanvases();
        await Task.Delay(250);
        ScreenCapture.CaptureScreenshot(Path.Combine(request.evidenceDirectory, name + ".png"));
        await Task.Delay(200);
    }

    private static async void Run(Request request) {
        checks.Clear();
        error = null;
        Application.logMessageReceived += OnLog;
        Application.runInBackground = true;
        int writes = 0;
        try {
            await Wait(() => {
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                return login != null && login.isOpened && login.txt.text == "点击屏幕或按任意键开始";
            }, "Normal Login ready", 90000);
            var saved = new PlayerProgressSaveVO { initialized = true };
            saved.cards.Add(new PlayerProgressSaveVO.CardLevelEntry { cardId = 2101, level = 1 });
            saved.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = 2001, count = 73 });
            saved.items.Add(new PlayerProgressSaveVO.ItemEntry { itemId = 12101, count = 1000 });
            var isolated = new PlayerProgressControl(CfgManager.tables, saved, ignored => writes++);
            writes = 0;
            PlayerProgressControl.InstallValidationSession(isolated);
            typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(UnityEngine.Object.FindObjectOfType<LoginPanel>(), new object[] { KeyCode.Return });
            await Wait(() => PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL)?.isOpened == true, "Main ready");
            var main = (MainPanel)PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL);
            main.btnDeck.onClick.Invoke();
            await Task.Delay(600);
            main.GetComponentInChildren<DeckPresentationView>(true).RequestCardInfo(2101);
            await Wait(() => PanelMgr.ins.GetPanel(CardCultivationControl.PanelId)?.isOpened == true, "Cultivation ready");
            var parent = (CardCultivationPanel)PanelMgr.ins.GetPanel(CardCultivationControl.PanelId);
            await Wait(() => parent.GetComponentsInChildren<CardCultivationMaterialItem>().Count(x => x.CurrentItemId > 0) == 2, "Material sources ready");
            await Task.Delay(1200);
            var materials = parent.GetComponentsInChildren<CardCultivationMaterialItem>().Where(x => x.CurrentItemId > 0).ToArray();
            var fragment = materials.First(x => x.CurrentItemId == 12101);
            var diamond = materials.First(x => x.CurrentItemId == 2001);
            Click((RectTransform)fragment.transform);
            var tip = await Tip();
            await Capture(request, "first-open-diagnostic");
            await Wait(() => tip.imgItemIcon.sprite != null && tip.imgQualityIcon.sprite != null, "Real tip icons");
            Check(tip.txtItemName.text == "陆战队员碎片" && tip.txtQuality.text == "普通", "正式道具名称品质绑定");
            Check(tip.txtDescription.text == CfgManager.tables.ItemObj.Get(12101).Desc, "正式说明只读绑定");
            Check(tip.txtItemName.alignment == TMPro.TextAlignmentOptions.TopLeft && tip.txtDescription.alignment == TMPro.TextAlignmentOptions.TopLeft && tip.txtQuality.alignment == TMPro.TextAlignmentOptions.TopLeft, "右列三段文本统一左对齐");
            Check(tip.txtItemName.fontStyle == TMPro.FontStyles.Normal && tip.txtDescription.fontStyle == TMPro.FontStyles.Normal, "文字采用批准常规字重");
            float titleTop = tip.txtItemName.rectTransform.anchoredPosition.y + ItemTipPanel.VisibleGlyphTop(tip.txtItemName);
            Check(Mathf.Abs(titleTop - tip.imgItemIcon.rectTransform.anchoredPosition.y) <= 1, "名称可见字形上缘与图标上缘对齐");
            Check(tip.UsesWindowTransition && tip.layer == PanelLayer.SCALE_TIP, "共用Tips层与统一开关动画");
            Check(parent.CurrentCardId == 2101 && parent.isOpened, "打开Tips保留培养页卡牌");
            await Capture(request, "short-game");
            float shortHeight = tip.imgBody.rectTransform.rect.height;
            Click((RectTransform)fragment.transform);
            await Closed();
            Check(parent.isOpened, "同源点击关闭且父页保留");
            Click((RectTransform)fragment.transform);
            tip = await Tip();
            Click((RectTransform)diamond.transform);
            await Task.Delay(150);
            Check(ReferenceEquals(tip, PanelMgr.ins.GetPanel(ItemTipControl.PanelId)) && tip.txtItemName.text == CfgManager.tables.ItemObj.Get(2001).Name, "异源点击直接切换同一Tips实例");
            Check(tip.txtDescription.text == "", "空说明不编造内容");
            tip.OnPanelOperate(PanelOperateEnum.ESC);
            await Closed();
            Check(parent.isOpened, "返回仅关闭Tips");
            Click((RectTransform)fragment.transform);
            tip = await Tip();
            int level = isolated.Cards.model.GetState(2101).level;
            Click(parent.btnUpgrade.GetComponent<RectTransform>());
            await Closed();
            Check(isolated.Cards.model.GetState(2101).level == level && writes == 0, "外部点击关闭并消耗，升级与存档不触发");
            Click((RectTransform)fragment.transform);
            tip = await Tip();
            tip.txtDescription.text = string.Join("。", Enumerable.Repeat(CfgManager.tables.ItemObj.Get(12101).Desc, 4));
            tip.LayoutTip();
            Check(tip.imgBody.rectTransform.rect.height > shortHeight, "说明增加自动增高");
            await Capture(request, "long-game");
            tip.txtDescription.text = string.Join("。", Enumerable.Repeat(CfgManager.tables.ItemObj.Get(12101).Desc, 80));
            tip.LayoutTip();
            var scroll = tip.descriptionViewport.GetComponent<ScrollRect>();
            Check(scroll.vertical && scroll.content.rect.height > scroll.viewport.rect.height, "超长说明达到安全区上限后局部滚动");
            scroll.verticalNormalizedPosition = 0;
            await Task.Delay(100);
            Check(scroll.content.anchoredPosition.y > 0, "长说明可滚至底部");
            await Capture(request, "scroll-game");
            ItemTipControl.Close();
            await Closed();
            Click((RectTransform)fragment.transform);
            tip = await Tip();
            object originalMaterialData = typeof(ScrollListItem).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fragment);
            fragment.gameObject.SetActive(false);
            await Closed();
            Check(parent.isOpened, "来源隐藏回收自动关闭Tips");
            fragment.gameObject.SetActive(true);
            fragment.SetData(0, originalMaterialData);
            parent.Close();
            await Task.Delay(300);
            main.GetComponentInChildren<DeckPresentationView>(true).RequestCardInfo(2101);
            await Wait(() => PanelMgr.ins.GetPanel(CardCultivationControl.PanelId)?.isOpened == true, "Cultivation reopen");
            parent = (CardCultivationPanel)PanelMgr.ins.GetPanel(CardCultivationControl.PanelId);
            await Task.Delay(900);
            fragment = parent.GetComponentsInChildren<CardCultivationMaterialItem>().First(x => x.CurrentItemId == 12101);
            Click((RectTransform)fragment.transform);
            tip = await Tip();
            var source = (RectTransform)fragment.transform;
            Vector2 position = source.anchoredPosition;
            source.anchoredPosition += Vector2.up * 20;
            await Closed();
            source.anchoredPosition = position;
            Check(parent.isOpened, "来源滚动移位自动关闭");
            Click(source);
            tip = await Tip();
            PanelMgr.ins.OpenPanel(MainUIEnum.GM_PANEL);
            await Wait(() => PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL)?.isOpened == true, "GM modal ready");
            await Closed();
            Check(parent.isOpened, "新交互窗口关闭Tips且GM正常打开");
            PanelMgr.ins.ClosePanelByType(MainUIEnum.GM_PANEL);
            await Wait(() => !PanelMgr.ins.IsPanelVisible(MainUIEnum.GM_PANEL), "GM closed");
            Check(FrameworkDevelopmentSettings.GMEnabled && PanelMgr.ins.GetPanel(GMEntryView.PanelId) != null, "已验收GM开发入口保持");
            var probe = new GameObject("ItemTip edge probe", typeof(RectTransform)).GetComponent<RectTransform>();
            probe.SetParent(parent.transform, false);
            probe.anchorMin = probe.anchorMax = probe.pivot = new Vector2(.5f, .5f);
            probe.sizeDelta = new Vector2(50, 50);
            Rect area = UIScreenRegions.LocalContentArea(parent.trans);
            foreach (Vector2 point in new[] { new Vector2(area.xMin + 35, area.yMax - 35), new Vector2(area.xMax - 35, area.yMax - 35), new Vector2(area.xMin + 35, area.yMin + 35), new Vector2(area.xMax - 35, area.yMin + 35) }) {
                probe.anchoredPosition = point;
                ItemTipControl.Open(12101, probe.gameObject, fragment.itemResourceMap);
                tip = await Tip();
                var bodyCorners = new Vector3[4];
                tip.imgBody.rectTransform.GetWorldCorners(bodyCorners);
                Vector3 lo = tip.trans.InverseTransformPoint(bodyCorners[0]);
                Vector3 hi = tip.trans.InverseTransformPoint(bodyCorners[2]);
                Rect safe = UIScreenRegions.LocalContentArea(tip.trans);
                Check(lo.x >= safe.xMin && hi.x <= safe.xMax && lo.y >= safe.yMin && hi.y <= safe.yMax, "四角来源Tips自动避让 " + point);
                ItemTipControl.Close();
                await Closed();
            }
            UnityEngine.Object.Destroy(probe.gameObject);
            Click(source);
            await Task.Delay(50);
            ItemTipControl.Close();
            await Closed();
            Check(parent.isOpened, "打开动画中关闭可回收且不误关父页");
            Click(source);
            tip = await Tip();
            var overrideBefore = UIScreenRegions.EditorOverride;
            UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(24, 35, Screen.width - 48, Screen.height - 110) };
            await Task.Delay(200);
            Check(PanelMgr.ins.GetPanel(ItemTipControl.PanelId) == tip && tip.isOpened, "安全区变化重定位而非丢失Tips");
            UIScreenRegions.EditorOverride = overrideBefore;
            await Task.Delay(200);
            ItemTipControl.Close();
            await Closed();
            ItemTipControl.Open(12101, source.gameObject, fragment.itemResourceMap);
            ItemTipControl.Close();
            ItemTipControl.Open(2001, diamond.gameObject, diamond.itemResourceMap);
            tip = await Tip();
            Check(tip.txtItemName.text == CfgManager.tables.ItemObj.Get(2001).Name, "快速取消重开仅呈现最新来源");
            ItemTipControl.Close();
            await Closed();
            Click(source);
            tip = await Tip();
            await Wait(() => tip.imgItemIcon.sprite != null, "Final icons ready");
            await Capture(request, "review-ready-game");
            Check(writes == 0, "整个测试未修改培养等级或写入真实存档");
            Check(string.IsNullOrEmpty(error), "正常入口运行无Error/Exception/Assert");
            Write(request, "passed", null, true);
        } catch (Exception exception) {
            ScreenCapture.CaptureScreenshot(Path.Combine(request.evidenceDirectory, "failure-game.png"));
            Write(request, "failed", exception.ToString(), true);
            Debug.LogException(exception);
        } finally {
            Application.logMessageReceived -= OnLog;
            SessionState.EraseString(Key);
            // 留下本工具的隔离Play供用户看效果；退出Play后内存测试会话自然销毁。
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

