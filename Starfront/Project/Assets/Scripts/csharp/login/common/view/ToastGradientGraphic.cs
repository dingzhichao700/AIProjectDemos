using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 黑色半透明、左右渐隐的滚动提示底板。
/// </summary>
/// <remarks>直接生成顶点Alpha，无位图或额外材质；渐隐宽度保持设计像素。</remarks>
[RequireComponent(typeof(CanvasRenderer))]
[DisallowMultipleComponent]
public sealed class ToastGradientGraphic : MaskableGraphic {

    protected override void OnPopulateMesh(VertexHelper vertices) {
        vertices.Clear();
        Rect rect = GetPixelAdjustedRect();
        float fade = Mathf.Min(64, rect.width / 2);
        const int segments = 8;
        for (int i = 0; i <= segments * 2 + 1; i++) {
            float x;
            float alpha;
            if (i <= segments) {
                float t = (float)i / segments;
                x = rect.xMin + fade * t;
                alpha = Mathf.SmoothStep(0, .72f, t);
            } else {
                float t = (float)(i - segments - 1) / segments;
                x = rect.xMax - fade + fade * t;
                alpha = Mathf.SmoothStep(.72f, 0, t);
            }
            Color value = new Color(0, 0, 0, alpha * color.a);
            vertices.AddVert(new Vector3(x, rect.yMin), value, Vector2.zero);
            vertices.AddVert(new Vector3(x, rect.yMax), value, Vector2.one);
            if (i > 0) {
                int start = (i - 1) * 2;
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start + 2, start + 1, start + 3);
            }
        }
    }

}
