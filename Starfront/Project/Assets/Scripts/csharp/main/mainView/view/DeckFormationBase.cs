using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 上阵位底座item，独立组合品质展台、职能徽章和等级铭牌。
/// </summary>
/// <remarks>由UIBinder生成成员；只接收显示状态，不加载卡牌或保存业务数据。</remarks>
public sealed class DeckFormationBase : BaseView {

    /******************* UIComponent Define begin ************************/
    public Image stageImage;
    public Image badgeBase;
    public Image roleIcon;
    public TextMeshProUGUI level;
    /******************* UIComponent Define finish ************************/

    /**由所属上阵位提交状态；图标的配置加载仍由卡牌表现层管理。*/
    public void Refresh(Sprite stage, Color tint, string label, bool occupied) {
        stageImage.sprite = stage;
        stageImage.color = tint;
        var layers = stageImage.GetComponent<FormationStageLayers>();
        if (layers != null) layers.Refresh(occupied);
        level.text = label;
        level.color = occupied ? Color.white : new Color(.65f, .71f, .77f);
        badgeBase.enabled = occupied;
        roleIcon.enabled = occupied && roleIcon.sprite != null;
        roleIcon.color = Color.white;
    }

}
