using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>独立死亡表现快照；不持有逻辑单位，不参与伤害、人口或逻辑回收。</summary>
public sealed class BattleUnitDeathView : IDisposable {
    private sealed class Visual {
        public RectTransform root;
        public Image body;
        public Image effect;
        public BattleUnitShadowGraphic shadow;
        public Sprite sprite;
        public float alpha = -1;
    }
    private sealed class Corpse {
        public FrameAnimationRes animation;
        public FrameAnimationRes effect;
        public int holdMs;
        public int fadeMs;
        public Vector2 ground;
        public float altitude;
        public bool mirror;
        public BattleSceneCamp camp;
        public float started;
        public Visual visual;
    }
    private readonly BattleSceneModel model;
    private readonly RectTransform parent;
    private readonly RectTransform shadowParent;
    private readonly Dictionary<long, Corpse> entries = new Dictionary<long, Corpse>();
    private readonly Stack<Visual> pool = new Stack<Visual>();
    private readonly List<long> expired = new List<long>();
    private readonly float angle, length, shadowAlpha;
    private const int MaxPool = 32;
    public int count => entries.Count;
    public int visibleCount { get; private set; }

    public BattleUnitDeathView(BattleSceneModel model, RectTransform parent, RectTransform shadowParent) {
        this.model = model;
        this.parent = parent;
        this.shadowParent = shadowParent;
        angle = BattleConst.ShadowAngle;
        length = BattleConst.ShadowLength;
        shadowAlpha = BattleConst.ShadowAlpha;
    }

    private static float Clock(BattleSceneCamp camp) {
        return camp == BattleSceneCamp.FRIENDLY ? RookieEngine.playerTimer.curTime : RookieEngine.enemyTimer.curTime;
    }

    public void OnDied(BattleCombatEvent change) {
        if (change.type != BattleCombatEventType.Died || entries.ContainsKey(change.targetId)
            || !model.elements.TryGetValue(change.targetId, out var element) || !(element is BattleUnitVO unit)) return;
        var direction = BattleAim.ResolveDirection(unit.heading);
        var appearance = unit.config.Appearances.Single(a => a.Action == UnitActionType.DEATH && a.Direction == direction);
        if (appearance.Loop) throw new InvalidOperationException("死亡动作禁止循环：" + unit.config.Id);
        string path = ResourceConst.GetFrameAnimationPath(appearance.Resource);
        var animation = FrameAnimationManager.GetRes(path) ?? throw new InvalidOperationException("死亡动作未预加载：" + path);
        bool mirror = unit.heading.x < 0;
        entries.Add(unit.id, new Corpse {
            animation = animation, holdMs = unit.visual.DeathHoldMs, fadeMs = unit.visual.DeathFadeMs,
            effect = string.IsNullOrWhiteSpace(unit.visual.DeathEffect) ? null : FrameAnimationManager.GetRes(ResourceConst.GetFrameAnimationPath(unit.visual.DeathEffect)),
            mirror = mirror, camp = unit.camp, started = Clock(unit.camp), altitude = unit.altitude,
            ground = change.position + new Vector2(mirror ? -appearance.Offset.X : appearance.Offset.X, appearance.Offset.Y - model.config.GroundThickness)
        });
    }

