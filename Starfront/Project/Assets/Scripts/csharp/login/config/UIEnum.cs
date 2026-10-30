/// <summary>
/// UI 预制路径（相对 Assets/Prefab/）。
/// </summary>
public static class UIEnum
{
    // opening
    public const string OPENING_PANEL = "opening/OpeningPanel";

    // title
    public const string LOGIN_PANEL = "title/title/LoginPanel";
    public const string OPTION_PANEL = "title/option/OptionPanel";
    public const string SAVE_PANEL = "title/save/SavePanel";
    public const string SAVE_DELETE_PANEL = "title/save/SaveDeletePanel";
    public const string SETTING_PANEL = "title/setting/SettingPanel";
    public const string LOADING_PANEL = "title/loading/LoadingPanel";
    public const string WARNING_PANEL = "title/warning/WarningPanel";

    // 登录阶段需要知道的正式内容入口；其他正式窗口由 MainUIEnum 定义。
    public const string MAIN_PANEL = "default/mainView/MainPanel";
}
