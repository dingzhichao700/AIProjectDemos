using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 对比灰度地表和红蓝六边形混合的标准运行时窗口。
/// </summary>
/// <remarks>实验参数属于界面本地状态，不修改战斗数据或存档。</remarks>
public sealed class LayerEffectTestPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public Button btnClose;
    public TextMeshProUGUI txtLeftPreviewTitle;
    public Button btnCompare;
    public TextMeshProUGUI txtCompareLabel;
    public RawImage rawGrayPreview;
    public RawImage rawResultPreview;
    public Button btnModeMultiply;
    public Button btnModeOverlay;
    public Button btnModeSoftLight;
    public Button btnModeScreen;
    public Button btnModeAdd;
    public TMP_InputField inpRedHex;
    public TMP_InputField inpBlueHex;
    public Image imgRedSwatch;
    public Image imgBlueSwatch;
    public Slider sliderStrength;
    public TextMeshProUGUI txtStrengthValue;
    public Slider sliderBrightness;
    public TextMeshProUGUI txtBrightnessValue;
    public Slider sliderContrast;
    public TextMeshProUGUI txtContrastValue;
    public Button btnPaintCamp;
    public TextMeshProUGUI txtPaintCampLabel;
    public Button btnClearPaint;
    public Button btnReset;
    public TextMeshProUGUI txtHint;
    public Button btnGridSettings;
    public TextMeshProUGUI txtGridInfo;
    public RectTransform boxGridSettings;
    public TMP_InputField inpGridWidth;
    public TMP_InputField inpGridHeight;
    public TMP_InputField inpGridOriginX;
    public TMP_InputField inpGridOriginY;
    public Button btnGridCancel;
    public Button btnGridApply;
    public Button btnToggleGrid;
    public TextMeshProUGUI txtGridToggleLabel;
    /******************* UIComponent Define finish ************************/

    private LayerEffectTestSettings settings;

    private LayerEffectTestRenderer rendererView;

    private LayerEffectPaintInput paintInput;

    private bool original;

    private enum BrushMode { Red, Blue, CellHole, LocalHole, Restore }

    private BrushMode brush;

    private Vector2? lastPaintPoint;

    private bool pendingGrid;

    public LayerEffectTestPanel() {
        layer = PanelLayer.SCALE_PANEL_SECOND;
    }

    /**通过绑定窗口生命周期管理GPU资源和监听；池化重开不继承旧试验状态。*/
    public override void OnOpen() {
        rendererView = GetComponent<LayerEffectTestRenderer>();
        rendererView.Initialize();
        paintInput = rawResultPreview.GetComponent<LayerEffectPaintInput>();
        ResetParameters();
        AddLis();
        FitViewport();
    }

    public override void OnClose() {
        RemoveLis();
        boxGridSettings.gameObject.SetActive(false);
        rawGrayPreview.texture = null;
        rawResultPreview.texture = null;
        rendererView.Release();
    }

    private void AddLis() {
        btnClose.onClick.AddListener(Close);
        btnCompare.onClick.AddListener(ToggleCompare);
        btnModeMultiply.onClick.AddListener(Multiply);
        btnModeOverlay.onClick.AddListener(Overlay);
        btnModeSoftLight.onClick.AddListener(SoftLight);
        btnModeScreen.onClick.AddListener(ScreenBlend);
        btnModeAdd.onClick.AddListener(AddBlend);
        inpRedHex.onEndEdit.AddListener(ChangeRed);
        inpBlueHex.onEndEdit.AddListener(ChangeBlue);
        sliderStrength.onValueChanged.AddListener(ChangeStrength);
        sliderBrightness.onValueChanged.AddListener(ChangeBrightness);
        sliderContrast.onValueChanged.AddListener(ChangeContrast);
        btnPaintCamp.onClick.AddListener(ToggleBrush);
        btnClearPaint.onClick.AddListener(ClearPaint);
        btnReset.onClick.AddListener(ResetParameters);
        btnGridSettings.onClick.AddListener(OpenGrid);
        btnGridCancel.onClick.AddListener(CancelGrid);
        btnGridApply.onClick.AddListener(ApplyGrid);
        paintInput.Painted += Paint;
        paintInput.StrokeStarted += BeginStroke;
        btnToggleGrid.onClick.AddListener(ToggleGrid);
        PanelMgr.ins.On(PanelEvent.WINDOW_RESIZE, FitViewport);
    }

    private void RemoveLis() {
        btnClose.onClick.RemoveListener(Close);
        btnCompare.onClick.RemoveListener(ToggleCompare);
        btnModeMultiply.onClick.RemoveListener(Multiply);
        btnModeOverlay.onClick.RemoveListener(Overlay);
        btnModeSoftLight.onClick.RemoveListener(SoftLight);
        btnModeScreen.onClick.RemoveListener(ScreenBlend);
        btnModeAdd.onClick.RemoveListener(AddBlend);
        inpRedHex.onEndEdit.RemoveListener(ChangeRed);
        inpBlueHex.onEndEdit.RemoveListener(ChangeBlue);
        sliderStrength.onValueChanged.RemoveListener(ChangeStrength);
        sliderBrightness.onValueChanged.RemoveListener(ChangeBrightness);
        sliderContrast.onValueChanged.RemoveListener(ChangeContrast);
        btnPaintCamp.onClick.RemoveListener(ToggleBrush);
        btnClearPaint.onClick.RemoveListener(ClearPaint);
        btnReset.onClick.RemoveListener(ResetParameters);
        btnGridSettings.onClick.RemoveListener(OpenGrid);
        btnGridCancel.onClick.RemoveListener(CancelGrid);
        btnGridApply.onClick.RemoveListener(ApplyGrid);
        paintInput.Painted -= Paint;
        paintInput.StrokeStarted -= BeginStroke;
        btnToggleGrid.onClick.RemoveListener(ToggleGrid);
        PanelMgr.ins.Off(PanelEvent.WINDOW_RESIZE, FitViewport);
    }

    /**保留720×1280设计比例，当前父画布改变时重新适配。*/
    private void FitViewport() {
        var viewport = trans.parent as RectTransform;
        if (viewport != null && viewport.rect.width > 0 && viewport.rect.height > 0) {
            float ratio = Mathf.Min(viewport.rect.width / 720, viewport.rect.height / 1280);
            trans.localScale = Vector3.one * ratio;
        }
    }

    private void ResetParameters() {
        settings = new LayerEffectTestSettings();
        original = false;
        brush = BrushMode.Red;
        lastPaintPoint = null;
        boxGridSettings.gameObject.SetActive(false);
        rendererView.ClearPaint();
        inpRedHex.SetTextWithoutNotify("#B84A33");
        inpBlueHex.SetTextWithoutNotify("#4A84B8");
        sliderStrength.SetValueWithoutNotify(settings.strength);
        sliderBrightness.SetValueWithoutNotify(settings.brightness);
        sliderContrast.SetValueWithoutNotify(settings.contrast);
        txtLeftPreviewTitle.text = "灰度底图";
        txtCompareLabel.text = "看原图";
        txtPaintCampLabel.text = "画笔：红色";
        txtHint.text = "点击画笔切换染色/挖洞/还原；右侧预览支持拖动。";
        if (rendererView.IsGroundScene) {
            txtLeftPreviewTitle.text = "地块底图";
            txtHint.text = "普通格含侧面染色；中立保留原色。点击左下按钮互换阵营。";
            btnGridSettings.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "阵营：正常";
        }
        Refresh();
    }

    private void ToggleCompare() {
        original = !original;
        txtLeftPreviewTitle.text = original ? "原始底图" : "灰度底图";
        txtCompareLabel.text = original ? "看灰度" : "看原图";
        Refresh();
    }

    private void Multiply() {
        SetMode(2);
    }

    private void Overlay() {
        SetMode(3);
    }

    private void SoftLight() {
        SetMode(4);
    }

    private void ScreenBlend() {
        SetMode(5);
    }

    private void AddBlend() {
        SetMode(6);
    }

    private void SetMode(int mode) {
        settings.mode = mode;
        Refresh();
    }

    private void ChangeRed(string value) {
        ChangeColor(value, false);
    }

    private void ChangeBlue(string value) {
        ChangeColor(value, true);
    }

    /**只接受六位RGB；无效输入恢复最后有效颜色，不污染Shader参数。*/
    private void ChangeColor(string value, bool blue) {
        value = value.Trim();
        if (value.Length != 7 || value[0] != '#' || !uint.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb) || !ColorUtility.TryParseHtmlString(value, out Color color)) {
            (blue ? inpBlueHex : inpRedHex).SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGB(blue ? settings.blue : settings.red));
            txtHint.text = "颜色格式应为 #RRGGBB，已保留原颜色。";
            return;
        }
        if (blue) {
            settings.blue = color;
        } else {
            settings.red = color;
        }
        (blue ? inpBlueHex : inpRedHex).SetTextWithoutNotify(value.ToUpperInvariant());
        Refresh();
    }

    private void ChangeStrength(float value) {
        settings.strength = value;
        Refresh();
    }

    private void ChangeBrightness(float value) {
        settings.brightness = value;
        Refresh();
    }

    private void ChangeContrast(float value) {
        settings.contrast = value;
        Refresh();
    }

    private void ToggleBrush() {
        brush = (BrushMode)(((int)brush + 1) % (rendererView.IsGroundScene ? 2 : 5));
        lastPaintPoint = null;
        txtPaintCampLabel.text = new[] { "画笔：红色", "画笔：蓝色", "画笔：整格挖洞", "画笔：局部挖洞", "画笔：还原格子" }[(int)brush];
    }

    private void ClearPaint() {
        rendererView.ClearPaint();
        Refresh();
    }

    private void Paint(Vector2 point) {
        if (boxGridSettings.gameObject.activeSelf) {
            return;
        }
        if (rendererView.IsGroundScene) {
            rendererView.PaintScenePoint(point, brush == BrushMode.Blue);
            Refresh();
            return;
        }
        var cell = LayerEffectTestRenderer.CellAt(point, settings);
        if (brush == BrushMode.CellHole) {
            rendererView.SetCellHole(cell, true);
        } else if (brush == BrushMode.LocalHole) {
            rendererView.CutStroke(lastPaintPoint ?? point, point, settings.cutBrushRadius);
        } else if (brush == BrushMode.Restore) {
            rendererView.RestoreCell(cell, settings);
        } else {
            rendererView.Paint(cell, brush == BrushMode.Blue);
        }
        lastPaintPoint = point;
        Refresh();
    }

    private void BeginStroke() {
        lastPaintPoint = null;
    }

    private void ToggleGrid() {
        pendingGrid = !pendingGrid;
        txtGridToggleLabel.text = pendingGrid ? "格线：开启" : "格线：关闭";
    }

    private void OpenGrid() {
        if (rendererView.IsGroundScene) {
            settings.layout = settings.layout == 0 ? 1 : 0;
            btnGridSettings.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = settings.layout == 0 ? "阵营：正常" : "阵营：互换";
            Refresh();
            return;
        }
        pendingGrid = settings.showGrid;
        txtGridToggleLabel.text = pendingGrid ? "格线：开启" : "格线：关闭";
        inpGridWidth.SetTextWithoutNotify(Number(settings.hexSize.x));
        inpGridHeight.SetTextWithoutNotify(Number(settings.hexSize.y));
        inpGridOriginX.SetTextWithoutNotify(Number(settings.origin.x));
        inpGridOriginY.SetTextWithoutNotify(Number(settings.origin.y));
        boxGridSettings.gameObject.SetActive(true);
    }

    private void CancelGrid() {
        boxGridSettings.gameObject.SetActive(false);
    }

    /**弹窗所有字段同时通过才提交；网格身份改变时旧画笔覆盖失效。*/
    private void ApplyGrid() {
        if (!TryNumber(inpGridWidth.text, 8, 720, out float width) || !TryNumber(inpGridHeight.text, 8, 1280, out float height) || !TryNumber(inpGridOriginX.text, -720, 1440, out float x) || !TryNumber(inpGridOriginY.text, -1280, 2560, out float y)) {
            txtHint.text = "网格参数无效：宽8–720、高8–1280；基点超出范围。";
            return;
        }
        bool geometryChanged = settings.hexSize != new Vector2(width, height) || settings.origin != new Vector2(x, y);
        settings.showGrid = pendingGrid;
        settings.hexSize = new Vector2(width, height);
        settings.origin = new Vector2(x, y);
        if (geometryChanged) {
            rendererView.ClearPaint();
        }
        lastPaintPoint = null;
        boxGridSettings.gameObject.SetActive(false);
        Refresh();
    }

    private static bool TryNumber(string value, float min, float max, out float number) {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !float.IsNaN(number) && !float.IsInfinity(number) && number >= min && number <= max;
    }

    private static string Number(float value) {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void Refresh() {
        rendererView.Render(settings, original);
        rawGrayPreview.texture = rendererView.Gray;
        rawResultPreview.texture = rendererView.Result;
        imgRedSwatch.color = settings.red;
        imgBlueSwatch.color = settings.blue;
        txtStrengthValue.text = settings.strength.ToString("0.00", CultureInfo.InvariantCulture);
        txtBrightnessValue.text = settings.brightness.ToString("0.00", CultureInfo.InvariantCulture);
        txtContrastValue.text = settings.contrast.ToString("0.00", CultureInfo.InvariantCulture);
        txtGridInfo.text = "格子 " + Number(settings.hexSize.x) + " × " + Number(settings.hexSize.y) + " · 基点 " + Number(settings.origin.x) + ", " + Number(settings.origin.y);
        if (rendererView.IsGroundScene) {
            txtGridInfo.text = "场景1001 · 中立不染色";
        }
        var modes = new[] { btnModeMultiply, btnModeOverlay, btnModeSoftLight, btnModeScreen, btnModeAdd };
        for (int i = 0; i < modes.Length; i++) {
            ((Image)modes[i].targetGraphic).sprite = settings.mode == i + 2 ? rendererView.selectedPlate : rendererView.neutralPlate;
        }
        RefreshFill("imgStrengthFill", sliderStrength.normalizedValue);
        RefreshFill("imgBrightnessFill", sliderBrightness.normalizedValue);
        RefreshFill("imgContrastFill", sliderContrast.normalizedValue);
    }

    private void RefreshFill(string name, float value) {
        var suffix = name.Substring(3, name.Length - 7);
        var fill = (RectTransform)trans.Find("slider" + suffix + "/" + name);
        fill.sizeDelta = new Vector2(442 * Mathf.Clamp01(value), 6);
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC && !inpRedHex.isFocused && !inpBlueHex.isFocused && !inpGridWidth.isFocused && !inpGridHeight.isFocused && !inpGridOriginX.isFocused && !inpGridOriginY.isFocused) {
            if (boxGridSettings.gameObject.activeSelf) {
                CancelGrid();
            } else {
                Close();
            }
        }
    }

}
