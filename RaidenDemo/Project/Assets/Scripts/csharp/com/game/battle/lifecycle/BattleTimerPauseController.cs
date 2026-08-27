using UnityEngine;

/// <summary>统一冻结并恢复战斗场景使用的三类 Timer。</summary>
internal sealed class BattleTimerPauseController {

    private bool paused;
    private float sceneScale;
    private float playerScale;
    private float enemyScale;

    /**同步调整场景内三类 Timer 的时间倍率。*/
    public void AdjustScale(float offset) {
        float currentScale = paused ? sceneScale : RookieEngine.sceneTimer.scale;
        float targetScale = Mathf.Clamp(Mathf.Round((currentScale + offset) * 10f) / 10f, BattleConst.SceneTimerScaleMin, BattleConst.SceneTimerScaleMax);
        sceneScale = targetScale;
        playerScale = targetScale;
        enemyScale = targetScale;
        if (!paused) {
            ApplyScale(targetScale);
        }
        Debug.Log($"战斗场景 Timer 倍率：{targetScale:F1}");
    }

    /**保存当前倍率并冻结场景、玩家和敌方时间流。*/
    public void Pause() {
        if (paused) {
            return;
        }
        sceneScale = RookieEngine.sceneTimer.scale;
        playerScale = RookieEngine.playerTimer.scale;
        enemyScale = RookieEngine.enemyTimer.scale;
        RookieEngine.sceneTimer.scale = 0f;
        RookieEngine.playerTimer.scale = 0f;
        RookieEngine.enemyTimer.scale = 0f;
        paused = true;
    }

    /**恢复冻结前的三类 Timer 倍率。*/
    public void Resume() {
        if (!paused) {
            return;
        }
        RookieEngine.sceneTimer.scale = sceneScale;
        RookieEngine.playerTimer.scale = playerScale;
        RookieEngine.enemyTimer.scale = enemyScale;
        paused = false;
    }

    /**将三类场景 Timer 设置为相同倍率。*/
    private void ApplyScale(float scale) {
        RookieEngine.sceneTimer.scale = scale;
        RookieEngine.playerTimer.scale = scale;
        RookieEngine.enemyTimer.scale = scale;
    }
}
