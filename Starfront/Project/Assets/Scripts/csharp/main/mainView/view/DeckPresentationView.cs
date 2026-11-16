using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 卡组展示层：当前上阵位、职业筛选和ScrollList牌库。
/// </summary>
/// <remarks>运行时牌库来自玩家卡牌和背包状态；方案由队伍编辑业务管理。</remarks>
public sealed class DeckPresentationView : MonoBehaviour {

    public DeckCardView[] slots;
    public ScrollList library;
    [NonSerialized] public DeckCardView.DisplayData[] previewCards;
    private CardCollectionControl collection;
    public Button editTeam;
    public Button[] filters;
    public Button[] categories;
    public Image[] filterPlates;
    public Sprite filterNormal, filterSelected;
    public GameObject[] categoryPlates;
    public TextMeshProUGUI ownedCount;
    public TextMeshProUGUI powerValue;
    public PowerDisplay powerDisplay;
    private int filter, category;

    public event Action<int> CardInfoRequested;

    private readonly List<UnityAction> choiceActions = new List<UnityAction>();
    private bool started;

    private async void Start() {
        await CfgManager.EnsureLoadedAsync();
        if (this == null) {
            return;
        }
        collection = CardCollectionControl.ins;
        started = true;
        if (!isActiveAndEnabled) {
            return;
        }
        collection.model.Changed += RefreshCollection;
        LoadConfiguredPreview();
        ShowPreview();
    }

    private void OnEnable() {
        if (editTeam != null) {
            editTeam.onClick.AddListener(OpenTeam);
            BindChoices(filters, SelectFilter);
            BindChoices(categories, SelectCategory);
        }
        if (started) {
            collection.model.Changed += RefreshCollection;
            LoadConfiguredPreview();
            ShowPreview();
        }
    }

    private void OnDisable() {
        CardCultivationControl.CloseForSource(this);
        if (collection != null) {
            collection.model.Changed -= RefreshCollection;
        }
        int offset = 0;
        RemoveChoices(filters, ref offset);
        RemoveChoices(categories, ref offset);
        if (editTeam != null) editTeam.onClick.RemoveListener(OpenTeam);
        choiceActions.Clear();

        foreach (DeckCardView slot in slots) {
            slot.Render(null, null);
        }
    }

    /**业务接入后由调用方传入当前方案和牌库；视图不推导拥有范围或自行保存。*/
    public void SetPresentation(int schemeIndex, string name, DeckCardView.DisplayData[] formation, DeckCardView.DisplayData[] cards) {
        for (int i = 0; i < slots.Length; i++) {
            var value = formation != null && i < formation.Length ? formation[i] : null;
            slots[i].Render(value, RequestFormationCardInfo);
        }
        // ScrollList自身负责实例池；未知/空数据不留下旧卡片。
        library.array = cards ?? new DeckCardView.DisplayData[0];
        var content = library.GetComponent<ScrollRect>().content;
        content.sizeDelta = Vector2.Max(Vector2.zero, content.sizeDelta);
    }

    private void RefreshCollection() {
        LoadConfiguredPreview();
        ShowPreview();
    }

    private void LoadConfiguredPreview() {
        if (collection == null) {
            return;
        }
        var cards = new List<DeckCardView.DisplayData>();
        foreach (var card in CfgManager.tables.CardObj.DataList) {
            if (!card.ShowInLibrary) {
                continue;
            }
            var state = collection.model.GetState(card.Id);
            cards.Add(new DeckCardView.DisplayData {
                artId = card.ArtId, cardId = card.Id, artPreview = false,
                title = card.Name, quality = (int)card.Quality, qualityLabel = card.QualityLabel,
                tagId = card.TagId, level = state.level, owned = state.owned,
                cellUnlockCost = TeamEditControl.ins.Query.ConstructionCost(card.Id),
                canUpgrade = state.canUpgrade, maxLevel = state.maxed,
                fragments = state.fragments, requiredFragments = state.requiredFragments,
                cardFaceReady = CfgManager.tables.CardArtObj.Get(card.ArtId).CardFaceReady,
                formationCanvas = card.Id == 1101 ? new Vector2(161, 335) : Vector2.zero,
                formationPivot = card.Id == 1101 ? new Vector2(80f / 161, 99f / 335) : Vector2.zero
            });
        }
        previewCards = cards.ToArray();
    }

