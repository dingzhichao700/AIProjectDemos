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

    private readonly RectTransform parent;

    private readonly Dictionary<long, Image> active = new Dictionary<long, Image>();

    private readonly Stack<Image> pool = new Stack<Image>();

    private readonly HashSet<long> visible = new HashSet<long>();

    private readonly List<long> removed = new List<long>();

    public int visibleCount => active.Count;

    public BattleUnitView(BattleSceneModel model, RectTransform parent) {
        this.model = model;
        this.parent = parent;
    }

    public void Refresh(Rect visibleRect) {
        visible.Clear();
        foreach (var unit in model.elements.Values.OfType<BattleUnitVO>()) {
            if (unit.destroyed) {
                continue;
            }
            var direction = ResolveDirection(unit.heading);
            var appearance = unit.config.Appearances.First(item => item.Action == UnitActionType.MOVE && item.Direction == direction);
            string path = ResourceConst.PATH_FRAME_ANIMATION + appearance.Resource;
            var animation = FrameAnimationManager.GetRes(path) ?? throw new InvalidOperationException("单位动作未预加载：" + path);
            bool mirror = unit.heading.x < 0;
            float milliseconds = unit.moving ? (float)(unit.animationMs % animation.totalDuration) : 0;
            int frameIndex = animation.GetIndexByTime(milliseconds);
            Sprite sprite = animation.sprites[frameIndex];
            // Sprite区域自身使用零pivot。把完整画布的固定脚底点换算到裁剪区域，
            // 再由RectTransform围绕该点镜像，不能用区域左下角代替角色锚点。
            Vector2 framePivot = Vector2.Scale(animation.sourceSizes[frameIndex], animation.pivot) - animation.frameOffsets[frameIndex];
            Vector2 point = unit.position + new Vector2(mirror ? -appearance.Offset.X : appearance.Offset.X, appearance.Offset.Y);
            Vector2 pivot = framePivot;
            if (mirror) {
                pivot.x = sprite.rect.width - pivot.x;
            }
            var bounds = new Rect(point.x - pivot.x, point.y - (sprite.rect.height - pivot.y), sprite.rect.width, sprite.rect.height);
            if (!bounds.Overlaps(visibleRect, true)) {
                continue;
            }
            visible.Add(unit.id);
            if (!active.TryGetValue(unit.id, out Image image)) {
                image = pool.Count > 0 ? pool.Pop() : CreateImage();
                active.Add(unit.id, image);
                image.name = "unit_" + unit.id;
                image.gameObject.SetActive(true);
            }
            image.sprite = sprite;
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
        }
        // 与建筑共用地面深度顺序，镜头缩放由场景根应用一次。
        if (active.Count > 0) {
            var ordered = Enumerable.Range(0, parent.childCount).Select(index => (RectTransform)parent.GetChild(index)).Where(rect => rect.gameObject.activeSelf).OrderByDescending(rect => rect.anchoredPosition.y).ThenBy(rect => rect.anchoredPosition.x).ToArray();
            foreach (var rect in ordered) {
                rect.SetAsLastSibling();
            }
        }
    }

    /**将八方向压到五个源方向，左侧由锚点镜像显示。*/
    public static UnitAppearanceDirection ResolveDirection(Vector2 heading) {
        float x = Mathf.Abs(heading.x);
        float y = Mathf.Abs(heading.y);
        float threshold = Mathf.Tan(Mathf.PI / 8);
        if (x <= y * threshold) {
            return heading.y < 0 ? UnitAppearanceDirection.UP : UnitAppearanceDirection.DOWN;
        }
        if (y <= x * threshold) {
            return UnitAppearanceDirection.RIGHT;
        }
        return heading.y < 0 ? UnitAppearanceDirection.UP_RIGHT : UnitAppearanceDirection.DOWN_RIGHT;
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

    public void Dispose() {
        foreach (var image in active.Values.Concat(pool)) {
            Reset(image);
            UnityEngine.Object.Destroy(image.gameObject);
        }
        active.Clear();
        pool.Clear();
    }

}
