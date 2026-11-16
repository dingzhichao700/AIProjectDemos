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
public static class CardSkillDetailValidation {

    private const string Key = "CardSkillDetail.Validation.v1";

    private const string Pending = "Library/AIUI/pending-card-skill-detail.json";

    private const string ResultPath = "Library/AIUI/card-skill-detail-result.json";

    [Serializable] private sealed class Request { public string requestId; public int processId; }

    [Serializable] private sealed class Result { public string requestId; public string status; public string error; public string[] checks; }

    [Serializable] private sealed class SceneBackup { public RootState[] roots; }

    [Serializable] private sealed class RootState { public int scene; public int index; public string name; public bool active; }

    private static readonly List<string> checks = new List<string>();

    private static string runtimeError;

    private static Camera camera;

    private static RenderTexture target;

    private static Canvas canvas;

    static CardSkillDetailValidation() {
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
            string path = "Assets/Prefab/" + CardSkillDetailControl.PanelId + ".prefab";
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.FindGroup("Default Local Group"));
            entry.SetAddress(path);
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
                root.SetActive(false);
            }
            SessionState.SetInt(Key + ".mode", settings.ActivePlayModeDataBuilderIndex);
            settings.ActivePlayModeDataBuilderIndex = 0;
            SessionState.SetString(Key, request.requestId);

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
        try {
            new GameObject("SkillValidationDispatcher", typeof(UnityMainThreadDispatcher));
            ResourcePresentation.Install(new GameResourcePresentationPolicy());
            timerField.SetValue(null, new Timer());
            var loading = CfgManager.EnsureLoadedAsync();
            if (await Task.WhenAny(loading, Task.Delay(45000)) != loading) {
                throw new TimeoutException("Configuration load timed out");
            }
            await loading;
            int writes = 0;
            var session = new PlayerProgressControl(CfgManager.tables, null, ignored => writes++);
            writes = 0; // 新会话的初始种子仅写入内存回调，验证从初始化完成后计数。
            previousSession = PlayerProgressControl.InstallValidationSession(session);
            installed = true;
            BuildStage();
            UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(0, 0, 720, 1280) };
            await Run(session);
            Check(writes == 0, "真实玩家存档与隔离会话均无写入");
            if (!string.IsNullOrEmpty(runtimeError)) {
                throw new InvalidOperationException(runtimeError);
            }
            WriteResult(id, null);
        } catch (Exception error) {
            WriteResult(id, error);
            Debug.LogException(error);
        } finally {
            CardSkillDetailControl.Close();
            if (installed) {
                PlayerProgressControl.InstallValidationSession(previousSession);
            }
            UIScreenRegions.EditorOverride = previousRegions;
            timerField.SetValue(null, previousTimer);
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

    private static async Task Run(PlayerProgressControl session) {
        var tables = CfgManager.tables;
        foreach (int level in new[] { 1, 2, 3 }) {
            var detail = CardSkillDetailQuery.Read(tables, 1101, level, CardSkillSlot.Basic);
            var row = tables.CardUnitLevelObj.DataList.Single(value => value.CardId == 1101 && value.Level == level);
            var skill = tables.SkillObj.Get(row.SkillIds.Single());
            Check(detail.skillId == skill.Id && detail.skillName == skill.Name && detail.battleSummary.Contains(skill.CooldownMs + " 毫秒"), "步兵等级" + level + "按正式配置读取技能身份与冷却");
            Check(detail.stages.Count == 0 && detail.missing.Contains("当前生效阶数"), "卡牌等级" + level + "不会被误写成技能阶数");
        }
        var lockedCard = CardSkillDetailQuery.Read(tables, 1302, 0, CardSkillSlot.Passive);
        Check(lockedCard.missing.Count > 0 && lockedCard.stages.Count == 0, "未拥有且未配置卡牌仍可查看缺项");
        bool rejected = false;
        try {
            CardSkillDetailQuery.Read(tables, 1302, 0, CardSkillSlot.Basic, 1101);
        } catch (ArgumentException) {
            rejected = true;
        }
        Check(rejected, "拒绝其他卡牌的技能ID，防止展示串卡");
        rejected = false;
        try {
            CardSkillDetailQuery.Read(tables, 1101, 1, CardSkillSlot.Passive, 1101);
        } catch (ArgumentException) {
            rejected = true;
        }
        Check(rejected, "拒绝普攻技能被当作被动入口");
        var source = new GameObject("IndependentSourceCard1101", typeof(RectTransform), typeof(BasePanel));
        var sourceRect = source.GetComponent<RectTransform>();
        sourceRect.sizeDelta = new Vector2(720, 1280);
        var sourcePanel = source.GetComponent<BasePanel>();
        sourcePanel.layer = PanelLayer.SCALE_PANEL_THIRD;
        sourcePanel.panelEnum = "validation/skill-source";
        var sourceText = Text(source.transform, "txtSource", new Vector2(0, 470), new Vector2(640, 90), "技能详情 · 独立运行验证\n来源卡牌：远征步兵 1101", 22);
        var sourceButton = new GameObject("btnSourceAction", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        sourceButton.transform.SetParent(source.transform, false);
        var sourceButtonRect = (RectTransform)sourceButton.transform;
        sourceButtonRect.sizeDelta = new Vector2(360, 70);
        sourceButtonRect.anchoredPosition = new Vector2(0, -400);
        sourceButton.targetGraphic = sourceButton.GetComponent<Image>();
        sourceButton.targetGraphic.color = new Color(.04f, .28f, .4f);
        Text(sourceButton.transform, "txtLabel", Vector2.zero, new Vector2(300, 32), "来源页操作（验证拦截）", 20);
        int sourceClicks = 0;
        sourceButton.onClick.AddListener(() => sourceClicks++);
        typeof(PanelMgr).GetMethod("OpenPanelInstance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(PanelMgr.ins, new object[] { sourcePanel, null, Rect.zero });
        long revision = session.model.Revision;
        int callbacks = 0;
        await CardSkillDetailControl.OpenAsync(1101, CardSkillSlot.Basic, onClosed: () => callbacks++);
        await Task.Delay(300);
        await Tick();
        var panel = PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) as CardSkillDetailPanel;
        Check(panel != null && panel.isOpened && panel.txtTitle.text == tables.SkillObj.Get(1101).Name, "通过正式打开API、Addressables和UIBinder显示当前卡牌技能");
        Check(panel.txtSummary.text.Contains("20 点伤害") && panel.txtMissing.text.Contains("解锁条件"), "正式数值与缺项分区显示");
        Check(Vector2.Distance(panel.imgMask.rectTransform.rect.size, PanelMgr.ins.viewportScale.rect.size) < .1f, "遮罩覆盖整个实际舞台：" + panel.imgMask.rectTransform.rect.size + "/" + PanelMgr.ins.viewportScale.rect.size);
        ValidateBindings(panel);
        foreach (var size in new[] { new Vector2Int(720, 1600), new Vector2Int(1280, 720), new Vector2Int(360, 640) }) {
            Resize(size.x, size.y);
            UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(size.x * .03f, size.y * .03f, size.x * .94f, size.y * .91f) };
            await Task.Delay(300);
        await Tick();
            panel.GetComponent<PanelSafeContent>().Apply();
            Canvas.ForceUpdateCanvases();
            Check(Vector2.Distance(panel.imgMask.rectTransform.rect.size, PanelMgr.ins.viewportScale.rect.size) < .1f, "视窗变化后遮罩完整覆盖：" + size);
            var corners = new Vector3[4];
            panel.boxWindow.GetWorldCorners(corners);
            Rect area = UIScreenRegions.LocalContentArea(PanelMgr.ins.viewportScale);
            Check(corners.All(corner => { var point = PanelMgr.ins.viewportScale.InverseTransformPoint(corner); return point.x >= area.xMin - 1 && point.x <= area.xMax + 1 && point.y >= area.yMin - 1 && point.y <= area.yMax + 1; }), "视窗与安全边距变化后内容完整容纳：" + size);
        }
        Resize(720, 1280);
        UIScreenRegions.EditorOverride = new UIScreenRegions.Regions { safeArea = new Rect(0, 0, 720, 1280) };
        await Task.Delay(300);
        await Tick();
        panel.GetComponent<PanelSafeContent>().Apply();
        sourcePanel.GetComponent<PanelSafeContent>().Apply();
        Capture("card-skill-detail-formal.png");
        Check(panel.txtTitle.textInfo.characterInfo.Any(value => value.isVisible), "实际渲染技能名称字形可见");
        var hit = Click((RectTransform)panel.imgWindowBg.transform);
        Check(hit != null && hit.transform.IsChildOf(panel.transform) && panel.isOpened && sourceClicks == 0, "点击弹窗内部空白不关闭、不穿透");
        var iconProbe = Fixture(1);
        iconProbe.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/atlasSource/default/cardSkillDetail/skillClose.png");
        panel.Render(iconProbe);
        Check(panel.imgSkillIcon.enabled && panel.imgSkillIcon.sprite == iconProbe.icon, "外部持有Sprite消费接口可用，未制作技能图标");
        panel.Render(CardSkillDetailQuery.Read(tables, 1101, 1, CardSkillSlot.Basic));
        await Task.Delay(300);
        await Tick();
        Check(!panel.imgSkillIcon.enabled && panel.imgSkillIcon.sprite == null, "缺少正式关联时清空图标，不串用外部临时引用");
        var oldPanel = panel;
        Click((RectTransform)panel.btnClose.transform);
        Check(PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) == null && callbacks == 1 && sourceClicks == 0, "关闭按钮只关闭弹窗且回调一次");
        Check(PanelMgr.ins.GetPanel(sourcePanel.panelEnum) == sourcePanel && session.Cards.model.GetState(1101).level == 1, "关闭后保留原卡牌与来源页实例");
        await CardSkillDetailControl.OpenAsync(1101, CardSkillSlot.Passive, onClosed: () => callbacks++);
        await Task.Delay(300);
        await Tick();
        panel = (CardSkillDetailPanel)PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId);
        Check(ReferenceEquals(oldPanel, panel) && panel.txtType.text == "被动", "池化重开刷新所选技能，不沿用上次普攻");
        panel.Render(Fixture(1));
        await Task.Delay(300);
        await Tick();
        var first = panel.listStages.GetCell(0).GetComponent<CardSkillStageItem>();
        var second = panel.listStages.GetCell(1).GetComponent<CardSkillStageItem>();
        Check(first.imgStageCurrent.enabled && first.imgStageCurrent.gameObject.activeInHierarchy && first.txtCondition.text == "当前" && !first.imgStageNormal.enabled && !first.imgLock.enabled && second.imgLock.enabled, "隔离阶数样例：当前阶高亮、锁定阶及解锁条件可读");
        Capture("card-skill-detail-stages-test.png");
        Check(!first.txtEffect.isTextOverflowing, "两行阶数效果完整显示");
        Check(first.txtCondition.fontSizeMax == 17 && !first.txtCondition.isTextOverflowing && !second.txtCondition.isTextOverflowing, "当前与解锁标记按状态字号显示且不溢出");
        Check(first.txtCondition.textInfo.characterInfo.Any(value => value.isVisible) && second.txtCondition.textInfo.characterInfo.Any(value => value.isVisible), "实际渲染当前标记和锁定解锁条件可见");
        Click((RectTransform)second.transform);
        Check(panel.isOpened && sourceClicks == 0, "锁定阶可查看且点击不关闭或穿透");
        var rankTwo = Fixture(2);
        panel.Render(rankTwo);
        await Task.Delay(300);
        await Tick();
        var lower = panel.listStages.GetCell(0).GetComponent<CardSkillStageItem>();
        var active = panel.listStages.GetCell(1).GetComponent<CardSkillStageItem>();
        var future = panel.listStages.GetCell(2).GetComponent<CardSkillStageItem>();
        Check(!lower.imgStageCurrent.enabled && lower.imgStageNormal.enabled && !lower.imgLock.enabled && lower.txtCondition.text == "已解锁", "低阶已解锁但不再标当前");
        Check(active.imgStageCurrent.enabled && !active.imgStageNormal.enabled && !active.imgLock.enabled && future.imgLock.enabled, "当前阶、已解锁低阶和锁定高阶互斥装配");
        Check(active.txtEffect.text == rankTwo.stages[1].effect, "显示当前阶完整效果，不拼接其他阶效果");
        Capture("card-skill-detail-rank2-test.png");
        panel.Render(Fixture(0));
        await Task.Delay(300);
        await Tick();
        Check(!panel.listStages.GetCell(0).GetComponent<CardSkillStageItem>().imgStageCurrent.enabled, "同一列表复用为全部锁定后清除当前阶高亮");
        Capture("card-skill-detail-locked-test.png");
        var longFixture = Fixture(1);
        longFixture.stages = Enumerable.Range(1, 8).Select(rank => new CardSkillStageVO { rank = rank, effect = "仅验证滚动的测试效果 " + rank, unlockCondition = "测试条件", unlocked = true, current = rank == 8 }).ToArray();
        panel.Render(longFixture);
        await Task.Delay(300);
        await Tick();
        panel.listStages.GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
        await Task.Delay(300);
        await Tick();
        Check(panel.listStages.GetCell(7) != null && panel.listStages.GetCell(7).GetComponent<CardSkillStageItem>().txtRank.text == "8阶", "各阶列表超过三项仍可滚动与复用");
        Click(sourceButtonRect);
        Check(PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId) == null && sourceClicks == 0 && callbacks == 2, "点击外部遮罩关闭，同一次点击不触发底层按钮");
        Click(sourceButtonRect);
        Check(sourceClicks == 1, "关闭后的下一次点击恢复底层操作");
        await CardSkillDetailControl.OpenAsync(1302, CardSkillSlot.Ultimate, onClosed: () => callbacks++);
        await Task.Delay(300);
        await Tick();
        panel = (CardSkillDetailPanel)PanelMgr.ins.GetPanel(CardSkillDetailControl.PanelId);
        Check(panel.txtType.text == "大招" && panel.txtTitle.text.Contains("暂无配置"), "未拥有卡牌大招可打开，显示真实缺项");
        panel.OnPanelOperate(PanelOperateEnum.ESC);
        CardSkillDetailControl.Close();
        Check(callbacks == 3 && !PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId), "Esc和重复关闭不重复通知来源");
        var pending = CardSkillDetailControl.OpenAsync(1101, CardSkillSlot.Basic);
        CardSkillDetailControl.Close();
        await pending;
        await Task.Delay(300);
        await Tick();
        Check(!PanelMgr.ins.IsPanelOpen(CardSkillDetailControl.PanelId), "来源页取消后异步打开不残留弹窗");
        Check(session.model.Revision == revision && session.Cards.model.GetState(1302).level == 0, "测试未改变卡牌等级、拥有状态或事务revision");
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
