using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗背景表现
/// </summary>
/// <remarks>
/// 创建并同步关卡循环滚动背景。
/// </remarks>
internal sealed class BattleBackgroundPresenter {
    private readonly RectTransform layer;
    private readonly List<RectTransform> backgrounds = new List<RectTransform>();

    public BattleBackgroundPresenter(RectTransform layer) {
        this.layer = layer;
    }

    public void Initialize() {
        backgrounds.Clear();
        backgrounds.Add(BattleViewFactory.CreateImage("imgBattleBackgroundA", layer,
            new Vector2(720f, 1280f), Vector2.zero, BattleConst.BackgroundPath));
        backgrounds.Add(BattleViewFactory.CreateImage("imgBattleBackgroundB", layer,
            new Vector2(720f, 1280f), new Vector2(0f, 1280f), BattleConst.BackgroundPath));
    }

    public void Update(float deltaTime) {
        float distance = BattleConst.BackgroundScrollSpeed * deltaTime;
        foreach (RectTransform background in backgrounds) {
            background.anchoredPosition += Vector2.down * distance;
        }
        foreach (RectTransform background in backgrounds) {
            if (background.anchoredPosition.y <= -1280f) {
                RectTransform other = backgrounds[0] == background
                    ? backgrounds[1]
                    : backgrounds[0];
                background.anchoredPosition = new Vector2(0f,
                    other.anchoredPosition.y + 1280f);
            }
        }
    }

    public void Clear() {
        backgrounds.Clear();
    }
}
