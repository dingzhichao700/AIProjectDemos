using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 创建战斗界面运行时所需的基础图片与文本节点。
/// </summary>
internal static class BattleViewFactory {

    /**创建无射线响应的运行时图片节点*/
    public static RectTransform CreateImage(string name, RectTransform parent,
        Vector2 size, Vector2 position, string imagePath, Vector2? pivot = null,
        Vector2? anchor = null) {
        GameObject imageObject = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        SetupRect(rect, parent, size, position, pivot ?? new Vector2(0f, 1f), anchor);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        UITools.SetImage(image, imagePath);
        return rect;
    }

    /**创建沿用指定字体的运行时文本节点*/
    public static TextMeshProUGUI CreateText(string name, RectTransform parent,
        Vector2 size, Vector2 position, float fontSize, TMP_FontAsset font) {
        GameObject textObject = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetupRect(rect, parent, size, position,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.outlineColor = new Color32(0, 0, 0, 220);
        text.outlineWidth = 0.2f;
        return text;
    }

    /**统一设置运行时 RectTransform 的挂点、尺寸和位置*/
    public static void SetupRect(RectTransform rect, RectTransform parent, Vector2 size,
        Vector2 position, Vector2 pivot, Vector2? anchor = null) {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0f, 1f);
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
