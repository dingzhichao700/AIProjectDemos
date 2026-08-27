using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗背景表现
/// </summary>
/// <remarks>
/// 创建并同步关卡循环滚动背景。
/// </remarks>
internal sealed class BattleBackgroundPresenter {
    private const float BackgroundWidth = 720f;
    private const float BackgroundHeight = 2560f;
    private readonly RectTransform layer;
    private readonly List<RectTransform[]> backgroundLayers = new List<RectTransform[]>();
    private readonly List<float> scrollSpeeds = new List<float>();

    public BattleBackgroundPresenter(RectTransform layer) {
        this.layer = layer;
    }

    /**按远近顺序创建四层循环背景。*/
    public void Initialize(BattleSceneBackgroundVO config) {
        backgroundLayers.Clear();
        scrollSpeeds.Clear();
        AddLayer("Far", config.backgroundRes, config.backgroundScrollSpeed, true);
        AddLayer("Low", config.lowRes, config.lowScrollSpeed, false);
        AddLayer("Middle", config.middleRes, config.middleScrollSpeed, false);
        AddLayer("High", config.highRes, config.highScrollSpeed, false);
    }

    /**使用场景计时器增量同步各层视差滚动。*/
    public void Update(float deltaTime) {
        for (int layerIndex = 0; layerIndex < backgroundLayers.Count; layerIndex++) {
            RectTransform[] pair = backgroundLayers[layerIndex];
            float distance = scrollSpeeds[layerIndex] * deltaTime;
            foreach (RectTransform background in pair) {
                background.anchoredPosition += Vector2.down * distance;
            }
            foreach (RectTransform background in pair) {
                if (background.anchoredPosition.y <= -BackgroundHeight) {
                    RectTransform other = pair[0] == background ? pair[1] : pair[0];
                    background.anchoredPosition = new Vector2(0f, other.anchoredPosition.y + BackgroundHeight);
                }
            }
        }
    }

    public void Clear() {
        backgroundLayers.Clear();
        scrollSpeeds.Clear();
    }

    /**创建同一视差层首尾衔接的两张图片。*/
    private void AddLayer(string layerName, string resourceName, float scrollSpeed, bool required) {
        if (string.IsNullOrWhiteSpace(resourceName)) {
            if (required) {
                throw new System.InvalidOperationException("场景背景必须配置远景地表资源");
            }
            return;
        }
        string path = BattleConst.GetSceneBackgroundImagePath(resourceName);
        Vector2 size = new Vector2(BackgroundWidth, BackgroundHeight);
        RectTransform first = BattleViewFactory.CreateImage($"imgBattleBackground{layerName}A", layer, size, Vector2.zero, path);
        RectTransform second = BattleViewFactory.CreateImage($"imgBattleBackground{layerName}B", layer, size, new Vector2(0f, BackgroundHeight), path);
        backgroundLayers.Add(new[] { first, second });
        scrollSpeeds.Add(scrollSpeed);
    }
}
