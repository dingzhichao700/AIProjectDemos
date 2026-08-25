using TMPro;
using UnityEngine.UI;

/// <summary>
/// 雷电战机主界面
/// </summary>
public class HomePanel : BasePanel {

    private static readonly UnityEngine.Vector2 AircraftStationPosition = new UnityEngine.Vector2(0, -584);

    /******************* UIComponent Define begin ************************/
    public GameButton btnHangar;

    public GameButton btnSettings;

    public GameButton btnCampaign;

    public TextMeshProUGUI txtPower;

    public Image imgPlayerFighter;
    /******************* UIComponent Define finish ************************/

    public HomePanel() {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen() {
        AddLis();
        RefreshAircraft();
    }

    public override void OnClose() {
        RemoveLis();
    }

    private void AddLis() {
        OnClick(btnHangar.gameObject, OnHangar);
        OnClick(btnSettings.gameObject, OnSettings);
        OnClick(btnCampaign.gameObject, OnCampaign);
    }

    private void RemoveLis() {
        OffClick(btnHangar.gameObject, OnHangar);
        OffClick(btnSettings.gameObject, OnSettings);
        OffClick(btnCampaign.gameObject, OnCampaign);
    }

    private void OnHangar() {
        Close();
        PanelMgr.ins.OpenPanel(UIEnum.HANGAR_PANEL);
    }

    private void OnSettings() {
        PanelMgr.ins.OpenPanel(UIEnum.SETTING_PANEL);
    }

    private void OnCampaign() {
        Close();
        PanelMgr.ins.OpenPanel(UIEnum.STAGE_SELECT_PANEL);
    }

    /**刷新当前出战飞机及其战力*/
    private void RefreshAircraft() {
        PlayerAircraftVO aircraft = RaidenControl.ins.GetSelectedPlayerAircraft();
        if (aircraft == null) {
            return;
        }
        txtPower.text = $"战力 {aircraft.basePower}";
        imgPlayerFighter.rectTransform.sizeDelta = aircraft.displaySize;
        imgPlayerFighter.rectTransform.anchoredPosition = AircraftStationPosition;
        UITools.SetImage(imgPlayerFighter, aircraft.appearancePath, true);
    }

}
