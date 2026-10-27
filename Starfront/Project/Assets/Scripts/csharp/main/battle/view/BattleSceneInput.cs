using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>鼠标/触屏输入；双指会消费整次手势，所有手指松开后才允许再次点击。</summary>
public sealed class BattleSceneInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler {
    public event Action<Vector2> Dragged;
    public event Action<Vector2> Clicked;
    public event Action<float, Vector2, Vector2> Zoomed;
    [SerializeField] private float dragTolerance = 8f;
    [SerializeField] private float wheelSensitivity = 0.12f;
    private readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
    private Vector2 pressScreen;
    private Vector2 pressLocal;
    private Vector2 lastLocal;
    private bool dragging;
    private bool multiTouchSession;

    public void OnInitializePotentialDrag(PointerEventData data) {
        data.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData data) {
        if (data.button != PointerEventData.InputButton.Left || pointers.ContainsKey(data.pointerId)) {
            return;
        }
        pointers.Add(data.pointerId, Local(data));
        if (pointers.Count > 1) {
            multiTouchSession = true;
            dragging = true;
            return;
        }
        pressScreen = data.position;
        pressLocal = lastLocal = Local(data);
        dragging = false;
    }

    public void OnBeginDrag(PointerEventData data) {
        OnDrag(data);
    }

    public void OnDrag(PointerEventData data) {
        if (!pointers.ContainsKey(data.pointerId)) {
            return;
        }
        Vector2 local = Local(data);
        if (multiTouchSession) {
            if (pointers.Count == 2) {
                Vector2 old = pointers[data.pointerId];
                Vector2 other = default;
                foreach (var pointer in pointers) {
                    if (pointer.Key != data.pointerId) {
                        other = pointer.Value;
                    }
                }
                float previousDistance = Vector2.Distance(old, other);
                float distance = Vector2.Distance(local, other);
                pointers[data.pointerId] = local;
                if (previousDistance > 1f && distance > 1f) {
                    Zoomed?.Invoke(distance / previousDistance, (old + other) / 2, (local + other) / 2);
                }
            } else {
                pointers[data.pointerId] = local;
            }
            return;
        }
        pointers[data.pointerId] = local;
        float scale = GetComponentInParent<Canvas>().scaleFactor;
        if (!dragging && (data.position - pressScreen).sqrMagnitude > Mathf.Pow(dragTolerance * scale, 2)) {
            dragging = true;
            lastLocal = pressLocal;
        }
        if (dragging) {
            Dragged?.Invoke(local - lastLocal);
            lastLocal = local;
        }
    }

    public void OnPointerUp(PointerEventData data) {
        if (!pointers.ContainsKey(data.pointerId)) {
            return;
        }
        OnDrag(data);
        bool click = !multiTouchSession && !dragging && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, data.position, data.pressEventCamera);
        pointers.Remove(data.pointerId);
        if (pointers.Count == 0) {
            ResetGesture();
        }
        if (click) {
            Clicked?.Invoke(Local(data));
        }
    }

    public void OnEndDrag(PointerEventData data) {
        // 正常释放由OnPointerUp处理；多指期间不得重置另一根仍按住的手指。
    }

    public void OnScroll(PointerEventData data) {
        if (multiTouchSession || Mathf.Approximately(data.scrollDelta.y, 0)) {
            return;
        }
        if (pointers.Count > 0) {
            dragging = true;
        }
        Vector2 focus = Local(data);
        float exponent = Mathf.Clamp(data.scrollDelta.y * wheelSensitivity, -10f, 10f);
        Zoomed?.Invoke(Mathf.Exp(exponent), focus, focus);
    }

    private Vector2 Local(PointerEventData data) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, data.position, data.pressEventCamera, out Vector2 point);
        return point;
    }

    public void ResetGesture() {
        pointers.Clear();
        dragging = false;
        multiTouchSession = false;
    }

    private void OnDisable() {
        ResetGesture();
    }

    private void OnApplicationFocus(bool focused) {
        if (!focused) {
            ResetGesture();
        }
    }
}
