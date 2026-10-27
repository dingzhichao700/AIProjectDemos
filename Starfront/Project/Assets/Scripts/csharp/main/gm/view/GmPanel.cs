using System;

/// <summary>
/// 展示配置表中的 GM 指令及参数输入项。
/// </summary>
public class GmPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public GameButton btnClose;
    public ScrollList listItem;
    /******************* UIComponent Define finish ************************/

    /**GM 面板覆盖正式内容界面*/
    public GmPanel() {
        layer = PanelLayer.SCALE_TOP_SECOND;
    }

    /// <summary>
    /// 配置就绪后展示命令列表并注册关闭操作。
    /// </summary>
    public override void OnOpen() {
        if (CfgManager.tables == null) {
            throw new InvalidOperationException("GM 面板需要先初始化配置表。");
        }
        AddLis();
        listItem.array = CfgManager.tables.GmObj.DataList;
    }

    /**关闭时成对注销交互监听*/
    public override void OnClose() {
        RemoveLis();
    }

    /**注册关闭按钮*/
    private void AddLis() {
        OnClick(btnClose.gameObject, Close);
    }

    /**注销关闭按钮*/
    private void RemoveLis() {
        OffClick(btnClose.gameObject, Close);
    }

    /**键盘返回与关闭按钮共用关闭流程*/
    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            Close();
        }
    }

}
