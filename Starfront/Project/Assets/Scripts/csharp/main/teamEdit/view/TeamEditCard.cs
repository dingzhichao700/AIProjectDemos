using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 编辑窗口的复用卡牌项。
/// </summary>
/// <remarks>上阵区与牌库使用同一独立状态刷新路径。</remarks>
public sealed class TeamEditCard : ScrollListItem {

    /******************* UIComponent Define begin ************************/
    public Image imgArtwork;
    public Image imgCardFrame;
    public TextMeshProUGUI txtCardTitle;
    public TextMeshProUGUI txtCardState;
    public TextMeshProUGUI txtCardCost;
    public Image imgSelected;
    public Button btnCard;
    public Image imgLock;
    public Image imgShade;
    public Image imgProfession;
    public Image imgProfessionBase;
    public Image imgCardCostBase;
    public Image imgDeployedTag;
    public TextMeshProUGUI txtDeployedLabel;
    /******************* UIComponent Define finish ************************/

    public Material lockedFrameMaterial;

    public Sprite frameGreat;

    public Sprite frameRare;

    public Sprite frameEpic;

    public Sprite frameLegend;

    public Sprite frameLocked;

    public Sprite professionVanguard;

    public Sprite professionFirepower;

    public Sprite professionHeavy;

    public Sprite professionSupport;

    public sealed class Row {

        public int id;

        public TeamEditQuery query;

        public bool deployed;

        public System.Action<int> clicked;

    }

    private Row current;

    private CardArtImage loader;

    private int requestVersion;

    protected override void OnInitListItem() {
        loader = imgArtwork.gameObject.AddComponent<CardArtImage>();
        loader.autoLoadOnEnable = false;
        imgArtwork.preserveAspect = true;
        loader.Clear();
    }

    // 子图片关闭时会释放资源；条目缓存必须同时失效，重开才能重新绑定同一张卡。
    private void OnDisable() {
        requestVersion++;
        current = null;
        loader?.Clear();
    }

    protected override void OnSetData(int index, object value) {
        var row = (Row)value;
        bool same = current != null && current.id == row.id;
        current = row;
        btnCard.onClick.RemoveListener(Clicked);
        btnCard.onClick.AddListener(Clicked);
        var card = row.query.Card(row.id);
        int level = row.query.Level(row.id);
        txtCardTitle.text = card.Name;
        txtCardTitle.fontSize = card.Name.Length > 5 ? 13.5f : 14.4f;
        txtCardState.text = level == 0 ? "未解锁" : "Lv." + level;
        bool hasCost = row.query.ConstructionCost(row.id).HasValue;
        txtCardCost.text = hasCost ? row.query.Cost(row.id) : "";
        txtCardCost.gameObject.SetActive(hasCost);
        imgCardCostBase.gameObject.SetActive(hasCost);
        int? profession = row.query.Profession(row.id);
        imgProfession.sprite = profession == 1 ? professionVanguard : profession == 2 ? professionFirepower : profession == 3 ? professionHeavy : profession == 4 ? professionSupport : null;
        imgProfession.gameObject.SetActive(imgProfession.sprite != null);
        imgProfessionBase.gameObject.SetActive(imgProfession.sprite != null);
        imgDeployedTag.gameObject.SetActive(row.deployed);
        txtDeployedLabel.gameObject.SetActive(row.deployed);
        imgShade.gameObject.SetActive(level == 0);
        imgSelected.gameObject.SetActive(row.deployed);
        imgArtwork.color = Color.white;
        imgCardFrame.color = Color.white;
        imgLock.gameObject.SetActive(level == 0);
        imgCardFrame.sprite = level == 0 ? frameLocked : (card.Quality == cfg.Quality.NORMAL || card.Quality == cfg.Quality.GREAT) ? frameGreat : card.Quality == cfg.Quality.RARE ? frameRare : card.Quality == cfg.Quality.EPIC ? frameEpic : frameLegend;
        imgCardFrame.material = null;

        if (!same) {
            Load(card.ArtId, ++requestVersion);
        }
    }

    private async void Load(int artId, int version) {
        try {
            await loader.BindAsync(artId, CardArtImage.Usage.Card);
        } catch (System.Exception) {
            if (this != null && current != null && requestVersion == version) {
                txtCardState.text = "卡面资源缺失";
            }
        }
    }

    private void Clicked() {
        current?.clicked?.Invoke(current.id);
    }

    public override void Clear() {
        requestVersion++;
        current = null;
        data = null;
        btnCard.onClick.RemoveListener(Clicked);
        loader?.Clear();
        imgDeployedTag.gameObject.SetActive(false);
        txtDeployedLabel.gameObject.SetActive(false);
        imgSelected.gameObject.SetActive(false);
    }

}
