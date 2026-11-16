using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 卡牌展示组件，负责完整刷新与未知状态清理。
/// </summary>
/// <remarks>用于上阵位和牌库；不持有编组业务，外部Sprite由调用方持有；配置资源由CardArtImage管理共享租用。</remarks>
public sealed class DeckCardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {

    [Serializable]
    public sealed class DisplayData {

        public int cardId;
        public int artId;
        // 仅美术目录预览，不表示玩家持有或可编组。
        public bool artPreview;
        public int tagId;
        public bool owned = true;
        public bool canUpgrade;
        public bool maxLevel;
        public int? cellUnlockCost;
        // 用途交付记录中的候选锚点和画布，非业务配置；零画布时采用Sprite的正式pivot。
        public Vector2 formationCanvas;
        public Vector2 formationPivot;
        public string title;
        public int quality;
        public string qualityLabel;
        public bool cardFaceReady = true;
        public int level = -1;
        public long fragments = -1;
        public long requiredFragments = -1;
        public bool deployed;
        public Sprite artwork;
        public Sprite roleIcon;

    }

    public bool formation;
    public Image artwork;
    public Image qualityImage;
    public Image roleIcon;
    public Image fragmentFill;
    public GameObject deployed;
    public TextMeshProUGUI title;
    public TextMeshProUGUI level;
    public TextMeshProUGUI fragments;
    public TextMeshProUGUI placeholder;
    public Button button;
    public Sprite[] qualitySprites;
    public Sprite artPreviewFrame;
    public Sprite emptyStage;
    public Image lockIcon;
    public Image upgradeArrow;
    public Material lockedFrameMaterial;
    public Sprite lockedQualitySprite;
    public GameObject cellCostBadge;
    public TextMeshProUGUI cellCostLabel;
    public Vector2 artAnchor = new Vector2(80, -208);
    public Vector2 artBounds = new Vector2(149, 208);

    private CardArtImage artLoader;
    private CardArtImage tagLoader;
    private int requestedTagId;
    private System.Threading.Tasks.Task tagTask = System.Threading.Tasks.Task.CompletedTask;
    private int artVersion;
    private int requestedArtId;
    private bool requestedFormation;
    public System.Threading.Tasks.Task ArtLoading { get; private set; } = System.Threading.Tasks.Task.CompletedTask;
    private DisplayData current;
    private Action<int> clicked;

    private int? pressedPointer;
    private Vector3 restingArtScale;

    public void OnPointerDown(PointerEventData e) {
        if (!formation || pressedPointer.HasValue || current == null || current.cardId <= 0 ||
            e.button != PointerEventData.InputButton.Left || !button.IsInteractable()) return;
        pressedPointer = e.pointerId;
        restingArtScale = artwork.rectTransform.localScale;
        artwork.rectTransform.localScale = restingArtScale * 0.95f;
    }

    public void OnPointerUp(PointerEventData e) {
        if (pressedPointer == e.pointerId) RestoreArtScale();
    }

    public void OnPointerExit(PointerEventData e) {
        if (pressedPointer == e.pointerId) RestoreArtScale();
    }

    private void RestoreArtScale() {
        if (!pressedPointer.HasValue) return;
        artwork.rectTransform.localScale = restingArtScale;
        pressedPointer = null;
    }

    private void OnEnable() {
        button.onClick.AddListener(OnClick);
        if (Application.isPlaying && current != null) {
            Render(current, clicked);
        }
    }

    private void OnDisable() {
        RestoreArtScale();
        button.onClick.RemoveListener(OnClick);
        artVersion++;
        requestedArtId = 0;
        requestedTagId = 0;
        if (tagLoader != null) {
            tagLoader.artId = 0;
            tagLoader.Clear();
        }
        if (artLoader != null) {
            artLoader.artId = 0;
            artLoader.Clear();
        }
    }

