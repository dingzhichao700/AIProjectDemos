using UnityEngine;

/// <summary>
/// 将节点约束到设备安全区域，供 HUD 和关键交互控件使用。
/// </summary>
/// <remarks>
/// 按父容器的实际屏幕位置计算交集，兼容居中竖屏区域与全屏画布。
/// </remarks>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SafeAreaFitter : MonoBehaviour {

    private Rect lastSafeArea;

    private Rect lastParentRect;

    private readonly Vector3[] corners = new Vector3[4];

    public static RectTransform GetOrCreate(RectTransform parent) {
        if (parent == null) {
            return null;
        }
        RectTransform child = parent.Find("SafeAreaRoot") as RectTransform;
        if (child == null) {
            child = new GameObject("SafeAreaRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            child.SetParent(parent, false);
        }
        SafeAreaFitter fitter = child.GetComponent<SafeAreaFitter>();
        if (fitter == null) {
            fitter = child.gameObject.AddComponent<SafeAreaFitter>();
        }
        fitter.Apply();
        return child;
    }

    private void OnEnable() {
        Apply();
    }

    private void LateUpdate() {
        Apply();
    }

    /**将安全区与父容器求交，再换算成相对于父容器的锚点*/
    public void Apply() {
        RectTransform rect = transform as RectTransform;
        RectTransform parent = transform.parent as RectTransform;
        if (rect == null || parent == null || Screen.width <= 0 || Screen.height <= 0) {
            return;
        }
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        parent.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        Rect parentRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        Rect safeArea = Screen.safeArea;
        if (parentRect.width <= 0f || parentRect.height <= 0f || (safeArea == lastSafeArea && parentRect == lastParentRect)) {
            return;
        }
        Rect anchors = GetSafeAnchors(parentRect, safeArea);
        rect.anchorMin = anchors.min;
        rect.anchorMax = anchors.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        lastSafeArea = safeArea;
        lastParentRect = parentRect;
    }

    /**返回父容器内安全区域的归一化坐标；黑边已吸收的安全区不再重复留白*/
    public static Rect GetSafeAnchors(Rect parentPixels, Rect safePixels) {
        if (parentPixels.width <= 0f || parentPixels.height <= 0f) {
            return Rect.zero;
        }
        float xMin = Mathf.Clamp01((safePixels.xMin - parentPixels.xMin) / parentPixels.width);
        float yMin = Mathf.Clamp01((safePixels.yMin - parentPixels.yMin) / parentPixels.height);
        float xMax = Mathf.Clamp01((safePixels.xMax - parentPixels.xMin) / parentPixels.width);
        float yMax = Mathf.Clamp01((safePixels.yMax - parentPixels.yMin) / parentPixels.height);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

}
