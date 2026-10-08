using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 雷电战机机库界面
/// </summary>
public class HangarPanel : BasePanel {

    private static readonly Vector2 AircraftStationPosition = new Vector2(360, -617);

    private const string TabSelectedPath = "Assets/Art/atlas/default/raiden/hangarTabSelected";

    private const string TabNormalPath = "Assets/Art/atlas/default/raiden/hangarTabNormal";

    /******************* UIComponent Define begin ************************/
    public GameButton btnBack;
    public GameButton btnAircraftTab;
    public GameButton btnWingmanTab;
    public TextMeshProUGUI txtStarBalance;
    public Image imgAircraft;
    public TextMeshProUGUI txtAircraftName;
    public TextMeshProUGUI txtAircraftLevel;
    public TextMeshProUGUI txtAircraftPower;
    public ScrollList listAircraft;
    public GameButton btnPrimaryAction;
    public Image imgPrimaryActionStar;
    public RectTransform wingmanUnavailable;
    /******************* UIComponent Define finish ************************/

    private List<PlayerAircraftVO> aircraftList;

    /**当前仅用于机库预览的飞机类型，不直接改变出战选择*/
    public int previewAircraftId { get; private set; }

    public HangarPanel() {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen() {
        AddLis();
        aircraftList = RaidenControl.ins.GetAllPlayerAircraft();
        previewAircraftId = RaidenControl.ins.selectedAircraftId;
        ShowAircraftTab();
        RefreshView();
    }

    public override void OnClose() {
        RemoveLis();
    }

    private void AddLis() {
        OnClick(btnBack.gameObject, OnBack);
        OnClick(btnAircraftTab.gameObject, ShowAircraftTab);
        OnClick(btnWingmanTab.gameObject, ShowWingmanTab);
        OnClick(btnPrimaryAction.gameObject, OnPrimaryAction);
    }

    private void RemoveLis() {
        OffClick(btnBack.gameObject, OnBack);
        OffClick(btnAircraftTab.gameObject, ShowAircraftTab);
        OffClick(btnWingmanTab.gameObject, ShowWingmanTab);
        OffClick(btnPrimaryAction.gameObject, OnPrimaryAction);
    }

    /**切换浏览机型时只刷新展示，确认按钮才会修改解锁或出战状态*/
    public void SelectPreviewAircraft(int aircraftId) {
        previewAircraftId = aircraftId;
        RefreshView();
    }

    private void RefreshView() {
        txtStarBalance.text = RaidenControl.ins.availableStarCount.ToString();
        listAircraft.array = aircraftList;
        RefreshAircraftPreview();
    }

    private void RefreshAircraftPreview() {
        PlayerAircraftVO aircraft = RaidenControl.ins.model.GetPlayerAircraft(previewAircraftId);
        if (aircraft == null) {
            return;
        }
        txtAircraftName.text = aircraft.displayName;
        txtAircraftLevel.text = $"默认等级  Lv.{aircraft.level} / {aircraft.maxLevel}";
        txtAircraftPower.text = $"战力  {aircraft.basePower}";
        imgAircraft.rectTransform.anchoredPosition = AircraftStationPosition;
        UITools.SetImage(imgAircraft, aircraft.appearancePath, true);

        bool unlocked = RaidenControl.ins.IsPlayerAircraftUnlocked(aircraft.id);
        bool equipped = RaidenControl.ins.selectedAircraftId == aircraft.id;
        bool canUnlock = !unlocked && RaidenControl.ins.availableStarCount >= aircraft.unlockStarCost;
        bool showUnlockCost = canUnlock;
        imgPrimaryActionStar.gameObject.SetActive(showUnlockCost);
        if (equipped) {
            btnPrimaryAction.Label = "当前出战";
            btnPrimaryAction.Interactable = false;
        } else if (unlocked) {
            btnPrimaryAction.Label = "设为出战";
            btnPrimaryAction.Interactable = true;
        } else if (canUnlock) {
            btnPrimaryAction.Label = $"{aircraft.unlockStarCost}  解锁";
            btnPrimaryAction.Interactable = true;
        } else {
            btnPrimaryAction.Label = "星数不足";
            btnPrimaryAction.Interactable = false;
        }
    }

    private void OnPrimaryAction() {
        PlayerAircraftVO aircraft = RaidenControl.ins.model.GetPlayerAircraft(previewAircraftId);
        if (aircraft == null) {
            return;
        }
        if (!RaidenControl.ins.IsPlayerAircraftUnlocked(aircraft.id)) {
            RaidenControl.ins.UnlockPlayerAircraft(aircraft.id);
        } else if (RaidenControl.ins.selectedAircraftId != aircraft.id) {
            RaidenControl.ins.SelectPlayerAircraft(aircraft.id);
        }
        RefreshView();
    }

    private void ShowAircraftTab() {
        wingmanUnavailable.gameObject.SetActive(false);
        btnAircraftTab.SetImage(TabSelectedPath);
        btnWingmanTab.SetImage(TabNormalPath);
        btnAircraftTab.LabelColor = new Color32(7, 27, 40, 255);
        btnWingmanTab.LabelColor = new Color32(234, 250, 255, 255);
    }

    private void ShowWingmanTab() {
        wingmanUnavailable.gameObject.SetActive(true);
        btnAircraftTab.SetImage(TabNormalPath);
        btnWingmanTab.SetImage(TabSelectedPath);
        btnAircraftTab.LabelColor = new Color32(234, 250, 255, 255);
        btnWingmanTab.LabelColor = new Color32(7, 27, 40, 255);
    }

    private void OnBack() {
        Close();
        PanelMgr.ins.OpenPanel(UIEnum.HOME_PANEL);
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            OnBack();
        }
    }

}