    /**每次复用先清空所有可变状态，不能残留上一张卡的图像或进度。*/
    public void Render(DisplayData value, Action<int> onClick) {
        // ScrollList会反复提交仍在视窗内的数据；同资源保留图片及在途请求。
        bool reuseArt = Application.isPlaying && isActiveAndEnabled && value != null && value.artId > 0 && value.artwork == null && requestedArtId == value.artId && requestedFormation == formation;
        if (!reuseArt) {
            artVersion++;
            requestedArtId = 0;
            ArtLoading = System.Threading.Tasks.Task.CompletedTask;
            if (artLoader != null) {
                artLoader.artId = 0;
                artLoader.Clear();
            }
        }
        if (current?.cardId != value?.cardId) RestoreArtScale();
        current = value;
        if (artLoader != null) { artLoader.SetAnimationActivated(value != null && value.owned); }
        clicked = onClick;
        title.text = value == null ? string.Empty : value.title;
        var feedback = GetComponent<GameButton>();
        if (feedback != null) {
            feedback.Label = title.text;
        }
        bool locked = !formation && value != null && !value.owned;
        if (locked) {
            // 卡面中央承担锁定提示，名称行只显示实际等级。
            level.text = string.Empty;
        } else {
            level.text = value != null && value.level == 0 ? "未解锁" : value == null || value.level < 0 ? "Lv.—" : "Lv." + value.level;
        }
        if (formation) {
            title.gameObject.SetActive(value != null);
            level.gameObject.SetActive(value != null);
        }
        button.interactable = value != null && value.cardId > 0;
        if (feedback != null) {
            feedback.Interactable = button.interactable;
        }
        deployed.SetActive(!formation && value != null && value.owned && value.deployed);
        if (upgradeArrow != null) {
            upgradeArrow.gameObject.SetActive(!formation && value != null && value.owned && value.canUpgrade && !value.maxLevel);
        }
        if (lockIcon != null) {
            lockIcon.gameObject.SetActive(locked);
        }
        if (cellCostBadge != null) {
            cellCostBadge.SetActive(value != null);
            cellCostLabel.text = value?.cellUnlockCost?.ToString() ?? "—";
        }
        if (!formation) {
            qualityImage.material = locked && lockedQualitySprite == null ? lockedFrameMaterial : null;
        }
        bool reuseTag = Application.isPlaying && isActiveAndEnabled && value != null && value.tagId > 0 && value.roleIcon == null && requestedTagId == value.tagId;
        if (!reuseTag) {
            requestedTagId = 0;
            if (tagLoader != null) {
                tagLoader.artId = 0;
                tagLoader.Clear();
            }
            roleIcon.sprite = value == null ? null : value.roleIcon;
            roleIcon.enabled = roleIcon.sprite != null;
        }
        if (!reuseArt) {
            artwork.sprite = value == null ? null : value.artwork;
            artwork.enabled = artwork.sprite != null;
        }
        artwork.color = value != null && !value.owned ? new Color(.5f, .5f, .5f, 1) : Color.white;
        artwork.preserveAspect = true;
        UpdatePlaceholder(value);
        int quality = value == null ? 0 : Mathf.Clamp(value.quality - 2, 0, qualitySprites.Length - 1);
        qualityImage.sprite = value != null && value.artPreview && value.quality == 0 ? artPreviewFrame : qualitySprites[quality];
        if (formation && value == null) {
            qualityImage.sprite = emptyStage;
        }
        if (locked && lockedQualitySprite != null) {
            qualityImage.sprite = lockedQualitySprite;
        }
        qualityImage.color = Color.white;
        if (formation) {
            var baseItem = qualityImage.GetComponentInParent<DeckFormationBase>();
            if (baseItem != null && baseItem.isInit) {
                baseItem.Refresh(qualityImage.sprite, qualityImage.color, level.text, value != null);
            }
        }
        var foreground = formation ? GetComponent<FormationSlotInfo>() : null;
        if (foreground != null) {
            foreground.Refresh(value != null, value == null ? 0 : value.tagId);
        }
        // 碎片进度仅属于牌库；上阵位刷新时也必须保持隐藏。
        fragments.enabled = !formation;
        fragments.text = "—/—";
        fragmentFill.fillAmount = 0;
        fragmentFill.enabled = false;
        if (!formation && value != null && value.fragments >= 0 && value.requiredFragments > 0) {
            fragments.text = value.fragments + "/" + value.requiredFragments;
            fragmentFill.fillAmount = Mathf.Clamp01((float)value.fragments / value.requiredFragments);
            fragmentFill.enabled = value.fragments > 0;
        }
        if (!formation && value != null && value.maxLevel) {
            fragments.text = "已满级";
            fragmentFill.enabled = false;
        }
        ApplyArtLayout(value);
        if (!reuseArt && Application.isPlaying && isActiveAndEnabled && value != null && value.artId > 0 && value.artwork == null) {
            requestedArtId = value.artId;
            requestedFormation = formation;
            ArtLoading = LoadArtAsync(value, artVersion);
        }
        if (!reuseTag && Application.isPlaying && isActiveAndEnabled && value != null && value.tagId > 0 && value.roleIcon == null) {
            requestedTagId = value.tagId;
            tagTask = LoadTagAsync(value.tagId);
            ArtLoading = System.Threading.Tasks.Task.WhenAll(ArtLoading, tagTask);
        }
    }

