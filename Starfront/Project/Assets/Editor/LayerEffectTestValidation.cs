using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 从真实登录和主界面入口验证图层测试窗口。
/// </summary>
/// <remarks>检查像素Alpha、控件输入、画笔和池化重开，不修改业务状态。</remarks>
[InitializeOnLoad]
public static class LayerEffectTestValidation {

    private const string Key = "Starfront.LayerEffectValidation";

    private static double deadline;

    private static int phase;

    private static RenderTexture oldTarget;

    static LayerEffectTestValidation() {
        EditorApplication.playModeStateChanged += StateChanged;
    }

    public static void Start() {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/GameEntrance.unity") {
            throw new InvalidOperationException("Validation requires idle GameEntrance scene.");
        }
        SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".request", File.Exists("Library/layer-effect-validation-id.txt") ? File.ReadAllText("Library/layer-effect-validation-id.txt") : "layer-effect-runtime");
        EditorApplication.isPlaying = true;
    }

    private static void StateChanged(PlayModeStateChange state) {
        if (!SessionState.GetBool(Key, false)) {
            return;
        }
        if (state == PlayModeStateChange.EnteredPlayMode) {
            phase = 0;
            deadline = EditorApplication.timeSinceStartup + 90;
            SessionState.SetBool(Key + ".background", Application.runInBackground);
            Application.runInBackground = true;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        } else if (state == PlayModeStateChange.EnteredEditMode) {
            Application.runInBackground = SessionState.GetBool(Key + ".background", false);
            SessionState.SetBool(Key, false);
        }
    }

    private static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            Finish("failed", message + "\n" + stack);
        }
    }

    private static void Tick() {
        EditorApplication.QueuePlayerLoopUpdate();
        try {
            Require(EditorApplication.timeSinceStartup < deadline, "runtime startup timeout at phase " + phase);
            if (phase == 0) {
                var login = UnityEngine.Object.FindObjectOfType<LoginPanel>();
                if (login != null && login.isOpened && login.txt.text == "点击屏幕或按任意键开始") {
                    typeof(LoginPanel).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { KeyCode.Return });
                    phase = 1;
                }
            } else if (phase == 1) {
                var main = PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL);
                if (main != null && main.isOpened) {
                    Require(main.transform.Find("btnLayerEffectTest") == null, "test entry removed from main");
                    var command = CfgManager.tables.GmObj.DataList.Single(n => n.Content == LayerEffectTestGM.Command);
                    Require(command.Id == 1001 && command.ParamFormat.Count == 0 && command.ParamIntro.Count == 0, "GM command generated and loaded without parameters");
                    GMControl.ins.OpenPanel();
                    phase = 10;
                }
            } else if (phase == 10) {
                var gm = PanelMgr.ins.GetPanel(MainUIEnum.GM_PANEL) as GmPanel;
                if (gm != null && gm.isOpened) {
                    Require(gm.listItem.arraySource.Count == 6, "GM displays existing five and new test command");
                    Require(GMControl.ins.ExecuteGM(LayerEffectTestGM.Command), "GM opens test window");
                    phase = 2;
                }
            } else if (phase == 2) {
                var panel = PanelMgr.ins.GetPanel(MainUIEnum.LAYER_EFFECT_TEST_PANEL) as LayerEffectTestPanel;
                if (panel != null && panel.isOpened) {
                    CheckPanel(panel);
                    oldTarget = (RenderTexture)panel.rawResultPreview.texture;
                    panel.btnClose.onClick.Invoke();
                    Require(PanelMgr.ins.GetPanel(MainUIEnum.LAYER_EFFECT_TEST_PANEL) == null && !oldTarget.IsCreated(), "close releases targets");
                    Require(GMControl.ins.ExecuteGM(LayerEffectTestGM.Command), "GM reopens test window");
                    phase = 3;
                }
            } else if (phase == 3) {
                var panel = PanelMgr.ins.GetPanel(MainUIEnum.LAYER_EFFECT_TEST_PANEL) as LayerEffectTestPanel;
                if (panel != null && panel.isOpened) {
                    Require(Mathf.Abs(panel.sliderStrength.value - 1f) < .001f && panel.GetComponent<LayerEffectTestRenderer>().PaintCount == 0 && panel.rawResultPreview.texture != oldTarget, "pooled reopen resets parameters and resources");
                    panel.btnPaintCamp.onClick.Invoke();
                    Require(panel.txtPaintCampLabel.text == "画笔：蓝色", "listeners register once on reopen");
                    panel.OnPanelOperate(PanelOperateEnum.ESC);
                    Require(PanelMgr.ins.GetPanel(MainUIEnum.LAYER_EFFECT_TEST_PANEL) == null && PanelMgr.ins.GetPanel(UIEnum.MAIN_PANEL) != null, "ESC returns to main");
                    Finish("passed", "real Opening/Login/Main -> GM command 1001/openLayerEffectTest -> standard PanelMgr/UIBinder; six loaded GM commands; no test button on main; 37 retained bindings; complete ground scene ownership tint; neutral unchanged under camp swap, all five blend modes, brightness and contrast; neutral brush blocked; reset exact; alpha preserved; close/ESC/reopen cleanup; runtime screenshots captured");
                }
            }
        } catch (Exception exception) {
            Finish("failed", exception.ToString());
        }
    }

    /**Validate actual scene ownership, neutral preservation, alpha and UI camp swapping.*/
    private static void CheckGroundScene(LayerEffectTestPanel panel) {
        var renderer = panel.GetComponent<LayerEffectTestRenderer>();
        Require(panel.GetComponent<UIBinder>().uiList.Count == 37, "37 existing bindings retained");
        var normal = Read(renderer.Result);
        var reference = Read(renderer.Gray);
        var neutralIndices = new List<int>();
        var campIndices = new List<int>();
        Vector2 neutralPoint = Vector2.zero;
        for (int y = 0; y < 1280; y += 3) {
            for (int x = 0; x < 720; x += 3) {
                int index = (1279 - y) * 720 + x;
                if (normal[index].a < 250) {
                    continue;
                }
                var owner = renderer.SceneOwnerAt(new Vector2(x + .5f, y + .5f));
                int camp = Mathf.RoundToInt(owner.b * 255);
                if (camp == 3) {
                    neutralIndices.Add(index);
                    neutralPoint = new Vector2(x + .5f, y + .5f);
                    Require(normal[index].Equals(reference[index]), "neutral bypasses tint and gray controls");
                } else if (camp == 1 || camp == 2) {
                    campIndices.Add(index);
                }
            }
        }
        Require(neutralIndices.Count > 1000 && campIndices.Count > 10000, "full region and neutral cells visible");
        Require(normal.Where((pixel, i) => pixel.a != reference[i].a).Count() == 0, "source alpha retained");
        Capture(panel, "Library/AIUI/ground-tint-normal.png");
        panel.btnGridSettings.onClick.Invoke();
        var swapped = Read(renderer.Result);
        Require(neutralIndices.All(i => normal[i].Equals(swapped[i])), "camp swap leaves every sampled neutral pixel unchanged");
        Require(campIndices.Count(i => !normal[i].Equals(swapped[i])) > 10000, "camp swap recolors configured ordinary cells");
        Require(normal.Where((pixel, i) => pixel.a != swapped[i].a).Count() == 0, "swap preserves alpha");
        Capture(panel, "Library/AIUI/ground-tint-swapped.png");
        panel.sliderBrightness.value = 1.8f;
        panel.sliderContrast.value = 1.5f;
        foreach (var button in new[] { panel.btnModeMultiply, panel.btnModeOverlay, panel.btnModeSoftLight, panel.btnModeScreen, panel.btnModeAdd }) {
            button.onClick.Invoke();
            var pixels = Read(renderer.Result);
            Require(neutralIndices.All(i => normal[i].Equals(pixels[i])), "neutral unchanged under every blend mode and controls");
        }
        int before = renderer.PaintCount;
        renderer.PaintScenePoint(neutralPoint, true);
        Require(renderer.PaintCount == before, "neutral painting ignored");
        panel.btnReset.onClick.Invoke();
        Require(Read(renderer.Result).SequenceEqual(normal), "reset restores exact default output");
        Capture(panel, "Library/AIUI/layer-effect-runtime.png");
    }

    private static void CheckPanel(LayerEffectTestPanel panel) {
        if (panel.GetComponent<LayerEffectTestRenderer>().IsGroundScene) {
            CheckGroundScene(panel);
            return;
        }
        var binder = panel.GetComponent<UIBinder>();
        Require(binder.uiList.Count == 37 && binder.uiList.All(n => n.go != null), "37 actual bound objects");
        foreach (var field in typeof(LayerEffectTestPanel).GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => typeof(Component).IsAssignableFrom(f.FieldType))) {
            Require(field.GetValue(panel) != null, "non-null binding " + field.Name);
        }
        var gray = (RenderTexture)panel.rawGrayPreview.texture;
        var result = (RenderTexture)panel.rawResultPreview.texture;
        Require(gray.width == 720 && gray.height == 1280 && gray != result, "two full-size live textures");
        Require(panel.sliderStrength.handleRect.rect.size == new Vector2(24, 24), "slider preserves approved 24x24 handle size");
        Texture2D source = new Texture2D(2, 2);
        source.LoadImage(File.ReadAllBytes("Assets/Art/unpack/default/layereffect/terrainSource.png"));
        var pixels = source.GetPixels32();
        var grayPixels = Read(gray);
        var resultPixels = Read(result);
        Require(pixels.Where((pixel, i) => pixel.a != grayPixels[i].a || pixel.a != resultPixels[i].a).Count() == 0, "original holes and antialiased alpha retained pixel-for-pixel");
        var redGray = grayPixels.Where((pixel, i) => pixels[i].a == 255 && pixels[i].r > pixels[i].b).Select(pixel => (int)pixel.r).OrderBy(v => v).ToArray();
        var blueGray = grayPixels.Where((pixel, i) => pixels[i].a == 255 && pixels[i].b > pixels[i].r).Select(pixel => (int)pixel.r).OrderBy(v => v).ToArray();
        Require(Math.Abs(redGray[redGray.Length / 2] - blueGray[blueGray.Length / 2]) <= 1, "red/blue source regions have equal grayscale medians");
        Require(grayPixels.Where(pixel => pixel.a > 200).All(pixel => Math.Abs(pixel.r - pixel.g) <= 1 && Math.Abs(pixel.g - pixel.b) <= 1), "grayscale RGB channels");
        Capture(panel, "Library/AIUI/layer-effect-runtime.png");
        var modes = new[] { panel.btnModeMultiply, panel.btnModeOverlay, panel.btnModeSoftLight, panel.btnModeScreen, panel.btnModeAdd };
        var fingerprints = new HashSet<string>();
        foreach (var button in modes) {
            button.onClick.Invoke();
            var image = Read(result);
            fingerprints.Add(string.Join(",", image.Where((pixel, i) => i % 997 == 0 && pixel.a == 255).Select(pixel => pixel.r + ":" + pixel.g + ":" + pixel.b)));
            Require(((Image)button.targetGraphic).sprite.name == "plateSelected", "mode selected visual");
        }
        Require(fingerprints.Count == 5, "five distinct blend outputs");
        panel.btnCompare.onClick.Invoke();
        var original = Read(gray);
        Require(panel.txtLeftPreviewTitle.text == "原始底图" && original.Where((pixel, i) => Math.Abs(pixel.r - grayPixels[i].r) > 3).Count() > 100, "source toggle bypasses grayscale brightness correction");
        panel.inpRedHex.onEndEdit.Invoke("bad");
        Require(panel.inpRedHex.text == "#B84A33", "invalid color retains prior value");
        panel.inpRedHex.onEndEdit.Invoke("#CC4422");
        Require(panel.inpRedHex.text == "#CC4422" && Mathf.Abs(panel.imgRedSwatch.color.r - .8f) < .001f, "valid color refreshes swatch");
        panel.sliderStrength.value = .4f;
        panel.sliderBrightness.value = 1.5f;
        panel.sliderContrast.value = .8f;
        Require(panel.txtStrengthValue.text == "0.40" && panel.txtBrightnessValue.text == "1.50" && panel.txtContrastValue.text == "0.80" && panel.rawResultPreview.texture == result, "sliders update without target reallocation");
        var paint = panel.rawResultPreview.GetComponent<LayerEffectPaintInput>();
        var canvas = panel.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, paint.transform.TransformPoint(((RectTransform)paint.transform).rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left, pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() } };
        paint.OnPointerDown(pointer);
        Require(panel.GetComponent<LayerEffectTestRenderer>().PaintCount == 1, "pointer conversion paints one cell");
        panel.btnGridSettings.onClick.Invoke();
        Capture(panel, "Library/AIUI/layer-effect-grid-runtime.png");
        paint.OnPointerDown(pointer);
        Require(panel.GetComponent<LayerEffectTestRenderer>().PaintCount == 1, "modal blocks painting");
        panel.inpGridWidth.SetTextWithoutNotify("0");
        panel.btnGridApply.onClick.Invoke();
        Require(panel.boxGridSettings.gameObject.activeSelf, "invalid geometry not committed");
        panel.btnGridCancel.onClick.Invoke();
        Require(panel.txtGridInfo.text.Contains("107"), "cancel preserves prior grid");
        panel.btnGridSettings.onClick.Invoke();
        panel.inpGridWidth.SetTextWithoutNotify("109");
        panel.btnGridApply.onClick.Invoke();
        Require(!panel.boxGridSettings.gameObject.activeSelf && panel.txtGridInfo.text.Contains("109") && panel.GetComponent<LayerEffectTestRenderer>().PaintCount == 0, "grid commits atomically and clears old cell identities");
        panel.btnReset.onClick.Invoke();
        Require(panel.inpRedHex.text == "#B84A33" && panel.txtGridInfo.text.Contains("107"), "reset restores defaults");
        CheckHoles(panel);
        UnityEngine.Object.Destroy(source);
    }

    /**用真实指针入口挖相邻格与连续局部洞，再按GPU像素验证覆盖和还原。*/
    private static void CheckHoles(LayerEffectTestPanel panel) {
        panel.btnReset.onClick.Invoke();
        var target = (RenderTexture)panel.rawResultPreview.texture;
        var baselineAlpha = Read(target);
        var baselineGray = Read((RenderTexture)panel.rawGrayPreview.texture);
        panel.btnGridSettings.onClick.Invoke();
        panel.btnToggleGrid.onClick.Invoke();
        panel.btnGridApply.onClick.Invoke();
        panel.btnPaintCamp.onClick.Invoke();
        panel.btnPaintCamp.onClick.Invoke();
        Require(panel.txtPaintCampLabel.text == "画笔：整格挖洞", "whole-cell brush selected");
        PointerAt(panel, new Vector2(360, 355), false);
        PointerAt(panel, new Vector2(440.25f, 401.5f), false);
        var holes = Read(target);
        Require(Pixel(holes, 360, 355).a == 0 && Pixel(holes, 440, 401).a == 0, "both cell holes transparent");
        Require(Pixel(holes, 400, 378).a == 0 && Pixel(holes, 399, 378).a == 0, "adjacent hole shared edge contains no grid remnant");
        Require(Pixel(holes, 360, 448).a == 255, "neighbor cell retained");
        Require(Read((RenderTexture)panel.rawGrayPreview.texture).SequenceEqual(baselineGray), "left reference stays intact");
        var lineOn = Pixel(holes, 360, 494);
        panel.btnGridSettings.onClick.Invoke();
        panel.btnToggleGrid.onClick.Invoke();
        panel.btnGridApply.onClick.Invoke();
        var noLines = Read(target);
        var lineOff = Pixel(noLines, 360, 494);
        Require(lineOff.r > lineOn.r || lineOff.g > lineOn.g || lineOff.b > lineOn.b, "grid toggle changes edge appearance");
        Require(noLines.Where((pixel, i) => pixel.a != holes[i].a).Count() == 0, "grid switch preserves all holes");
        panel.btnGridSettings.onClick.Invoke();
        panel.btnToggleGrid.onClick.Invoke();
        panel.btnGridApply.onClick.Invoke();
        panel.btnPaintCamp.onClick.Invoke();
        Require(panel.txtPaintCampLabel.text == "画笔：局部挖洞", "local-cut brush selected");
        PointerAt(panel, new Vector2(330, 494), false);
        PointerAt(panel, new Vector2(390, 514), true);
        var cut = Read(target);
        Require(Pixel(cut, 330, 494).a == 0 && Pixel(cut, 360, 504).a == 0 && Pixel(cut, 390, 514).a == 0, "continuous local cut clips texture and crossing grid");
        Require(Pixel(cut, 430, 450).a == 255, "local cut has bounded radius");
        Capture(panel, "Library/AIUI/layer-effect-holes-runtime.png");
        panel.btnGridSettings.onClick.Invoke();
        Capture(panel, "Library/AIUI/layer-effect-grid-runtime.png");
        panel.btnGridCancel.onClick.Invoke();
        panel.btnPaintCamp.onClick.Invoke();
        PointerAt(panel, new Vector2(360, 355), false);
        PointerAt(panel, new Vector2(330, 490), false);
        var restored = Read(target);
        Require(Pixel(restored, 360, 355).a == 255 && Pixel(restored, 330, 490).a == 255, "restore clears selected cell and its partial mask");
        Require(Pixel(restored, 440, 401).a == 0, "restore leaves other cells cut");
        panel.btnClearPaint.onClick.Invoke();
        Require(Read(target).Where((pixel, i) => pixel.a != baselineAlpha[i].a).Count() == 0, "clear edits restores original source holes");
        panel.btnReset.onClick.Invoke();
        Require(panel.sliderStrength.value == 1 && Mathf.Abs(panel.sliderBrightness.value - 1.12f) < .001f && Mathf.Abs(panel.sliderContrast.value - .99f) < .001f, "approved visual defaults restored");
    }

    private static Color32 Pixel(Color32[] pixels, int x, int y) {
        return pixels[(1279 - y) * 720 + x];
    }

    private static void PointerAt(LayerEffectTestPanel panel, Vector2 point, bool drag) {
        Canvas.ForceUpdateCanvases();
        var input = panel.rawResultPreview.GetComponent<LayerEffectPaintInput>();
        var rect = (RectTransform)input.transform;
        var canvas = input.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3 local = new Vector3(rect.rect.xMin + point.x / 720 * rect.rect.width, rect.rect.yMax - point.y / 1280 * rect.rect.height);
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local)), button = PointerEventData.InputButton.Left, pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() } };
        if (drag) {
            input.OnDrag(pointer);
        } else {
            input.OnPointerDown(pointer);
        }
    }

    private static Color32[] Read(RenderTexture target) {
        var previous = RenderTexture.active;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try {
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            return texture.GetPixels32();
        } finally {
            RenderTexture.active = previous;
            UnityEngine.Object.Destroy(texture);
        }
    }

    /**复用现有隔离画布截图工具，之后恢复真实窗口的父节点、层级和变换。*/
    private static void Capture(LayerEffectTestPanel panel, string output) {
        var rect = panel.trans;
        var parent = rect.parent;
        var scale = rect.localScale;
        var position = rect.localPosition;
        int sibling = rect.GetSiblingIndex();
        var layers = panel.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.gameObject.layer);
        try {
            rect.localScale = Vector3.one;
            var request = new AIUIExportQueue.Request { width = 720, height = 1280, previewImagePath = output };
            typeof(AIUIExportQueue).GetMethod("CapturePreview", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { panel.gameObject, request });
        } finally {
            rect.SetParent(parent, false);
            rect.SetSiblingIndex(sibling);
            rect.localScale = scale;
            rect.localPosition = position;
            foreach (var layer in layers) {
                layer.Key.gameObject.layer = layer.Value;
            }
        }
    }

    private static void Require(bool value, string message) {
        if (!value) {
            throw new InvalidOperationException("LayerEffect validation: " + message);
        }
    }

    private static void Finish(string status, string detail) {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        string text = "{\"requestId\":\"" + SessionState.GetString(Key + ".request", "") + "\",\"status\":\"" + status + "\",\"detail\":\"" + detail.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\"}";
        File.WriteAllText("Library/layer-effect-runtime-validation.json", text);
        EditorApplication.isPlaying = false;
    }

}
