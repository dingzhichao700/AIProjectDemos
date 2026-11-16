using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卡牌培养页上方的技能详情模态窗口。
/// </summary>
/// <remarks>显示正式配置查询结果，不计算技能资格或修改培养状态。</remarks>
public sealed class CardSkillDetailPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public Image imgMask;
    public RectTransform boxWindow;
    public Image imgWindowBg;
    public TextMeshProUGUI txtTitle;
    public TextMeshProUGUI txtType;
    public Button btnClose;
    public ScrollList listStages;
    public TextMeshProUGUI txtSummary;
    public TextMeshProUGUI txtMissing;
    public Image imgSkillIcon;
    /******************* UIComponent Define finish ************************/

    private CardSkillDetailOpenVO request;

    private CardCollectionModel cards;

    private Image typeBadge;

    private Material passiveTypeBadgeMaterial;

    private CultivationImageLease skillIconLease;

    public CardSkillDetailPanel() {
        layer = PanelLayer.SCALE_PANEL_THIRD;
    }

    public override bool UsesWindowTransition => true;
    protected override RectTransform WindowTransitionContent => boxWindow;
    protected override Image WindowTransitionMask => imgMask;

    public override bool UsesFullScreenViewport => true;

    public override string[] FullBleedChildren => new[] { "imgMask" };

    public override Vector2 SafeContentDesignSize => new Vector2(516, 436);

    /**遮罩覆盖实际舞台，内容单独居中交给公共安全区适配，避免边缘漏点。*/
    public override void OnInit() {
        trans.anchorMin = Vector2.zero;
        trans.anchorMax = Vector2.one;
        trans.offsetMin = trans.offsetMax = Vector2.zero;
        boxWindow.anchorMin = boxWindow.anchorMax = boxWindow.pivot = new Vector2(.5f, .5f);
        boxWindow.anchoredPosition = Vector2.zero;
        imgMask.raycastTarget = true;
        // imgWindowBg只负责语义窗口命中；独立imgBg保留完整透明外晕，不扩大内部命中区。
        imgWindowBg.raycastTarget = true;
        skillIconLease = imgSkillIcon.GetComponent<CultivationImageLease>() ?? imgSkillIcon.gameObject.AddComponent<CultivationImageLease>();
        imgSkillIcon.preserveAspect = true;
        imgSkillIcon.color = Color.white;
        imgSkillIcon.enabled = false;
        // 底板不加入新的导出成员；缓存既有命名节点用于类型文字自适应。
        typeBadge = boxWindow.Find("imgTypeBg").GetComponent<Image>();
        typeBadge.type = Image.Type.Sliced;
        passiveTypeBadgeMaterial = typeBadge.material;
        txtType.enableWordWrapping = false;
        txtType.enableAutoSizing = false;
        ConfigureText(txtSummary, 18);
        ConfigureText(txtMissing, 12);
        txtTitle.overflowMode = TextOverflowModes.Ellipsis;
        txtMissing.overflowMode = TextOverflowModes.Overflow;
    }

    private static void ConfigureText(TextMeshProUGUI text, float size) {
        text.fontSize = size;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    /**只订阅当前来源数据；弹窗打开和关闭均不调用培养页切卡或升级。*/
    public override void OnOpen() {
        AddLis();
    }

    protected override void OnPrepareOpen() {
        if (openParams == null || openParams.Length != 1 || !(openParams[0] is CardSkillDetailOpenVO value)) {
            throw new ArgumentException("技能详情需要CardSkillDetailOpenVO打开参数");
        }
        // UIFactory先创建无父节点实例，PanelMgr随后挂入舞台；此时才能确定全屏边界。
        trans.anchorMin = Vector2.zero;
        trans.anchorMax = Vector2.one;
        trans.offsetMin = trans.offsetMax = Vector2.zero;
        GetComponent<PanelSafeContent>().Apply();
        request = value;
        cards = CardCollectionControl.ins.model;
        Refresh();
    }

    public override void OnClose() {
        RemoveLis();
        var callback = request == null ? null : request.onClosed;
        request = null;
        cards = null;
        openParams = null;
        skillIconLease.Clear();
        // 先清理来源引用，回调即使重新打开窗口也不会继承旧的关闭回调。
        try {
            callback?.Invoke();
        } catch (Exception error) {
            // 来源通知异常不能中断BasePanel随后执行的回收与层级恢复。
            Debug.LogException(error);
        }
    }

    private void AddLis() {
        btnClose.onClick.AddListener(RequestClose);
        OnClick(imgMask.gameObject, RequestClose);
        cards.Changed += Refresh;
    }

    private void RemoveLis() {
        btnClose.onClick.RemoveListener(RequestClose);
        OffClick(imgMask.gameObject, RequestClose);
        if (cards != null) {
            cards.Changed -= Refresh;
        }
    }

    private void RequestClose() {
        if (request != null && isOpened) {
            Close();
        }
    }

    private void Refresh() {
        if (request == null) {
            return;
        }
        var state = cards.GetState(request.cardId);
        Render(CardSkillDetailQuery.Read(CfgManager.tables, request.cardId, state.level, request.slot, request.skillId));
    }

    /**视图只消费快照，测试阶数数据和未来正式阶数查询共用同一渲染路径。*/
    public void Render(CardSkillDetailVO value) {
        if (value == null) {
            throw new ArgumentNullException(nameof(value));
        }
        txtTitle.text = value.skillName;
        // 详情只显示底层主动/被动类别，入口包装仍由培养列表负责。
        txtType.text = CardSkillDetailQuery.TypeName(value.slot);
        // 材质引用由Prefab持有；主动还原原蓝色，被动只变色不改变Sprite轮廓。
        typeBadge.material = value.slot == CardSkillSlot.Passive ? passiveTypeBadgeMaterial : null;
        LayoutTypeBadge();
        // 调用方Sprite继续由外部持有；正式图标地址复用培养资源租用，避免重开串图或提早释放。
        skillIconLease.Clear();
        if (value.icon != null) {
            imgSkillIcon.sprite = value.icon;
            imgSkillIcon.enabled = true;
        } else if (!string.IsNullOrWhiteSpace(value.iconAddress)) {
            LoadSkillIcon(value.iconAddress);
        }
        bool hasStages = value.stages != null && value.stages.Count > 0;
        listStages.gameObject.SetActive(hasStages);
        txtSummary.gameObject.SetActive(!hasStages);
        txtSummary.text = value.battleSummary ?? string.Empty;
        listStages.array = value.stages;
        var scroll = listStages.GetComponent<ScrollRect>();
        scroll.StopMovement();
        scroll.content.anchoredPosition = Vector2.zero;
        bool hasMissing = value.missing != null && value.missing.Count > 0;
        txtMissing.text = hasMissing ? "数据缺项：" + string.Join("、", value.missing) : string.Empty;
        txtMissing.gameObject.SetActive(hasMissing);
        LayoutDisplay(hasStages, hasMissing);
    }

    /**租用组件负责在切换/关闭时废弃过期结果；不销毁调用方直接传入的Sprite。*/
    private async void LoadSkillIcon(string address) {
        try {
            await skillIconLease.BindAsync(address);
        } catch (Exception error) {
            Debug.LogException(error);
        }
    }

    /**按字体实际测量宽度加左右留白；九宫格只拉伸中段，保留端帽与透明外晕。*/
    private void LayoutTypeBadge() {
        const int sidePadding = 18;
        const int glowPadding = 4;
        int textWidth = Mathf.CeilToInt(txtType.GetPreferredValues(txtType.text).x);
        int labelWidth = textWidth + 2;
        int badgeWidth = Mathf.Max(44, textWidth + (sidePadding + glowPadding) * 2);
        var background = typeBadge.rectTransform;
        background.sizeDelta = new Vector2(badgeWidth, 41);
        txtType.rectTransform.sizeDelta = new Vector2(labelWidth, 28);
        txtType.rectTransform.anchoredPosition = new Vector2(
            Mathf.Round(background.anchoredPosition.x + (badgeWidth - labelWidth) * .5f), -66);
    }

    /**缺项单列，不覆盖阶数效果；有缺项时列表仍可滚动查看全部阶。*/
    private void LayoutDisplay(bool hasStages, bool hasMissing) {
        var listRect = (RectTransform)listStages.transform;
        var footer = txtMissing.rectTransform;
        listRect.sizeDelta = new Vector2(480, 299);
        footer.anchoredPosition = new Vector2(33, -329);
        footer.sizeDelta = new Vector2(450, 90);
        if (hasStages && hasMissing) {
            int height = Mathf.Clamp(Mathf.CeilToInt(txtMissing.GetPreferredValues(txtMissing.text, 450, 0).y) + 4, 18, 90);
            int listHeight = 299 - height - 6;
            listRect.sizeDelta = new Vector2(480, listHeight);
            footer.anchoredPosition = new Vector2(33, -(116 + listHeight + 6));
            footer.sizeDelta = new Vector2(450, height);
        }
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            RequestClose();
        }
    }

}
