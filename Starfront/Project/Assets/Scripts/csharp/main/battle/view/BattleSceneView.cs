using System;
using UnityEngine;
using TMPro;

/// <summary>场景容器、镜头与表现生命周期。配置坐标X右Y下，只在这里转为uGUI坐标。</summary>
public sealed class BattleSceneView : MonoBehaviour {
    public RectTransform viewport;
    public RectTransform sceneRoot;
    public RectTransform lod;
    public RectTransform[] layers;
    public BattleSceneInput input;

    public TMP_FontAsset hintFont;

    private BattleUnlockView unlockHints;

    public int unlockVisibleCount => unlockHints?.visibleCount ?? 0;
    public event Action<long> CellClicked;
    public Vector2 cameraCenter => sceneCamera?.center ?? Vector2.zero;
    public float zoom => sceneCamera?.zoom ?? 1f;
    public int visibleCount => presentation?.visibleCount ?? 0;
    public int totalVisualCount => presentation?.totalCount ?? 0;
    private BattleSceneModel model;
    private BattleScenePresentation presentation;
    private BattleProductionView production;

    private BattleUnitView units;

    public int unitVisibleCount => units?.visibleCount ?? 0;
    public int productionVisibleCount => production?.visibleCount ?? 0;
    private Vector2 lastSize;
    private PortraitGameViewport portrait;
    private bool previousMode;
    private BattleSceneCamera sceneCamera;

    public void Bind(BattleSceneModel scene) {
        Unbind();
        var root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        root.anchoredPosition3D = Vector3.zero;
        root.localScale = Vector3.one;
        portrait = FindObjectOfType<PortraitGameViewport>();
        if (portrait != null) {
            previousMode = portrait.flexiblePortrait;
            portrait.SetFlexiblePortrait(true);
        }
        model = scene ?? throw new ArgumentNullException(nameof(scene));
        sceneRoot.sizeDelta = new Vector2(scene.config.Width, scene.config.Height);
        presentation = new BattleScenePresentation(scene, layers);
        unlockHints = new BattleUnlockView(scene, sceneRoot, lod, layers[(int)cfg.BattleSceneVisualLayer.GROUND_GUIDE - 1], hintFont);
        model.StructureChanged += OnStructureChanged;
        units = new BattleUnitView(scene, layers[(int)cfg.BattleSceneVisualLayer.ABOVE_GROUND - 1]);
        production = new BattleProductionView(scene, sceneRoot, lod);
        sceneCamera = new BattleSceneCamera(new Vector2(scene.config.Width, scene.config.Height), new Vector2(scene.config.CameraCenterX, scene.config.CameraCenterY));
        input.Dragged += OnDrag;
        input.Clicked += OnClick;
        input.Zoomed += OnZoom;
        Canvas.ForceUpdateCanvases();
        RefreshCamera();
    }

    private void LateUpdate() {
        if (model != null && viewport.rect.size != lastSize) {
            input.ResetGesture();
            RefreshCamera();
        } else if (model != null) {
            units.Refresh(sceneCamera.visibleRect);
            production.Refresh(sceneCamera.visibleRect, zoom);
            unlockHints.Refresh(sceneCamera.visibleRect, zoom);
        }
    }

    private void OnDrag(Vector2 delta) {
        sceneCamera.Pan(delta);
        RefreshCamera();
    }

    private void OnZoom(float factor, Vector2 previousFocus, Vector2 currentFocus) {
        sceneCamera.Zoom(factor, previousFocus - viewport.rect.center, currentFocus - viewport.rect.center);
        RefreshCamera();
    }

    private void OnClick(Vector2 point) {
        Vector2 centered = point - viewport.rect.center;
        var cell = model.FindCell(sceneCamera.ViewportToScene(centered));
        if (cell != null) {
            CellClicked?.Invoke(cell.id);
        }
    }

    private void RefreshCamera() {
        lastSize = viewport.rect.size;
        sceneCamera.Resize(lastSize);
        sceneRoot.localScale = Vector3.one * zoom;
        sceneRoot.anchoredPosition = new Vector2(-cameraCenter.x, cameraCenter.y) * zoom;
        presentation.Refresh(sceneCamera.visibleRect);
        units.Refresh(sceneCamera.visibleRect);
        production.Refresh(sceneCamera.visibleRect, zoom);
        unlockHints.Refresh(sceneCamera.visibleRect, zoom);
    }

    /**建筑生成后重新收集静态外观，复用已预加载资源，镜头位置保持不变。*/
    private void OnStructureChanged() {
        presentation.Dispose();
        presentation = new BattleScenePresentation(model, layers);
        RefreshCamera();
    }

    public void ShowInteractionHint(long cellId, string message) {
        unlockHints?.ShowMessage(cellId, message);
    }

    public void Unbind() {
        if (input != null) {
            input.Dragged -= OnDrag;
            input.Clicked -= OnClick;
            input.Zoomed -= OnZoom;
            input.ResetGesture();
        }
        if (model != null) {
            model.StructureChanged -= OnStructureChanged;
        }
        unlockHints?.Dispose();
        unlockHints = null;
        units?.Dispose();
        units = null;
        production?.Dispose();
        production = null;
        presentation?.Dispose();
        presentation = null;
        model = null;
        sceneCamera = null;
        if (sceneRoot != null) {
            sceneRoot.localScale = Vector3.one;
        }
        if (portrait != null) {
            portrait.SetFlexiblePortrait(previousMode);
            portrait = null;
        }
    }

    private void OnDestroy() {
        Unbind();
    }
}
