using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按逻辑单位ID同步视窗内的逐帧动画与位置。
/// </summary>
/// <remarks>共享帧资源保持框架缓存生命周期，回收仅清空图片引用；不创建单位计时器。</remarks>
public sealed class BattleUnitView : IDisposable {

    private readonly BattleSceneModel model;
    private readonly Dictionary<(cfg.resource.UnitResource, UnitActionType, UnitAppearanceDirection), VisualSource> visualSources = new Dictionary<(cfg.resource.UnitResource, UnitActionType, UnitAppearanceDirection), VisualSource>();
    private readonly List<DepthEntry> depthOrder = new List<DepthEntry>();
    private static readonly IComparer<DepthEntry> DepthOrder = new DepthComparer();

    private sealed class VisualSource {
        public cfg.bean.UnitAppearance appearance;
        public FrameAnimationRes animation;
    }

    private struct DepthEntry {
        public RectTransform rect;
        public float depth;
        public float x;
        public int originalIndex;
    }

    private sealed class DepthComparer : IComparer<DepthEntry> {
        public int Compare(DepthEntry a, DepthEntry b) {
            int value = b.depth.CompareTo(a.depth);
            if (value != 0) {
                return value;
            }
            value = a.x.CompareTo(b.x);
            return value != 0 ? value : a.originalIndex.CompareTo(b.originalIndex);
        }
    }

    private VisualSource ResolveVisual(cfg.resource.UnitResource config, UnitActionType action, UnitAppearanceDirection direction) {
        var key = (config, action, direction);
        if (visualSources.TryGetValue(key, out var cached)) {
            return cached;
        }
        cfg.bean.UnitAppearance appearance = null;
        foreach (var candidate in config.Appearances) {
            if (candidate.Action != action || candidate.Direction != direction) {
                continue;
            }
            if (appearance != null) {
                throw new InvalidOperationException("Sequence contains more than one matching element");
            }
            appearance = candidate;
        }
        if (appearance == null) {
            throw new InvalidOperationException("Sequence contains no matching element");
        }
        string path = ResourceConst.GetFrameAnimationPath(appearance.Resource);
        var animation = FrameAnimationManager.GetRes(path) ?? throw new InvalidOperationException("单位动作未预加载：" + path);
        cached = new VisualSource { appearance = appearance, animation = animation };
        visualSources.Add(key, cached);
        return cached;
    }
    private readonly BattleUnitDeathView deaths;

    private readonly RectTransform parent;
    private readonly RectTransform shadowParent;
    private readonly Dictionary<long, BattleUnitShadowGraphic> shadows = new Dictionary<long, BattleUnitShadowGraphic>();
    private readonly Stack<BattleUnitShadowGraphic> shadowPool = new Stack<BattleUnitShadowGraphic>();
    private readonly Dictionary<RectTransform, float> groundDepths = new Dictionary<RectTransform, float>();
    private readonly float angle;
    private readonly float length;
    private readonly float alpha;

    private readonly Dictionary<long, Image> active = new Dictionary<long, Image>();

    private readonly Stack<Image> pool = new Stack<Image>();

    private readonly HashSet<long> visible = new HashSet<long>();

    private readonly List<long> removed = new List<long>();

    private readonly Dictionary<long, float> flashes = new Dictionary<long, float>();

    // 空闲时间只用于显示；离屏仍持续计时，重新进入空闲才重置。
    private readonly Dictionary<long, float> idleStarts = new Dictionary<long, float>();

    public static UnitActionType ResolveAction(BattleUnitVO unit) {
        if (unit.activeCast != null) {
            return unit.activeCast.skill.config.Action;
        }
        return unit.moving ? UnitActionType.MOVE : UnitActionType.IDLE;
    }

    private static float AnimationClock(BattleUnitVO unit) {
        return unit.camp == BattleSceneCamp.FRIENDLY ? RookieEngine.playerTimer.curTime : RookieEngine.enemyTimer.curTime;
    }

    public int visibleCount => active.Count;

