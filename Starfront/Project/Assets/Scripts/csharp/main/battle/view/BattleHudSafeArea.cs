using UnityEngine;

/// <summary>
/// 战斗 HUD 使用共享安全区，场景视窗保持全屏。
/// </summary>
/// <remarks>控件尺寸按720×1280设计；顶部与底部分别锚定安全区域。</remarks>
public sealed class BattleHudSafeArea : MonoBehaviour {

    private SafeAreaFitter fitter;

    public void Initialize() {
        if (fitter == null) {
            var children = new Transform[transform.childCount];
            for (int i = 0; i < children.Length; i++) {
                children[i] = transform.GetChild(i);
            }
            var content = new GameObject("hudSafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(transform, false);
            fitter = content.gameObject.AddComponent<SafeAreaFitter>();
            fitter.minimumContentWidth = 720;
            foreach (var child in children) {
                child.SetParent(content, false);
            }
        }
        fitter.Apply();
    }

    /**兼容已有验证入口，坐标转换以共享服务为唯一实现。*/
    public static Rect ConvertSafeArea(Vector2 window, Vector2 screen, Rect area) {
        return UIScreenRegions.FromTopLeft(window, screen, area);
    }

}
