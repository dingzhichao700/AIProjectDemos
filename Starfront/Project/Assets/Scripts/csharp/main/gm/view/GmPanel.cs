using System;

/// <summary>
/// 展示配置表中的 GM 指令及参数输入项。
/// </summary>
public class GmPanel : BasePanel {

    /**局部弹窗尺寸独立于项目屏幕方向。*/
    public override UnityEngine.Vector2 SafeContentDesignSize => trans.rect.size;

    /******************* UIComponent Define begin ************************/
    public GameButton btnClose;
    public ScrollList listItem;
    /******************* UIComponent Define finish ************************/

    /**复用面板现有字体；诊断文本为运行时内容，不增加序列化绑定成员。*/
    private TMPro.TextMeshProUGUI diagnostics;

    /**只创建一次，列表让出顶部空间；反复打开不累积位移或节点。*/
    private void RefreshDiagnostics() {
        if (diagnostics == null) {
            var source = listItem.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (source == null) {
                throw new InvalidOperationException("GM 面板缺少公共字体来源。");
            }
            var node = new UnityEngine.GameObject("txtDiagnostics", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
            node.transform.SetParent(listItem.transform.parent, false);
            diagnostics = node.GetComponent<TMPro.TextMeshProUGUI>();
            diagnostics.font = source.font;
            diagnostics.fontSharedMaterial = source.fontSharedMaterial;
            diagnostics.fontSize = 20;
            diagnostics.color = UnityEngine.Color.white;
            diagnostics.alignment = TMPro.TextAlignmentOptions.TopLeft;
            diagnostics.enableWordWrapping = false;
            diagnostics.richText = false;
            diagnostics.raycastTarget = false;
            var rect = diagnostics.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new UnityEngine.Vector2(0, 1);
            rect.anchoredPosition = new UnityEngine.Vector2(20, -16);
            rect.sizeDelta = new UnityEngine.Vector2(580, 100);
            var listRect = (UnityEngine.RectTransform)listItem.transform;
            listRect.anchoredPosition += new UnityEngine.Vector2(0, -64);
            listRect.sizeDelta += new UnityEngine.Vector2(0, -64);
        }
        diagnostics.text = GMControl.ins.GetDiagnosticsText();
        UnityEngine.Debug.Log("[RuntimeDiagnostics] " + diagnostics.text.Replace("\n", " | "));
    }

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
        RefreshDiagnostics();
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
