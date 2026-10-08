/// <summary>统一处理战斗暂停、结算和界面跳转。</summary>
internal sealed class BattleNavigationCoordinator {

    private readonly BattleModel model;
    private readonly BattleTimerPauseController timerPauseController;

    public BattleNavigationCoordinator(BattleModel model,
        BattleTimerPauseController timerPauseController) {
        this.model = model;
        this.timerPauseController = timerPauseController;
    }

    public void Pause(BattlePanel owner) {
        if (!model.PauseBattle()) {
            return;
        }
        timerPauseController.Pause();
        PanelMgr.ins.OpenPanel(UIEnum.BATTLE_PAUSE_PANEL, new object[] { owner });
    }

    public void Resume() {
        if (model.ResumeBattle()) {
            timerPauseController.Resume();
        }
    }

    public void Restart(BattlePanel owner, int stageId, BattleFlowState requiredState) {
        if (model.flowState != requiredState) {
            return;
        }
        model.CloseBattle();
        owner.Close();
        PanelMgr.ins.OpenPanel(UIEnum.BATTLE_PANEL, new object[] { stageId });
    }

    public void Exit(BattlePanel owner, int stageId, BattleFlowState requiredState) {
        if (model.flowState != requiredState) {
            return;
        }
        model.CloseBattle();
        owner.Close();
        PanelMgr.ins.OpenPanel(UIEnum.STAGE_SELECT_PANEL, new object[] { stageId });
    }

    public void Complete(BattlePanel owner, int stageId, bool victory) {
        if (!model.SettleBattle()) {
            return;
        }
        int reward = victory ? 320 : 0;
        if (victory) {
            RaidenControl.ins.CompleteStage(stageId, model.score);
        }
        PanelMgr.ins.OpenPanel(UIEnum.BATTLE_RESULT_PANEL,
            new object[] { owner, victory, model.score, reward });
    }
}