    private async System.Threading.Tasks.Task LoadTagAsync(int id) {
        await System.Threading.Tasks.Task.Yield();
        if (this == null || !isActiveAndEnabled || requestedTagId != id) {
            return;
        }
        if (tagLoader == null) {
            tagLoader = roleIcon.GetComponent<CardArtImage>() ?? roleIcon.gameObject.AddComponent<CardArtImage>();
            tagLoader.autoLoadOnEnable = false;
        }
        await tagLoader.BindAsync(id, CardArtImage.Usage.Tag);
    }

    private void UpdatePlaceholder(DisplayData value) {
        string label = "";
        if (value == null) {
            label = string.Empty;
        } else if (!artwork.enabled) {
            label = formation ? "立绘待接入" : value != null && !value.cardFaceReady ? "卡面待接入" : "卡面加载中";
        } else if (!formation && !value.owned) {
            label = value.canUpgrade ? "可解锁" : "未解锁";
        } else if (!formation && value.canUpgrade && upgradeArrow == null) {
            label = "可升级";
        } else if (!formation && !value.cardFaceReady) {
            label = "卡面候选";
        }
        placeholder.text = label;
        placeholder.gameObject.SetActive(label.Length > 0);
    }

    /**旧请求不得回填复用项；共享资源的获得和释放只交给CardArtImage。*/
    private async System.Threading.Tasks.Task LoadArtAsync(DisplayData value, int request) {
        // 父级OnEnable时子Image可能尚未启用；下一次主线程续体再开始加载。
        await System.Threading.Tasks.Task.Yield();
        if (this == null || !isActiveAndEnabled || request != artVersion) {
            return;
        }
        if (artLoader == null) {
            artLoader = artwork.GetComponent<CardArtImage>();
            if (artLoader == null) {
                artLoader = artwork.gameObject.AddComponent<CardArtImage>();
            }
        }
        try {
            artLoader.autoLoadOnEnable = false;
            artLoader.SetAnimationActivated(value.owned);
            await artLoader.BindAsync(value.artId, formation ? CardArtImage.Usage.Formation : CardArtImage.Usage.Card);
            if (this == null || !isActiveAndEnabled || request != artVersion) {
                return;
            }
            ApplyArtLayout(current);
            UpdatePlaceholder(current);
        } catch (Exception exception) {
            if (this != null && isActiveAndEnabled && request == artVersion) {
                requestedArtId = 0;
                placeholder.text = "资源加载失败";
                placeholder.gameObject.SetActive(true);
                Debug.LogException(exception, this);
            }
        }
    }

    private void ApplyArtLayout(DisplayData value) {
        if (!artwork.enabled || !formation) {
            return;
        }
        Sprite sprite = artwork.sprite;
        bool candidate = value != null && value.formationCanvas.x > 0 && value.formationCanvas.y > 0;
        if (!candidate && value != null && FormationDisplayLayout.TryApply(value.cardId, artwork, qualityImage, artAnchor)) {
            return;
        }
        // 未定档或独立美术候选保留原显示，不从画布比例推测概念体型。
        float scale = Mathf.Min(artBounds.x / sprite.rect.width, artBounds.y / sprite.rect.height);
        artwork.rectTransform.pivot = candidate ? value.formationPivot : sprite.pivot / sprite.rect.size;
        artwork.rectTransform.sizeDelta = candidate ? value.formationCanvas : sprite.rect.size * scale;
        artwork.rectTransform.anchoredPosition = artAnchor;
        FormationDisplayLayout.AlignToStage(artwork, qualityImage);
    }

    private void OnClick() {
        if (current != null && current.cardId > 0) {
            clicked?.Invoke(current.cardId);
        }
    }

}
