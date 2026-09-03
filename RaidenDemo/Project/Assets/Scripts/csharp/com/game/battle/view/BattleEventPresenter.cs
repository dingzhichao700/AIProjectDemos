using System;
using cfg;
using UnityEngine;

/// <summary>
/// 战斗事件表现
/// </summary>
/// <remarks>
/// 响应战斗 Model 事件并同步场景、玩家、特效与 HUD。
/// </remarks>
internal sealed class BattleEventPresenter {

    private readonly BattleModel model;
    private readonly BattleScenePresenter scenePresenter;
    private readonly BattleBackgroundPresenter backgroundPresenter;
    private readonly BattleEffectPresenter effectPresenter;
    private readonly BattleHudPresenter hudPresenter;
    private readonly BattlePlayerPresenter playerPresenter;
    private readonly BattleFormationPresenter formationPresenter;
    private readonly BattlePlayerInputPresenter inputPresenter;
    private readonly BattlePlayerConfigCoordinator playerConfig;
    private readonly Action<int> applyPlayerLevel;
    private readonly Action<bool> completeBattle;
    private float healthFeedbackRemaining;

    public BattleEventPresenter(BattleModel model, BattleScenePresenter scenePresenter,
        BattleBackgroundPresenter backgroundPresenter, BattleEffectPresenter effectPresenter,
        BattleHudPresenter hudPresenter, BattlePlayerPresenter playerPresenter,
        BattleFormationPresenter formationPresenter, BattlePlayerInputPresenter inputPresenter,
        BattlePlayerConfigCoordinator playerConfig, Action<int> applyPlayerLevel,
        Action<bool> completeBattle) {
        this.model = model;
        this.scenePresenter = scenePresenter;
        this.backgroundPresenter = backgroundPresenter;
        this.effectPresenter = effectPresenter;
        this.hudPresenter = hudPresenter;
        this.playerPresenter = playerPresenter;
        this.formationPresenter = formationPresenter;
        this.inputPresenter = inputPresenter;
        this.playerConfig = playerConfig;
        this.applyPlayerLevel = applyPlayerLevel;
        this.completeBattle = completeBattle;
    }

    /**订阅本局全部逻辑与时间事件。*/
    public void Bind() {
        model.sceneTimeUpdated += OnSceneTimeUpdate;
        model.playerTimeUpdated += OnPlayerTimeUpdate;
        model.enemyTimeUpdated += OnEnemyTimeUpdate;
        model.playerProjectileHitEnemy += OnPlayerProjectileHitEnemy;
        model.enemyProjectileHitPlayer += OnEnemyProjectileHitPlayer;
        model.rewardCollected += OnRewardCollected;
        model.scoreChanged += hudPresenter.SetScore;
        model.victoryRequested += OnVictoryRequested;
        model.playerStatusChanged += OnPlayerStatusChanged;
        model.playerDefeatStarted += OnPlayerDefeatStarted;
        model.playerRespawnStarted += OnPlayerRespawnStarted;
        model.playerRespawnCompleted += OnPlayerRespawnCompleted;
        model.playerDefaultLevelRequested += OnPlayerDefaultLevelRequested;
        model.defeatRequested += OnDefeatRequested;
    }

    /**解除本局全部逻辑与时间事件。*/
    public void Unbind() {
        model.sceneTimeUpdated -= OnSceneTimeUpdate;
        model.playerTimeUpdated -= OnPlayerTimeUpdate;
        model.enemyTimeUpdated -= OnEnemyTimeUpdate;
        model.playerProjectileHitEnemy -= OnPlayerProjectileHitEnemy;
        model.enemyProjectileHitPlayer -= OnEnemyProjectileHitPlayer;
        model.rewardCollected -= OnRewardCollected;
        model.scoreChanged -= hudPresenter.SetScore;
        model.victoryRequested -= OnVictoryRequested;
        model.playerStatusChanged -= OnPlayerStatusChanged;
        model.playerDefeatStarted -= OnPlayerDefeatStarted;
        model.playerRespawnStarted -= OnPlayerRespawnStarted;
        model.playerRespawnCompleted -= OnPlayerRespawnCompleted;
        model.playerDefaultLevelRequested -= OnPlayerDefaultLevelRequested;
        model.defeatRequested -= OnDefeatRequested;
    }

    /**刷新战斗初始 HUD。*/
    public void RefreshHud() {
        hudPresenter.SetProgress(model.missionProgress);
        hudPresenter.SetScore(model.score);
        RefreshPlayer();
    }

