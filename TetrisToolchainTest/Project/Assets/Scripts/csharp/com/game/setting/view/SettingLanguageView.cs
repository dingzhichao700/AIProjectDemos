/// <summary>
/// 设置-语言
/// </summary>
public class SettingLanguageView : SettingBaseView {
    /******************* UIComponent Define begin ************************/
    public ScrollList listOption;
    /******************* UIComponent Define finish ************************/

    /**初始化完成接口*/
    override public void OnInit() {
        listOptionList = listOption;
    }

    override protected void OnSetData() {
        listOption.array = selections;
    }

}