    private void ShowPreview() {
        var ids = TeamEditControl.ins.ReadSelected(out int number);
        RenderPower(TeamEditControl.ins.Query.FormationPower(ids));
        var formation = new List<DeckCardView.DisplayData>();
        foreach (var id in ids) {
            var match = Array.Find(previewCards, c => c.cardId == id && c.owned);
            if (match != null && formation.Count < slots.Length) formation.Add(match);
        }
        foreach (var card in previewCards) card.deployed = formation.Contains(card);
        var cards = Array.FindAll(previewCards, c => category == 0 &&
            (filter == 0 || TeamEditControl.ins.Query.Profession(c.cardId) == filter));
        SetPresentation(number-1, "方案 " + number, formation.ToArray(), cards);
        if (ownedCount != null) ownedCount.text = "已获得 " + Array.FindAll(previewCards,c=>c.owned).Length + "/" + previewCards.Length;
        for (int i=0; filterPlates != null && i<filterPlates.Length; i++) filterPlates[i].sprite=i==filter?filterSelected:filterNormal;
        for (int i=0; categoryPlates != null && i<categoryPlates.Length; i++) categoryPlates[i].SetActive(i==category);
    }

    private void RenderPower(double? value) {
        if (powerValue != null) powerValue.enabled = false;
        if (powerDisplay != null) powerDisplay.SetValue(value);
    }

    private void BindChoices(Button[] buttons, Action<int> select) {
        if (buttons == null) return;
        for (int i=0;i<buttons.Length;i++) {
            int index=i;
            UnityAction action=()=>select(index);
            choiceActions.Add(action);
            buttons[i].onClick.AddListener(action);
        }
    }
    private void RemoveChoices(Button[] buttons, ref int offset) {
        if (buttons == null) return;
        foreach (var button in buttons) {
            if (offset < choiceActions.Count) button.onClick.RemoveListener(choiceActions[offset++]);
        }
    }
    private void SelectFilter(int index) { filter=index; if(started) ShowPreview(); }
    private void SelectCategory(int index) { category=index; if(started) ShowPreview(); }
    private async void OpenTeam() {
        try { await TeamEditControl.OpenAsync(() => { if (this && isActiveAndEnabled) RefreshCollection(); }); }
        catch (Exception error) {
            Debug.LogException(error, this);
            if (this && isActiveAndEnabled) GlobalToast.Warn("队伍编辑打开失败，请重试");
        }
    }





    public void RequestCardInfo(int cardId) {
        OpenCultivation(cardId, false);
    }

    public void RequestFormationCardInfo(int cardId) {
        OpenCultivation(cardId, true);
    }

    /**来源序列取当前实际显示列表副本；保持原视图、筛选与滚动实例。*/
    private async void OpenCultivation(int cardId, bool fromFormation) {
        if (CardInfoRequested != null) {
            CardInfoRequested.Invoke(cardId);
            return;
        }
        var ordered = new List<int>();
        if (fromFormation) {
            foreach (int id in TeamEditControl.ins.ReadSelected(out int number)) {
                if (id != 0) {
                    ordered.Add(id);
                }
            }
        } else {
            foreach (object item in library.arraySource) {
                if (item is DeckCardView.DisplayData card) {
                    ordered.Add(card.cardId);
                }
            }
        }
        var scroll = library.GetComponent<ScrollRect>();
        Vector2 position = scroll.content.anchoredPosition;
        int savedFilter = filter;
        int savedCategory = category;
        try {
            await CardCultivationControl.OpenAsync(new CardCultivationOpenVO {
                cardId = cardId, orderedCardIds = ordered.ToArray(), source = fromFormation ? "formation" : "library", owner = this,
                onReturn = () => {
                    if (this == null || !isActiveAndEnabled) {
                        return;
                    }
                    filter = savedFilter;
                    category = savedCategory;
                    RefreshCollection();
                    scroll.velocity = Vector2.zero;
                    scroll.content.anchoredPosition = position;
                }
            });
        } catch (Exception error) {
            Debug.LogException(error);
            if (this && isActiveAndEnabled) GlobalToast.Warn("卡牌培养打开失败，请重试");
        }
    }

}
