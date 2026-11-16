using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using UnityEngine;
using cfg;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 卡牌培养界面表现组件。
/// </summary>
public sealed class CardCultivationPanel : BasePanel {
    /******************* UIComponent Define begin ************************/
    public Image imgBackground;
    public Image imgPortrait;
    public Image imgPodium;
    public TextMeshProUGUI txtName;
    public TextMeshProUGUI txtCost;
    public TextMeshProUGUI txtProfession;
    public TextMeshProUGUI txtCounter;
    public Button btnPrevious;
    public Button btnNext;
    public TextMeshProUGUI txtPower;
    public TextMeshProUGUI txtLevel;
    public TextMeshProUGUI txtStageEmpty;
    public TextMeshProUGUI txtHint;
    public ScrollList listSkills;
    public ScrollList listStages;
    public ScrollList listMaterials;
    public Button btnUpgrade;
    public TextMeshProUGUI txtUpgrade;
    public Button btnReturn;
    public ScrollList listFunctionTabs;
    public Image imgRaceBackdrop;
    public TextMeshProUGUI txtQuality;
    public TextMeshProUGUI txtPowerValue;
    public TextMeshProUGUI txtUnitHpCurrent;
    public TextMeshProUGUI txtUnitHpDelta;
    public TextMeshProUGUI txtBuildingHpCurrent;
    public TextMeshProUGUI txtBuildingHpDelta;
    public TextMeshProUGUI txtRangeCurrent;
    public TextMeshProUGUI txtRangeDelta;
    public TextMeshProUGUI txtDamageCurrent;
    public TextMeshProUGUI txtDamageDelta;
    public TextMeshProUGUI txtProductionCurrent;
    public TextMeshProUGUI txtProductionDelta;
    public TextMeshProUGUI txtSpeedCurrent;
    public TextMeshProUGUI txtSpeedDelta;
    public Image imgUnitHpAttribute;
    public TextMeshProUGUI txtUnitHpLabel;
    public Image imgBuildingHpAttribute;
    public TextMeshProUGUI txtBuildingHpLabel;
    public Image imgRangeAttribute;
    public TextMeshProUGUI txtRangeLabel;
    public Image imgDamageAttribute;
    public TextMeshProUGUI txtDamageLabel;
    public Image imgProductionAttribute;
    public TextMeshProUGUI txtProductionLabel;
    public Image imgSpeedAttribute;
    public TextMeshProUGUI txtSpeedLabel;
    public Image imgQualityWordmark;
    public Image imgQualityGlow;
    public Image imgProfession;
    /******************* UIComponent Define finish ************************/

    private CardCultivationOpenVO request;
    private CardCultivationStateVO snapshot;
    private CardArtImage portrait;
    private CultivationViewportLayout viewportLayout;
    private Vector2 portraitAnchor;

    private CultivationImageLease background, qualityWordmark;
    private CultivationExhibitPalette exhibitPalette;
    private Dictionary<int, CultivationImageLease> attributeIcons;

    private int selectedIndex;
    private int lifetime;
    private int artVersion;
    private bool submitting, skillOpening;
    private PowerDisplay powerDisplay;
    public int CurrentCardId => request == null ? 0 : request.orderedCardIds[selectedIndex];
    public string Source => request == null ? null : request.source;

    public CardCultivationPanel() {
        layer = PanelLayer.SCALE_PANEL_SECOND;
    }

    public override bool UsesFullScreenViewport => true;
    public override string[] FullBleedChildren => new[] { "imgBackground", "imgRaceBackdrop" };
    public override Vector2 SafeContentAlignment => new Vector2(.5f, 0);

