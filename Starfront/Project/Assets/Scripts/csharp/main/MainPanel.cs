using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面内容视图与固定底部导航。
/// </summary>
/// <remarks>只处理视图切换与展示；功能开启由 FunctionControl 判定。</remarks>
public sealed class MainPanel : BasePanel {

    public override bool UsesFullScreenViewport => true;

    public override bool FitContentToSafeArea => false;

    /******************* UIComponent Define begin ************************/

    public Button btnShop;

    public Button btnDeck;

    public Button btnBattle;

    public Button btnTerritory;

    public Button btnRanking;

    public RectTransform viewDeck;

    public RectTransform viewBattle;

    public Image imgDeckSelected;

    public Image imgBattleSelected;

    public TextMeshProUGUI txtNavDeckLabel;

    public TextMeshProUGUI txtNavBattleLabel;

    /******************* UIComponent Define finish ************************/

    /**当前是否显示卡组视图*/
    private bool showingDeck;
    private CanvasGroup deckVisibility;

    /**取消切换时同时回收两段补间，避免关闭后回调修改视图*/
    private Tween incomingTween;

    private Tween outgoingTween;

    private MainPanelLayout layout;

    private MainFunctionNavigationView functionNavigation;
    private Button playBattleButton;
    private bool enteringBattle;
    private int battleRequestVersion;

