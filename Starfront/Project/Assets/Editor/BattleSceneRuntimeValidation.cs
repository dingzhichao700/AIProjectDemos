using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>真实登录入口至配置场景的编辑器回归；不修改玩家进度或配置数据。</summary>
[InitializeOnLoad]
public static class BattleSceneRuntimeValidation {
    private const string Key = "Starfront.BattleSceneValidation";
    private static double deadline;
    private static int phase;
    private static int openedCount;
    private static string error;
    private static double incomeDeadline;
    private static double screenshotReadyAt;
    private static double ringCaptureAt;
    private static long firstUnitId;
    private static Vector2 firstUnitPosition;

    static BattleSceneRuntimeValidation() {
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.update += TryInteractiveRequest;
    }

    /**只在用户现有GameEntrance中运行回归，完成后保留编辑器。*/
    private static void TryInteractiveRequest() {
        const string requestPath = "Library/battle-scene-validation-request.json";
        if (!File.Exists(requestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        var request = JsonUtility.FromJson<InteractiveRequest>(File.ReadAllText(requestPath));
        File.Delete(requestPath);
        if (request == null || string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
            File.WriteAllText("Library/battle-scene-validation-request-result.json", "{\"status\":\"failed\",\"error\":\"Invalid request or editor PID\"}");
            return;
        }
        SessionState.SetString(Key + ".requestId", request.requestId);
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/GameEntrance.unity") {
            WriteInteractiveResult("failed: requires idle GameEntrance scene");
            return;
        }
        SessionState.SetBool(Key + ".interactive", true);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    [Serializable]
    private class InteractiveRequest {
        public string requestId;
        public int processId;
    }

    [Serializable]
    private class InteractiveResult {
        public string requestId;
        public string result;
    }

    private static void WriteInteractiveResult(string result) {
        File.WriteAllText("Library/battle-scene-validation-request-result.json", JsonUtility.ToJson(new InteractiveResult { requestId = SessionState.GetString(Key + ".requestId", ""), result = result }, true));
    }

    public static void RunBatch() {
        AddressablesAutoBuilder.BuildPlayerContent();
        SessionState.SetBool(Key + ".interactive", false);
        SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".result", "failed: validation did not finish");
        EditorSceneManager.OpenScene("Assets/Scenes/GameEntrance.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayState(PlayModeStateChange state) {
        if (!SessionState.GetBool(Key, false)) {
            return;
        }
        if (state == PlayModeStateChange.EnteredPlayMode) {
            SessionState.SetBool(Key + ".background", Application.runInBackground);
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 120;
            phase = 0;
            openedCount = 0;
            screenshotReadyAt = 0;
            ringCaptureAt = 0;
            error = null;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        } else if (state == PlayModeStateChange.EnteredEditMode) {
            Application.runInBackground = SessionState.GetBool(Key + ".background", false);
            SessionState.SetBool(Key, false);
            string result = SessionState.GetString(Key + ".result", "failed");
            File.WriteAllText("Library/battle-scene-runtime-validation.txt", result);
            if (SessionState.GetBool(Key + ".interactive", false)) {
                WriteInteractiveResult(result);
            } else {
                EditorApplication.Exit(result.StartsWith("passed") ? 0 : 1);
            }
        }
    }

    private static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            error = message + "\n" + stack;
        }
    }

