using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 统一创建、复用和回收战斗场景中的图片表现对象。
/// </summary>
internal sealed class BattleVisualPool {

    private readonly Dictionary<string, Stack<RectTransform>> pools =
        new Dictionary<string, Stack<RectTransform>>(StringComparer.Ordinal);

    private readonly Dictionary<RectTransform, string> poolKeys =
        new Dictionary<RectTransform, string>();

    /**创建或复用一个带 imgVisual 子节点的场景表现对象*/
    public RectTransform Create(string name, RectTransform parent, Vector2 size,
        Vector2 position, string imagePath, float visualRotation = 0f,
        bool useNativeSpriteVisual = false) {
        RectTransform root = Take(imagePath);
        if (root == null) {
            GameObject entity = new GameObject(name, typeof(RectTransform));
            root = entity.GetComponent<RectTransform>();
            CreateImage("imgVisual", root, size, Vector2.zero,
                imagePath, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            poolKeys[root] = imagePath;
        }
        root.name = name;
        root.gameObject.SetActive(true);
        SetupRect(root, parent, size, position, new Vector2(0.5f, 0.5f));
        RectTransform visualRect = root.Find("imgVisual") as RectTransform;
        SetupRect(visualRect, root, size, Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        visualRect.localScale = Vector3.one;
        visualRect.localEulerAngles = new Vector3(0f, 0f, visualRotation);
        Image image = visualRect.GetComponent<Image>();
        if (image != null) {
            image.color = Color.white;
            if (useNativeSpriteVisual) {
                UITools.SetImage(image, imagePath, true, true);
            }
        }
        return root;
    }

    /**预先创建指定数量的对象并放回池中*/
    public void Prewarm(string imagePath, Vector2 size, int capacity,
        RectTransform temporaryParent) {
        if (capacity <= 0) {
            return;
        }
        List<RectTransform> instances = new List<RectTransform>(capacity);
        for (int i = 0; i < capacity; i++) {
            instances.Add(Create($"prewarm_{i}", temporaryParent, size,
                Vector2.zero, imagePath));
        }
        foreach (RectTransform instance in instances) {
            Recycle(instance);
        }
    }

    /**回收表现对象；非本池对象直接销毁*/
    public void Recycle(RectTransform root) {
        if (root == null) {
            return;
        }
        if (!poolKeys.TryGetValue(root, out string poolKey)) {
            UnityEngine.Object.Destroy(root.gameObject);
            return;
        }
        if (!pools.TryGetValue(poolKey, out Stack<RectTransform> pool)) {
            pool = new Stack<RectTransform>();
            pools.Add(poolKey, pool);
        }
        root.gameObject.SetActive(false);
        pool.Push(root);
    }

    /**清除对象池索引；实际节点由所属界面层统一销毁*/
    public void Clear() {
        pools.Clear();
        poolKeys.Clear();
    }

    private RectTransform Take(string imagePath) {
        if (!pools.TryGetValue(imagePath, out Stack<RectTransform> pool)) {
            return null;
        }
        while (pool.Count > 0) {
            RectTransform result = pool.Pop();
            if (result != null) {
                return result;
            }
        }
        return null;
    }

    private static RectTransform CreateImage(string name, RectTransform parent,
        Vector2 size, Vector2 position, string imagePath, Vector2 pivot, Vector2 anchor) {
        GameObject imageObject = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        SetupRect(rect, parent, size, position, pivot, anchor);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        UITools.SetImage(image, imagePath);
        return rect;
    }

    private static void SetupRect(RectTransform rect, RectTransform parent, Vector2 size,
        Vector2 position, Vector2 pivot, Vector2? anchor = null) {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0f, 1f);
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
