/// <summary>
/// 公共工具类
/// </summary>
public class CommonUtil {

    private static CommonUtil _ins;

    private static CommonUtil ins {
        get {
            return _ins;
        }
    }

    /// <summary>
    /// 打开警告窗口
    /// </summary>
    /// <param name="content">显示文本</param>
    /// <param name="sureHandler">确定回调</param>
    public static void OpenWarning(string content, Handler sureHandler) {
        object[] array = { content, sureHandler };
        PanelMgr.ins.OpenPanel(UIEnum.WARNING_PANEL, array);
    }

    /// <summary>
    /// 打开警告窗口完成
    /// </summary>
    /// <param name="content">显示文本</param>
    /// <param name="sureHandler">确定回调</param>
    private static void OpenWarningComplete(string content, Handler sureHandler) {
    }

}