    public override void OnInit() {
        portrait = imgPortrait.gameObject.AddComponent<CardArtImage>();
        portrait.autoLoadOnEnable = false;
        background = imgRaceBackdrop.gameObject.AddComponent<CultivationImageLease>();
        qualityWordmark = imgQualityWordmark.gameObject.AddComponent<CultivationImageLease>();
        txtQuality.enabled = false;
        exhibitPalette = GetComponent<CultivationExhibitPalette>();
        portraitAnchor = imgPortrait.rectTransform.anchoredPosition;
        imgPortrait.preserveAspect = true;
        viewportLayout = gameObject.AddComponent<CultivationViewportLayout>();
        viewportLayout.Configure(this);
        ConfigureHorizontal(listSkills);
        ConfigureHorizontal(listMaterials);
        ConfigureHorizontal(listFunctionTabs);
        // 已审阶段Viewport的Mask与射线区域由Prefab承担，运行时不重复添加裁切组件。
        ConfigureApprovedTypography();
        attributeIcons = new Dictionary<int, CultivationImageLease> {
            { 1, imgUnitHpAttribute.gameObject.AddComponent<CultivationImageLease>() },
            { 2, imgDamageAttribute.gameObject.AddComponent<CultivationImageLease>() },
            { 3, imgRangeAttribute.gameObject.AddComponent<CultivationImageLease>() },
            { 4, imgSpeedAttribute.gameObject.AddComponent<CultivationImageLease>() },
            { 5, imgBuildingHpAttribute.gameObject.AddComponent<CultivationImageLease>() },
            { 6, imgProductionAttribute.gameObject.AddComponent<CultivationImageLease>() }
        };
    }

    private static string WordmarkAddress(cfg.Quality quality) {
        string name;
        switch (quality) {
            case cfg.Quality.RARE: name = "rare"; break;
            case cfg.Quality.EPIC: name = "epic"; break;
            case cfg.Quality.LEGEND:
            case cfg.Quality.LEGEND_PLUS: name = "legend"; break;
            default: name = "normal"; break;
        }
        return "Assets/Art/atlasSource/default/cardCultivation/quality-" + name + ".png";
    }

    private static void ConfigureHorizontal(ScrollList list) {
        var scroll = list.GetComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        list.horiLimitNum = 0;
        list.vertLimitNum = 1;
    }

    public override void OnOpen() {
        // 从零尺寸池节点重挂后恢复全屏锚点，避免SetParent保留池中的矩形尺寸。
        trans.anchorMin = Vector2.zero;
        trans.anchorMax = Vector2.one;
        trans.offsetMin = trans.offsetMax = Vector2.zero;
        if (openParams == null || openParams.Length != 1 || !(openParams[0] is CardCultivationOpenVO value)) {
            throw new ArgumentException("培养页需要有效来源参数");
        }
        lifetime++;
        imgBackground.enabled = true;
        imgBackground.raycastTarget = true;
        imgBackground.color = new Color32(6, 19, 38, 255);
        request = value;
        selectedIndex = Array.IndexOf(request.orderedCardIds, request.cardId);
        if (selectedIndex < 0) {
            throw new ArgumentException("当前卡牌不在来源列表中");
        }
        imgBackground.rectTransform.anchorMin = Vector2.zero;
        imgBackground.rectTransform.anchorMax = Vector2.one;
        imgBackground.rectTransform.offsetMin = imgBackground.rectTransform.offsetMax = Vector2.zero;
        // 种族背景使用批准稿顶部区域，避免覆盖适配层并拉伸为整屏。
        imgRaceBackdrop.preserveAspect = false;
        viewportLayout.Apply();
        btnReturn.GetComponent<GameButton>().text = "返回阵容";
        btnPrevious.onClick.AddListener(Previous);
        btnNext.onClick.AddListener(Next);
        btnReturn.onClick.AddListener(Return);
        btnUpgrade.onClick.AddListener(Submit);
        CardCollectionControl.ins.model.Changed += Refresh;
        submitting = skillOpening = false;
        listFunctionTabs.array = new[] { "Upgrade" };
        ResetScroll(listSkills);
        ResetScroll(listStages);
        ResetScroll(listMaterials);
        Refresh();
    }