    private static void Tick() {
        EditorApplication.QueuePlayerLoopUpdate();
        try {
            if (error != null) {
                throw new InvalidOperationException(error);
            }
            Require(EditorApplication.timeSinceStartup < deadline, "runtime timeout");
            if (phase == 0) {
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                if (login == null || !login.isOpened || login.txt.text != "点击屏幕或按任意键开始") {
                    return;
                }
                typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { KeyCode.Return });
                phase = 1;
            } else if (phase == 1) {
                var main = PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL) as MainPanel;
                if (main == null || !main.isOpened) {
                    return;
                }
                Require(CfgManager.tables.BattleSceneObj.Get(1001).Appearances.Count == 232, "scene config preloaded");
                Require(CfgManager.tables.BattleSceneCellObj.DataList.Count == 171, "cell config preloaded");
                BattleEconomyValidation.ValidateLoadedConfiguration();
                BattleInfoRuntimeValidation.ValidateLoadedState();
                BattleUnlockValidation.ValidateLogic();
                main.GetComponent<MainPanelLayout>().battleButton.GetComponent<Button>().onClick.Invoke();
                phase = 2;
            } else if (phase == 2) {
                var panel = PanelMgr.ins.GetPanel(UIEnum.BATTLE_SCENE_PANEL) as BattleScenePanel;
                if (panel == null || !panel.isOpened) {
                    return;
                }
                var hud = PanelMgr.ins.GetPanel(UIEnum.BATTLE_INFO_PANEL) as BattleInfoPanel;
                if (hud == null || !hud.isOpened) {
                    return;
                }
                ValidateScene(panel);
                BattleControl.ins.model.StopTimeFlow();
                try {
                    BattleUnitProductionValidation.ValidateLogic();
                } finally {
                    BattleControl.ins.model.StartTimeFlow();
                }
                incomeDeadline = EditorApplication.timeSinceStartup + 8;
                phase = 3;
            } else if (phase == 3) {
                var panel = PanelMgr.ins.GetPanel(UIEnum.BATTLE_SCENE_PANEL) as BattleScenePanel;
                var current = BattleControl.ins.model;
                if (current.camps[cfg.BattleSceneCamp.FRIENDLY].currency < 60 || current.camps[cfg.BattleSceneCamp.ENEMY].currency < 60) {
                    Require(EditorApplication.timeSinceStartup < incomeDeadline, "actual base income timeout");
                    return;
                }
                Require(current.camps.Values.All(owner => owner.currency == 60), "actual first period pays both bases once while offscreen");
                var hud = PanelMgr.ins.GetPanel(UIEnum.BATTLE_INFO_PANEL) as BattleInfoPanel;
                var info = hud.GetComponent<BattleInfoView>();
                Require(info.friendlyCurrency.text == "60", "HUD reflects friendly income; enemy balance remains authoritative only");
                if (openedCount == 0 && ringCaptureAt == 0) {
                    ringCaptureAt = EditorApplication.timeSinceStartup + 2;
                    return;
                }
                if (openedCount == 0 && EditorApplication.timeSinceStartup < ringCaptureAt) {
                    return;
                }
                if (openedCount == 0 && screenshotReadyAt == 0) {
                    var captureView = panel.GetComponent<BattleSceneView>();
                    var captureTarget = current.GetCell(current.config.FriendlyBaseQ, current.config.FriendlyBaseK).position;
                    var captureDelta = new Vector2(captureView.cameraCenter.x - captureTarget.x, captureTarget.y - captureView.cameraCenter.y) * captureView.zoom;
                    typeof(BattleSceneView).GetMethod("OnDrag", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(captureView, new object[] { captureDelta });
                    Canvas.ForceUpdateCanvases();
                    BattleProductionValidation.ValidateAlignment(captureView, current);
                    File.WriteAllText("E:/ChatGPT/星域前线/work/unit-production-v1/capture-state.txt", "active=" + panel.gameObject.activeInHierarchy + "; viewport=" + captureView.viewport.rect + "; visible=" + captureView.visibleCount + "; root=" + captureView.sceneRoot.position + "; camera=" + captureView.cameraCenter + "; cull=" + captureView.viewport.GetComponent<Image>().canvasRenderer.cull + "; canvas=" + captureView.viewport.GetComponentInParent<Canvas>().enabled);
                    ScreenCapture.CaptureScreenshot("E:/ChatGPT/星域前线/work/unit-production-v1/runtime.png");
                    screenshotReadyAt = EditorApplication.timeSinceStartup + 0.5;
                    return;
                }
                if (openedCount == 0 && EditorApplication.timeSinceStartup < screenshotReadyAt) {
                    return;
                }
                openedCount++;
                if (openedCount == 2) {
                    BattleUnlockValidation.ValidateLiveCollector(panel);
                    incomeDeadline = EditorApplication.timeSinceStartup + 16;
                    phase = 5;
                    return;
                }
                var model = BattleControl.ins.model;
                string path = BattleSceneResources.SpritePath(model.config.FriendlyGroundResource);
                hud.OnPanelOperate(PanelOperateEnum.ESC);
                Require(PanelMgr.ins.GetPanel(UIEnum.BATTLE_INFO_PANEL) == null && info.friendlyCurrency.text == string.Empty, "HUD escape closes scene and clears text");
                Require(BattleControl.ins.model == null && model.cells.Count == 0, "close clears logic");
                Require(!ResourceManager.HasLoadedUnpackImage(path), "close releases scene sprites");
                Require(panel.GetComponent<BattleSceneView>().visibleCount == 0, "close clears presentation");
                Require(panel.GetComponent<BattleSceneView>().productionVisibleCount == 0, "close clears production rings");
                Require(!ResourceManager.HasLoadedUnpackImage(BattleSceneResources.SpritePath(BattleConst.ProductionFillResource)), "close releases owned ring resources");
                phase = 1;
            } else if (phase == 5) {
                var current = BattleControl.ins.model;
                var target = BattleUnlockValidation.GetLiveBarracks();
                if (current.camps[cfg.BattleSceneCamp.FRIENDLY].currency < target.unlockConfig.Cost) {
                    Require(EditorApplication.timeSinceStartup < incomeDeadline, "new collector actual income timeout");
                    return;
                }
                var panel = PanelMgr.ins.GetPanel(UIEnum.BATTLE_SCENE_PANEL) as BattleScenePanel;
                BattleUnlockValidation.ValidateLiveBarracks(panel);
                ScreenCapture.CaptureScreenshot("E:/ChatGPT/星域前线/work/unit-production-v1/runtime-unlocked.png");
                incomeDeadline = EditorApplication.timeSinceStartup + 15;
                phase = 6;
            } else if (phase == 6) {
                if (BattleControl.ins.model.GetUnitCount(cfg.BattleSceneCamp.FRIENDLY) == 0) {
                    Require(EditorApplication.timeSinceStartup < incomeDeadline, "real unit production timeout");
                    return;
                }
                var livePanel = PanelMgr.ins.GetPanel(UIEnum.BATTLE_SCENE_PANEL) as BattleScenePanel;
                var first = BattleUnitProductionValidation.ValidateLive(livePanel);
                firstUnitId = first.id;
                firstUnitPosition = first.position;
                screenshotReadyAt = EditorApplication.timeSinceStartup + 1.2;
                phase = 7;
            } else if (phase == 7) {
                if (EditorApplication.timeSinceStartup < screenshotReadyAt) {
                    return;
                }
                var moving = BattleControl.ins.model.elements[firstUnitId] as BattleUnitVO;
                Require(Vector2.Distance(moving.position, firstUnitPosition) > 25, "real unit moves under player Timer after birth");
                ScreenCapture.CaptureScreenshot("E:/ChatGPT/星域前线/work/unit-production-v1/runtime-units.png");
                screenshotReadyAt = EditorApplication.timeSinceStartup + .5;
                phase = 8;
            } else if (phase == 8) {
                if (EditorApplication.timeSinceStartup < screenshotReadyAt) {
                    return;
                }
                var current = BattleControl.ins.model;
                var panel = PanelMgr.ins.GetPanel(UIEnum.BATTLE_SCENE_PANEL) as BattleScenePanel;
                var info = (PanelMgr.ins.GetPanel(UIEnum.BATTLE_INFO_PANEL) as BattleInfoPanel).GetComponent<BattleInfoView>();
                RookieEngine.sceneTimer.SyncTime((float)(current.remainingSeconds + 1) * 1000);
                Require(BattleControl.ins.model == null && current.cells.Count == 0, "expiry clears purchased logical scene");
                Require(PanelMgr.ins.GetPanel(UIEnum.BATTLE_INFO_PANEL) == null && info.friendlyCurrency.text == string.Empty, "expiry clears HUD");
                Require(panel.GetComponent<BattleSceneView>().unitVisibleCount == 0, "expiry clears unit views");
                Require(panel.GetComponent<BattleSceneView>().unlockVisibleCount == 0, "expiry clears dynamic hints");
                Require(!ResourceManager.HasLoadedUnpackImage(BattleSceneResources.SpritePath(BattleConst.UnlockGlowResource)), "expiry releases glow after view cleanup");
                phase = 4;
            } else if (phase == 4) {
                var main = PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL) as MainPanel;
                if (main == null || !main.isOpened) {
                    return;
                }
                Finish("passed: unit production and movement; actual 10-second production, immediate HUD 1/100 and moving unit; population cap/wait/resume/contention; frame display/culling/mirror; unit cleanup; configuration-driven unlock; 12 seeded maps cover symmetry/guarantees/rejections/payment/empty expansion/card results; live input buys collector, real base+collector income buys barracks; glow/type/cost and red insufficient feedback; dynamic body/shadow generation; clean expiry; Login -> Main -> battle twice; ownership 73/73 distinct from unlocked bases; transparent approved HUD with 8 texts and 11 bindings; both populations 0/100 below avatars; configured 180-second scene countdown; friendly income 50->60; expiry returns Main and releases scene/HUD/rings; ESC cleanup; reopen resets timer and balances; existing map pan/zoom/culling and production ring checks");
            }
        } catch (Exception exception) {
            Finish("failed: " + exception);
        }
    }

    private static void ValidateScene(BattleScenePanel panel) {
        Canvas.ForceUpdateCanvases();
        var view = panel.GetComponent<BattleSceneView>();
        var model = BattleControl.ins.model;
        var hud = PanelMgr.ins.GetPanel(UIEnum.BATTLE_INFO_PANEL) as BattleInfoPanel;
        var info = hud.GetComponent<BattleInfoView>();
        Require(info.friendlyCurrency.text == "50", "HUD resets on open");
        Require(hud.txtFriendlyTerritory.text == "73格" && hud.txtEnemyTerritory.text == "73格", "ownership HUD 73/73");
        Require(hud.txtFriendlyPopulation.text == "0 / 100" && hud.txtEnemyPopulation.text == "0 / 100", "population HUD excludes buildings");
        Require(hud.txtCountdown.text == "03:00", "countdown resets on each open");
        Require(hud.txtFriendlyName.text == "玩家" && hud.txtEnemyName.text == "敌方AI", "configured camp names");
        Require(Mathf.Approximately(hud.imgFriendlyFill.fillAmount, .5f) && Mathf.Approximately(hud.imgEnemyFill.fillAmount, .5f), "ownership fills");
        Require(Mathf.Approximately(hud.imgDivider.rectTransform.anchoredPosition.x, 340), "divider uses full design length");
        Require(hud.GetComponent<Image>() == null && hud.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length == 8, "transparent wrapper and eight independent texts");
        var hudBefore = info.friendlyCurrency.rectTransform.position;
        BattleProductionValidation.Validate(view);
        BattleProductionValidation.ValidateAlignment(view, model);
        Require(model.camps.Count == 2 && model.buildings.Count == 2, "two camps and two bases");
        Require(model.camps.Values.All(owner => owner.currency == 50), "reopen resets both balances");
        var animations = BattleSceneResources.CollectAnimations(model);
        Require(animations.Count == 5 && animations.All(path => FrameAnimationManager.loadedMap.ContainsKey(path)), "all five movement animations fully preloaded");
        Require(animations.All(path => FrameAnimationManager.loadedMap[path].sprites.Length == 8), "all forty animation frames ready");
        Require(Mathf.Approximately(view.zoom, 1f), "open resets zoom");
        Require(model.cells.Count == 171 && view.totalVisualCount == 382, "logical and visual counts");
        Require(view.visibleCount > 0 && view.visibleCount < view.totalVisualCount, "viewport culling");
        Require(model.cells.Values.Count(cell => cell.groundPassable) == 146, "terrain passability");
        Require(model.cells.Values.All(cell => cell.flyingPassable), "air passability");
        foreach (var cell in model.cells.Values) {
            Require(model.FindCell(cell.position)?.id == cell.id, "cell center hit test");
        }
        int clicks = 0;
        Action<long> count = id => clicks++;
        view.CellClicked += count;
        var input = view.input;
        var canvas = view.viewport.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(camera, view.viewport.TransformPoint(view.viewport.rect.center));
        var data = new PointerEventData(EventSystem.current) { pointerId = 0, button = PointerEventData.InputButton.Left, position = screenCenter };
        Vector2 before = view.cameraCenter;
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.initializePotentialDrag);
        data.position += new Vector2(80, 40);
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.beginDragHandler);
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.endDragHandler);
        Require(view.cameraCenter != before && clicks == 0 && model.cells.Count == 171, "drag never unlocks or mutates map");
        var target = model.GetCell(0, 10);
        Vector2 delta = target.position - view.cameraCenter;
        data.position = RectTransformUtility.WorldToScreenPoint(camera, view.viewport.TransformPoint(new Vector2(delta.x, -delta.y)));
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.pointerDownHandler);
        data.position += Vector2.one;
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.pointerUpHandler);
        Require(clicks == 1 && panel.lastInteractionHint == "格子已解锁", "click/jitter forwarded once to gameplay");
        ValidateZoom(view, model, camera, screenCenter);
        BattleProductionValidation.ValidateAlignment(view, model);
        Require(clicks == 2, "pinch creates no click; zoomed single click delivered once");
        data.position = screenCenter;
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.pointerDownHandler);
        data.position += new Vector2(100000, 100000);
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject, data, ExecuteEvents.pointerUpHandler);
        Vector2 halfView = view.viewport.rect.size / (2 * view.zoom);
        Vector2 expectedEdge = new Vector2(Mathf.Min(halfView.x, model.config.Width / 2f), Mathf.Max(model.config.Height - halfView.y, model.config.Height / 2f));
        Require(Vector2.Distance(view.cameraCenter, expectedEdge) < 0.05f, "zoomed camera clamped: " + view.cameraCenter + " expected " + expectedEdge);
        Require(clicks == 2 && model.elements.Count == 173, "offscreen logic retained");
        Require(Vector3.Distance(hudBefore, info.friendlyCurrency.rectTransform.position) < 0.01f, "HUD unaffected by pan and zoom");
        Require(hud.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(graphic => !graphic.raycastTarget), "HUD allows map input through");
        BattleProductionValidation.ValidateAlignment(view, model);
        view.CellClicked -= count;
    }

    private static void ValidateZoom(BattleSceneView view, BattleSceneModel model, Camera camera, Vector2 screenCenter) {
        var input = view.input;
        Vector2 focus = view.viewport.rect.center + new Vector2(80, 50);
        Vector3 focusWorld = view.viewport.TransformPoint(focus);
        Vector3 before = view.sceneRoot.InverseTransformPoint(focusWorld);
        var wheel = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, focusWorld), scrollDelta = new Vector2(0, 1) };
        ExecuteEvents.Execute(input.gameObject, wheel, ExecuteEvents.scrollHandler);
        Require(view.zoom > 1f && view.zoom < 1.5f, "wheel zoom in");
        Require(Vector3.Distance(before, view.sceneRoot.InverseTransformPoint(focusWorld)) < 0.05f, "wheel retains world focus");
        wheel.scrollDelta = new Vector2(0, 100);
        ExecuteEvents.Execute(input.gameObject, wheel, ExecuteEvents.scrollHandler);
        Require(Mathf.Approximately(view.zoom, 1.5f), "maximum zoom");
        wheel.scrollDelta = new Vector2(0, -100);
        ExecuteEvents.Execute(input.gameObject, wheel, ExecuteEvents.scrollHandler);
        Require(Mathf.Approximately(view.zoom, 1f), "minimum zoom");
        var left = new PointerEventData(EventSystem.current) { pointerId = 10, button = PointerEventData.InputButton.Left, position = screenCenter - new Vector2(100, 0) };
        var right = new PointerEventData(EventSystem.current) { pointerId = 11, button = PointerEventData.InputButton.Left, position = screenCenter + new Vector2(100, 0) };
        ExecuteEvents.Execute(input.gameObject, left, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(input.gameObject, right, ExecuteEvents.pointerDownHandler);
        before = view.sceneRoot.InverseTransformPoint(view.viewport.TransformPoint(view.viewport.rect.center));
        left.position -= new Vector2(50, 0);
        right.position += new Vector2(50, 0);
        ExecuteEvents.Execute(input.gameObject, left, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject, right, ExecuteEvents.dragHandler);
        Require(Mathf.Approximately(view.zoom, 1.5f), "pinch out");
        Require(Vector3.Distance(before, view.sceneRoot.InverseTransformPoint(view.viewport.TransformPoint(view.viewport.rect.center))) < 0.05f, "pinch retains center focus");
        left.position += new Vector2(50, 0);
        right.position -= new Vector2(50, 0);
        ExecuteEvents.Execute(input.gameObject, left, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject, right, ExecuteEvents.dragHandler);
        Require(Mathf.Approximately(view.zoom, 1f), "pinch in");
        ExecuteEvents.Execute(input.gameObject, left, ExecuteEvents.pointerUpHandler);
        Vector2 center = view.cameraCenter;
        right.position += new Vector2(70, 20);
        ExecuteEvents.Execute(input.gameObject, right, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(input.gameObject, right, ExecuteEvents.pointerUpHandler);
        Require(view.cameraCenter == center, "remaining finger cannot pan or tap after pinch");
        wheel.position = screenCenter;
        wheel.scrollDelta = new Vector2(0, 100);
        ExecuteEvents.Execute(input.gameObject, wheel, ExecuteEvents.scrollHandler);
        var cell = model.cells.Values.Where(value => value.groundPassable).OrderBy(value => Vector2.Distance(value.position, view.cameraCenter)).First();
        var click = new PointerEventData(EventSystem.current) { pointerId = 12, button = PointerEventData.InputButton.Left, position = RectTransformUtility.WorldToScreenPoint(camera, view.sceneRoot.TransformPoint(new Vector2(cell.position.x, -cell.position.y))) };
        long hit = 0;
        Action<long> receive = id => hit = id;
        view.CellClicked += receive;
        ExecuteEvents.Execute(input.gameObject, click, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(input.gameObject, click, ExecuteEvents.pointerUpHandler);
        view.CellClicked -= receive;
        Require(hit == cell.id, "zoomed grid coordinate mapping");
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }

    private static void Finish(string result) {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SessionState.SetString(Key + ".result", result);
        EditorApplication.isPlaying = false;
    }
}
