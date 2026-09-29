using UnityEngine;

/// <summary>
/// 战斗场景 Timer 暂停控制器
/// </summary>
internal sealed class BattleTimerPauseController {

    private readonly BattleTimerDebugController debugController;
    private bool paused;

    /// <summary>
    /// 创建正式暂停控制器
    /// </summary>
    /// <param name="debugController">场景 Timer 倍率与暂停状态的统一执行对象</param>
    public BattleTimerPauseController(BattleTimerDebugController debugController) {
        this.debugController = debugController;
    }

    /**恢复默认倍率并清除暂停状态*/
    public void Reset() {
        paused = false;
        debugController.Reset();
    }

    /**保存当前倍率并冻结场景、玩家和敌方时间流。*/
    public void Pause() {
        if (paused) {
            return;
        }
        paused = true;
        debugController.SetBattlePaused(true);
    }

    /**恢复冻结前的三类 Timer 倍率。*/
    public void Resume() {
        if (!paused) {
            return;
        }
        paused = false;
        debugController.SetBattlePaused(false);
    }

}