    public override void OnClose() {
        lifetime++;
        artVersion++;
        CardSkillDetailControl.Close();
        btnPrevious.onClick.RemoveListener(Previous);
        btnNext.onClick.RemoveListener(Next);
        btnReturn.onClick.RemoveListener(Return);
        btnUpgrade.onClick.RemoveListener(Submit);
        CardCollectionControl.ins.model.Changed -= Refresh;
        portrait.Clear();
        background.Clear();
        exhibitPalette.ResetState(imgQualityGlow, imgPortrait);
        qualityWordmark.Clear();
        foreach (var lease in attributeIcons.Values) {
            lease.Clear();
        }
        var closed = request;
        request = null;
        snapshot = null;
        openParams = null;
        CardCultivationControl.OnClosed(closed);
        // 清空引用后才通知返回，保证池复用或回调重入也只恢复一次。
        try {
            closed?.onReturn?.Invoke();
        } catch (Exception error) {
            Debug.LogException(error);
        }
    }

    private void Refresh() {
        if (request == null) {
            return;
        }
        snapshot = CardCultivationQuery.Read(CfgManager.tables, PlayerProgressControl.ins, CurrentCardId);
        var labels = new Dictionary<int, TextMeshProUGUI> { { 1, txtUnitHpLabel }, { 2, txtDamageLabel }, { 3, txtRangeLabel }, { 4, txtSpeedLabel }, { 5, txtBuildingHpLabel }, { 6, txtProductionLabel } };
        foreach (var attribute in snapshot.attributes) {
            labels[attribute.attributeId].text = attribute.name;
        }
        txtName.text = snapshot.name;
        txtLevel.text = snapshot.growth.owned ? snapshot.growth.level.ToString() : "1级";
        txtPower.text = "";
        txtPower.gameObject.SetActive(false);
        txtPowerValue.enabled = false;
        if (powerDisplay == null) powerDisplay = GetComponentInChildren<PowerDisplay>(true);
        powerDisplay.SetValue(snapshot.power);
        txtQuality.text = CfgManager.tables.CardObj.Get(CurrentCardId).QualityLabel;
        txtLevel.fontSize = 48;
        txtCost.text = snapshot.constructionCost > 0 ? snapshot.constructionCost.ToString(CultureInfo.InvariantCulture) : "—";
        txtProfession.text = snapshot.profession.HasValue ? ProfessionName(snapshot.profession.Value) : "职业：—";
        FitProfessionBackground();
        txtCounter.text = "";
        txtCounter.gameObject.SetActive(false);
        txtUpgrade.text = snapshot.growth.owned ? "升级" : "解锁";
        btnUpgrade.GetComponent<Image>().color = snapshot.growth.maxed ? new Color(.45f, .45f, .45f) : Color.white;
        btnPrevious.interactable = selectedIndex > 0;
        btnNext.interactable = selectedIndex < request.orderedCardIds.Length - 1;
        btnPrevious.GetComponent<Image>().color = btnPrevious.interactable ? Color.white : Color.gray;
        btnNext.GetComponent<Image>().color = btnNext.interactable ? Color.white : Color.gray;
        RenderAttribute("unit.", ".hp", txtUnitHpCurrent, txtUnitHpDelta);
        RenderAttribute("building.", ".hp", txtBuildingHpCurrent, txtBuildingHpDelta);
        RenderAttribute("unit.", ".basicRange", txtRangeCurrent, txtRangeDelta);
        RenderAttribute("unit.", ".attack", txtDamageCurrent, txtDamageDelta);
        RenderAttribute("building.", ".production", txtProductionCurrent, txtProductionDelta);
        RenderAttribute("unit.", ".speed", txtSpeedCurrent, txtSpeedDelta);
        // 保留完整成本，超过三项由既有横向ScrollList滚动展示。
        listMaterials.array = snapshot.materials;
        ClampEmptyList(listMaterials, snapshot.materials.Count);
        // 材料列表始终从左侧排列；单项也不居中，不生成空材料槽。
        var materialViewport = listMaterials.GetComponent<ScrollRect>().viewport;
        float materialWidth = snapshot.materials.Count == 0 ? 0 : Mathf.Min(365, snapshot.materials.Count * 118 - 8);
        materialViewport.anchorMin = materialViewport.anchorMax = new Vector2(0, 1);
        materialViewport.pivot = new Vector2(0, 1);
        materialViewport.sizeDelta = new Vector2(materialWidth, 110);
        materialViewport.anchoredPosition = Vector2.zero;
        btnUpgrade.interactable = !submitting;
        listStages.array = snapshot.stages;
        ClampEmptyList(listStages, snapshot.stages.Count);
        txtStageEmpty.text = snapshot.stages.Count == 0 ? "阶段资料尚未配置" : "";
        var skills = snapshot.skills.ToList();
        // 正式技能按配置序号展示；缺数据仍保留开发期占位，缺项不作为玩家状态文案。
        foreach (SkillDisplaySlot slot in Enum.GetValues(typeof(SkillDisplaySlot))) {
            if (skills.Count >= 4) {
                break;
            }
            if (!skills.Any(skill => skill.slot == slot)) {
                skills.Add(CardSkillQuery.Read(CfgManager.tables, CurrentCardId, snapshot.growth.level, slot));
            }
        }
        // 顺序只取技能序号，不受首阶解锁等级或当前阶数影响。未配置占位无正式序号，保留在已知项之后。
        listSkills.array = skills.OrderBy(skill => skill.sortOrder > 0 ? skill.sortOrder : int.MaxValue).ToList();
        txtHint.text = "";
        LoadArt(snapshot, lifetime);
    }