    public MainPanel() {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen() {
        layout = GetComponent<MainPanelLayout>();
        layout.ActivateViewport();
        layout.LayoutChanged += OnLayoutChanged;
        LayerEffectTestGM.Init();
        InventoryGM.Init();
        FunctionControl.ins.Init();
        if (functionNavigation == null) {
            functionNavigation = new MainFunctionNavigationView(this);
        }
        SetInitialView();
        AddLis();
        RefreshFunctionState();
    }

    public override void OnClose() {
        battleRequestVersion++;
        if (enteringBattle) {
            BattleControl.ins.End();
            enteringBattle = false;
        }
        RemoveLis();
        SetViewImmediate(false);
        viewDeck.gameObject.SetActive(false);
        layout.LayoutChanged -= OnLayoutChanged;
        layout.RestoreViewport();
        InventoryGM.Shutdown();
        LayerEffectTestGM.Shutdown();
    }

    /**视窗变化时取消旧宽度上的动画，并保持当前选择。*/
    private void OnLayoutChanged() {
        if (FunctionControl.ins.model == null) {
            return;
        }
        if (!viewDeck.gameObject.activeSelf && !viewBattle.gameObject.activeSelf) {
            SetInitialView();
        } else {
            SetViewImmediate(showingDeck);
        }
    }

    private void AddLis() {
        playBattleButton = layout.battleButton.GetComponent<Button>();
        playBattleButton.interactable = true;
        playBattleButton.onClick.AddListener(StartBattle);
        btnShop.onClick.AddListener(ShowShop);
        btnDeck.onClick.AddListener(ShowDeck);
        btnBattle.onClick.AddListener(ShowBattle);
        btnTerritory.onClick.AddListener(ShowTerritory);
        btnRanking.onClick.AddListener(ShowRanking);
        FunctionControl.ins.On(FunctionConst.UnlockChanged, RefreshFunctionState);
    }

    private void RemoveLis() {
        playBattleButton.onClick.RemoveListener(StartBattle);
        btnShop.onClick.RemoveListener(ShowShop);
        btnDeck.onClick.RemoveListener(ShowDeck);
        btnBattle.onClick.RemoveListener(ShowBattle);
        btnTerritory.onClick.RemoveListener(ShowTerritory);
        btnRanking.onClick.RemoveListener(ShowRanking);
        FunctionControl.ins.Off(FunctionConst.UnlockChanged, RefreshFunctionState);
    }

    private void ShowDeck() {
        if (TryEnterFunction(FunctionConst.Deck)) {
            SwitchView(true);
        }
    }

    /**业务准备完成后再打开场景；窗口关闭会使旧请求失效。*/
    private async void StartBattle() {
        if (enteringBattle || BattleControl.ins.preparing || !TryEnterFunction(FunctionConst.Battle)) {
            return;
        }
        if (!BattleControl.ins.CanEnterSceneBattle(out string reason)) {
            GlobalToast.Warn(reason);
            return;
        }
        int version = ++battleRequestVersion;
        enteringBattle = true;
        playBattleButton.interactable = false;
        try {
            await ResourceLoader.LoadListAsync(new System.Collections.Generic.List<ResLoadInfo> { new ResLoadInfo(ResourceConst.GetUIPath(MainUIEnum.BATTLE_SCENE_PANEL), ResType.Prefab), new ResLoadInfo(ResourceConst.GetUIPath(MainUIEnum.BATTLE_INFO_PANEL), ResType.Prefab) });
            if (version != battleRequestVersion) {
                return;
            }
            await BattleControl.ins.PrepareAsync(BattleConst.InitialSceneId);
            if (version != battleRequestVersion) {
                return;
            }
            enteringBattle = false;
            Close();
            PanelMgr.ins.OpenPanel(MainUIEnum.BATTLE_SCENE_PANEL);
        } catch (System.OperationCanceledException) {
            // 离开页面后的完成回调不会重新打开场景。
        } catch (System.Exception exception) {
            BattleControl.ins.End();
            Debug.LogException(exception);
            if (version == battleRequestVersion && isOpened) {
                GlobalToast.Warn("场景加载失败，请重试");
            }
        } finally {
            if (version == battleRequestVersion) {
                enteringBattle = false;
                playBattleButton.interactable = true;
            }
        }
    }

    private void ShowBattle() {
        if (TryEnterFunction(FunctionConst.Battle)) {
            SwitchView(false);
        }
    }

    private void ShowShop() {
        ShowReservedFunction(FunctionConst.Shop);
    }

    private void ShowTerritory() {
        ShowReservedFunction(FunctionConst.Territory);
    }

    private void ShowRanking() {
        ShowReservedFunction(FunctionConst.Ranking);
    }

    /**未制作的页面不误认为功能仍锁定，保持当前视图并明确告知。*/
    private void ShowReservedFunction(int id) {
        if (TryEnterFunction(id)) {
            GlobalToast.Warn(FunctionControl.ins.model.GetConfig(id).Name + "已解锁\n功能页面尚未开放");
        }
    }

    private bool TryEnterFunction(int id) {
        if (!FunctionControl.ins.TryEnter(id, out string hint)) {
            GlobalToast.Warn(hint);
            return false;
        }
        return true;
    }

    private void RefreshFunctionState() {
        functionNavigation.Refresh(FunctionControl.ins.model);
    }

    /**默认优先进入对战；配置锁住两页时不绕过入口检查展示其内容。*/
    private void SetInitialView() {
        bool battleOpen = FunctionControl.ins.model.IsUnlocked(FunctionConst.Battle);
        bool deckOpen = FunctionControl.ins.model.IsUnlocked(FunctionConst.Deck);
        SetViewImmediate(!battleOpen && deckOpen);
        if (!battleOpen && !deckOpen) {
            viewDeck.gameObject.SetActive(false);
            viewBattle.gameObject.SetActive(false);
            imgDeckSelected.gameObject.SetActive(false);
            imgBattleSelected.gameObject.SetActive(false);
            txtNavDeckLabel.gameObject.SetActive(false);
            txtNavBattleLabel.gameObject.SetActive(false);
        }
    }

    /**恢复确定的视图位置，使快速点击、关闭重开不会累计位移。*/
    private void SetViewImmediate(bool deck) {
        incomingTween?.Kill();
        outgoingTween?.Kill();
        incomingTween = null;
        outgoingTween = null;
        showingDeck = deck;
        viewDeck.anchoredPosition = Vector2.zero;
        viewBattle.anchoredPosition = Vector2.zero;
        SetDeckVisible(deck);
        viewBattle.gameObject.SetActive(!deck);
        RefreshNavigation();
    }

    // 切页保留已访问的阵容及卡面租用，关闭窗口仍走正常的禁用与释放流程。
    private void SetDeckVisible(bool visible) {
        if (deckVisibility == null) {
            deckVisibility = viewDeck.GetComponent<CanvasGroup>();
            if (!deckVisibility) deckVisibility = viewDeck.gameObject.AddComponent<CanvasGroup>();
        }
        deckVisibility.alpha = visible ? 1f : 0f;
        deckVisibility.interactable = visible;
        deckVisibility.blocksRaycasts = visible;
        if (visible) {
            viewDeck.gameObject.SetActive(true);
        } else if (viewDeck.gameObject.activeInHierarchy) {
            CardCultivationControl.CloseForSource(viewDeck.GetComponentInChildren<DeckPresentationView>(true));
        }
    }

    /**导航保持不动，仅内容视图根据按钮顺序横向滑入。*/
    private void SwitchView(bool deck) {
        if (deck == showingDeck) {
            return;
        }
        SetViewImmediate(showingDeck);
        RectTransform outgoing = showingDeck ? viewDeck : viewBattle;
        RectTransform incoming = deck ? viewDeck : viewBattle;
        float direction = deck ? -1f : 1f;
        float width = incoming.rect.width;
        showingDeck = deck;
        if (deck) SetDeckVisible(true);
        else {
            deckVisibility.interactable = false;
            deckVisibility.blocksRaycasts = false;
            incoming.gameObject.SetActive(true);
        }
        incoming.anchoredPosition = new Vector2(direction * width, 0);
        RefreshNavigation();
        incomingTween = incoming.DOAnchorPosX(0, 0.25f).SetEase(Ease.OutCubic);
        outgoingTween = outgoing.DOAnchorPosX(-direction * width, 0.25f).SetEase(Ease.OutCubic).OnComplete(() => {
            if (deck) outgoing.gameObject.SetActive(false);
            else SetDeckVisible(false);
            outgoing.anchoredPosition = Vector2.zero;
        });
    }

    private void RefreshNavigation() {
        imgDeckSelected.gameObject.SetActive(showingDeck);
        imgBattleSelected.gameObject.SetActive(!showingDeck);
        txtNavDeckLabel.gameObject.SetActive(true);
        txtNavBattleLabel.gameObject.SetActive(true);
        // 美术两态跟随已有视图选择，不改变功能解锁和导航业务。
        GetComponent<MainPanelArtState>()?.SetNavigation(showingDeck ? 1 : 2);
    }

}

