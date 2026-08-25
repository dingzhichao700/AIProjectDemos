using System;

/// <summary>统一编排战斗模块的监听、时间流和运行对象清理顺序。</summary>
internal sealed class BattleLifecycleCoordinator {

    private readonly BattleModel model;
    private readonly BattleScenePresenter scenePresenter;
    private readonly BattlePlayerPresenter playerPresenter;
    private readonly BattleBackgroundPresenter backgroundPresenter;
    private readonly BattleEffectPresenter effectPresenter;
    private readonly BattleEntityViewManager entityViews;
    private readonly BattleVisualPool visualPool;

    public BattleLifecycleCoordinator(BattleModel model,
        BattleScenePresenter scenePresenter, BattlePlayerPresenter playerPresenter,
        BattleBackgroundPresenter backgroundPresenter,
        BattleEffectPresenter effectPresenter, BattleEntityViewManager entityViews,
        BattleVisualPool visualPool) {
        this.model = model;
        this.scenePresenter = scenePresenter;
        this.playerPresenter = playerPresenter;
        this.backgroundPresenter = backgroundPresenter;
        this.effectPresenter = effectPresenter;
        this.entityViews = entityViews;
        this.visualPool = visualPool;
    }

    /**打开前清除上一次运行残留，保证初始化起点唯一。*/
    public void ResetForOpen(Action unbindPanelEvents, Action removeUiListeners,
        Action resumeTimers, Action clearVisualLayers, Action resetPanelReferences) {
        StopAndUnbind(unbindPanelEvents, removeUiListeners);
        resumeTimers?.Invoke();
        ClearRuntime(clearVisualLayers, resetPanelReferences);
    }

    /**按固定顺序订阅表现事件并启动场景时间流。*/
    public void Start(Action bindPanelEvents, Action addUiListeners) {
        bindPanelEvents?.Invoke();
        scenePresenter.Bind(model);
        addUiListeners?.Invoke();
        model.StartTimeFlow();
    }

    /**关闭战斗并完整释放本局运行状态。*/
    public void Shutdown(Action unbindPanelEvents, Action removeUiListeners,
        Action resumeTimers, Action clearVisualLayers, Action resetPanelReferences) {
        model.CloseBattle();
        StopAndUnbind(unbindPanelEvents, removeUiListeners);
        resumeTimers?.Invoke();
        ClearRuntime(clearVisualLayers, resetPanelReferences);
    }

    private void StopAndUnbind(Action unbindPanelEvents, Action removeUiListeners) {
        model.SetSimulationActive(false);
        model.StopTimeFlow();
        scenePresenter.Unbind();
        unbindPanelEvents?.Invoke();
        removeUiListeners?.Invoke();
    }

    private void ClearRuntime(Action clearVisualLayers, Action resetPanelReferences) {
        backgroundPresenter.Clear();
        effectPresenter.Clear();
        playerPresenter.Clear();
        entityViews.Clear();
        visualPool.Clear();
        clearVisualLayers?.Invoke();
        model.Clear();
        resetPanelReferences?.Invoke();
    }
}
