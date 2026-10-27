using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>独立战斗信息窗口，绑定本局余额事件，随战斗关闭清理。</summary>
public sealed class BattleInfoPanel : BasePanel {
    /******************* UIComponent Define begin ************************/
    public Image imgFriendlyFill;
    public Image imgEnemyFill;
    public Image imgDivider;
    public TextMeshProUGUI txtFriendlyTerritory;
    public TextMeshProUGUI txtEnemyTerritory;
    public TextMeshProUGUI txtFriendlyPopulation;
    public TextMeshProUGUI txtFriendlyName;
    public TextMeshProUGUI txtEnemyPopulation;
    public TextMeshProUGUI txtEnemyName;
    public TextMeshProUGUI txtCountdown;
    public TextMeshProUGUI txtCurrency;
    /******************* UIComponent Define finish ************************/

    private BattleSceneModel model;
    private BattleInfoView view;

    public BattleInfoPanel() {
        layer = PanelLayer.SCALE_PANEL_SECOND;
    }

    public override void OnOpen() {
        view = GetComponent<BattleInfoView>();
        model = openParams != null && openParams.Length > 0 ? openParams[0] as BattleSceneModel : null;
        // 关闭战斗后才返回的加载回调，不能绑定新一局或显示旧余额。
        if (model == null || model != BattleControl.ins.model) {
            Close();
            return;
        }
        trans.anchorMin = Vector2.zero;
        trans.anchorMax = Vector2.one;
        trans.pivot = new Vector2(0.5f, 0.5f);
        trans.sizeDelta = Vector2.zero;
        trans.anchoredPosition = Vector2.zero;
        view.Bind(this);
        AddLis();
        view.Refresh(model);
    }

    private void AddLis() {
        model.CurrencyChanged += OnCurrencyChanged;
        model.InformationChanged += OnInformationChanged;
    }

    private void RemoveLis() {
        if (model != null) {
            model.CurrencyChanged -= OnCurrencyChanged;
            model.InformationChanged -= OnInformationChanged;
        }
    }

    private void OnInformationChanged() {
        view.Refresh(model);
    }

    private void OnCurrencyChanged(BattleCampVO owner) {
        view.Refresh(model);
    }

    public override void OnClose() {
        RemoveLis();
        model = null;
        view.Clear();
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            PanelMgr.ins.GetPanel(UIEnum.BATTLE_SCENE_PANEL)?.OnPanelOperate(operateCode);
        }
    }
}
