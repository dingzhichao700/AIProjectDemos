using UnityEngine;

/// <summary>按固定顺序创建单局战斗配置、逻辑对象和初始表现。</summary>
internal sealed class BattleSetupCoordinator {

    private readonly BattleModel model;
    private readonly BattleConfigProvider configProvider;
    private readonly BattlePlayerConfigCoordinator playerConfig;
    private readonly BattleFormationPresenter formationPresenter;
    private readonly BattlePlayerPresenter playerPresenter;
    private readonly BattleBackgroundPresenter backgroundPresenter;
    private readonly BattleHudPresenter hudPresenter;
    private readonly BattleVisualPool visualPool;
    private readonly RectTransform effectLayer;

    public BattleSetupCoordinator(BattleModel model, BattleConfigProvider configProvider, BattlePlayerConfigCoordinator playerConfig, BattleFormationPresenter formationPresenter, BattlePlayerPresenter playerPresenter, BattleBackgroundPresenter backgroundPresenter, BattleHudPresenter hudPresenter, BattleVisualPool visualPool, RectTransform effectLayer) {
        this.model = model;
        this.configProvider = configProvider;
        this.playerConfig = playerConfig;
        this.formationPresenter = formationPresenter;
        this.playerPresenter = playerPresenter;
        this.backgroundPresenter = backgroundPresenter;
        this.hudPresenter = hudPresenter;
        this.visualPool = visualPool;
        this.effectLayer = effectLayer;
    }

    /**初始化关卡、出战飞机、背景和预热对象。*/
    public StageConfigVO Initialize(int stageId) {
        StageConfigVO stage = configProvider.GetStage(stageId);
        model.InitializeStage(stage, stageId);
        playerConfig.Initialize();
        backgroundPresenter.Initialize(configProvider.GetSceneBackground(stage.sceneId));
        AircraftVO player = formationPresenter.CreatePlayer(playerConfig.current);
        playerConfig.ApplyBattleStats(model, player);
        formationPresenter.ApplyPlayerVisual(playerConfig.current);
        playerPresenter.Initialize(player);
        hudPresenter.Initialize();
        BattlePrewarmService.Prewarm(stage, visualPool, effectLayer);
        return stage;
    }
}
