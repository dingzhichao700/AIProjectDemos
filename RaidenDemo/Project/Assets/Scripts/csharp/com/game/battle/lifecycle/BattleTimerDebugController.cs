using UnityEngine;

/// <summary>
/// 战斗场景 Timer 调试控制器
/// </summary>
internal sealed class BattleTimerDebugController {

    private const float MinScale = 0.1f;
    private const float MaxScale = 1f;
    private const float ScaleStep = 0.2f;

    private float targetScale = MaxScale;
    private bool debugPaused;
    private bool battlePaused;

    /**降低场景、玩家和敌方 Timer 的目标倍率*/
    public void DecreaseScale() {
        SetTargetScale(targetScale - ScaleStep);
    }

    /**提高场景、玩家和敌方 Timer 的目标倍率*/
    public void IncreaseScale() {
        SetTargetScale(targetScale + ScaleStep);
    }

    /**切换调试暂停状态*/
    public void TogglePause() {
        debugPaused = !debugPaused;
        ApplyScale();
        Debug.Log(debugPaused ? "场景 Timer 调试暂停" : $"场景 Timer 恢复，倍率：{targetScale:0.0}");
    }

    /**同步正式战斗暂停状态*/
    public void SetBattlePaused(bool value) {
        battlePaused = value;
        ApplyScale();
    }

    /**恢复默认倍率并清除全部暂停状态*/
    public void Reset() {
        targetScale = MaxScale;
        debugPaused = false;
        battlePaused = false;
        ApplyScale();
    }

    private void SetTargetScale(float value) {
        targetScale = Mathf.Round(Mathf.Clamp(value, MinScale, MaxScale) * 10f) / 10f;
        ApplyScale();
        Debug.Log($"场景 Timer 目标倍率：{targetScale:0.0}");
    }

    private void ApplyScale() {
        float scale = debugPaused || battlePaused ? 0f : targetScale;
        RookieEngine.sceneTimer.scale = scale;
        RookieEngine.playerTimer.scale = scale;
        RookieEngine.enemyTimer.scale = scale;
    }

}
