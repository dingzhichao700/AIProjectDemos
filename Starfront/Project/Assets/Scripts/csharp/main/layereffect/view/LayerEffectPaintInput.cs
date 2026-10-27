using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 将预览区域的鼠标和触摸统一转换为源图坐标。
/// </summary>
/// <remarks>坐标通过当前RectTransform换算，兼容画布缩放与窗口尺寸变化。</remarks>
public sealed class LayerEffectPaintInput : MonoBehaviour, IPointerDownHandler, IDragHandler {

    public event Action<Vector2> Painted;

    public event Action StrokeStarted;

    public void OnPointerDown(PointerEventData eventData) {
        StrokeStarted?.Invoke();
        Apply(eventData);
    }

    public void OnDrag(PointerEventData eventData) {
        Apply(eventData);
    }

    private void Apply(PointerEventData eventData) {
        if (eventData.button != PointerEventData.InputButton.Left) {
            return;
        }
        var rect = (RectTransform)transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 point) && rect.rect.Contains(point)) {
            Painted?.Invoke(new Vector2((point.x - rect.rect.xMin) / rect.rect.width * LayerEffectTestSettings.PreviewWidth, (rect.rect.yMax - point.y) / rect.rect.height * LayerEffectTestSettings.PreviewHeight));
        } else {
            StrokeStarted?.Invoke();
        }
    }

}
