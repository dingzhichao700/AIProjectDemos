using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 机库中的玩家飞机列表项
/// </summary>
public class HangarAircraftItem : ScrollListItem {

    private static readonly Vector2 AircraftStationPosition = new Vector2(59, -111);

    private const string SelectedPath = "Assets/Art/atlas/default/raiden/hangarCardSelected";

    private const string UnlockedPath = "Assets/Art/atlas/default/raiden/hangarCardNormal";

    private const string LockedPath = "Assets/Art/atlas/default/raiden/hangarCardLocked";

    /******************* UIComponent Define begin ************************/
    public GameButton btnAircraft;
    public Image imgAircraftItem;
    public Image imgUnlockStar;
    public TextMeshProUGUI txtAircraftItemName;
    public TextMeshProUGUI txtAircraftItemState;
    /******************* UIComponent Define finish ************************/

    private PlayerAircraftVO aircraft;

    override protected void OnInitListItem() {
        OnClick(btnAircraft.gameObject, OnSelect);
    }

    override protected void OnSetData(int index, object value) {
        aircraft = value as PlayerAircraftVO;
        if (aircraft == null) {
            return;
        }
        HangarPanel panel = GetComponentInParent<HangarPanel>();
        bool previewSelected = panel != null && panel.previewAircraftId == aircraft.id;
        bool unlocked = RaidenControl.ins.IsPlayerAircraftUnlocked(aircraft.id);
        bool equipped = RaidenControl.ins.selectedAircraftId == aircraft.id;

        btnAircraft.SetImage(previewSelected ? SelectedPath : unlocked ? UnlockedPath : LockedPath);
        imgAircraftItem.rectTransform.anchoredPosition = AircraftStationPosition;
        UITools.SetImage(imgAircraftItem, aircraft.appearancePath, true);
        txtAircraftItemName.text = aircraft.displayName;
        txtAircraftItemName.color = unlocked ? new Color32(234, 250, 255, 255) : new Color32(120, 148, 160, 255);
        imgUnlockStar.gameObject.SetActive(!unlocked);
        txtAircraftItemState.rectTransform.anchoredPosition = new Vector2(unlocked ? 9 : 34, -164);
        txtAircraftItemState.rectTransform.sizeDelta = new Vector2(unlocked ? 100 : 75, 22);
        txtAircraftItemState.text = equipped ? "当前出战" : unlocked ? "已解锁" : aircraft.unlockStarCost.ToString();
        txtAircraftItemState.color = unlocked ? new Color32(108, 235, 240, 255) : new Color32(255, 212, 90, 255);
    }

    private void OnSelect() {
        if (aircraft == null) {
            return;
        }
        HangarPanel panel = GetComponentInParent<HangarPanel>();
        if (panel != null) {
            panel.SelectPreviewAircraft(aircraft.id);
        }
    }

    public override void Destroy() {
        OffClick(btnAircraft.gameObject, OnSelect);
        base.Destroy();
    }

}
