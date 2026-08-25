/// <summary>统一冻结并恢复战斗场景使用的三类 Timer。</summary>
internal sealed class BattleTimerPauseController {

    private bool paused;
    private float sceneScale;
    private float playerScale;
    private float enemyScale;

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
}
