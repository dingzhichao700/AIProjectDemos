using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>锚定道具来源的材料说明浮窗。</summary>
public sealed class ItemTipPanel : BaseTipPanel {
    /******************* UIComponent Define begin ************************/
    public Image imgOutside;
    public RectTransform tipAnchor;
    public RectTransform motionContent;
    public Image imgBody;
    public Image imgPointer;
    public Image imgQualityIcon;
    public Image imgItemIcon;
    public TextMeshProUGUI txtItemName;
    public TextMeshProUGUI txtQuality;
    public RectTransform descriptionViewport;
    public TextMeshProUGUI txtDescription;
    /******************* UIComponent Define finish ************************/

    private ItemTipOpenVO request;
    private CultivationImageLease iconLease;
    private CultivationImageLease frameLease;
    private ScrollRect scroll;
    private Rect sourceRect;
    private Rect safeRect;
    private readonly Vector3[] corners = new Vector3[4];

    public ItemTipPanel() {
        layer = PanelLayer.SCALE_TIP;
    }

    public override bool CanOpen => ItemTipControl.Pending != null && ItemTipControl.Pending.IsValid;
    public override bool UsesWindowTransition => true;
    protected override RectTransform WindowTransitionContent => motionContent;

    public override void OnInit() {
        iconLease = imgItemIcon.gameObject.AddComponent<CultivationImageLease>();
        frameLease = imgQualityIcon.gameObject.AddComponent<CultivationImageLease>();
        imgOutside.raycastTarget = true;
        imgOutside.gameObject.AddComponent<ItemTipClickShield>().panel = this;
        imgBody.raycastTarget = true;
        descriptionViewport.gameObject.AddComponent<RectMask2D>();
        var target = descriptionViewport.gameObject.GetComponent<Image>();
        target.raycastTarget = true;
        scroll = descriptionViewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = descriptionViewport;
        scroll.content = txtDescription.rectTransform;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        txtDescription.raycastTarget = false;
        foreach (var text in new[] { txtItemName, txtQuality, txtDescription }) {
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.richText = false;
            text.margin = Vector4.zero;
        }
        txtDescription.richText = txtItemName.richText = false;
        txtDescription.enableWordWrapping = true;
        txtDescription.overflowMode = TextOverflowModes.Overflow;
        txtDescription.margin = Vector4.zero;
        txtDescription.lineSpacing = 0;
        txtItemName.enableWordWrapping = true;
        txtItemName.overflowMode = TextOverflowModes.Overflow;
        imgBody.sprite = ItemTipAppearance.Body;
        imgBody.type = Image.Type.Sliced;
        imgBody.color = Color.white;
        imgPointer.sprite = ItemTipAppearance.Pointer;
        imgPointer.color = Color.white;
    }

    protected override void OnPrepareOpen() {
        trans.anchorMin = Vector2.zero;
        trans.anchorMax = Vector2.one;
        trans.offsetMin = trans.offsetMax = Vector2.zero;
        Present(ItemTipControl.Pending);
    }

    public override void OnOpen() { }

