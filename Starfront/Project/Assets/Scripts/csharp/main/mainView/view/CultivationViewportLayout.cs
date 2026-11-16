using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 培养页背景独立铺屏，保持已批准下方414像素图像带并向上延伸。
/// </summary>
/// <remarks>消费公共安全区适配结果，不自行读取平台接口。</remarks>
[DefaultExecutionOrder(100)]
public sealed class CultivationViewportLayout : MonoBehaviour {

    private CardCultivationPanel panel;
    private readonly Vector3[] corners = new Vector3[4];

    public void Configure(CardCultivationPanel owner) {
        panel = owner;
    }

    private void LateUpdate() {
        if (panel != null && panel.isOpened) {
            Apply();
        }
    }

    public void Apply() {
        if (panel == null || panel.SafeContentRoot == panel.trans) {
            return;
        }
        var root = panel.trans;
        panel.SafeContentRoot.GetWorldCorners(corners);
        float contentTop = root.InverseTransformPoint(corners[1]).y;
        float contentScale = panel.SafeContentRoot.lossyScale.y / root.lossyScale.y;
        float backgroundBottom = contentTop - 414 * contentScale;
        float widthScale = root.rect.width / 720;
        var background = panel.imgRaceBackdrop.rectTransform;
        background.anchorMin = new Vector2(0, 0);
        background.anchorMax = new Vector2(1, 0);
        background.pivot = new Vector2(.5f, 0);
        background.anchoredPosition = new Vector2(0, backgroundBottom - root.rect.yMin);
        background.sizeDelta = new Vector2(0, Mathf.Max(862 * widthScale, root.rect.yMax - backgroundBottom));
        // Sprite底边保护区保留原主体背景；超长屏仅拉伸上方延展区。
        panel.imgRaceBackdrop.type = background.sizeDelta.y > 862 * widthScale + .1f ? Image.Type.Sliced : Image.Type.Simple;
        panel.imgRaceBackdrop.pixelsPerUnitMultiplier = 1 / Mathf.Max(.01f, widthScale);
    }
}
