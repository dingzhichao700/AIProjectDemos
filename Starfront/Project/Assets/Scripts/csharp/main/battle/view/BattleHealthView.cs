using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>只读生命快照，复用已审核血条素材；显隐不改变逻辑生命。</summary>
public sealed class BattleHealthView : IDisposable {
    private sealed class Bar {
        public RectTransform root;
        public Image background;
        public Image fill;
    }
    private readonly BattleSceneModel model;
    private readonly RectTransform sceneRoot;
    private readonly RectTransform lod;
    private readonly Dictionary<long, Bar> active = new Dictionary<long, Bar>();
    private readonly Stack<Bar> pool = new Stack<Bar>();
    public int visibleCount => active.Count;

    public BattleHealthView(BattleSceneModel model, RectTransform sceneRoot, RectTransform lod) {
        this.model = model;
        this.sceneRoot = sceneRoot;
        this.lod = lod;
    }

    public void Refresh(Rect visible, float zoom) {
        var keep = new HashSet<long>();
        foreach (var target in model.elements.Values.OfType<BattleCombatantVO>()) {
            if (!target.alive) {
                continue;
            }
            bool unit = target is BattleUnitVO;
            string key = unit ? "unit" : "building";
            Sprite background = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(BattleConst.HealthBackgrounds[key]));
            var fills = target.camp == BattleSceneCamp.FRIENDLY ? BattleConst.FriendlyHealthFills : BattleConst.EnemyHealthFills;
            Sprite fill = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(fills[key]));
            if (background == null || fill == null) {
                throw new InvalidOperationException("血条资源未预加载。");
            }
            float height = BattleConst.BuildingHealthOffset;
            if (target is BattleUnitVO soldier) {
                // 配置高度稳定于动作/朝向；半个背景高度使6px间距落到血条下沿。
                height = soldier.visual.NormalHeight + soldier.altitude + BattleConst.UnitHealthGap + background.rect.height / 2;
            }
            var point = target.position - new Vector2(0, model.config.GroundThickness + height);
            if (!new Rect(point - background.rect.size / 2, background.rect.size).Overlaps(visible)) {
                continue;
            }
            keep.Add(target.id);
            if (!active.TryGetValue(target.id, out var bar)) {
                bar = pool.Count > 0 ? pool.Pop() : Create();
                active.Add(target.id, bar);
            }
            bar.root.name = "health_" + target.id;
            bar.root.gameObject.SetActive(true);
            bar.root.localPosition = lod.InverseTransformPoint(sceneRoot.TransformPoint(new Vector3(point.x, -point.y, 0)));
            bar.root.localScale = Vector3.one * zoom;
            bar.background.sprite = background;
            bar.background.rectTransform.sizeDelta = background.rect.size;
            bar.fill.sprite = fill;
            bar.fill.rectTransform.sizeDelta = fill.rect.size;
            bar.fill.fillAmount = Mathf.Clamp01((float)(target.hp / target.maxHp));
        }
        foreach (long id in active.Keys.Where(id => !keep.Contains(id)).ToArray()) {
            var bar = active[id];
            active.Remove(id);
            Reset(bar);
            pool.Push(bar);
        }
    }

    private Bar Create() {
        var root = new GameObject("Health", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(lod, false);
        var bg = NewImage("Background", root);
        var fill = NewImage("Fill", root);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        return new Bar { root = root, background = bg, fill = fill };
    }

    private static Image NewImage(string name, RectTransform parent) {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.raycastTarget = false;
        return image;
    }

    private static void Reset(Bar bar) {
        bar.background.sprite = null;
        bar.fill.sprite = null;
        bar.fill.fillAmount = 1;
        bar.root.gameObject.SetActive(false);
    }

    public void Dispose() {
        foreach (var bar in active.Values.Concat(pool)) {
            Reset(bar);
            UnityEngine.Object.Destroy(bar.root.gameObject);
        }
        active.Clear();
        pool.Clear();
    }
}
