using UnityEngine;

/// <summary>
/// 小游戏平台安全区域适配入口。
/// </summary>
/// <remarks>微信实现位于 Login，后续平台可替换提供器，UI 不直接调用 SDK。</remarks>
public static class GameUIScreenRegions {

    private static Vector2 lastSize;

    private static float nextRead;

    private static UIScreenRegions.Regions cached;

    public static void Install() {
        lastSize = Vector2.zero;
        nextRead = 0;
        UIScreenRegions.Install(Read);
    }

    private static UIScreenRegions.Regions Read() {
#if (UNITY_WEBGL && GAMEFRAMEWORK_WECHAT) || UNITY_EDITOR
        if (!Application.isEditor) {
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (screen != lastSize || Time.realtimeSinceStartup >= nextRead) {
                nextRead = Time.realtimeSinceStartup + 1f;
                lastSize = screen;
                var window = WeChatWASM.WX.GetWindowInfo();
                var menu = WeChatWASM.WX.GetMenuButtonBoundingClientRect();
                Vector2 size = new Vector2((float)window.windowWidth, (float)window.windowHeight);
                var safe = window.safeArea;
                cached = new UIScreenRegions.Regions {
                    safeArea = UIScreenRegions.FromTopLeft(size, screen, new Rect((float)safe.left, (float)safe.top, (float)(safe.right - safe.left), (float)(safe.bottom - safe.top))),
                    menu = UIScreenRegions.FromTopLeft(size, screen, new Rect((float)menu.left, (float)menu.top, (float)menu.width, (float)menu.height))
                };
            }
            return cached;
        }
#endif
        return default;
    }

}
