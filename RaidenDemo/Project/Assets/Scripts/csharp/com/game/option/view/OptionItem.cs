using TMPro;

public class OptionItem : ScrollListItem {

    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txt;
    /******************* UIComponent Define finish ************************/

    private OptionEnum option;

    /**当前是否被选中*/
    private bool isSelected => option == OptionControl.ins.curSelectOption;

    override protected void OnInitListItem() {
        OptionControl.ins.On(OptionEvent.FOCUS_OPTION, UpdateFocus);
    }

    override protected void OnSetData(int index, object value) {
        option = (OptionEnum)value;
        string optionName = "";
        switch (option) {
            case OptionEnum.CONTINUE:
                optionName = "继续";
                break;
            case OptionEnum.LOADSAVE:
                optionName = "加载存档";
                break;
            case OptionEnum.NEWGAME:
                optionName = "新游戏";
                break;
            case OptionEnum.SETTING:
                optionName = "选项";
                break;
            case OptionEnum.EXIT:
                optionName = "退出";
                break;
        }
        txt.text = optionName;
        UpdateFocus();
    }

    private void UpdateFocus() {
        txt.color = isSelected ? ColorConst.RED : ColorConst.WHITE;
    }

    public override void Destroy() {
        base.Destroy();
        OptionControl.ins.Off(OptionEvent.FOCUS_OPTION, UpdateFocus);
    }

}
