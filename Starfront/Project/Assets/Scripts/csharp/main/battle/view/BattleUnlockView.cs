using System;
using System.Collections.Generic;
using System.Globalization;
using cfg;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 显示可扩展格的光效、类型、费用及交互反馈。
/// </summary>
/// <remarks>光效归地面指引层，文字图标归LOD；只消费玩法查询，不决定解锁资格。</remarks>
public sealed class BattleUnlockView : IDisposable {

    private sealed class Hint {
        public RectTransform root;
        public Image glow;
        public Image icon;
        public Image currency;
        public TextMeshProUGUI cost;
    }

    private readonly BattleSceneModel model;

    private readonly RectTransform sceneRoot;

    private readonly RectTransform lod;

    private readonly RectTransform guides;

    private readonly TMP_FontAsset font;

    private readonly Dictionary<long, Hint> active = new Dictionary<long, Hint>();

    private readonly Stack<Hint> pool = new Stack<Hint>();

    private readonly HashSet<long> visible = new HashSet<long>();

    private readonly List<long> removed = new List<long>();

    private readonly TextMeshProUGUI message;

    private double phase;

    private float messageRemaining;

    private long messageCellId;

    public int visibleCount => active.Count;

    public BattleUnlockView(BattleSceneModel model, RectTransform sceneRoot, RectTransform lod, RectTransform guides, TMP_FontAsset font) {
        this.model = model;
        this.sceneRoot = sceneRoot;
        this.lod = lod;
        this.guides = guides;
        this.font = font ?? throw new InvalidOperationException("解锁提示字体未配置。");
        message = CreateText(lod, "unlockMessage", new Vector2(260, 36), Vector2.zero, 24);
        message.alignment = TextAlignmentOptions.Center;
        message.color = new Color32(255, 230, 130, 255);
        message.gameObject.SetActive(false);
        RookieEngine.timer.AddUpdateListener(this, OnUiTime);
    }

    /**呼吸与短时反馈只使用通用UI计时，不改变场景或生产时间。*/
    private void OnUiTime(float deltaTime) {
        phase = (phase + deltaTime / BattleConst.UnlockPulseSeconds) % 1;
        messageRemaining = Mathf.Max(0, messageRemaining - deltaTime);
        if (messageRemaining == 0) {
            message.gameObject.SetActive(false);
        }
    }

    public void ShowMessage(long cellId, string reason) {
        messageCellId = cellId;
        messageRemaining = BattleConst.InteractionHintSeconds;
        message.text = reason;
        message.gameObject.SetActive(true);
    }

    public void Refresh(Rect visibleRect, float zoom) {
        visible.Clear();
        float alpha = Mathf.Lerp(BattleConst.UnlockMinAlpha, 1, (1 - Mathf.Cos((float)phase * Mathf.PI * 2)) / 2);
        foreach (var cell in model.cells.Values) {
            if (cell.camp != BattleSceneCamp.FRIENDLY || !model.unlocks.IsRevealed(cell) || !new Rect(cell.position - new Vector2(62, 55), new Vector2(124, 110)).Overlaps(visibleRect, true)) {
                continue;
            }
            visible.Add(cell.id);
            if (!active.TryGetValue(cell.id, out Hint hint)) {
                hint = pool.Count > 0 ? pool.Pop() : CreateHint();
                active.Add(cell.id, hint);
                hint.root.name = "unlock_" + cell.id;
                hint.glow.name = "unlockGlow_" + cell.id;
                hint.glow.sprite = Sprite(BattleConst.UnlockGlowResource);
                hint.icon.sprite = Sprite(BattleConst.UnlockIconResource(cell.unlockConfig.Id));
                hint.currency.sprite = Sprite(BattleConst.UnlockCurrencyResource);
                hint.glow.rectTransform.sizeDelta = hint.glow.sprite.rect.size;
                hint.icon.rectTransform.sizeDelta = hint.icon.sprite.rect.size;
                hint.currency.rectTransform.sizeDelta = hint.currency.sprite.rect.size;
                hint.root.gameObject.SetActive(true);
                hint.glow.gameObject.SetActive(true);
            }
            hint.cost.text = cell.unlockConfig.Cost.ToString(CultureInfo.InvariantCulture);
            hint.cost.color = model.camps[cell.camp].currency >= cell.unlockConfig.Cost ? new Color32(255, 231, 119, 255) : new Color32(255, 89, 89, 255);
            hint.glow.color = new Color(1, 1, 1, alpha);
            hint.glow.rectTransform.anchoredPosition = new Vector2(cell.position.x, -cell.position.y);
            hint.glow.rectTransform.localScale = Vector3.one;
            PositionLod(hint.root, cell.position, zoom);
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
        var messageCell = model.GetCell(messageCellId);
        if (messageRemaining > 0 && messageCell != null) {
            PositionLod(message.rectTransform, messageCell.position + new Vector2(0, -56), zoom);
            message.gameObject.SetActive(visibleRect.Contains(messageCell.position));
            message.transform.SetAsLastSibling();
        }
    }

    private void PositionLod(RectTransform rect, Vector2 position, float zoom) {
        rect.localPosition = lod.InverseTransformPoint(sceneRoot.TransformPoint(new Vector3(position.x, -position.y, 0)));
        rect.localScale = Vector3.one * zoom;
    }

    private Hint CreateHint() {
        var root = new GameObject("UnlockHint", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(lod, false);
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(BattleConst.CellWidth, BattleConst.CellHeight);
        var glow = CreateImage(guides, "UnlockGlow", Vector2.zero);
        glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0, 1);
        var icon = CreateImage(root, "type", new Vector2(0, 15));
        var currency = CreateImage(root, "currency", new Vector2(-20, -22));
        var cost = CreateText(root, "cost", new Vector2(54, 27), new Vector2(19, -22), 20);
        cost.alignment = TextAlignmentOptions.MidlineLeft;
        return new Hint { root = root, glow = glow, icon = icon, currency = currency, cost = cost };
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
        hint.glow.gameObject.SetActive(false);
        hint.root.gameObject.SetActive(false);
        hint.glow.sprite = hint.icon.sprite = hint.currency.sprite = null;
        hint.glow.color = Color.white;
        hint.cost.text = string.Empty;
        hint.cost.color = Color.white;
        hint.root.localPosition = Vector3.zero;
        hint.root.localScale = Vector3.one;
        hint.glow.rectTransform.anchoredPosition = Vector2.zero;
    }

    public void Dispose() {
        RookieEngine.timer.RemoveUpdateListener(this, OnUiTime);
        foreach (var hint in active.Values) {
            Reset(hint);
            UnityEngine.Object.Destroy(hint.root.gameObject);
            UnityEngine.Object.Destroy(hint.glow.gameObject);
        }
        foreach (var hint in pool) {
            Reset(hint);
            UnityEngine.Object.Destroy(hint.root.gameObject);
            UnityEngine.Object.Destroy(hint.glow.gameObject);
        }
        active.Clear();
        pool.Clear();
        visible.Clear();
        removed.Clear();
        message.text = string.Empty;
        message.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(message.gameObject);
    }

}