    /**只投影既有查询值；多单位不能唯一匹配时显示缺项，不自行合并属性。*/
    private void RenderAttribute(string prefix, string suffix, TextMeshProUGUI current, TextMeshProUGUI delta) {
        var matches = snapshot.attributes.Where(row => row.key.StartsWith(prefix, StringComparison.Ordinal) && row.key.EndsWith(suffix, StringComparison.Ordinal)).ToArray();
        var value = matches.Length == 1 ? matches[0] : null;
        current.text = value == null ? "—" : AttributeNumber(value, value.current);
        current.color = Color.white;
        delta.text = "";
        if (!snapshot.growth.owned || value == null || !value.current.HasValue || !value.target.HasValue || snapshot.growth.maxed) {
            return;
        }
        double change = value.target.Value - value.current.Value;
        if (Math.Abs(change) < .000001) {
            return;
        }
        delta.text = (change > 0 ? "+" : "−") + AttributeNumber(value, Math.Abs(change));
        bool improved = suffix == ".production" ? change < 0 : change > 0;
        delta.color = improved ? new Color32(0, 255, 94, 255) : new Color32(255, 103, 85, 255);
    }

    /**保持批准稿的独立标签边界；阴影使用TMP材质，不增加文字描边。*/
    private void ConfigureApprovedTypography() {
        foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true)) {
            text.enableWordWrapping = text.name == "txtMaterialName";
            text.outlineWidth = 0;
            var material = text.fontMaterial;
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0, .02f, .05f, .75f));
            material.SetFloat("_UnderlayOffsetX", .3f);
            material.SetFloat("_UnderlayOffsetY", -.3f);
            material.SetFloat("_UnderlaySoftness", .1f);
            if (text.name.EndsWith("Current", StringComparison.Ordinal) || text.name == "txtMaterialOwned") {
                text.alignment = TextAlignmentOptions.MidlineRight;
            } else if (text.name.EndsWith("Label", StringComparison.Ordinal) || text.name.EndsWith("Delta", StringComparison.Ordinal) || text.name == "txtName" || text.name == "txtQuality" || text.name == "txtProfession" || text.name == "txtCounter" || text.name == "txtMaterialRequired") {
                text.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }
    }

    /**按当前字体真实可见字形宽度适配底板；保持端部切片及垂直位置。*/
    private void FitProfessionBackground() {
        txtProfession.ForceMeshUpdate(true, true);
        var bounds = txtProfession.textBounds;
        var plate = imgProfession.rectTransform;
        plate.sizeDelta = new Vector2(Mathf.Max(32, Mathf.Ceil(bounds.size.x + 28)), plate.sizeDelta.y);
        var text = txtProfession.rectTransform;
        float actualLeft = text.parent.InverseTransformPoint(text.TransformPoint(new Vector3(bounds.min.x, 0, 0))).x;
        float wantedLeft = text.parent.InverseTransformPoint(plate.TransformPoint(new Vector3(plate.rect.xMin + 14, 0, 0))).x;
        var position = text.localPosition;
        position.x += wantedLeft - actualLeft;
        text.localPosition = position;
    }

    private static string ProfessionName(CardProfessionType type) {
        switch (type) {
            case CardProfessionType.Vanguard: return "先锋";
            case CardProfessionType.Firepower: return "火力";
            case CardProfessionType.Heavy: return "重装";
            default: return "支援";
        }
    }

    private static string AttributeNumber(CultivationAttributeVO row, double? value) {
        if (row.valueType == AttributeValueType.Percentage) {
            return value.HasValue ? Number(value * 100) + "%" : "—";
        }
        if (row.unit == "毫秒") {
            return value.HasValue ? Number(value / 1000) + "秒" : "—";
        }
        return Number(value);
    }

    private static string Number(double? value) {
        return value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "—";
    }

    private async void LoadArt(CardCultivationStateVO state, int token) {
        // 同卡解锁/升级也可能重入刷新，独立版本阻止旧灰化状态在异步完成后回写。
        int artToken = ++artVersion;
        try {
            portrait.SetAnimationActivated(state.growth.owned);
            // 座台是Prefab静态图，不读取历史逐卡座台覆盖或偏移。
            exhibitPalette.Apply(imgQualityGlow, imgPortrait, state.quality, state.growth.owned);
            // 同卡数值刷新保持已加载立绘，换卡/换素材仍走原租约与过期回调保护。
            bool samePortrait = !string.IsNullOrWhiteSpace(state.portraitAddress) && portrait.artId == state.artId && portrait.usage == CardArtImage.Usage.Portrait && ((!portrait.Loading.IsCompleted) || (imgPortrait.enabled && imgPortrait.sprite != null && imgPortrait.sprite == ResourceManager.GetUnpackImage(state.portraitAddress)));
            var portraitTask = string.IsNullOrWhiteSpace(state.portraitAddress) ? Task.CompletedTask : samePortrait ? portrait.Loading : portrait.BindAsync(state.artId, CardArtImage.Usage.Portrait);
            if (string.IsNullOrWhiteSpace(state.portraitAddress)) {
                portrait.Clear();
            }
            await Task.WhenAll(qualityWordmark.BindAsync(WordmarkAddress(state.quality)), portraitTask, background.BindAsync(state.backgroundAddress), Task.WhenAll(state.attributes.Select(attribute => attributeIcons[attribute.attributeId].BindAsync(attribute.iconAddress))));
            if (request != null && token == lifetime && artToken == artVersion) {
                AlignExhibit(state.artId, state.portraitAddress);
                // CardArtImage异步加载完成后再次应用当前状态，过期回调由生命周期令牌拦截。
                exhibitPalette.Apply(imgQualityGlow, imgPortrait, state.quality, state.growth.owned);
            }
        } catch (Exception error) {
            if (this != null && request != null && token == lifetime && artToken == artVersion) {
                GlobalToast.Warn("形象资源加载失败");
                Debug.LogException(error);
            }
        }
    }

    /**整组立绘共用落脚点；使用导入Sprite的语义pivot，等比适配后不再抵消锚点。*/
    private void AlignExhibit(int artId, string portraitAddress) {
        var sprite = imgPortrait.sprite;
        if (sprite == null) {
            return;
        }
        var rt = imgPortrait.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = sprite.pivot / sprite.rect.size;
        if (NativePortraitGeometry.TryGet(artId, sprite, out var native)) {
            rt.sizeDelta = native.canvas;
            // R7同台基准：台面中心为320宽座台图内(160,40)，角色Pivot直接落此点。
            var podium = imgPodium.rectTransform;
            Rect area = imgPodium.GetPixelAdjustedRect();
            float h = area.width * 724f / 2171f;
            float bottom = area.y + (area.height - h) * podium.pivot.y;
            rt.position = podium.TransformPoint(new Vector3(area.center.x, bottom + h - area.width / 8f, 0));
            return;
        }
        var art = CfgManager.tables.CardArtObj.Get(artId);
        float scale = Mathf.Min(220f / sprite.rect.width, 270f / sprite.rect.height) * CultivationPortraitPlacement.ReadScale(art, portraitAddress);
        rt.sizeDelta = sprite.rect.size * scale;
        rt.anchoredPosition = portraitAnchor;
        rt.anchoredPosition += new Vector2(CultivationPortraitPlacement.ReadOffsetX(art, portraitAddress), -CultivationPortraitPlacement.ReadOffsetY(art, portraitAddress));
    }

    private static void ClampEmptyList(ScrollList list, int count) {
        var scroll = list.GetComponent<ScrollRect>();
        scroll.content.sizeDelta = Vector2.Max(Vector2.zero, scroll.content.sizeDelta);
        // 空列表会停用ScrollRect；同级滑块必须同步隐藏，避免保留上一张卡的滚动状态。
        if (scroll.verticalScrollbar != null) {
            scroll.verticalScrollbar.gameObject.SetActive(count > 0 && scroll.content.rect.height > scroll.viewport.rect.height + .01f);
        }
        list.gameObject.SetActive(count > 0);
    }

    private static void ResetScroll(ScrollList list) {
        var scroll = list.GetComponent<ScrollRect>();
        scroll.velocity = Vector2.zero;
        scroll.content.anchoredPosition = Vector2.zero;
    }

    private void Previous() {
        Switch(-1);
    }

    private void Next() {
        Switch(1);
    }

    private void Switch(int delta) {
        int next = selectedIndex + delta;
        if (submitting || skillOpening || request == null || next < 0 || next >= request.orderedCardIds.Length) {
            return;
        }
        selectedIndex = next;
        lifetime++;
        ResetScroll(listSkills);
        ResetScroll(listStages);
        ResetScroll(listMaterials);
        Refresh();
    }

    /**一点击只提交一次；资格、扣料与保存都由既有成长事务决定。*/
    private async void Submit() {
        if (submitting || skillOpening || snapshot == null) {
            return;
        }
        submitting = true;
        int token = lifetime;
        btnUpgrade.interactable = false;
        var state = snapshot.growth;
        try {
            bool success = CardCollectionControl.ins.TryUpgrade(CurrentCardId, state.level, state.revision, out string reason);
            if (success) {
                GlobalToast.Show(state.owned ? "升级成功" : "解锁成功");
            } else {
                // 培养页文案适配不改变共享成长事务的拒绝原因或资格校验。
                string message = reason == "道具不足" ? "材料不足" : reason == "已满级" ? "已升至最大等级" : reason;
                GlobalToast.Warn(message);
            }
            await Task.Yield();
        } finally {
            if (request != null && token == lifetime) {
                submitting = false;
                btnUpgrade.interactable = snapshot != null;
            }
        }
    }

    public async void OpenSkill(CardSkillStateVO skill) {
        if (request == null || submitting || skillOpening || skill.cardId != CurrentCardId) {
            return;
        }
        skillOpening = true;
        int token = lifetime;
        try {
            await CardSkillDetailControl.OpenAsync(CurrentCardId, ToDetailSlot(skill.slot), skill.skillId, () => {
                if (this != null && request != null && token == lifetime) {
                    skillOpening = false;
                }
            });
        } catch (Exception error) {
            if (this != null && request != null && token == lifetime) {
                GlobalToast.Warn("技能详情打开失败，请重试");
                skillOpening = false;
            }
            Debug.LogException(error);
        }
    }

    private static CardSkillSlot ToDetailSlot(SkillDisplaySlot slot) {
        switch (slot) {
            case SkillDisplaySlot.Basic: return CardSkillSlot.Basic;
            case SkillDisplaySlot.Passive: return CardSkillSlot.Passive;
            case SkillDisplaySlot.Active: return CardSkillSlot.Active;
            case SkillDisplaySlot.Ultimate: return CardSkillSlot.Ultimate;
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    private void Return() {
        if (!submitting) {
            Close();
        }
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            Return();
        }
    }

}
