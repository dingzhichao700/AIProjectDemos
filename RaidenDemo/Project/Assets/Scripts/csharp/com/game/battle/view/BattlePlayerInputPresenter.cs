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
    private readonly Action<Vector2> setPosition;
    private bool dragging;

    public BattlePlayerInputPresenter(RectTransform inputLayer, Action<Vector2> setPosition) {
        this.inputLayer = inputLayer;
        this.setPosition = setPosition;
    }

    /**读取当前拖动位置，并约束在玩家可行动区域内。*/
    public void Update(bool playerAlive) {
        if (!playerAlive) {
            dragging = false;
            return;
        }
        if (Input.GetMouseButtonDown(0)) {
            dragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
        }
        if (Input.GetMouseButtonUp(0)) {
            dragging = false;
        }
        if (!dragging || !Input.GetMouseButton(0)) {
            return;
        }
        Canvas canvas = inputLayer.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            inputLayer, Input.mousePosition, camera, out Vector2 localPoint)) {
            return;
        }
        Rect viewport = inputLayer.rect;
        localPoint.x = Mathf.Clamp(localPoint.x, viewport.xMin, viewport.xMax);
        localPoint.y = Mathf.Clamp(localPoint.y, viewport.yMin, viewport.yMax);
        setPosition(new Vector2(Mathf.Round(localPoint.x), Mathf.Round(localPoint.y)));
    }
}
