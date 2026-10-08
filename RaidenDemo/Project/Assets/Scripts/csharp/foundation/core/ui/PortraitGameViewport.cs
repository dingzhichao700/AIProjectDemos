using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 Overlay UI 约束到居中的竖屏游戏区域。
/// </summary>
/// <remarks>
/// 缩放层保持固定设计坐标，固定像素层保留像素单位；外围由独立底层画布填黑。
/// </remarks>
[DisallowMultipleComponent]
public sealed class PortraitGameViewport : MonoBehaviour {

    [SerializeField] private Vector2 designSize = new Vector2(720f, 1280f);

    private RectTransform scaleCanvas;

    private RectTransform constantCanvas;

    /**供常规面板使用的固定设计尺寸容器*/
    public RectTransform scaleRoot { get; private set; }

    /**供固定像素面板使用的容器*/
    public RectTransform constantRoot { get; private set; }

    /// <summary>
    /// 在打开任何面板前建立容器，保持场景原有 Canvas 引用可复用。
    /// </summary>
    public void Initialize(RectTransform scaledCanvas, RectTransform fixedCanvas) {
        scaleCanvas = scaledCanvas;
        constantCanvas = fixedCanvas;
        scaleRoot = GetOrCreateRoot(scaleCanvas);
        constantRoot = GetOrCreateRoot(constantCanvas);
        CreateBackground();
        Canvas.ForceUpdateCanvases();
        Refresh();
    }

    /**由入口在面板更新前调用，兼容 PC 窗口缩放与 CanvasScaler 更新*/
    public void Refresh() {
        if (scaleRoot == null || constantRoot == null) {
            return;
        }
        Vector2 size = new Vector2(Mathf.Max(1f, designSize.x), Mathf.Max(1f, designSize.y));
        Rect scaledFit = FitRect(scaleCanvas.rect.size, size);
        scaleRoot.sizeDelta = size;
        scaleRoot.localScale = Vector3.one * (scaledFit.width / size.x);
        constantRoot.sizeDelta = FitRect(constantCanvas.rect.size, size).size;
        constantRoot.localScale = Vector3.one;
    }

    private void OnEnable() {
        Canvas.preWillRenderCanvases += Refresh;
    }

    private void OnDisable() {
        Canvas.preWillRenderCanvases -= Refresh;
    }

    /**在给定区域内完整容纳设计画面，返回居中的等比矩形*/
    public static Rect FitRect(Vector2 available, Vector2 design) {
        if (available.x <= 0f || available.y <= 0f || design.x <= 0f || design.y <= 0f) {
            return Rect.zero;
        }
        float scale = Mathf.Min(available.x / design.x, available.y / design.y);
        Vector2 size = design * scale;
        return new Rect((available - size) * 0.5f, size);
    }

    private static RectTransform GetOrCreateRoot(RectTransform parent) {
        RectTransform root = parent.Find("PortraitGameRoot") as RectTransform;
        if (root == null) {
            root = new GameObject("PortraitGameRoot", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            root.gameObject.layer = parent.gameObject.layer;
            root.SetParent(parent, false);
        }
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        return root;
    }

    private void CreateBackground() {
        if (transform.Find("PortraitBackground") != null) {
            return;
        }
        // 独立底层 Canvas 避免全屏黑底遮住原有固定像素层。
        Canvas background = new GameObject("PortraitBackground", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        background.transform.SetParent(transform, false);
        background.renderMode = RenderMode.ScreenSpaceOverlay;
        background.sortingOrder = Mathf.Min(scaleCanvas.GetComponent<Canvas>().sortingOrder, constantCanvas.GetComponent<Canvas>().sortingOrder) - 1;
        Image fill = new GameObject("BlackFill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(background.transform, false);
        fill.color = Color.black;
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
    }

}
