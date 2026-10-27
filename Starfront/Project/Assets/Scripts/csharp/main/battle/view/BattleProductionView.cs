using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>按建筑ID绑定己方生产圆环；只读取生产进度，离屏回收不停止生产。</summary>
public sealed class BattleProductionView : IDisposable {
    private sealed class Ring {
        public RectTransform root;
        public Image background;
        public Image fill;
    }

    private readonly BattleSceneModel model;
    private readonly RectTransform sceneRoot;
    private readonly RectTransform lod;
    private readonly Sprite backgroundSprite;
    private readonly Sprite fillSprite;
    private readonly Dictionary<long, Ring> active = new Dictionary<long, Ring>();
    private readonly Stack<Ring> pool = new Stack<Ring>();
    private readonly HashSet<long> visibleIds = new HashSet<long>();
    private readonly List<long> toRecycle = new List<long>();
    public int visibleCount => active.Count;

    public BattleProductionView(BattleSceneModel model, RectTransform sceneRoot, RectTransform lod) {
        this.model = model;
        this.sceneRoot = sceneRoot;
        this.lod = lod;
        backgroundSprite = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(BattleConst.ProductionBackgroundResource));
        fillSprite = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(BattleConst.ProductionFillResource));
        if (backgroundSprite == null || fillSprite == null) {
            throw new InvalidOperationException("生产圆环资源尚未预加载。");
        }
    }

    public void Refresh(Rect visibleRect, float zoom) {
        visibleIds.Clear();
        Vector2 size = backgroundSprite.rect.size;
        // 已审核样例位于格子中心下方三分之一格高，尺寸直接沿用素材。
        Vector2 offset = new Vector2(0, BattleConst.CellHeight / 3f);
        foreach (var building in model.buildings.Values) {
            if (building.destroyed || building.owner.camp != BattleSceneCamp.FRIENDLY || (building.currencyProduction == null && building.unitProduction == null)) {
                continue;
            }
            Vector2 position = building.position + offset - new Vector2(0, model.config.GroundThickness);
            var bounds = new Rect(position - size / 2, size);
            if (!bounds.Overlaps(visibleRect, true)) {
                continue;
            }
            visibleIds.Add(building.id);
            if (!active.TryGetValue(building.id, out Ring ring)) {
                ring = pool.Count > 0 ? pool.Pop() : CreateRing();
                active.Add(building.id, ring);
                ring.root.name = "production_" + building.id;
                ring.background.sprite = backgroundSprite;
                ring.fill.sprite = fillSprite;
                ring.root.gameObject.SetActive(true);
            }
            // 从场景坐标转换到独立LOD坐标，只应用一次镜头变换。
            Vector3 worldPoint = sceneRoot.TransformPoint(new Vector3(position.x, -position.y, 0));
            ring.root.localPosition = lod.InverseTransformPoint(worldPoint);
            ring.root.localScale = Vector3.one * zoom;
            ring.fill.fillAmount = Mathf.Clamp01((float)building.productionProgress);
        }
        toRecycle.Clear();
        foreach (long id in active.Keys) {
            if (!visibleIds.Contains(id)) {
                toRecycle.Add(id);
            }
        }
        foreach (long id in toRecycle) {
            Ring ring = active[id];
            active.Remove(id);
            Reset(ring);
            pool.Push(ring);
        }
    }

    private Ring CreateRing() {
        var root = new GameObject("Production", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(lod, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = backgroundSprite.rect.size;
        Image background = CreateImage(root, "background");
        Image fill = CreateImage(root, "fill");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        return new Ring { root = root, background = background, fill = fill };
    }

    private static Image CreateImage(RectTransform parent, string name) {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static void Reset(Ring ring) {
        ring.root.gameObject.SetActive(false);
        ring.background.sprite = null;
        ring.fill.sprite = null;
        ring.fill.fillAmount = 0;
        ring.root.localPosition = Vector3.zero;
        ring.root.localScale = Vector3.one;
    }

    public void Dispose() {
        foreach (Ring ring in active.Values) {
            Reset(ring);
            UnityEngine.Object.Destroy(ring.root.gameObject);
        }
        foreach (Ring ring in pool) {
            Reset(ring);
            UnityEngine.Object.Destroy(ring.root.gameObject);
        }
        active.Clear();
        pool.Clear();
        visibleIds.Clear();
        toRecycle.Clear();
    }
}