    public BattleUnitView(BattleSceneModel model, RectTransform parent, RectTransform shadowParent) {
        this.model = model;
        this.parent = parent;
        this.shadowParent = shadowParent;
        angle = BattleConst.ShadowAngle;
        length = BattleConst.ShadowLength;
        alpha = BattleConst.ShadowAlpha;
        if (!(angle > 0 && angle < 180) || !(length > 0 && length < float.PositiveInfinity) || !(alpha >= 0 && alpha <= 1)) {
            throw new InvalidOperationException("单位阴影配置越界");
        }
        deaths = new BattleUnitDeathView(model, parent, shadowParent);
        if (model.combat != null) {
            model.combat.Changed += OnCombatChanged;
        }
    }

    public void Refresh(Rect visibleRect) {
        removed.Clear();
        foreach (long id in idleStarts.Keys) {
            if (!model.elements.ContainsKey(id)) {
                removed.Add(id);
            }
        }
        foreach (long id in removed) {
            idleStarts.Remove(id);
        }
        visible.Clear();
        groundDepths.Clear();
        deaths.Refresh(visibleRect);
        foreach (var element in model.elements.Values) {
            if (!(element is BattleUnitVO unit)) {
                continue;
            }
            if (unit.destroyed) {
                continue;
            }
            var direction = ResolveDirection(unit.displayHeading);
            var action = ResolveAction(unit);
            var source = ResolveVisual(unit.config, action, direction);
            var appearance = source.appearance;
            var animation = source.animation;
            bool mirror = unit.displayHeading.x < 0;
            double elapsed;
            if (unit.activeCast != null && action != UnitActionType.IDLE) {
                idleStarts.Remove(unit.id);
                elapsed = unit.skillTimeMs - unit.activeCast.startedAtMs;
            } else if (unit.moving) {
                idleStarts.Remove(unit.id);
                elapsed = unit.animationMs;
            } else {
                if (!idleStarts.TryGetValue(unit.id, out float started)) {
                    started = AnimationClock(unit);
                    idleStarts.Add(unit.id, started);
                }
                elapsed = AnimationClock(unit) - started;
            }
            elapsed = Math.Max(0, elapsed);
            // 动作完成不改变业务阶段：释放仍在后摇时保持末帧，阶段结束才切空闲。
            float milliseconds = appearance.Loop ? (float)(elapsed % animation.totalDuration) : (float)Math.Min(elapsed, animation.totalDuration - 1);
            int frameIndex = animation.GetIndexByTime(milliseconds);
            Sprite sprite = animation.sprites[frameIndex];
            // Sprite区域自身使用零pivot。把完整画布的固定脚底点换算到裁剪区域，
            // 再由RectTransform围绕该点镜像，不能用区域左下角代替角色锚点。
            Vector2 framePivot = Vector2.Scale(animation.sourceSizes[frameIndex], animation.pivot) - animation.frameOffsets[frameIndex];
            Vector2 point = unit.position + new Vector2(mirror ? -appearance.Offset.X : appearance.Offset.X, appearance.Offset.Y - model.config.GroundThickness);
            Vector2 ground = point;
            point.y -= unit.altitude;
            Rect shadowBounds = BattleUnitShadowGraphic.Bounds(sprite.rect.size, framePivot, mirror, ground, unit.altitude, angle, length);
            Vector2 pivot = framePivot;
            if (mirror) {
                pivot.x = sprite.rect.width - pivot.x;
            }
            var bounds = new Rect(point.x - pivot.x, point.y - (sprite.rect.height - pivot.y), sprite.rect.width, sprite.rect.height);
            if (!bounds.Overlaps(visibleRect, true) && !shadowBounds.Overlaps(visibleRect, true)) {
                continue;
            }
            visible.Add(unit.id);
            if (!active.TryGetValue(unit.id, out Image image)) {
                image = pool.Count > 0 ? pool.Pop() : CreateImage();
                active.Add(unit.id, image);
                image.name = "unit_" + unit.id;
                image.gameObject.SetActive(true);
            }
            if (!shadows.TryGetValue(unit.id, out var shadow)) {
                shadow = shadowPool.Count > 0 ? shadowPool.Pop() : CreateShadow();
                shadows.Add(unit.id, shadow);
                shadow.name = "shadow_" + unit.id;
                shadow.gameObject.SetActive(true);
            }
            shadow.SetFrame(sprite, framePivot, mirror, ground, unit.altitude, angle, length, alpha);
            groundDepths[image.rectTransform] = -ground.y;
            image.sprite = sprite;
            image.color = flashes.TryGetValue(unit.id, out float until) && Time.unscaledTime < until ? new Color(1f, .45f, .45f, 1f) : Color.white;
            image.rectTransform.pivot = framePivot / sprite.rect.size;
            image.rectTransform.sizeDelta = sprite.rect.size;
            image.rectTransform.anchoredPosition = new Vector2(point.x, -point.y);
            image.rectTransform.localScale = new Vector3(mirror ? -1 : 1, 1, 1);
        }
        removed.Clear();
        foreach (long id in active.Keys) {
            if (!visible.Contains(id)) {
                removed.Add(id);
            }
        }
        foreach (long id in removed) {
            var image = active[id];
            active.Remove(id);
            Reset(image);
            pool.Push(image);
            var shadow = shadows[id];
            shadows.Remove(id);
            shadow.ResetFrame();
            shadowPool.Push(shadow);
        }
        // 与建筑共用地面深度顺序，镜头缩放由场景根应用一次。
        if (active.Count > 0 || deaths.visibleCount > 0) {
            depthOrder.Clear();
            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++) {
                var rect = (RectTransform)parent.GetChild(i);
                if (!rect.gameObject.activeSelf) {
                    continue;
                }
                Vector2 point = rect.anchoredPosition;
                depthOrder.Add(new DepthEntry { rect = rect, depth = groundDepths.TryGetValue(rect, out float depth) ? depth : point.y, x = point.x, originalIndex = i });
            }
            depthOrder.Sort(DepthOrder);
            // Reverse placement also preserves inactive pooled siblings in front.
            // Stable frames avoid changing sibling order and dirtying Canvas again.
            for (int i = depthOrder.Count - 1; i >= 0; i--) {
                RectTransform rect = depthOrder[i].rect;
                int target = childCount - depthOrder.Count + i;
                if (rect.GetSiblingIndex() != target) {
                    rect.SetSiblingIndex(target);
                }
            }
        }
    }

    /**将八方向压到五个源方向，左侧由锚点镜像显示。*/
    public static UnitAppearanceDirection ResolveDirection(Vector2 heading) {
        return BattleAim.ResolveDirection(heading);
    }

    private BattleUnitShadowGraphic CreateShadow() {
        var shadow = new GameObject("UnitShadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleUnitShadowGraphic)).GetComponent<BattleUnitShadowGraphic>();
        shadow.transform.SetParent(shadowParent, false);
        shadow.rectTransform.anchorMin = shadow.rectTransform.anchorMax = new Vector2(0, 1);
        shadow.raycastTarget = false;
        return shadow;
    }

    private Image CreateImage() {
        var image = new GameObject("Unit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0, 1);
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        return image;
    }

    private static void Reset(Image image) {
        image.sprite = null;
        image.color = Color.white;
        image.rectTransform.localScale = Vector3.one;
        image.rectTransform.anchoredPosition = Vector2.zero;
        image.gameObject.SetActive(false);
    }

    private void OnCombatChanged(BattleCombatEvent change) {
        if (change.type == BattleCombatEventType.Damaged) {
            flashes[change.targetId] = Time.unscaledTime + BattleConst.HitFlashSeconds;
        } else if (change.type == BattleCombatEventType.Died) {
            deaths.OnDied(change);
            flashes.Remove(change.targetId);
            idleStarts.Remove(change.targetId);
        }
    }

    public void Dispose() {
        if (model.combat != null) {
            model.combat.Changed -= OnCombatChanged;
        }
        deaths.Dispose();
        flashes.Clear();
        idleStarts.Clear();
        foreach (var image in active.Values.Concat(pool)) {
            Reset(image);
            UnityEngine.Object.Destroy(image.gameObject);
        }
        foreach (var shadow in shadows.Values.Concat(shadowPool)) {
            shadow.ResetFrame();
            UnityEngine.Object.Destroy(shadow.gameObject);
        }
        shadows.Clear();
        shadowPool.Clear();
        groundDepths.Clear();
        active.Clear();
        pool.Clear();
        visualSources.Clear();
        depthOrder.Clear();
    }

}
