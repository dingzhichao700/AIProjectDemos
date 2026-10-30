using System;
using System.Collections.Generic;
using cfg;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>解锁卡牌场景样板：仅消费本次结果，业务已完成；离屏不补播。</summary>
public sealed class BattleCardRevealView : IDisposable {
    private sealed class Card {
        public RectTransform root;
        public CanvasGroup group;
        public Image frame, background, art;
        public TextMeshProUGUI name;
        public BattleUnlockResultVO result;
        public float started;
    }
    private readonly BattleSceneModel model;
    private readonly RectTransform sceneRoot, lod, container;
    private readonly TMP_FontAsset font;
    private readonly Dictionary<long, Card> active = new Dictionary<long, Card>();
    private readonly Stack<Card> pool = new Stack<Card>();
    private readonly List<long> removed = new List<long>();
    private readonly Vector2 size;
    private readonly float popMs, holdMs, fadeMs, startScale, border, nameHeight, fontSize;
    private Rect visible;
    private float zoom = 1;
    private bool cameraReady;
    public int visibleCount => active.Count;

    public BattleCardRevealView(BattleSceneModel model, RectTransform sceneRoot, RectTransform lod, TMP_FontAsset font) {
        this.model = model;
        this.sceneRoot = sceneRoot;
        this.lod = lod;
        this.font = font;
        var p = BattleConst.CardRevealLayout;
        foreach (var value in p.Values) {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) {
                throw new InvalidOperationException("解锁卡牌样板参数须为有限正数。");
            }
        }
        size = new Vector2(p["width"], p["height"]);
        popMs = p["popMs"]; holdMs = p["holdMs"]; fadeMs = p["fadeMs"];
        startScale = p["startScale"]; border = p["border"]; nameHeight = p["nameHeight"]; fontSize = p["fontSize"];
        if (startScale > 1 || border * 2 >= size.x || border * 2 + nameHeight >= size.y) {
            throw new InvalidOperationException("解锁卡牌样板尺寸非法。");
        }
        foreach (var color in BattleConst.CardRevealColors.Values) {
            if (!ColorUtility.TryParseHtmlString(color, out _)) {
                throw new InvalidOperationException("解锁卡牌品质颜色非法。");
            }
        }
        container = new GameObject("UnlockCardResults", typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(lod, false);
        container.anchorMin = container.anchorMax = new Vector2(.5f, .5f);
        container.anchoredPosition = Vector2.zero;
        model.UnlockCompleted += OnUnlocked;
    }

    private static float Clock(BattleSceneCamp camp) {
        return camp == BattleSceneCamp.FRIENDLY ? RookieEngine.playerTimer.curTime : RookieEngine.enemyTimer.curTime;
    }

    private Vector2 Ground(BattleUnlockResultVO result) {
        return result.position - Vector2.up * model.config.GroundThickness;
    }

    private void OnUnlocked(BattleUnlockResultVO result) {
        if (!cameraReady || model.expired || !result.cardId.HasValue || active.ContainsKey(result.cellId) || !visible.Contains(Ground(result))) {
            return;
        }
        var config = CfgManager.tables.CardObj.Get(result.cardId.Value);
        var sprite = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(config.Image));
        if (sprite == null) {
            throw new InvalidOperationException("解锁卡面未预加载：" + config.Id);
        }
        var card = pool.Count > 0 ? pool.Pop() : Create();
        card.result = result;
        card.started = Clock(result.camp);
        card.root.name = "unlockCard_" + result.cellId;
        card.root.gameObject.SetActive(true);
        card.art.sprite = sprite;
        card.name.text = config.Name;
        card.frame.color = ColorFor(config.Quality.ToString());
        card.background.color = ColorFor("Background");
        card.group.alpha = 1;
        active.Add(result.cellId, card);
        Refresh(visible, zoom);
    }

    private static Color ColorFor(string key) {
        if (!BattleConst.CardRevealColors.TryGetValue(key, out var value) || !ColorUtility.TryParseHtmlString(value, out var color)) {
            throw new InvalidOperationException("缺少卡牌样板颜色：" + key);
        }
        return color;
    }

    public void Refresh(Rect visibleRect, float scale) {
        visible = visibleRect;
        zoom = scale;
        cameraReady = true;
        container.SetAsLastSibling();
        removed.Clear();
        foreach (var pair in active) {
            var card = pair.Value;
            float elapsed = Mathf.Max(0, Clock(card.result.camp) - card.started);
            Vector2 ground = Ground(card.result);
            if (model.expired || elapsed >= popMs + holdMs + fadeMs || !visible.Contains(ground)) {
                removed.Add(pair.Key);
                continue;
            }
            float t = Mathf.Clamp01(elapsed / popMs);
            float popScale = Mathf.Lerp(startScale, 1, 1 - Mathf.Pow(1 - t, 3));
            card.root.localPosition = container.InverseTransformPoint(sceneRoot.TransformPoint(new Vector3(ground.x, -ground.y, 0)));
            card.root.localScale = Vector3.one * (zoom * popScale);
            card.group.alpha = 1 - Mathf.Clamp01((elapsed - popMs - holdMs) / fadeMs);
        }
        foreach (long id in removed) {
            var card = active[id];
            active.Remove(id);
            Reset(card);
            pool.Push(card);
        }
    }

    private Card Create() {
        var root = new GameObject("UnlockCard", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        root.SetParent(container, false);
        root.pivot = new Vector2(.5f, 0);
        root.sizeDelta = size;
        var group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var frame = Image(root, "QualityFrame", size, Vector2.zero);
        var background = Image(root, "Background", size - Vector2.one * border * 2, Vector2.up * border);
        var art = Image(root, "CardArt", new Vector2(size.x - border * 2, size.y - border * 2 - nameHeight), Vector2.up * (border + nameHeight));
        art.preserveAspect = true;
        var label = new GameObject("CardName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(root, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 0);
        label.rectTransform.pivot = new Vector2(.5f, 0);
        label.rectTransform.anchoredPosition = Vector2.up * border;
        label.rectTransform.sizeDelta = new Vector2(size.x - border * 2, nameHeight);
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return new Card { root = root, group = group, frame = frame, background = background, art = art, name = label };
    }

    private static Image Image(RectTransform parent, string name, Vector2 dimensions, Vector2 offset) {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, 0);
        image.rectTransform.pivot = new Vector2(.5f, 0);
        image.rectTransform.sizeDelta = dimensions;
        image.rectTransform.anchoredPosition = offset;
        image.raycastTarget = false;
        return image;
    }

    private static void Reset(Card card) {
        card.root.gameObject.SetActive(false);
        card.art.sprite = null;
        card.name.text = string.Empty;
        card.result = null;
        card.group.alpha = 1;
        card.root.localScale = Vector3.one;
        card.frame.color = card.background.color = Color.white;
    }

    public void Dispose() {
        model.UnlockCompleted -= OnUnlocked;
        foreach (var card in active.Values) {
            Reset(card);
        }
        foreach (var card in pool) {
            Reset(card);
        }
        active.Clear();
        pool.Clear();
        removed.Clear();
        UnityEngine.Object.Destroy(container.gameObject);
    }
}
