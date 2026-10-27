using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面功能锁图标与条件提示。
/// </summary>
/// <remarks>复用已批准的导航节点和字体，只渲染业务层提供的状态。</remarks>
public sealed class MainFunctionNavigationView {

    private readonly Dictionary<int, GameObject> locks = new Dictionary<int, GameObject>();

    private readonly Dictionary<int, Image> icons = new Dictionary<int, Image>();

    private readonly Dictionary<int, TMP_Text> labels = new Dictionary<int, TMP_Text>();

    private readonly RectTransform root;

    private readonly TMP_FontAsset font;

    private GameObject hintBox;

    private TMP_Text hintText;

    public MainFunctionNavigationView(MainPanel panel) {
        root = panel.trans;
        font = panel.txtNavBattleLabel.font;
        Image lockTemplate = panel.btnShop.transform.Find("imgShopLock").GetComponent<Image>();
        Bind(panel.btnShop, FunctionConst.Shop, "Shop", lockTemplate);
        Bind(panel.btnDeck, FunctionConst.Deck, "Deck", lockTemplate);
        Bind(panel.btnBattle, FunctionConst.Battle, "Battle", lockTemplate);
        Bind(panel.btnTerritory, FunctionConst.Territory, "Territory", lockTemplate);
        Bind(panel.btnRanking, FunctionConst.Ranking, "Ranking", lockTemplate);
    }

    /**锁定仍允许点击；交互许可由业务 API 判断，锁图标不拦截输入。*/
    private void Bind(Button button, int id, string suffix, Image template) {
        Transform lockTransform = button.transform.Find("img" + suffix + "Lock");
        if (lockTransform == null) {
            Image copy = UnityEngine.Object.Instantiate(template, button.transform, false);
            copy.name = "img" + suffix + "Lock";
            lockTransform = copy.transform;
        }
        lockTransform.GetComponent<Image>().raycastTarget = false;
        locks.Add(id, lockTransform.gameObject);
        icons.Add(id, button.transform.Find("img" + suffix + "Icon").GetComponent<Image>());
        labels.Add(id, button.transform.Find("txtNav" + suffix + "Label").GetComponent<TMP_Text>());
        button.interactable = true;
    }

    public void Refresh(FunctionModel model) {
        foreach (var pair in locks) {
            bool unlocked = model.IsUnlocked(pair.Key);
            pair.Value.SetActive(!unlocked);
            icons[pair.Key].color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1);
            labels[pair.Key].text = model.GetConfig(pair.Key).Name;
        }
    }

    /**提示固定在导航上方；重复点击替换内容，切页或关闭时立即隐藏。*/
    public void ShowHint(string message) {
        if (hintBox == null) {
            hintBox = new GameObject("boxFunctionHint", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = (RectTransform)hintBox.transform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 170);
            hintBox.GetComponent<Image>().color = new Color(0.025f, 0.06f, 0.09f, 0.97f);
            hintBox.GetComponent<Button>().onClick.AddListener(HideHint);
            GameObject textObject = new GameObject("txtFunctionHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18, 14);
            textRect.offsetMax = new Vector2(-18, -14);
            hintText = textObject.GetComponent<TextMeshProUGUI>();
            hintText.font = font;
            hintText.fontSize = 24;
            hintText.color = new Color(0.8f, 0.95f, 1);
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.raycastTarget = false;
            hintText.richText = false;
        }
        float width = Mathf.Min(540, root.rect.width - 40);
        hintText.text = message;
        float height = Mathf.Max(90, hintText.GetPreferredValues(message, width - 36, Mathf.Infinity).y + 28);
        ((RectTransform)hintBox.transform).sizeDelta = new Vector2(width, Mathf.Ceil(height));
        hintBox.transform.SetAsLastSibling();
        hintBox.SetActive(true);
        RookieEngine.timer.Clear(this, HideHint);
        RookieEngine.timer.Once(this, 4000, HideHint);
    }

    public void HideHint() {
        RookieEngine.timer.Clear(this, HideHint);
        if (hintBox != null) {
            hintBox.SetActive(false);
        }
    }

}
