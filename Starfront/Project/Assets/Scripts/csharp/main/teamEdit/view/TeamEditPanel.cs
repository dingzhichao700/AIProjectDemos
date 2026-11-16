using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 队伍编辑模态窗口。
/// </summary>
/// <remarks>调用方案业务接口，显示草稿；不改变主界面预制体。</remarks>
public sealed class TeamEditPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public Image imgMask;
    public RectTransform boxWindow;
    public Image imgWindowBg;
    public Button btnClose;
    public Button btnSlot1;
    public Image imgSlot1;
    public TextMeshProUGUI txtSlot1;
    public Button btnSlot2;
    public Image imgSlot2;
    public TextMeshProUGUI txtSlot2;
    public Button btnSlot3;
    public Image imgSlot3;
    public TextMeshProUGUI txtSlot3;
    public Button btnSlot4;
    public Image imgSlot4;
    public TextMeshProUGUI txtSlot4;
    public Button btnScheme1;
    public Button btnScheme2;
    public Button btnScheme3;
    public Button btnScheme4;
    public TextMeshProUGUI txtDraft;
    public TextMeshProUGUI txtCount;
    public Button btnFilter0;
    public Button btnFilter1;
    public Button btnFilter2;
    public Button btnFilter3;
    public Button btnFilter4;
    public Button btnQuality;
    public Button btnLevel;
    public ScrollList listCards;
    public Button btnClear;
    public Button btnSave;
    public RectTransform boxConfirm;
    public Image imgConfirmMask;
    public Button btnDiscard;
    public Button btnContinue;
    public Image imgStage1;
    public Image imgSlotCostBase1;
    public TextMeshProUGUI txtSlotCost1;
    public TextMeshProUGUI txtSlotLevel1;
    public TextMeshProUGUI txtSlotEmpty1;
    public Image imgStage2;
    public Image imgSlotCostBase2;
    public TextMeshProUGUI txtSlotCost2;
    public TextMeshProUGUI txtSlotLevel2;
    public TextMeshProUGUI txtSlotEmpty2;
    public Image imgStage3;
    public Image imgSlotCostBase3;
    public TextMeshProUGUI txtSlotCost3;
    public TextMeshProUGUI txtSlotLevel3;
    public TextMeshProUGUI txtSlotEmpty3;
    public Image imgStage4;
    public Image imgSlotCostBase4;
    public TextMeshProUGUI txtSlotCost4;
    public TextMeshProUGUI txtSlotLevel4;
    public TextMeshProUGUI txtSlotEmpty4;
    public Image imgQualityDown;
    public Image imgQualityUp;
    public Image imgLevelDown;
    public Image imgLevelUp;
    /******************* UIComponent Define finish ************************/


    private TeamEditControl owner;

    private TeamEditDraft draft;

    private Action savedCallback;

    private Action pendingLeave;

    private Button[] slots;

    private Button[] schemes;

    private Button[] filters;

    private Image[] portraits;

    private TextMeshProUGUI[] labels;

    private CardArtImage[] loaders;

    private readonly System.Collections.Generic.List<UnityEngine.Events.UnityAction> slotActions = new System.Collections.Generic.List<UnityEngine.Events.UnityAction>();

    private readonly System.Collections.Generic.List<UnityEngine.Events.UnityAction> schemeActions = new System.Collections.Generic.List<UnityEngine.Events.UnityAction>();

    private readonly System.Collections.Generic.List<UnityEngine.Events.UnityAction> filterActions = new System.Collections.Generic.List<UnityEngine.Events.UnityAction>();

    private int filter;

    private int sort;

    private bool descending = true;

    private int renderVersion;

    private TeamEditStagePalette stagePalette;

    private Image[] stages;
    private Image[] costBases;
    private TextMeshProUGUI[] costLabels;
    private TextMeshProUGUI[] levelLabels;
    private TextMeshProUGUI[] emptyLabels;
    private Sprite schemeNormal;
    private Sprite filterNormal;
    private Sprite sortNormal;

    public TeamEditDraft Draft => draft;

    public TeamEditPanel() {
        layer = PanelLayer.SCALE_PANEL_SECOND;
    }

    public override bool UsesFullScreenViewport => true;

    public override string[] FullBleedChildren => new[] { "imgMask", "boxConfirm" };

    public override Vector2 SafeContentDesignSize => new Vector2(661, 967);

    /**背景覆盖实际舞台；独立内容窗口居中交给公共安全区，不用窗口尺寸改变设计方向。*/
    public override void OnInit() {
        slots = new[] { btnSlot1, btnSlot2, btnSlot3, btnSlot4 };
        schemes = new[] { btnScheme1, btnScheme2, btnScheme3, btnScheme4 };
        filters = new[] { btnFilter0, btnFilter1, btnFilter2, btnFilter3, btnFilter4 };
        portraits = new[] { imgSlot1, imgSlot2, imgSlot3, imgSlot4 };
        labels = new[] { txtSlot1, txtSlot2, txtSlot3, txtSlot4 };
        stagePalette = GetComponent<TeamEditStagePalette>();
        stages = new[] { imgStage1, imgStage2, imgStage3, imgStage4 };
        costBases = new[] { imgSlotCostBase1, imgSlotCostBase2, imgSlotCostBase3, imgSlotCostBase4 };
        costLabels = new[] { txtSlotCost1, txtSlotCost2, txtSlotCost3, txtSlotCost4 };
        levelLabels = new[] { txtSlotLevel1, txtSlotLevel2, txtSlotLevel3, txtSlotLevel4 };
        emptyLabels = new[] { txtSlotEmpty1, txtSlotEmpty2, txtSlotEmpty3, txtSlotEmpty4 };
        schemeNormal = btnScheme1.image.sprite;
        filterNormal = btnFilter0.image.sprite;
        sortNormal = btnQuality.image.sprite;
        loaders = new CardArtImage[4];
        for (int i = 0; i < 4; i++) {
            loaders[i] = portraits[i].gameObject.AddComponent<CardArtImage>();
            loaders[i].autoLoadOnEnable = false;
            loaders[i].Clear();
            portraits[i].color = Color.white;
            portraits[i].preserveAspect = true;
        }
        boxWindow.anchorMin = boxWindow.anchorMax = boxWindow.pivot = new Vector2(.5f, .5f);
        boxWindow.anchoredPosition = Vector2.zero;
        imgMask.raycastTarget = imgWindowBg.raycastTarget = imgConfirmMask.raycastTarget = true;
        // 未保存提示遮罩属于整个舞台，不能随窗口安全区再次产生偏移。
        boxConfirm.anchorMin = Vector2.zero;
        boxConfirm.anchorMax = Vector2.one;
        boxConfirm.offsetMin = boxConfirm.offsetMax = Vector2.zero;
    }

    public override bool UsesWindowTransition => true;
    protected override RectTransform WindowTransitionContent => boxWindow;
    protected override Image WindowTransitionMask => imgMask;

    public override void OnOpen() {
        AddLis();
    }

    protected override void OnPrepareOpen() {
        if (openParams == null || openParams.Length != 2 || !(openParams[0] is TeamEditControl value)) {
            throw new ArgumentException("队伍编辑需要TeamEditControl及成功回调");
        }
        trans.anchorMin = Vector2.zero;
        trans.anchorMax = Vector2.one;
        trans.offsetMin = trans.offsetMax = Vector2.zero;
        GetComponent<PanelSafeContent>().Apply();
        owner = value;
        savedCallback = openParams[1] as Action;
        draft = new TeamEditDraft(owner.model);
        filter = 0;
        sort = 1;
        descending = true;
        boxConfirm.gameObject.SetActive(false);
        pendingLeave = null;
        Refresh();
    }

    public override void OnClose() {
        RemoveLis();
        renderVersion++;
        foreach (var loader in loaders) {
            loader.Clear();
        }
        listCards.array = new object[0];
        pendingLeave = null;
        savedCallback = null;
        draft = null;
        owner = null;
        openParams = null;
    }

    private void AddLis() {
        btnClose.onClick.AddListener(RequestClose);
        btnClear.onClick.AddListener(ClearDraft);
        btnSave.onClick.AddListener(SaveDraft);
        btnQuality.onClick.AddListener(SortQuality);
        btnLevel.onClick.AddListener(SortLevel);
        btnDiscard.onClick.AddListener(Discard);
        btnContinue.onClick.AddListener(ContinueEditing);
        OnClick(imgMask.gameObject, RequestClose);
        for (int i = 0; i < 4; i++) {
            int index = i;
            UnityEngine.Events.UnityAction slot = () => SelectSlot(index);
            UnityEngine.Events.UnityAction scheme = () => SwitchScheme(index + 1);
            slotActions.Add(slot);
            schemeActions.Add(scheme);
            slots[i].onClick.AddListener(slot);
            schemes[i].onClick.AddListener(scheme);
        }
        for (int i = 0; i < 5; i++) {
            int index = i;
            UnityEngine.Events.UnityAction action = () => Filter(index);
            filterActions.Add(action);
            filters[i].onClick.AddListener(action);
        }
        CardCollectionControl.ins.model.Changed += OnCardsChanged;
    }

    private void RemoveLis() {
        btnClose.onClick.RemoveListener(RequestClose);
        btnClear.onClick.RemoveListener(ClearDraft);
        btnSave.onClick.RemoveListener(SaveDraft);
        btnQuality.onClick.RemoveListener(SortQuality);
        btnLevel.onClick.RemoveListener(SortLevel);
        btnDiscard.onClick.RemoveListener(Discard);
        btnContinue.onClick.RemoveListener(ContinueEditing);
        OffClick(imgMask.gameObject, RequestClose);
        for (int i = 0; i < slotActions.Count; i++) {
            slots[i].onClick.RemoveListener(slotActions[i]);
            schemes[i].onClick.RemoveListener(schemeActions[i]);
        }
        for (int i = 0; i < filterActions.Count; i++) {
            filters[i].onClick.RemoveListener(filterActions[i]);
        }
        slotActions.Clear();
        schemeActions.Clear();
        filterActions.Clear();
        CardCollectionControl.ins.model.Changed -= OnCardsChanged;
    }

    /**外部收藏状态变化不覆盖草稿；保存仍会重新校验真实解锁状态。*/
    private void OnCardsChanged() {
        Refresh();
    }

    public void SelectSlot(int index) {
        if (boxConfirm.gameObject.activeSelf) {
            return;
        }
        draft.RemoveAt(index);
        Refresh();
    }

    public void SelectCard(int id) {
        if (boxConfirm.gameObject.activeSelf) {
            return;
        }
        bool success = draft.TryAddCard(id, out string reason);
        Refresh();
        if (!success) {
            GlobalToast.Warn(reason);
        }
    }

    public void ClearDraft() {
        if (boxConfirm.gameObject.activeSelf) {
            return;
        }
        draft.Clear();
        Refresh();
    }

    public void SaveDraft() {
        if (boxConfirm.gameObject.activeSelf) {
            return;
        }
        bool success = draft.TrySave(out string reason);
        Refresh();
        if (success) {
            GlobalToast.Show("队伍已保存，已选用方案 " + draft.Scheme);
        } else {
            GlobalToast.Warn(reason);
        }
        if (success) {
            try {
                savedCallback?.Invoke();
            } catch (Exception error) {
                Debug.LogException(error);
            }
            // 提交成功后返回原阵容，由调用方刷新已保存方案。
            Close();
        }
    }

    public void SwitchScheme(int number) {
        if (boxConfirm.gameObject.activeSelf || number == draft.Scheme) {
            return;
        }
        if (!draft.TrySwitch(number)) {
            ConfirmLeave(() => { draft.TrySwitch(number, true); Refresh(); });
        } else {
            Refresh();
        }
    }

    public void RequestClose() {
        if (!isOpened || draft == null || boxConfirm.gameObject.activeSelf) {
            return;
        }
        if (draft.Dirty) {
            ConfirmLeave(() => Close());
        } else {
            Close();
        }
    }

    private void ConfirmLeave(Action action) {
        pendingLeave = action;
        boxConfirm.SetAsLastSibling();
        boxConfirm.gameObject.SetActive(true);
    }

    public void Discard() {
        var action = pendingLeave;
        pendingLeave = null;
        boxConfirm.gameObject.SetActive(false);
        action?.Invoke();
    }

    public void ContinueEditing() {
        pendingLeave = null;
        boxConfirm.gameObject.SetActive(false);
    }

    public void Filter(int value) {
        if (boxConfirm.gameObject.activeSelf) {
            return;
        }
        filter = value;
        Refresh();
        if (filter > 0 && owner.Query.List(filter, sort, descending).Length == 0) {
            GlobalToast.Show("当前职业没有卡牌。");
        }
        ResetScroll();
    }

    private void SortQuality() {
        SetSort(1);
    }

    private void SortLevel() {
        SetSort(2);
    }

    public void SetSort(int value) {
        if (boxConfirm.gameObject.activeSelf) {
            return;
        }
        descending = sort == value ? !descending : true;
        sort = value;
        Refresh();
        ResetScroll();
    }

    private void ResetScroll() {
        var scroll = listCards.GetComponent<ScrollRect>();
        scroll.StopMovement();
        scroll.content.anchoredPosition = Vector2.zero;
    }

    private void Refresh() {
        if (draft == null) {
            return;
        }
        int version = ++renderVersion;
        int[] selected = draft.Slots;
        txtDraft.text = draft.Dirty ? "未保存修改" : "点击即切换";
        txtCount.text = "已获得 " + owner.Query.Owned + "/" + owner.Query.Total;
        btnQuality.GetComponentInChildren<TextMeshProUGUI>().text = "品质";
        btnLevel.GetComponentInChildren<TextMeshProUGUI>().text = "等级";
        btnQuality.image.sprite = sort == 1 ? btnQuality.spriteState.selectedSprite : sortNormal;
        btnLevel.image.sprite = sort == 2 ? btnLevel.spriteState.selectedSprite : sortNormal;
        imgQualityDown.gameObject.SetActive(sort == 1 && descending);
        imgQualityUp.gameObject.SetActive(sort == 1 && !descending);
        imgLevelDown.gameObject.SetActive(sort == 2 && descending);
        imgLevelUp.gameObject.SetActive(sort == 2 && !descending);
        for (int i = 0; i < 4; i++) {
            schemes[i].image.sprite = draft.Scheme == i + 1 ? schemes[i].spriteState.selectedSprite : schemeNormal;
            schemes[i].image.color = Color.white;
            slots[i].image.color = draft.SelectedSlot == i ? new Color(.2f, .9f, 1, .12f) : Color.clear;
            int id = selected[i];
            bool occupied = id != 0;
            labels[i].text = occupied ? owner.Query.Card(id)?.Name : "";
            labels[i].gameObject.SetActive(occupied);
            levelLabels[i].text = occupied ? "Lv." + owner.Query.Level(id) : "";
            levelLabels[i].gameObject.SetActive(occupied);
            // 底座属于上阵槽位本身；是否有单位只控制立绘、文本与费用。
            stagePalette.Apply(stages[i], occupied ? owner.Query.Card(id).Quality : (cfg.Quality?)null);
            emptyLabels[i].gameObject.SetActive(!occupied);
            bool hasCost = occupied && owner.Query.ConstructionCost(id).HasValue;
            costBases[i].gameObject.SetActive(hasCost);
            costLabels[i].gameObject.SetActive(hasCost);
            costLabels[i].text = hasCost ? owner.Query.Cost(id) : "";
            if (id == 0) {
                loaders[i].Clear();
            } else {
                LoadSlot(i, id, version);
            }
        }
        for (int i = 0; i < 5; i++) {
            filters[i].image.sprite = filter == i ? filters[i].spriteState.selectedSprite : filterNormal;
            filters[i].image.color = Color.white;
        }
        var ids = owner.Query.List(filter, sort, descending);
        var rows = new System.Collections.Generic.List<TeamEditCard.Row>();
        foreach (int id in ids) {
            rows.Add(new TeamEditCard.Row { id = id, query = owner.Query, deployed = Array.IndexOf(selected, id) >= 0, clicked = SelectCard });
        }
        listCards.array = rows;
    }

    private async void LoadSlot(int slot, int id, int version) {
        try {
            var card = owner.Query.Card(id);
            if (loaders[slot].artId != card.ArtId || !portraits[slot].enabled) {
                await loaders[slot].BindAsync(card.ArtId, CardArtImage.Usage.Formation);
            }
            if (this == null || draft == null || renderVersion != version || draft.Slots[slot] != id || portraits[slot].sprite == null) {
                return;
            }
            var sprite = portraits[slot].sprite;
            portraits[slot].rectTransform.anchorMin = portraits[slot].rectTransform.anchorMax = new Vector2(0, 1);
            if (FormationDisplayLayout.TryApply(id, portraits[slot], stages[slot], new Vector2(70, -159.5f))) {
                return;
            }
            // 未定档单位保留原显示，待样件通过后加入共用参数。
            float scale = Mathf.Min(121 / sprite.rect.width, 251 / sprite.rect.height);
            var rect = portraits[slot].rectTransform;
            rect.pivot = sprite.pivot / sprite.rect.size;
            rect.sizeDelta = sprite.rect.size * scale;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(70, -159.5f);
            FormationDisplayLayout.AlignToStage(portraits[slot], stages[slot]);

        } catch (Exception error) {
            if (this != null && draft != null && renderVersion == version) {
                Debug.LogWarning("展示资源加载失败：" + error.Message);
                GlobalToast.Warn("展示资源加载失败，请重试");
            }
        }
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            if (boxConfirm.gameObject.activeSelf) {
                ContinueEditing();
            } else {
                RequestClose();
            }
        }
    }

}
