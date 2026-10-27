using UnityEngine;

/// <summary>纯表现镜头状态；视窗坐标Y向上，场景坐标Y向下，不修改玩法对象。</summary>
public sealed class BattleSceneCamera {
    public const float MinimumZoom = 1f;
    public const float MaximumZoom = 1.5f;
    public Vector2 center { get; private set; }
    public float zoom { get; private set; } = MinimumZoom;
    public Vector2 viewportSize { get; private set; }
    private readonly Vector2 sceneSize;

    public BattleSceneCamera(Vector2 sceneSize, Vector2 initialCenter) {
        this.sceneSize = sceneSize;
        center = initialCenter;
    }

    public Rect visibleRect => new Rect(center - viewportSize / (2 * zoom), viewportSize / zoom);

    public void Resize(Vector2 size) {
        viewportSize = size;
        Clamp();
    }

    public void Pan(Vector2 viewportDelta) {
        center += new Vector2(-viewportDelta.x, viewportDelta.y) / zoom;
        Clamp();
    }

    public Vector2 ViewportToScene(Vector2 viewportPoint) {
        return center + new Vector2(viewportPoint.x, -viewportPoint.y) / zoom;
    }

    public Vector2 SceneToViewport(Vector2 scenePoint) {
        Vector2 delta = (scenePoint - center) * zoom;
        return new Vector2(delta.x, -delta.y);
    }

    /**保留手势焦点下的场景位置；双指中心平移同时驱动镜头平移。*/
    public void Zoom(float factor, Vector2 previousFocus, Vector2 currentFocus) {
        if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0) {
            return;
        }
        Vector2 worldFocus = ViewportToScene(previousFocus);
        zoom = Mathf.Clamp(zoom * factor, MinimumZoom, MaximumZoom);
        center = worldFocus - new Vector2(currentFocus.x, -currentFocus.y) / zoom;
        Clamp();
    }

    private void Clamp() {
        Vector2 half = viewportSize / (2 * zoom);
        center = new Vector2(ClampAxis(center.x, sceneSize.x, half.x), ClampAxis(center.y, sceneSize.y, half.y));
    }

    private static float ClampAxis(float value, float extent, float halfView) {
        return extent <= halfView * 2 ? extent / 2 : Mathf.Clamp(value, halfView, extent - halfView);
    }
}
