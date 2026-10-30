using System;
using UnityEngine;

/// <summary>
/// 统一提供设备安全区域和平台保留区域。
/// </summary>
/// <remarks>全部返回 Unity 左下原点屏幕像素；平台实现由启动层注入，基础层不依赖 SDK。</remarks>
public static class UIScreenRegions {

    public struct Regions {
        public Rect safeArea;
        public Rect menu;
    }

    private static Func<Regions> provider;

    private static bool warned;

    /**平台提供器负责自己的 SDK 查询缓存；无平台接口时使用设备安全区。*/
    public static void Install(Func<Regions> value) {
        provider = value;
        warned = false;
    }

#if UNITY_EDITOR
    public static Regions? EditorOverride;
#endif

    public static Rect ContentArea {
        get {
            Rect screen = new Rect(0, 0, Screen.width, Screen.height);
            Regions regions = new Regions { safeArea = Screen.safeArea };
            try {
                if (provider != null) {
                    var platform = provider();
                    regions.safeArea = Intersect(regions.safeArea, platform.safeArea);
                    regions.menu = platform.menu;
                }
            } catch (Exception exception) {
                if (!warned) {
                    Debug.LogWarning("平台安全区域读取失败，暂用设备安全区：" + exception.Message);
                    warned = true;
                }
            }
#if UNITY_EDITOR
            if (EditorOverride.HasValue) {
                regions = EditorOverride.Value;
            }
#endif
            return Resolve(screen, regions.safeArea, regions.menu);
        }
    }

    /**胶囊下方整排避让；无效数据不能把整个界面压成零大小。*/
    public static Rect Resolve(Rect screen, Rect deviceSafe, Rect menu) {
        Rect safe = Intersect(screen, deviceSafe);
        if (Valid(menu) && menu.Overlaps(screen) && menu.yMin > safe.yMin) {
            safe.yMax = Mathf.Min(safe.yMax, menu.yMin);
        }
        return safe;
    }

    public static bool Valid(Rect rect) {
        return rect.width > 0 && rect.height > 0 && !float.IsNaN(rect.x + rect.y + rect.width + rect.height) && !float.IsInfinity(rect.x + rect.y + rect.width + rect.height);
    }

    public static Rect Intersect(Rect first, Rect second) {
        if (!Valid(second)) {
            return first;
        }
        Rect result = Rect.MinMaxRect(Mathf.Max(first.xMin, second.xMin), Mathf.Max(first.yMin, second.yMin), Mathf.Min(first.xMax, second.xMax), Mathf.Min(first.yMax, second.yMax));
        return Valid(result) ? result : first;
    }

    /**平台逻辑像素转换只执行一次，不额外乘设备像素比。*/
    public static Rect FromTopLeft(Vector2 window, Vector2 screen, Rect area) {
        if (window.x <= 0 || window.y <= 0 || !Valid(area)) {
            return Rect.zero;
        }
        return new Rect(area.x * screen.x / window.x, (window.y - area.yMax) * screen.y / window.y, area.width * screen.x / window.x, area.height * screen.y / window.y);
    }

    /**用父容器真实屏幕边界求交，已有留白不重复扣除。*/
    public static Rect LocalContentArea(RectTransform parent) {
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, parent.TransformPoint(parent.rect.min));
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, parent.TransformPoint(parent.rect.max));
        Rect anchors = SafeAreaFitter.GetSafeAnchors(Rect.MinMaxRect(min.x, min.y, max.x, max.y), ContentArea);
        return new Rect(parent.rect.min + Vector2.Scale(anchors.min, parent.rect.size), Vector2.Scale(anchors.size, parent.rect.size));
    }

}
