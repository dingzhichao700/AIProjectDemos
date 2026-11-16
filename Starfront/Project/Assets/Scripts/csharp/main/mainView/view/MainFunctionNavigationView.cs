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

    public MainFunctionNavigationView(MainPanel panel) {
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
            labels[pair.Key].text = NavigationTitle(pair.Key, model.GetConfig(pair.Key).Name);
        }
    }

    private static string NavigationTitle(int id, string fallback) {
        switch (id) {
            case FunctionConst.Deck: return "阵容";
            case FunctionConst.Battle: return "竞技";
            case FunctionConst.Territory: return "科技";
            case FunctionConst.Ranking: return "联盟";
            default: return fallback;
        }
    }

}
