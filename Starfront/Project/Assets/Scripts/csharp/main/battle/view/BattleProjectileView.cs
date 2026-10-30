using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>弹道与命中特效共用前景层及对象池；显示不决定命中或伤害。</summary>
public sealed class BattleProjectileView : IDisposable {
    private sealed class Impact {
        public BattleProjectileHitEvent hit;
        public double started;
    }
    private readonly BattleSceneModel scene;
    private readonly BattleProjectileModel model;
    private readonly RectTransform parent;
    private readonly Dictionary<long, Image> active = new Dictionary<long, Image>();
    private readonly Dictionary<long, Impact> impacts = new Dictionary<long, Impact>();
    private readonly Stack<Image> pool = new Stack<Image>();
    private readonly HashSet<long> visible = new HashSet<long>();
    public int visibleCount => active.Count;

    public BattleProjectileView(BattleSceneModel scene, RectTransform parent) {
        this.scene = scene;
        model = scene.combat.projectiles;
        this.parent = parent;
        model.Hit += OnHit;
    }

    private static double Clock(BattleSceneCamp camp) {
        return camp == BattleSceneCamp.FRIENDLY ? RookieEngine.playerTimer.curTime : RookieEngine.enemyTimer.curTime;
    }

    private void OnHit(BattleProjectileHitEvent hit) {
        impacts[hit.projectileId] = new Impact { hit = hit, started = Clock(hit.camp) - hit.elapsedSinceHitMs };
    }

    public void Refresh(Rect bounds) {
        visible.Clear();
        foreach (var value in model.projectiles.Values) {
            var sprite = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(value.config.Resource));
            if (sprite == null) {
                throw new InvalidOperationException("子弹未预加载：" + value.config.Resource);
            }
            float angle = -Mathf.Atan2(value.visualHeading.y, value.visualHeading.x) * Mathf.Rad2Deg;
            Draw(value.id, sprite, sprite.pivot / sprite.rect.size, value.visualPosition, angle, bounds);
        }
        foreach (var pair in impacts.ToArray()) {
            var impact = pair.Value;
            var animation = FrameAnimationManager.GetRes(ResourceConst.GetFrameAnimationPath(impact.hit.resource));
            if (animation == null) {
                throw new InvalidOperationException("命中特效未预加载：" + impact.hit.resource);
            }
            double age = Clock(impact.hit.camp) - impact.started;
            if (age >= animation.totalDuration) {
                impacts.Remove(pair.Key);
                continue;
            }
            int index = animation.GetIndexByTime((float)Math.Max(0, age));
            var sprite = animation.sprites[index];
            Vector2 pivot = (Vector2.Scale(animation.sourceSizes[index], animation.pivot) - animation.frameOffsets[index]) / sprite.rect.size;
            Draw(-pair.Key, sprite, pivot, impact.hit.position, 0, bounds);
        }
        foreach (long id in active.Keys.Where(id => !visible.Contains(id)).ToArray()) {
            var image = active[id];
            active.Remove(id);
            Reset(image);
            pool.Push(image);
        }
    }

    private void Draw(long id, Sprite sprite, Vector2 pivot, Vector2 position, float angle, Rect bounds) {
        position.y -= scene.config.GroundThickness;
        // 包含旋转及裁剪锚点外移的保守视窗范围。
        float radius = sprite.rect.size.magnitude * (1 + pivot.magnitude);
        if (!new Rect(position - Vector2.one * radius, Vector2.one * radius * 2).Overlaps(bounds, true)) {
            return;
        }
        visible.Add(id);
        if (!active.TryGetValue(id, out var image)) {
            if (pool.Count > 0) {
                image = pool.Pop();
            } else {
                image = new GameObject("Projectile", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(parent, false);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0, 1);
                image.raycastTarget = false;
            }
            active.Add(id, image);
        }
        image.name = id > 0 ? "projectile_" + id : "impact_" + -id;
        image.sprite = sprite;
        image.color = Color.white;
        image.rectTransform.pivot = pivot;
        image.rectTransform.sizeDelta = sprite.rect.size;
        image.rectTransform.anchoredPosition = new Vector2(position.x, -position.y);
        image.rectTransform.localScale = Vector3.one;
        image.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        image.gameObject.SetActive(true);
    }

    private static void Reset(Image image) {
        image.sprite = null;
        image.color = Color.white;
        image.rectTransform.localScale = Vector3.one;
        image.rectTransform.localRotation = Quaternion.identity;
        image.rectTransform.anchoredPosition = Vector2.zero;
        image.gameObject.SetActive(false);
    }

    public void Dispose() {
        model.Hit -= OnHit;
        impacts.Clear();
        foreach (var image in active.Values.Concat(pool)) {
            Reset(image);
            UnityEngine.Object.Destroy(image.gameObject);
        }
        active.Clear();
        pool.Clear();
    }
}
