using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 牌库复用项，将ScrollList数据传给同一张卡牌展示组件。
/// </summary>
/// <remarks>清理回收状态，不拥有卡牌业务。</remarks>
public sealed class DeckLibraryCard : ScrollListItem {

    /******************* UIComponent Define begin ************************/
    public DeckCardView card;
    public Image artwork;
    public Image qualityImage;
    public Image roleIcon;
    public Image fragmentFill;
    public RectTransform boxDeployed;
    public TextMeshProUGUI title;
    public TextMeshProUGUI level;
    public TextMeshProUGUI fragments;
    public TextMeshProUGUI placeholder;
    public Button button;
    public Image lockIcon;
    /******************* UIComponent Define finish ************************/
    public DeckPresentationView owner;

    /**绑定器先注入动态节点，再把同一组引用交给可复用卡牌表现组件。*/
    protected override void OnInitListItem() {
        card.artwork = artwork;
        card.qualityImage = qualityImage;
        card.roleIcon = roleIcon;
        card.fragmentFill = fragmentFill;
        card.deployed = boxDeployed.gameObject;
        card.title = title;
        card.level = level;
        card.fragments = fragments;
        card.placeholder = placeholder;
        card.button = button;
    }

    protected override void OnSetData(int index, object value) {
        // 列表先实例化后挂入Content，数据提交时解析所属视图。
        owner = GetComponentInParent<DeckPresentationView>();
        if (owner == null) {
            throw new System.InvalidOperationException("牌库项缺少所属DeckPresentationView");
        }
        card.Render(value as DeckCardView.DisplayData, owner.RequestCardInfo);
    }

    public override void Clear() {
        data = null;
        card.Render(null, null);
    }

}
