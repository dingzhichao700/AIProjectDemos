using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 共用战力图片字显示：只装配业务传入的数值，不计算或保存战力。
/// </summary>
public sealed class PowerDisplay : MonoBehaviour {

    public RectTransform digitsArea;
    public Sprite[] digits;
    public Sprite missing;
    public float glyphHeight = 34.435f;
    public float nominalAdvance = 34.435f;
    public float trackingReduction = 1.565f;
    private readonly List<Image> glyphs = new List<Image>();
    private string displayed = "—";

    public string DisplayedValue => displayed;
    public int VisibleGlyphCount => displayed.Length;

    public void SetValue(double? value) {
        // 查询负责权威数值；整数展示沿用阵容既有不带分组符格式。
        string next = value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value >= 0
            ? value.Value.ToString("0", CultureInfo.InvariantCulture) : "—";
        displayed = next;
        Render();
    }

    private void OnEnable() {
        Render();
    }

    private void OnRectTransformDimensionsChange() {
        Render();
    }

    private void Render() {
        if (digitsArea == null || digits == null || digits.Length != 10 || missing == null) {
            return;
        }
        if (glyphs.Count == 0) {
            for (int i = 0; i < digitsArea.childCount; i++) {
                var image = digitsArea.GetChild(i).GetComponent<Image>();
                if (image != null) glyphs.Add(image);
            }
        }
        int count = displayed.Length;
        while (glyphs.Count < count) {
            var image = new GameObject("imgPowerDigit" + glyphs.Count, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(digitsArea, false);
            image.raycastTarget = false;
            glyphs.Add(image);
        }
        float width = Mathf.Max(0, digitsArea.rect.width);
        float scale = Mathf.Min(1, width / Mathf.Max(1, count * nominalAdvance), digitsArea.rect.height / Mathf.Max(1, glyphHeight));
        float height = glyphHeight * scale;
        // R3：保留字高，收紧步进；数值区右边界由统一Prefab确定。
        float advance = Mathf.Max(0, nominalAdvance * scale - trackingReduction * scale);
        float total = count * advance;
        for (int i = 0; i < glyphs.Count; i++) {
            Image image = glyphs[i];
            image.gameObject.SetActive(i < count);
            if (i >= count) continue;
            Sprite sprite = displayed[i] == '—' ? missing : digits[displayed[i] - '0'];
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            float naturalWidth = sprite == null ? 0 : height * sprite.rect.width / sprite.rect.height;
            rect.sizeDelta = new Vector2(naturalWidth, height);
            rect.anchoredPosition = new Vector2(-total + advance * (i + .5f), 0);
        }
    }

}