    public override void OnPostOpen() {
        // 位置由共享安全区与真实来源计算，不使用旧基类的固定尺寸摆放。
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            ItemTipControl.Close();
        }
    }

    public override void OnClose() {
        iconLease.Clear();
        frameLease.Clear();
        ItemTipControl.Released(request);
        request = null;
        scroll.StopMovement();
    }

    public async void Present(ItemTipOpenVO value) {
        if (request == null || request.itemId != value.itemId) {
            iconLease.Clear();
        }
        request = value;
        var item = CfgManager.tables.ItemObj.Get(value.itemId);
        txtItemName.text = item.Name ?? "";
        int quality = (int)item.Quality;
        string[] names = { "", "普通", "普通", "稀有", "史诗", "传奇", "传奇" };
        string[] colors = { "#FFFFFF", "#4FF881", "#4FF881", "#4ABEFF", "#CE67FF", "#FFC744", "#FF8454" };
        txtQuality.text = quality > 0 && quality < names.Length ? names[quality] : "";
        ColorUtility.TryParseHtmlString(quality > 0 && quality < colors.Length ? colors[quality] : "#FFFFFF", out Color tint);
        txtQuality.color = tint;
        txtDescription.text = item.Desc ?? "";
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
        LayoutTip();
        try {
            if (CultivationItemIconResources.TryGetQualityBackgroundAddress(value.resourceMap, value.itemId, quality, out string background)) {
                await frameLease.BindAsync(background);
            } else {
                frameLease.Clear();
            }
            if (!ReferenceEquals(request, value) || !value.IsValid || isClosing) {
                return;
            }
            if (CultivationItemIconResources.TryGetAddress(value.resourceMap, value.itemId, out string address)) {
                await iconLease.BindAsync(address);
            } else {
                iconLease.Clear();
            }
        } catch (System.Exception error) {
            if (ReferenceEquals(request, value) && !isClosing) {
                Debug.LogException(error);
            }
        }
    }

    private Rect SourceBounds() {
        ((RectTransform)request.source.transform).GetWorldCorners(corners);
        Vector3 a = trans.InverseTransformPoint(corners[0]);
        Vector3 b = trans.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    private void LateUpdate() {
        if (request == null || isClosing) {
            return;
        }
        if (!request.IsValid) {
            ItemTipControl.Close();
            return;
        }
        Rect area = UIScreenRegions.LocalContentArea(trans);
        Rect now = SourceBounds();
        if (area != safeRect) {
            LayoutTip();
        } else if ((now.center - sourceRect.center).sqrMagnitude > 1 || (now.size - sourceRect.size).sqrMagnitude > 1) {
            // 来源滚动/复用不追着跳动；用户可在新位置重新点开。
            ItemTipControl.Close();
        }
    }

    /**标题与品质贴齐图标上缘；说明按TMP真实换行增长，到安全区上限后局部滚动。*/
    public void LayoutTip() {
        safeRect = UIScreenRegions.LocalContentArea(trans);
        sourceRect = SourceBounds();
        txtItemName.margin = txtQuality.margin = txtDescription.margin = Vector4.zero;
        float width = Mathf.Min(420, safeRect.width - 16);
        float textWidth = Mathf.Max(40, width - 152);
        float titleHeight = Mathf.Max(36, txtItemName.GetPreferredValues(txtItemName.text, textWidth, 0).y);
        float qualityY = 52 + Mathf.Max(0, titleHeight - 36);
        float descY = qualityY + 35;
        float textHeight = string.IsNullOrEmpty(txtDescription.text) ? 0 : txtDescription.GetPreferredValues(txtDescription.text, textWidth, 0).y + 4;
        float desired = Mathf.Max(113, descY + textHeight) + 18;
        float above = safeRect.yMax - sourceRect.yMax - 8;
        float below = sourceRect.yMin - safeRect.yMin - 8;
        bool showAbove = desired + 12 <= above || above >= below;
        float limit = Mathf.Min(safeRect.height - 28, Mathf.Max(above, below) - 12);
        float height = Mathf.Min(desired, Mathf.Max(descY + 40, limit));
        float visible = Mathf.Max(20, height - descY - 18);
        SetRect(imgBody.rectTransform, 0, 0, width, height);
        SetRect(txtItemName.rectTransform, 128, 11, textWidth, titleHeight);
        SetRect(txtQuality.rectTransform, 128, qualityY, textWidth, 26);
        SetRect((RectTransform)motionContent.Find("imgDivider"), 128, qualityY + 28, textWidth, 1);
        SetRect((RectTransform)motionContent.Find("imgDividerAccent"), 128, qualityY + 28, 25, 1);
        SetRect(descriptionViewport, 128, descY, textWidth, visible);
        SetRect(txtDescription.rectTransform, 0, 0, textWidth, Mathf.Max(visible, textHeight));
        scroll.vertical = textHeight > visible + 1;
        txtDescription.rectTransform.anchoredPosition = Vector2.zero;
        float x = Mathf.Clamp(sourceRect.center.x - 96, safeRect.xMin + 8, safeRect.xMax - width - 8);
        float y = showAbove ? sourceRect.yMax + 8 + height + 12 : sourceRect.yMin - 8;
        y = Mathf.Clamp(y, safeRect.yMin + height + 12 + 8, safeRect.yMax - 8);
        tipAnchor.anchorMin = tipAnchor.anchorMax = new Vector2(.5f, .5f);
        tipAnchor.pivot = new Vector2(0, 1);
        tipAnchor.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
        tipAnchor.sizeDelta = new Vector2(width, height + 12);
        SetRect(motionContent, 0, 0, width, height + 12);
        float pointerX = Mathf.Clamp(sourceRect.center.x - x - 10, 12, width - 32);
        SetRect(imgPointer.rectTransform, pointerX, showAbove ? height - 5 : -10, 20, 15);
        imgPointer.rectTransform.localRotation = Quaternion.Euler(0, 0, showAbove ? 0 : 180);
        AlignVisibleTop(txtItemName);
        AlignVisibleTop(txtQuality);
        AlignVisibleTop(txtDescription);
        Canvas.ForceUpdateCanvases();
    }

    /**按当前字形网格校正字体内置顶留白，不移动滚动内容的零位置。*/
    private static void AlignVisibleTop(TextMeshProUGUI text) {
        if (string.IsNullOrEmpty(text.text)) {
            return;
        }
        text.ForceMeshUpdate();
        float inset = text.rectTransform.rect.yMax - VisibleGlyphTop(text);
        text.margin = new Vector4(0, 1 - inset, 0, 0);
        text.ForceMeshUpdate();
    }

    /**TMP网格包含SDF扩展边；几何对齐使用实际字形bearing而非扩展四边形。*/
    public static float VisibleGlyphTop(TextMeshProUGUI text) {
        float top = float.MinValue;
        for (int i = 0; i < text.textInfo.characterCount; i++) {
            var character = text.textInfo.characterInfo[i];
            if (character.isVisible && character.lineNumber == 0 && character.textElement != null) {
                top = Mathf.Max(top, character.baseLine + character.textElement.glyph.metrics.horizontalBearingY * character.scale);
            }
        }
        return top == float.MinValue ? text.rectTransform.rect.yMax : top;
    }

    private static void SetRect(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
