using System;
using System.Collections.Generic;
using System.Globalization;
using cfg;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 显示己方可解锁格的空间类型与费用标记。
/// </summary>
/// <remarks>类型与费用归地面指引层；独立阵营覆盖归地面贴合层，短时操作反馈由Panel调用全局滚动提示；只消费玩法查询，不决定解锁资格。</remarks>
public sealed class BattleUnlockView : IDisposable {

    private sealed class Hint {
        public RectTransform root;
        public Image icon;
        public Image currency;
        public TextMeshProUGUI cost;
    }

    private readonly BattleSceneModel model;

    private readonly RectTransform guides;

    private readonly TMP_FontAsset font;

    private readonly Dictionary<long, Hint> active = new Dictionary<long, Hint>();

    private readonly Stack<Hint> pool = new Stack<Hint>();

    private readonly HashSet<long> visible = new HashSet<long>();

    private readonly List<long> removed = new List<long>();

    public int visibleCount => active.Count;

    public BattleUnlockView(BattleSceneModel model, RectTransform guides, TMP_FontAsset font) {
        this.model = model;
        this.guides = guides;
        this.font = font ?? throw new InvalidOperationException("解锁提示字体未配置。");
    }

    public void Refresh(Rect visibleRect, float zoom) {
        visible.Clear();
        foreach (var cell in model.cells.Values) {
            if (cell.camp != BattleSceneCamp.FRIENDLY || !model.unlocks.IsRevealed(cell) || !new Rect(cell.position - new Vector2(62, 55 + model.config.GroundThickness), new Vector2(124, 110)).Overlaps(visibleRect, true)) {
                continue;
            }
            visible.Add(cell.id);
            if (!active.TryGetValue(cell.id, out Hint hint)) {
                hint = pool.Count > 0 ? pool.Pop() : CreateHint();
                active.Add(cell.id, hint);
                hint.root.name = "unlock_" + cell.id;

                hint.currency.sprite = Sprite(BattleConst.UnlockCurrencyResource);

                hint.currency.rectTransform.sizeDelta = hint.currency.sprite.rect.size;
                hint.root.gameObject.SetActive(true);
            }
            var offer = model.unlocks.OfferedUnit(cell);
            hint.icon.sprite = Sprite(offer?.Image ?? BattleConst.UnlockIconResource(cell.unlockConfig.Id));
            hint.icon.preserveAspect = true;
            hint.icon.rectTransform.sizeDelta = offer != null ? new Vector2(52, 40) : hint.icon.sprite.rect.size;
            int cost = model.unlocks.Cost(cell);
            hint.cost.text = cost.ToString(CultureInfo.InvariantCulture);
            hint.cost.color = model.camps[cell.camp].currency >= cost ? new Color32(255, 231, 119, 255) : new Color32(255, 89, 89, 255);
            // 根节点以地块顶面定位，缩放只由场景根继承一次。
            hint.root.anchoredPosition = new Vector2(cell.position.x, -cell.position.y + model.config.GroundThickness);
            hint.root.localScale = Vector3.one;
        }
        removed.Clear();
        foreach (long id in active.Keys) {
            if (!visible.Contains(id)) {
                removed.Add(id);
            }
        }
        foreach (long id in removed) {
            var hint = active[id];
            active.Remove(id);
            Reset(hint);
            pool.Push(hint);
        }
    }

    private Hint CreateHint() {
        var root = new GameObject("UnlockHint", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(guides, false);
        root.anchorMin = root.anchorMax = new Vector2(0, 1);
        root.pivot = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(BattleConst.CellWidth, BattleConst.CellHeight);
        var icon = CreateImage(root, "type", new Vector2(0, 15));
        var currency = CreateImage(root, "currency", new Vector2(-20, -22));
        var cost = CreateText(root, "cost", new Vector2(54, 32), new Vector2(19, -22), 20);
        cost.alignment = TextAlignmentOptions.MidlineLeft;
        cost.fontStyle = FontStyles.Bold;
        // Instance material: never modify the shared font atlas material.
        cost.fontMaterial = new Material(cost.fontSharedMaterial);
        cost.outlineColor = new Color32(35, 22, 30, 235);
        cost.outlineWidth = .12f;
        return new Hint { root = root, icon = icon, currency = currency, cost = cost };
    }

    private static Image CreateImage(RectTransform parent, string name, Vector2 position) {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        var rect = image.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, Vector2 size, Vector2 position, int fontSize) {
        var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var rect = text.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Normal;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite Sprite(string resource) {
        return ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(resource)) ?? throw new InvalidOperationException("解锁资源未预加载：" + resource);
    }

    private static void Reset(Hint hint) {
        hint.root.gameObject.SetActive(false);
        hint.icon.sprite = hint.currency.sprite = null;
        hint.cost.text = string.Empty;
        hint.cost.color = Color.white;
        hint.root.localPosition = Vector3.zero;
        hint.root.localScale = Vector3.one;
    }

    public void Dispose() {
        foreach (var hint in active.Values) {
            Reset(hint);
            UnityEngine.Object.Destroy(hint.cost.fontMaterial);
            UnityEngine.Object.Destroy(hint.root.gameObject);
        }
        foreach (var hint in pool) {
            Reset(hint);
            UnityEngine.Object.Destroy(hint.cost.fontMaterial);
            UnityEngine.Object.Destroy(hint.root.gameObject);
        }
        active.Clear();
        pool.Clear();
        visible.Clear();
        removed.Clear();
    }

}
