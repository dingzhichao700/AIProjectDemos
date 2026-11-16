using UnityEngine;

/// <summary>
/// 将完整面板的设计内容等比容纳于安全区域，保留背景节点独立布局。
/// </summary>
/// <remarks>只建立一次内容容器；不改变序列化绑定，不累计缩放。</remarks>
[DisallowMultipleComponent]
public sealed class PanelSafeContent : MonoBehaviour {

    public RectTransform content { get; private set; }

    private Vector2 design;
    private Vector2 alignment = new Vector2(.5f, .5f);

    public void Initialize(string[] backgroundNames, Vector2 designSize, Vector2? contentAlignment = null) {
        var requested = contentAlignment ?? new Vector2(.5f, .5f);
        alignment = new Vector2(Mathf.Clamp01(requested.x), Mathf.Clamp01(requested.y));
        if (designSize.x <= 0 || designSize.y <= 0) {
            throw new System.ArgumentOutOfRangeException(nameof(designSize), "UI design size must be configured before opening panels.");
        }
        if (content == null) {
            var root = (RectTransform)transform;
            design = designSize;
            var children = new Transform[root.childCount];
            for (int i = 0; i < children.Length; i++) {
                children[i] = root.GetChild(i);
            }
            content = new GameObject("safeContent", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(root, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.sizeDelta = design;
            foreach (var child in children) {
                if (System.Array.IndexOf(backgroundNames, child.name) < 0) {
                    child.SetParent(content, false);
                }
            }
        }
        design = designSize;
        content.sizeDelta = design;
        Apply();
    }

    private void LateUpdate() {
        Apply();
    }

    public void Apply() {
        if (content == null || !(transform.parent is RectTransform parent)) {
            return;
        }
        Rect available = UIScreenRegions.LocalContentArea(parent);
        Rect fit = PortraitGameViewport.FitRect(available.size, design);
        float scale = Mathf.Min(1f, fit.width / design.x);
        Vector2 fittedSize = design * scale;
        Vector2 targetCenter = available.min + Vector2.Scale(available.size - fittedSize, alignment) + fittedSize * .5f;
        Vector3 center = transform.InverseTransformPoint(parent.TransformPoint(targetCenter));
        content.localPosition = center;
        content.localScale = Vector3.one * scale;
    }

}
