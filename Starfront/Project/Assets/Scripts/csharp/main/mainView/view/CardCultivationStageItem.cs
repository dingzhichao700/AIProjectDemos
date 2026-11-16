using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卡牌独立等级效果条目，只呈现查询结果，不执行属性加成。
/// </summary>
public sealed class CardCultivationStageItem : ScrollListItem {
    /******************* UIComponent Define begin ************************/
    public Image imgGained;
    public Image imgNotGained;
    public TextMeshProUGUI txtStageLevel;
    public TextMeshProUGUI txtStageEffect;
    public TextMeshProUGUI txtStageStatus;
    /******************* UIComponent Define finish ************************/

    protected override void OnSetData(int index, object value) {
        var stage = (CultivationStageVO)value;
        imgGained.enabled = stage.gained;
        imgNotGained.enabled = !stage.gained;
        txtStageLevel.text = "Lv." + stage.unlockLevel;
        txtStageEffect.text = stage.description.Replace("\r", " ").Replace("\n", " ");
        txtStageStatus.text = stage.gained ? "已获得" : "未获得";
        txtStageStatus.color = stage.gained ? new Color32(0,255,94,255) : new Color32(242,247,255,255);
    }

    public override void Clear() {
        data = null;
        imgGained.enabled = false;
        imgNotGained.enabled = true;
        txtStageLevel.text = txtStageEffect.text = txtStageStatus.text = "";
    }
}
