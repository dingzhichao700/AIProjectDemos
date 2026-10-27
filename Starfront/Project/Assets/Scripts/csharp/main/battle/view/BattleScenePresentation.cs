using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>按可见包围框创建和回收Image。纯外观记录不会注册成玩法元素。</summary>
public sealed class BattleScenePresentation : IDisposable {
    private sealed class Item {
        public long id;
        public SceneElementVO owner;
        public Vector2 position;
        public Sprite sprite;
        public Rect bounds;
        public RectTransform parent;
        public int layer;
    }
    private readonly List<Item> items = new List<Item>();
    private readonly Dictionary<long, Image> active = new Dictionary<long, Image>();
    private readonly Stack<Image> pool = new Stack<Image>();
    public int visibleCount => active.Count;
    public int totalCount => items.Count;

    public BattleScenePresentation(BattleSceneModel model, RectTransform[] layers) {
        foreach (var cell in model.cells.Values) {
            if (cell.type == BattleSceneCellType.NORMAL) {
                Add(cell.id, cell.position + new Vector2(0, 4), BattleSceneResources.GroundResource(model, cell.camp), BattleSceneVisualLayer.GROUND, layers);
            }
        }
        long decorationId = -1;
        foreach (var appearance in model.config.Appearances) {
            Vector2 anchor = model.GetCell(appearance.Anchor.Q, appearance.Anchor.K).position;
            Add(decorationId--, anchor + new Vector2(appearance.Offset.X, appearance.Offset.Y), appearance.Resource, appearance.Layer, layers);
        }
        foreach (var building in model.buildings.Values) {
            foreach (var appearance in building.config.Appearances) {
                var layer = appearance.Part == BuildingAppearancePart.SHADOW ? BattleSceneVisualLayer.GROUND_SHADOW : BattleSceneVisualLayer.ABOVE_GROUND;
                Add(decorationId--, building.position + new Vector2(appearance.Offset.X, appearance.Offset.Y), appearance.Resource, layer, layers, building);
            }
        }
        items.Sort((a, b) => {
            int layer = a.layer.CompareTo(b.layer);
            int y = a.position.y.CompareTo(b.position.y);
            int x = a.position.x.CompareTo(b.position.x);
            return layer != 0 ? layer : y != 0 ? y : x != 0 ? x : a.id.CompareTo(b.id);
        });
    }

    private void Add(long id, Vector2 position, string resource, BattleSceneVisualLayer layer, RectTransform[] layers, SceneElementVO owner = null) {
        Sprite sprite = ResourceManager.GetUnpackImage(BattleSceneResources.SpritePath(resource));
        int index = (int)layer - 1;
        if (sprite == null || index < 0 || index >= layers.Length || layers[index] == null) {
            throw new InvalidOperationException("场景资源未预加载或层级缺失：" + resource);
        }
        Vector2 pivot = sprite.pivot;
        Vector2 size = sprite.rect.size;
        items.Add(new Item { id = id, owner = owner, position = position, sprite = sprite, parent = layers[index], layer = index, bounds = new Rect(position.x - pivot.x, position.y - (size.y - pivot.y), size.x, size.y) });
    }

    public void Refresh(Rect visibleRect) {
        foreach (Item item in items) {
            bool visible = (item.owner == null || !item.owner.destroyed) && item.bounds.Overlaps(visibleRect, true);
            if (!visible) {
                if (active.TryGetValue(item.id, out Image old)) {
                    active.Remove(item.id);
                    old.sprite = null;
                    old.gameObject.SetActive(false);
                    pool.Push(old);
                }
                continue;
            }
            if (!active.TryGetValue(item.id, out Image image)) {
                image = pool.Count > 0 ? pool.Pop() : new GameObject("SceneVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                active.Add(item.id, image);
                image.name = "visual_" + item.id;
                image.transform.SetParent(item.parent, false);
                image.sprite = item.sprite;
                image.color = Color.white;
                image.raycastTarget = false;
                image.type = Image.Type.Simple;
                image.gameObject.SetActive(true);
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = item.sprite.pivot / item.sprite.rect.size;
                rect.sizeDelta = item.sprite.rect.size;
                rect.anchoredPosition = new Vector2(item.position.x, -item.position.y);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }
            image.transform.SetAsLastSibling();
        }
    }

    public void Dispose() {
        foreach (Image image in active.Values) {
            image.sprite = null;
            image.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(image.gameObject);
        }
        foreach (Image image in pool) {
            image.sprite = null;
            UnityEngine.Object.Destroy(image.gameObject);
        }
        active.Clear();
        pool.Clear();
        items.Clear();
    }
}