    public void Clear() {
        healthFeedbackRemaining = 0f;
        hudPresenter.ClearFloatingTexts();
        hudPresenter.ResetFeedbackColors();
    }

    private void OnSceneTimeUpdate(float deltaTime) {
        if (!model.isPlaying) {
            return;
        }
        backgroundPresenter.Update(deltaTime);
        scenePresenter.SyncSceneViews();
        hudPresenter.UpdateFloatingTexts(deltaTime);
        UpdateHealthFeedback(deltaTime);
    }

    private void OnPlayerTimeUpdate(float deltaTime) {
        if (!model.isPlaying) {
            return;
        }
        inputPresenter.Update(model.isPlayerAlive);
        formationPresenter.Sync();
        playerPresenter.Update(formationPresenter.player, deltaTime);
        scenePresenter.SyncPlayerViews();
        effectPresenter.Update(deltaTime, TimerType.PLAYER);
    }

    private void OnEnemyTimeUpdate(float deltaTime) {
        if (model.isPlaying) {
            scenePresenter.SyncEnemyViews();
            effectPresenter.Update(deltaTime, TimerType.ENEMY);
        }
    }

    private void OnPlayerProjectileHitEnemy(BulletVO projectile,
        AircraftVO enemy, Vector2 contactPoint) {
        effectPresenter.PlayBulletHit(projectile.hitEffectId, contactPoint, TimerType.ENEMY);
    }

    private void OnEnemyProjectileHitPlayer(BulletVO projectile, Vector2 contactPoint) {
        effectPresenter.PlayBulletHit(projectile.hitEffectId, contactPoint, TimerType.PLAYER);
    }

    private void OnRewardCollected(RewardVO reward, int healed) {
        scenePresenter.PlayRewardPickup(reward, () => model.RemoveReward(reward));
        Vector2 floatingTextPosition = formationPresenter.player != null ? formationPresenter.player.position + BattleConst.RewardFloatingTextPlayerOffset : reward.position;
        hudPresenter.PlayFloatingText(floatingTextPosition, reward.pickupText);
        if (reward.type == StageItemEffectType.HEALTH) {
            if (healed > 0 && hudPresenter.playerHealthText != null) {
                hudPresenter.playerHealthText.color = new Color32(80, 255, 120, 255);
                healthFeedbackRemaining = 0.65f;
            }
            return;
        }
        if (reward.type == StageItemEffectType.PLAYER_UPGRADE) {
            if (playerConfig.TryGetUpgradeLevel(out int targetLevel)) {
                playerPresenter.BeginUpgrade(formationPresenter.player, targetLevel);
            }
            return;
        }
        if (reward.type == StageItemEffectType.ADD_WINGMAN) {
            formationPresenter.ApplyWingmanReward();
            return;
        }
        if (hudPresenter.playerHealthText != null) {
            hudPresenter.playerHealthText.color = new Color32(255, 220, 70, 255);
            healthFeedbackRemaining = 0.65f;
        }
    }

    private void OnPlayerStatusChanged() {
        RefreshPlayer();
        playerPresenter.RefreshHitFeedback(formationPresenter.player);
    }

    private void OnPlayerDefeatStarted() {
        playerPresenter.OnDefeatStarted(formationPresenter.player);
        effectPresenter.PlayAircraftDeath(formationPresenter.GetView(formationPresenter.player), formationPresenter.player, true);
    }

    private void OnPlayerDefaultLevelRequested() {
        applyPlayerLevel(playerConfig.GetDefaultLevel());
    }

    private void OnPlayerRespawnStarted() {
        playerPresenter.OnRespawnStarted(formationPresenter.player);
        RefreshPlayer();
    }

    private void OnPlayerRespawnCompleted() {
        playerPresenter.OnRespawnCompleted(formationPresenter.player);
    }

    private void OnVictoryRequested() {
        completeBattle(true);
    }

    private void OnDefeatRequested() {
        completeBattle(false);
    }

    public void RefreshPlayer() {
        hudPresenter.RefreshPlayer(formationPresenter.player, model.playerLives);
    }

    private void UpdateHealthFeedback(float deltaTime) {
        if (healthFeedbackRemaining <= 0f || hudPresenter.playerHealthText == null) {
            return;
        }
        healthFeedbackRemaining = Mathf.Max(0f, healthFeedbackRemaining - deltaTime);
        if (healthFeedbackRemaining <= 0f) {
            hudPresenter.ResetFeedbackColors();
        }
    }
}
