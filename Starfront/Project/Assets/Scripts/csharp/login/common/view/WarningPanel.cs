
using TMPro;

/// <summary>
/// 警告窗口
/// </summary>
public class WarningPanel : BasePanel {

    public override bool UsesWindowTransition => true;
    protected override UnityEngine.UI.Image WindowTransitionMask => transform.Find("imgMask").GetComponent<UnityEngine.UI.Image>();

    /**局部弹窗尺寸独立于项目屏幕方向。*/
    public override UnityEngine.Vector2 SafeContentDesignSize => new UnityEngine.Vector2(860, 340);

    public override string[] FullBleedChildren => new[] { "imgMask" };
    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtContent;
    public GameButton btnCancel;
    public GameButton btnSure;
    /******************* UIComponent Define finish ************************/

    private Handler sureHandler;

    public WarningPanel() {
        layer = PanelLayer.SCALE_CONFIRM;
    }

    public override void OnOpen() {
        AddLis();
    }

    protected override void OnPrepareOpen() {
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
