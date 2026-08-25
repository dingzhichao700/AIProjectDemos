using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>集中装配单个 BattlePanel 所需的战斗表现与流程对象。</summary>
internal sealed class BattleCompositionRoot {

    public BattleEffectPresenter effectPresenter { get; }
    public BattleHudPresenter hudPresenter { get; }
    public BattleFormationPresenter formationPresenter { get; }
    public BattlePlayerPresenter playerPresenter { get; }
    public BattlePlayerInputPresenter inputPresenter { get; }
    public BattleNavigationCoordinator navigationCoordinator { get; }
    public BattleScenePresenter scenePresenter { get; }
    public BattleBackgroundPresenter backgroundPresenter { get; }
    public BattleEventPresenter eventPresenter { get; }
    public BattleSetupCoordinator setupCoordinator { get; }
    public BattleLifecycleCoordinator lifecycleCoordinator { get; }

    public BattleCompositionRoot(BattleModel model, BattleConfigProvider configProvider, BattlePlayerConfigCoordinator playerConfig, BattleTimerPauseController timerPauseController, BattleVisualPool visualPool, BattleEntityViewManager entityViews, RectTransform backgroundLayer, RectTransform entityLayer, RectTransform projectileLayer, RectTransform effectLayer, Image progressFill, TextMeshProUGUI lifeText, TextMeshProUGUI scoreText, RectTransform bossHealthRoot, Image bossHealthFill, TextMeshProUGUI bossHealthText, Action<Vector2> setPlayerPosition, Action<int> applyPlayerLevel, Action<bool> completeBattle) {
        effectPresenter = new BattleEffectPresenter(effectLayer, visualPool);
        hudPresenter = new BattleHudPresenter(progressFill, lifeText, scoreText, bossHealthRoot, bossHealthFill, bossHealthText);
        formationPresenter = new BattleFormationPresenter(entityLayer, model, visualPool, entityViews);
        playerPresenter = new BattlePlayerPresenter(formationPresenter.GetVisual, formationPresenter.SyncUnit, () => formationPresenter.leftWingman, () => formationPresenter.rightWingman, applyPlayerLevel, model.SetPlayerUpgradeBlocked);
        inputPresenter = new BattlePlayerInputPresenter(entityLayer, () => playerConfig.current?.collision, setPlayerPosition);
        navigationCoordinator = new BattleNavigationCoordinator(model, timerPauseController);
        scenePresenter = new BattleScenePresenter(entityLayer, projectileLayer, effectLayer, bossHealthRoot, bossHealthFill, visualPool, entityViews, effectPresenter, hudPresenter);
        backgroundPresenter = new BattleBackgroundPresenter(backgroundLayer);
        eventPresenter = new BattleEventPresenter(model, scenePresenter, backgroundPresenter, effectPresenter, hudPresenter, playerPresenter, formationPresenter, inputPresenter, playerConfig, applyPlayerLevel, completeBattle);
        setupCoordinator = new BattleSetupCoordinator(model, configProvider, playerConfig, formationPresenter, playerPresenter, backgroundPresenter, hudPresenter, visualPool, effectLayer);
        lifecycleCoordinator = new BattleLifecycleCoordinator(model, scenePresenter, playerPresenter, backgroundPresenter, effectPresenter, entityViews, visualPool);
    }
}
