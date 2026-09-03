using TMPro;
using UnityEngine;

/// <summary>
/// 战斗 HUD 飘字表现
/// </summary>
/// <remarks>
/// 根据坐标和文本创建一次向上移动并逐渐淡出的提示文字。
/// </remarks>
internal sealed class BattleFloatingTextView {

    private readonly RectTransform rect;
    private readonly TextMeshProUGUI label;
    private float elapsed;

    public BattleFloatingTextView(RectTransform parent, TextMeshProUGUI template, Vector2 position, string content) {
        GameObject gameObject = new GameObject("txtFloatingReward", typeof(RectTransform), typeof(TextMeshProUGUI));
        rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 48f);
        label = gameObject.GetComponent<TextMeshProUGUI>();
        label.font = template.font;
        label.fontSharedMaterial = ResourceManager.GetMaterial(BattleConst.RewardFloatingTextMaterialPath);
        label.fontSize = 28f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        label.color = Color.white;
        label.text = content;
    }

    /**返回当前飘字是否仍需继续播放。*/
    public bool Update(float deltaTime) {
        elapsed += deltaTime;
        float progress = Mathf.Clamp01(elapsed / BattleConst.RewardFloatingTextDuration);
        rect.anchoredPosition += Vector2.up * (BattleConst.RewardFloatingTextDistance / BattleConst.RewardFloatingTextDuration) * deltaTime;
        float fadeProgress = Mathf.InverseLerp(BattleConst.RewardFloatingTextFadeStartProgress, 1f, progress);
        Color color = label.color;
        color.a = 1f - fadeProgress;
        label.color = color;
        return progress < 1f;
    }

    public void Dispose() {
        if (rect != null) {
            Object.Destroy(rect.gameObject);
        }
    }
}
