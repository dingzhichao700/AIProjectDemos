using UnityEngine;
using TMPro;

/// <summary>将动态文本按实际字体度量容纳于已批准的显示框，保留原字号上限。</summary>
public static class PlayerMatchingTextBounds {
    public static void Fit(TextMeshProUGUI text, bool wrap=false) {
        if (text==null) return;
        if (!text.enableAutoSizing) {
            float original=text.fontSize;
            text.fontSizeMax=original;
            text.fontSizeMin=Mathf.Max(5f,Mathf.Min(12f,original*.6f));
            text.enableAutoSizing=true;
        }
        text.enableWordWrapping=wrap;
        text.overflowMode=TextOverflowModes.Ellipsis;
    }
}
