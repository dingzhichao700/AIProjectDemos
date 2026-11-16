using UnityEngine;
using cfg;
using System;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 卡牌培养界面表现组件。
/// </summary>
public sealed class CardCultivationSkillItem : ScrollListItem {
    /******************* UIComponent Define begin ************************/
    public Button btnSkill;
    public Image imgSkillFrame;
    public Image imgSkillKind;
    public Image imgSkillIcon;
    public Image imgSkillLock;
    public TextMeshProUGUI txtSkillKind;
    public TextMeshProUGUI txtSkillState;
    public Image imgSkillLockedShade;
    /******************* UIComponent Define finish ************************/

    public UnityEngine.Sprite basicPlate, passivePlate, activePlate, ultimatePlate;

    public Sprite basicTemporaryIcon, passiveTemporaryIcon, activeTemporaryIcon, ultimateTemporaryIcon;

    private CardSkillStateVO current;
    private CultivationImageLease icon;

    protected override void OnInitListItem() {
        btnSkill.GetComponent<Image>().color = Color.clear;
        btnSkill.onClick.AddListener(Open);
        icon = imgSkillIcon.gameObject.AddComponent<CultivationImageLease>();
        imgSkillIcon.preserveAspect = true;
    }

    protected override void OnSetData(int index, object value) {
        current = (CardSkillStateVO)value;
        imgSkillKind.sprite = current.slot == SkillDisplaySlot.Basic ? basicPlate : current.slot == SkillDisplaySlot.Passive ? passivePlate : current.slot == SkillDisplaySlot.Active ? activePlate : ultimatePlate;
        txtSkillKind.text = CardSkillQuery.TypeName(current.slot);
        // 缺配置的诊断保留在查询missing中；玩家只看到已解锁阶数或未解锁，不改变权威解锁值。
        bool unlocked = current.unlocked == true && current.currentRank.HasValue;
        imgSkillFrame.color = unlocked ? Color.white : new Color(.32f, .42f, .50f);
        imgSkillLock.enabled = !unlocked;
        imgSkillLockedShade.enabled = !unlocked;
        txtSkillState.text = unlocked ? current.currentRank + "阶" : "未解锁";
        var fallback = current.slot == SkillDisplaySlot.Basic ? basicTemporaryIcon : current.slot == SkillDisplaySlot.Passive ? passiveTemporaryIcon : current.slot == SkillDisplaySlot.Active ? activeTemporaryIcon : current.slot == SkillDisplaySlot.Ultimate ? ultimateTemporaryIcon : null;
        LoadIcon(current.iconAddress, fallback);
    }

    private async void LoadIcon(string address, Sprite fallback) {
        try {
            if (string.IsNullOrWhiteSpace(address)) {
                icon.Clear();
                imgSkillIcon.sprite = fallback;
                imgSkillIcon.enabled = fallback != null;
                imgSkillIcon.color = Color.white;
            } else {
                await icon.BindAsync(address);
            }
        } catch (Exception error) {
            Debug.LogException(error);
        }
    }

    private void Open() {
        if (current != null) {
            GetComponentInParent<CardCultivationPanel>().OpenSkill(current);
        }
    }

    public override void Clear() {
        data = current = null;
        if (icon != null) {
            icon.Clear();
        }
        imgSkillLock.enabled = false;
        imgSkillLockedShade.enabled = false;
        imgSkillFrame.color = Color.white;
        txtSkillKind.text = txtSkillState.text = "";
    }

}
