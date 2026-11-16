using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 仅按钮区域接收输入的可拖动GM常驻宿主。
/// </summary>
/// <remarks>位置只保存在本次运行；共享舞台、安全区及窗口排序，不参与返回键。</remarks>
public sealed class GMEntryView : BasePanel, IPointerDownHandler, IDragHandler, IPointerUpHandler {

    public const string PanelId = "runtime/gm/entry";

    private static Vector2? sessionPosition;

    private RectTransform button;

    private CanvasGroup visibility;

    private Vector2 pointerStart;

    private Vector2 buttonStart;

    private int pointerId = int.MinValue;

    private bool dragged;

    private bool available;

    public override bool FitContentToSafeArea => false;

    public override bool ReceivesPanelInput => false;

    public override bool KeepOpenOnCloseAll => true;

    public GMEntryView() {
        layer = PanelLayer.SCALE_GM_ENTRY;
    }

    /**已批准的扁平GM外观；保留原宿主、文字及输入结构。*/
    public static GMEntryView Create() {
        var root = new GameObject("GMEntryView", typeof(RectTransform));
        var view = root.AddComponent<GMEntryView>();
        var image = new GameObject("btnGM", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
        image.transform.SetParent(root.transform, false);
        image.sprite = GMEntryAppearance.Backplate;
        image.color = Color.white;
        image.raycastTarget = true;
        view.button = image.rectTransform;
        view.button.sizeDelta = new Vector2(64, 48);
        view.visibility = image.GetComponent<CanvasGroup>();
        var label = new GameObject("txtLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(image.transform, false);
        label.font = TMP_Settings.defaultFontAsset;
        label.text = "GM";
        label.fontSize = 22;
        label.alignment = TextAlignmentOptions.Center;
        label.color = GMEntryAppearance.Accent;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = new Vector2(56, 28);
        return view;
    }

    public override void OnOpen() {
        trans.anchorMin = Vector2.zero;
        trans.anchorMax = Vector2.one;
        trans.offsetMin = trans.offsetMax = Vector2.zero;
        button.anchoredPosition = sessionPosition ?? new Vector2(UIScreenRegions.LocalContentArea(trans).xMin + 44, UIScreenRegions.LocalContentArea(trans).center.y);
        ClampPosition();
    }

    public override void OnClose() {
        pointerId = int.MinValue;
        available = false;
    }

    public void SetAvailable(bool visible, bool interactable) {
        available = visible && interactable;
        visibility.alpha = visible ? 1 : 0;
        visibility.blocksRaycasts = visible;
        visibility.interactable = available;
        if (!available) {
            pointerId = int.MinValue;
        }
    }

    private void LateUpdate() {
        if (isOpened) {
            ClampPosition();
        }
    }

    /**坐标和可用区都在同一舞台，胶囊已由公共服务整排避让。*/
    private void ClampPosition() {
        Rect area = UIScreenRegions.LocalContentArea(trans);
        Vector2 half = button.rect.size * .5f;
        Vector2 pos = button.anchoredPosition;
        pos.x = area.width >= half.x * 2 ? Mathf.Clamp(pos.x, area.xMin + half.x, area.xMax - half.x) : area.center.x;
        pos.y = area.height >= half.y * 2 ? Mathf.Clamp(pos.y, area.yMin + half.y, area.yMax - half.y) : area.center.y;
        button.anchoredPosition = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
        sessionPosition = button.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData data) {
        if (!available || pointerId != int.MinValue) {
            return;
        }
        pointerId = data.pointerId;
        pointerStart = data.position;
        buttonStart = button.anchoredPosition;
        dragged = false;
    }

    public void OnDrag(PointerEventData data) {
        if (!available || data.pointerId != pointerId) {
            return;
        }
        float threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10;
        dragged |= (data.position - pointerStart).sqrMagnitude >= threshold * threshold;
        if (!dragged) {
            return;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(trans, pointerStart, data.pressEventCamera, out Vector2 start);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(trans, data.position, data.pressEventCamera, out Vector2 current);
        button.anchoredPosition = buttonStart + current - start;
        ClampPosition();
    }

    public void OnPointerUp(PointerEventData data) {
        if (!available || data.pointerId != pointerId) {
            return;
        }
        pointerId = int.MinValue;
        if (!dragged && RectTransformUtility.RectangleContainsScreenPoint(button, data.position, data.pressEventCamera)) {
            GMControl.ins.OpenPanel();
        }
    }

}


