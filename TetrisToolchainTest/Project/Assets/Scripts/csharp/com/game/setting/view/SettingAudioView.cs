using System.Linq;

/// <summary>
/// 设置-音效
/// </summary>
public class SettingAudioView : SettingBaseView {
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
