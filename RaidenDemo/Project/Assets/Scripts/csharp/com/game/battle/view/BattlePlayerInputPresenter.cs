using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 玩家战斗输入
/// </summary>
/// <remarks>
/// 将玩家拖动输入转换为战斗逻辑坐标并提交给战斗 Model。
/// </remarks>
internal sealed class BattlePlayerInputPresenter {

    private readonly RectTransform inputLayer;
    private readonly Func<Vector2> getPosition;
    private readonly Action<Vector2> setPosition;
    private bool dragging;
    private Vector2 lastPointerPosition;

    public BattlePlayerInputPresenter(RectTransform inputLayer, Func<Vector2> getPosition, Action<Vector2> setPosition) {
        this.inputLayer = inputLayer;
        this.getPosition = getPosition;
        this.setPosition = setPosition;
    }

    /**将鼠标按住后的相对移动量同步给玩家飞机。*/
    public void Update(bool playerAlive) {
        if (!playerAlive) {
            dragging = false;
            return;
        }
        if (Input.GetMouseButtonDown(0)) {
            dragging = (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) && TryGetPointerPosition(out lastPointerPosition);
        }
        if (Input.GetMouseButtonUp(0)) {
            dragging = false;
        }
        if (!dragging || !Input.GetMouseButton(0)) {
            return;
        }
        if (!TryGetPointerPosition(out Vector2 pointerPosition)) {
            return;
        }
        Vector2 pointerDelta = pointerPosition - lastPointerPosition;
        lastPointerPosition = pointerPosition;
        Vector2 nextPosition = getPosition() + pointerDelta;
        Rect viewport = inputLayer.rect;
        nextPosition.x = Mathf.Clamp(nextPosition.x, viewport.xMin, viewport.xMax);
        nextPosition.y = Mathf.Clamp(nextPosition.y, viewport.yMin, viewport.yMax);
        setPosition(new Vector2(Mathf.Round(nextPosition.x), Mathf.Round(nextPosition.y)));
    }

    /**将屏幕鼠标坐标转换为战斗视窗本地坐标。*/
    private bool TryGetPointerPosition(out Vector2 pointerPosition) {
        Canvas canvas = inputLayer.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(inputLayer, Input.mousePosition, camera, out pointerPosition);
    }
}
