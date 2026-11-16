using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 卡牌培养界面表现组件。
/// </summary>
public sealed class CardCultivationMaterialItem : ScrollListItem {
    /******************* UIComponent Define begin ************************/
    public TextMeshProUGUI txtMaterialName;
    public TextMeshProUGUI txtMaterialOwned;
    public TextMeshProUGUI txtMaterialSlash;
    public TextMeshProUGUI txtMaterialRequired;
    public Image imgMaterialIcon;
    /******************* UIComponent Define finish ************************/

    public Sprite temporaryIcon;
    public TextAsset itemResourceMap;

    public int CurrentItemId { get; private set; }

    private CultivationImageLease icon;
    private CultivationImageLease qualityBackground;
    private Image slot;
    private Sprite originalBackground;
    private int iconVersion;

    protected override void OnInitListItem() {
        icon = imgMaterialIcon.gameObject.AddComponent<CultivationImageLease>();
        foreach (var image in GetComponentsInChildren<Image>(true)) {
            if (image.name == "imgMaterialSlot") {
                slot = image;
                break;
            }
        }
        if (slot == null) {
            throw new InvalidOperationException("材料品质背景节点缺失");
        }
        originalBackground = slot.sprite;
        qualityBackground = slot.gameObject.AddComponent<CultivationImageLease>();
        imgMaterialIcon.preserveAspect = true;
        // 来源本身承接点击，子图标/数量保持原有资源与布局。
        var target = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        target.color = Color.clear;
        target.raycastTarget = true;
        OnClick(gameObject, OnMaterialClick);
    }

    private void OnMaterialClick() {
        ItemTipControl.Open(CurrentItemId, gameObject, itemResourceMap);
    }

    private async void LoadIcon(int itemId) {
        int token = ++iconVersion;
        if (CurrentItemId != itemId) {
            icon.Clear();
            qualityBackground.Clear();
            slot.sprite = originalBackground;
            slot.enabled = originalBackground != null;
        }
        CurrentItemId = itemId;
        try {
            int quality = (int)CfgManager.tables.ItemObj.Get(itemId).Quality;
            if (CultivationItemIconResources.TryGetBackgroundAddress(itemResourceMap, itemId, quality, out string backgroundAddress)) {
                await qualityBackground.BindAsync(backgroundAddress);
                if (this == null || token != iconVersion || !isActiveAndEnabled) {
                    return;
                }
            }
            else {
                qualityBackground.Clear();
                slot.sprite = originalBackground;
                slot.enabled = originalBackground != null;
            }
            if (CultivationItemIconResources.TryGetAddress(itemResourceMap, itemId, out string address)) {
                // 交付内图已使用同一B开口裁切Alpha；按原生画布装配，不再次缩小或羽化。
                imgMaterialIcon.rectTransform.anchoredPosition = new Vector2(6, 0);
                imgMaterialIcon.rectTransform.sizeDelta = new Vector2(97, 101);
                await icon.BindAsync(address);
            } else {
                icon.Clear();
                // 未交付的通用道具保留原占位，不能冒用别的卡牌碎片。
                imgMaterialIcon.rectTransform.anchoredPosition = new Vector2(24, -14);
                imgMaterialIcon.rectTransform.sizeDelta = new Vector2(64, 64);
                imgMaterialIcon.sprite = temporaryIcon;
                imgMaterialIcon.enabled = temporaryIcon != null;
                imgMaterialIcon.color = Color.white;
            }
        } catch (Exception error) {
            if (this != null && token == iconVersion && isActiveAndEnabled) {
                icon.Clear();
                Debug.LogException(error);
            }
        }
    }

    protected override void OnSetData(int index, object value) {
        var material = (CultivationMaterialVO)value;
        LoadIcon(material.itemId);
        txtMaterialName.enableWordWrapping = false;
        // 名称由独立道具Tips显示。
        txtMaterialName.text = "";
        txtMaterialName.gameObject.SetActive(false);
        txtMaterialOwned.text = material.owned.ToString();
        txtMaterialSlash.text = "/";
        txtMaterialRequired.text = material.required.ToString();
        txtMaterialOwned.color = material.owned >= material.required ? new Color32(0, 255, 94, 255) : new Color32(255, 103, 85, 255);
        // 数量变化只向左扩展，始终锁定图标右下角；避免多位数量挤出图标右边。
        float right = 88;
        foreach (var label in new[] { txtMaterialRequired, txtMaterialSlash, txtMaterialOwned }) {
            label.fontSize = 22;
            label.fontStyle |= FontStyles.Bold;
            float width = label.GetPreferredValues(label.text).x;
            var rect = label.rectTransform;
            rect.sizeDelta = new Vector2(width, 26);
            right -= width;
            rect.anchoredPosition = new Vector2(right, 0);
            label.alignment = TextAlignmentOptions.MidlineRight;
        }
    }

    private void OnDisable() {
        // 整页入池只停用父节点，ScrollList未必逐项调用Clear；身份必须与图片租约同时清掉。
        if (isInit) { Clear(); }
    }

    public override void Clear() {
        ItemTipControl.CancelSource(gameObject);
        data = null;
        CurrentItemId = 0;
        iconVersion++;
        if (icon != null) { icon.Clear(); }
        if (qualityBackground != null) {
            qualityBackground.Clear();
            slot.sprite = originalBackground;
            slot.enabled = originalBackground != null;
        }
        imgMaterialIcon.sprite = null;
        imgMaterialIcon.enabled = false;
        txtMaterialName.text = txtMaterialOwned.text = txtMaterialSlash.text = txtMaterialRequired.text = "";
    }

}
