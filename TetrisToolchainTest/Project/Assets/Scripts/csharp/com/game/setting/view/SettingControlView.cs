/// <summary>
/// 设置-控制
/// </summary>
public class SettingControlView : SettingBaseView {
    /******************* UIComponent Define begin ************************/
    public ScrollList listOption;
    public GameButton btnReset;
    /******************* UIComponent Define finish ************************/

    /**初始化完成接口*/
    override public void OnInit() {
        listOptionList = listOption;
        SettingControl.ins.On(SettingEvent.FOCUS_OPTION_UPDATE, OnFocusUpdate);
    }

    override protected void OnSetData() {
        listOption.array = selections;
    }

    override protected void OnArrowDown() {
        if (SettingControl.ins.selectOptionIndex < selections.Count) {
            SettingControl.ins.SetFocusOptionIndex(SettingControl.ins.selectOptionIndex + 1);
        }
    }

    private void OnFocusUpdate() {
        if (_isCurrentView) {
            btnReset.LabelColor = SettingControl.ins.selectOptionIndex == selections.Count ? ColorConst.RED : ColorConst.BLACK;
        }
    }

}
