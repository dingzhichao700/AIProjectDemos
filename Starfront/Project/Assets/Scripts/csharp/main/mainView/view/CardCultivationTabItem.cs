
using UnityEngine.UI;

/// <summary>
/// 卡牌培养界面表现组件。
/// </summary>
public sealed class CardCultivationTabItem : ScrollListItem {
    /******************* UIComponent Define begin ************************/
    public Button btnTab;
    /******************* UIComponent Define finish ************************/

    protected override void OnInitListItem() {
        // 当前只有升级视图，单标签不创建占位未来功能。
        btnTab.GetComponent<GameButton>().text = "升级";
        btnTab.interactable = false;
    }

}