    public void Refresh(Rect visibleRect) {
        visibleCount = 0;
        expired.Clear();
        foreach (var pair in entries) {
            var item = pair.Value;
            float elapsed = Mathf.Max(0, Clock(item.camp) - item.started);
            if (elapsed >= (double)Math.Max(item.animation.totalDuration + item.holdMs + item.fadeMs, item.effect?.totalDuration ?? 0)) {
                Recover(item); expired.Add(pair.Key); continue;
            }
            int index = item.animation.GetIndexByTime(Mathf.Min(elapsed, item.animation.totalDuration - 1));
            var sprite = item.animation.sprites[index];
            Vector2 pivot = Vector2.Scale(item.animation.sourceSizes[index], item.animation.pivot) - item.animation.frameOffsets[index];
            Vector2 mirroredPivot = pivot;
            if (item.mirror) mirroredPivot.x = sprite.rect.width - mirroredPivot.x;
            var bodyBounds = new Rect(item.ground.x - mirroredPivot.x, item.ground.y - item.altitude - (sprite.rect.height - mirroredPivot.y), sprite.rect.width, sprite.rect.height);
            Rect shadowBounds = BattleUnitShadowGraphic.Bounds(sprite.rect.size, pivot, item.mirror, item.ground, item.altitude, angle, length);
            var effectBounds = new Rect(item.ground.x - 128, item.ground.y - item.altitude - 128, 256, 256);
            if (!bodyBounds.Overlaps(visibleRect, true) && !shadowBounds.Overlaps(visibleRect, true) && !(item.effect != null && elapsed < item.effect.totalDuration && effectBounds.Overlaps(visibleRect, true))) {
                Recover(item); continue;
            }
            visibleCount++;
            if (item.visual == null) {
                item.visual = pool.Count > 0 ? pool.Pop() : Create();
                var visual = item.visual;
                visual.root.anchoredPosition = new Vector2(item.ground.x, -item.ground.y);
                visual.body.rectTransform.anchoredPosition = new Vector2(0, item.altitude);
                visual.body.rectTransform.localScale = new Vector3(item.mirror ? -1 : 1, 1, 1);
                visual.root.gameObject.SetActive(true);
                visual.shadow.gameObject.SetActive(true);
            }
            var v = item.visual;
            float alpha = item.fadeMs == 0 ? (elapsed < item.animation.totalDuration + item.holdMs ? 1 : 0)
                : 1 - Mathf.Clamp01((elapsed - item.animation.totalDuration - item.holdMs) / item.fadeMs);
            bool effectVisible = item.effect != null && elapsed < item.effect.totalDuration;
            v.effect.gameObject.SetActive(effectVisible);
            if (effectVisible) {
                int effectIndex = item.effect.GetIndexByTime(elapsed);
                var effectSprite = item.effect.sprites[effectIndex];
                var effectPivot = Vector2.Scale(item.effect.sourceSizes[effectIndex], item.effect.pivot) - item.effect.frameOffsets[effectIndex];
                v.effect.sprite = effectSprite;
                v.effect.rectTransform.pivot = effectPivot / effectSprite.rect.size;
                v.effect.rectTransform.sizeDelta = effectSprite.rect.size;
                v.effect.rectTransform.anchoredPosition = new Vector2(0, item.altitude);
            }
            bool frameChanged = v.sprite != sprite;
            if (frameChanged) {
                v.body.sprite = sprite;
                v.body.rectTransform.pivot = pivot / sprite.rect.size;
                v.body.rectTransform.sizeDelta = sprite.rect.size;
            }
            if (frameChanged || v.alpha != alpha) {
                v.body.color = new Color(1, 1, 1, alpha);
                v.shadow.SetFrame(sprite, pivot, item.mirror, item.ground, item.altitude, angle, length, shadowAlpha * alpha);
                v.sprite = sprite; v.alpha = alpha;
            }
        }
        foreach (long id in expired) entries.Remove(id);
    }

    private Visual Create() {
        var root = new GameObject("UnitCorpse", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0, 1);
        root.sizeDelta = Vector2.zero;
        var body = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        body.transform.SetParent(root, false);
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(.5f, .5f);
        body.raycastTarget = false;
        body.type = Image.Type.Simple;
        var shadow = new GameObject("CorpseShadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleUnitShadowGraphic)).GetComponent<BattleUnitShadowGraphic>();
        shadow.transform.SetParent(shadowParent, false);
        shadow.rectTransform.anchorMin = shadow.rectTransform.anchorMax = new Vector2(0, 1);
        shadow.raycastTarget = false;
        var effect = new GameObject("DeathEffect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        effect.transform.SetParent(root, false);
        effect.rectTransform.anchorMin = effect.rectTransform.anchorMax = new Vector2(.5f, .5f);
        effect.raycastTarget = false;
        effect.gameObject.SetActive(false);
        return new Visual { root = root, body = body, shadow = shadow, effect = effect };
    }

    private static void Reset(Visual v) {
        v.effect.sprite = null;
        v.effect.gameObject.SetActive(false);
        v.body.sprite = null;
        v.body.color = Color.white;
        v.body.rectTransform.pivot = new Vector2(.5f, .5f);
        v.body.rectTransform.sizeDelta = Vector2.zero;
        v.body.rectTransform.anchoredPosition = Vector2.zero;
        v.body.rectTransform.localScale = Vector3.one;
        v.root.anchoredPosition = Vector2.zero;
        v.root.localScale = Vector3.one;
        v.root.localRotation = Quaternion.identity;
        v.shadow.ResetFrame();
        v.root.gameObject.SetActive(false);
        v.sprite = null; v.alpha = -1;
    }
    private void Recover(Corpse item) {
        if (item.visual == null) return;
        var v = item.visual; item.visual = null;
        Reset(v);
        if (pool.Count < MaxPool) pool.Push(v); else Destroy(v);
    }
    private static void Destroy(Visual v) {
        UnityEngine.Object.Destroy(v.root.gameObject);
        UnityEngine.Object.Destroy(v.shadow.gameObject);
    }
    public void Dispose() {
        foreach (var item in entries.Values) {
            if (item.visual != null) { Reset(item.visual); Destroy(item.visual); item.visual = null; }
        }
        entries.Clear();
        foreach (var v in pool) Destroy(v);
        pool.Clear(); expired.Clear(); visibleCount = 0;
    }
}
