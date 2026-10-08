
using TMPro;

/// <summary>
/// 警告窗口
/// </summary>
public class WarningPanel : BasePanel {
    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtContent;
    public GameButton btnCancel;
    public GameButton btnSure;
    /******************* UIComponent Define finish ************************/

    private Handler sureHandler;

    public WarningPanel() {
        layer = PanelLayer.SCALE_TOP_FOUTH;
    }

    override public void OnOpen() {
        AddLis();
        txtContent.text = openParams[0] as string;
        sureHandler = openParams[1] as Handler;
    }

    private void AddLis() {
        OnClick(btnCancel.gameObject, OnCancel);
        OnClick(btnSure.gameObject, OnSure);
    }

    private void RemoveLis() {
        OffClick(btnCancel.gameObject, OnCancel);
        OffClick(btnSure.gameObject, OnSure);
    }

    private void OnCancel() {
        Close();
    }

    private void OnSure() {
        if (sureHandler != null) {
            sureHandler.Run();
            sureHandler = null;
        }
        Close();
    }

    /**窗口操作*/
    override public void OnPanelOperate(PanelOperateEnum operateCode) {
        switch (operateCode) {
            case PanelOperateEnum.ESC:
                Close();
                break;
        }
    }

    override public void OnClose() {
        RemoveLis();
    }

}
