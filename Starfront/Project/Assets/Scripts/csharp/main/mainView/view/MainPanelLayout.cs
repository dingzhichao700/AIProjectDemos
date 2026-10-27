using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MainPanel 的全屏布局：顶部内容、底部导航和等比覆盖背景。
/// </summary>
/// <remarks>只改变显示布局，不控制卡组数据、功能解锁或导航选择。</remarks>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MainPanelLayout : MonoBehaviour {

    public RectTransform background;

    public RectTransform contentViewport;

    public RectTransform navigation;

    public RectTransform navigationContent;

    public RectTransform deckContent;

    public RectTransform library;

    public RectTransform battleButton;

    public RectTransform[] views;

    /**尺寸变化时由 MainPanel 收束尚未完成的切页动画。*/
    public event Action LayoutChanged;

    private PortraitGameViewport viewport;

    private bool previousViewportMode;

    private bool ownsViewportMode;

    private bool applying;

    private Vector2 lastSize = new Vector2(-1, -1);

    private void OnEnable() {
        ApplyLayout();
    }

    private void OnRectTransformDimensionsChange() {
        ApplyLayout();
    }

    /**只在主界面打开期间改变舞台策略，不影响其他窗口的设计基准。*/
    public void ActivateViewport() {
        // PanelMgr 完成挂载后再设全屏，消除 SetParent 保留世界尺寸产生的偏移。
        RectTransform rect = (RectTransform)transform;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        Stretch(rect, Vector2.zero, Vector2.zero);
        rect.anchoredPosition3D = Vector3.zero;
        lastSize = new Vector2(-1, -1);
        if (!ownsViewportMode) {
            viewport = FindObjectOfType<PortraitGameViewport>();
            if (viewport != null) {
                previousViewportMode = viewport.flexiblePortrait;
                ownsViewportMode = true;
                viewport.SetFlexiblePortrait(true);
            }
        }
        ApplyLayout();
    }

    public void RestoreViewport() {
        if (ownsViewportMode && viewport != null) {
            viewport.SetFlexiblePortrait(previousViewportMode);
        }
        ownsViewportMode = false;
        viewport = null;
    }

    private void OnDestroy() {
        RestoreViewport();
    }

    /**从固定设计尺寸计算，避免反复缩放累积误差。*/
    public void ApplyLayout() {
        RectTransform root = transform as RectTransform;
        if (applying || root == null || background == null || contentViewport == null || navigation == null || navigationContent == null || deckContent == null || library == null || battleButton == null || views == null) {
            return;
        }
        Vector2 size = root.rect.size;
        if (size.x <= 0 || size.y <= 0 || (size - lastSize).sqrMagnitude < 0.0001f) {
            return;
        }
        applying = true;
        try {
            float scale = size.x / 720f;
            float navHeight = 150f * scale;
            Stretch(contentViewport, new Vector2(0, navHeight), Vector2.zero);
            navigation.anchorMin = Vector2.zero;
            navigation.anchorMax = new Vector2(1, 0);
            navigation.pivot = new Vector2(0.5f, 0);
            navigation.sizeDelta = new Vector2(0, navHeight);
            navigation.anchoredPosition = Vector2.zero;
            navigationContent.anchorMin = navigationContent.anchorMax = new Vector2(0.5f, 0);
            navigationContent.pivot = new Vector2(0.5f, 0);
            navigationContent.sizeDelta = new Vector2(720, 150);
            navigationContent.anchoredPosition = Vector2.zero;
            navigationContent.localScale = Vector3.one * scale;

            foreach (RectTransform view in views) {
                Stretch(view, Vector2.zero, Vector2.zero);
            }
            float contentHeight = Mathf.Max(0, size.y - navHeight) / scale;
            deckContent.anchorMin = deckContent.anchorMax = new Vector2(0.5f, 1);
            deckContent.pivot = new Vector2(0.5f, 1);
            deckContent.sizeDelta = new Vector2(720, contentHeight);
            deckContent.anchoredPosition = Vector2.zero;
            deckContent.localScale = Vector3.one * scale;
            library.sizeDelta = new Vector2(668, Mathf.Max(1, Mathf.Round(contentHeight - 693)));

            battleButton.anchorMin = battleButton.anchorMax = new Vector2(0.5f, 459f / 1130f);
            battleButton.pivot = new Vector2(0.5f, 0.5f);
            battleButton.anchoredPosition = Vector2.zero;
            battleButton.sizeDelta = new Vector2(480, 158);
            battleButton.localScale = Vector3.one * scale;

            Image image = background.GetComponent<Image>();
            Vector2 source = image != null && image.sprite != null ? image.sprite.rect.size : new Vector2(720, 1130);
            float cover = Mathf.Max(size.x / source.x, size.y / source.y);
            background.anchorMin = background.anchorMax = new Vector2(0.5f, 1);
            background.pivot = new Vector2(0.5f, 1);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(Mathf.Ceil(source.x * cover), Mathf.Ceil(source.y * cover));
            lastSize = size;
        } finally {
            applying = false;
        }
        LayoutChanged?.Invoke();
    }

    private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minimum;
        rect.offsetMax = maximum;
    }

}
