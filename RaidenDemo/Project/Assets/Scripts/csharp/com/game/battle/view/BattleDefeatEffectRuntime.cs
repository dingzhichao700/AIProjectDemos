using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敌机死亡表现状态
/// </summary>
/// <remarks>
/// 保存敌机死亡表现播放期间的临时 View 状态。
/// </remarks>
internal sealed class BattleDefeatEffectRuntime {

    public readonly RectTransform root;
    public readonly RectTransform visual;
    public readonly Image image;
    public float remaining;

    public BattleDefeatEffectRuntime(RectTransform root, RectTransform visual) {
        this.root = root;
        this.visual = visual;
        image = visual != null ? visual.GetComponent<Image>() : null;
        if (image != null) {
            image.color = Color.white;
        }
        remaining = BattleConst.EnemyDefeatDuration;
    }
}
