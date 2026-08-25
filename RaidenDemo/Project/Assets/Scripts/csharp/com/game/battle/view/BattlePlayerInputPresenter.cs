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
    private readonly Func<AircraftCollisionVO> getCollision;
    private readonly Action<Vector2> setPosition;

    public BattlePlayerInputPresenter(RectTransform inputLayer,
        Func<AircraftCollisionVO> getCollision, Action<Vector2> setPosition) {
        this.inputLayer = inputLayer;
        this.getCollision = getCollision;
        this.setPosition = setPosition;
    }

    /**读取当前拖动位置，并约束在玩家可行动区域内。*/
    public void Update(bool playerAlive) {
        if (!playerAlive) {
            return;
        }
        bool pointerOverUi = EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
        if (!Input.GetMouseButton(0) || pointerOverUi) {
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
        AircraftCollisionVO collision = getCollision();
        if (collision == null) {
            return;
        }
        float halfWidth = collision.boundsSize.x * 0.5f;
        float halfHeight = collision.boundsSize.y * 0.5f;
        Vector2 offset = collision.boundsCenterOffset;
        localPoint.x = Mathf.Clamp(localPoint.x, halfWidth - offset.x,
            720f - halfWidth - offset.x);
        localPoint.y = Mathf.Clamp(localPoint.y, -1280f + halfHeight - offset.y,
            -180f - halfHeight - offset.y);
        setPosition(new Vector2(Mathf.Round(localPoint.x), Mathf.Round(localPoint.y)));
    }
}
