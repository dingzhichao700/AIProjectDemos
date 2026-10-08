using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 精英敌机血条表现
/// </summary>
/// <remarks>
/// 在敌机下方同步浅弧形血条的位置与生命比例。
/// </remarks>
internal sealed class EliteEnemyHealthBarView {

    private readonly RectTransform root;
    private readonly RectTransform fillViewport;

    private EliteEnemyHealthBarView(RectTransform root, RectTransform fillViewport) {
        this.root = root;
        this.fillViewport = fillViewport;
    }

    /**创建不受敌机旋转影响的独立 HUD 节点。*/
    public static EliteEnemyHealthBarView Create(RectTransform parent) {
        RectTransform root = CreateRect("eliteHealthBar", parent, BattleConst.EliteHealthBarSize, Vector2.zero, new Vector2(0f, 1f));
        CreateImage("imgBackground", root, BattleConst.EliteHealthBarSize, BattleConst.EliteHealthBarBackgroundPath);
        RectTransform fillViewport = CreateRect("fillViewport", root, BattleConst.EliteHealthBarSize, new Vector2(-BattleConst.EliteHealthBarSize.x * 0.5f, 0f), new Vector2(0.5f, 0.5f));
        fillViewport.pivot = new Vector2(0f, 0.5f);
        fillViewport.gameObject.AddComponent<RectMask2D>();
        RectTransform fill = CreateImage("imgFill", fillViewport, BattleConst.EliteHealthBarSize, BattleConst.EliteHealthBarFillPath);
        fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        return new EliteEnemyHealthBarView(root, fillViewport);
    }

    /**同步血条跟随位置。*/
    public void SetPosition(Vector2 enemyPosition, float enemyHeight) {
        root.anchoredPosition = enemyPosition + new Vector2(0f, -enemyHeight * 0.5f - BattleConst.EliteHealthBarVerticalGap);
    }

    /**按当前生命比例裁切红色填充。*/
    public void SetHealth(int health, int maxHealth) {
        float ratio = maxHealth > 0 ? Mathf.Clamp01((float)health / maxHealth) : 0f;
        fillViewport.sizeDelta = new Vector2(BattleConst.EliteHealthBarSize.x * ratio, BattleConst.EliteHealthBarSize.y);
    }

    /**移除血条表现。*/
    public void Dispose() {
        if (root != null) {
            Object.Destroy(root.gameObject);
        }
    }

    private static RectTransform CreateImage(string name, RectTransform parent, Vector2 size, string path) {
        BattlePreloadCollector.RequireUnpackImagePreloaded(path);
        RectTransform rect = CreateRect(name, parent, size, Vector2.zero, new Vector2(0.5f, 0.5f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        UITools.SetImage(image, path);
        return rect;
    }

    private static RectTransform CreateRect(string name, RectTransform parent, Vector2 size, Vector2 position, Vector2 anchor) {
        GameObject instance = new GameObject(name, typeof(RectTransform));
        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }
}
