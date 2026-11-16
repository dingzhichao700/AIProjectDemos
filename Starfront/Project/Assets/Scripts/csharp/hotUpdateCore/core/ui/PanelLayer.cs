/// <summary>
/// 单缩放舞台的绘制顺序。
/// </summary>
/// <remarks>交互焦点独立选择；被动宿主不抢返回键。</remarks>
public static class PanelLayer {

    public const int SCALE_BOTTOM = 10000;
    public const int SCALE_PANEL_FIRST = 10100;
    public const int SCALE_PANEL_SECOND = 10200;
    public const int SCALE_PANEL_THIRD = 10300;
    public const int SCALE_TIP = 10500;
    public const int SCALE_TOOL = 19020;
    public const int SCALE_CONFIRM = 19040;
    public const int SCALE_TOAST = 19050;
    public const int SCALE_GM_ENTRY = 19060;
    public const int SCALE_LOADING = 19100;
    public const int SCALE_TOP_MONITOR = 20000;

}
