using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家飞机表现
/// </summary>
/// <remarks>
/// 集中同步玩家飞机的受击、升级、死亡与复活表现。
/// </remarks>
internal sealed class BattlePlayerPresenter {

    private enum UpgradePhase {
        None,
        Charging,
        Flashing,
        Transforming,
        Completing
    }

    private readonly Func<AircraftVO, RectTransform> getView;
    private readonly Func<AircraftVO, RectTransform> getVisual;
    private readonly Action<AircraftVO> syncUnitView;
    private readonly Func<IReadOnlyList<AircraftVO>> getWingmen;
    private readonly Action<int> applyAircraftLevel;
    private readonly Action<bool> setUpgradeBlocked;
    private readonly Action<bool> setFiringEnabled;
    private readonly BattleEffectPresenter effectPresenter;
    private Color baseColor = Color.white;
    private float baseRotation;
    private UpgradePhase upgradePhase;
    private float upgradePhaseRemaining;
    private int pendingLevel;
    private float completionElapsed;
    private int nextCompletionEffectIndex;
    private FrameAnimationView loopingUpgradeEffect;

    public BattlePlayerPresenter(Func<AircraftVO, RectTransform> getView, Func<AircraftVO, RectTransform> getVisual, Action<AircraftVO> syncUnitView, Func<IReadOnlyList<AircraftVO>> getWingmen, Action<int> applyAircraftLevel, Action<bool> setUpgradeBlocked, Action<bool> setFiringEnabled, BattleEffectPresenter effectPresenter) {
        this.getView = getView;
        this.getVisual = getVisual;
        this.syncUnitView = syncUnitView;
        this.getWingmen = getWingmen;
        this.applyAircraftLevel = applyAircraftLevel;
        this.setUpgradeBlocked = setUpgradeBlocked;
        this.setFiringEnabled = setFiringEnabled;
        this.effectPresenter = effectPresenter;
    }

    /**记录玩家飞机表现的原始颜色和角度。*/
    public void Initialize(AircraftVO player) {
        RectTransform visual = getVisual(player);
        Image image = visual != null ? visual.GetComponent<Image>() : null;
        baseColor = image != null ? image.color : Color.white;
        baseRotation = visual != null ? visual.localEulerAngles.z : 0f;
        upgradePhase = UpgradePhase.None;
        upgradePhaseRemaining = 0f;
        pendingLevel = 0;
        completionElapsed = 0f;
        nextCompletionEffectIndex = 0;
    }

    /**推进玩家生命周期、受击和升级表现。*/
    public void Update(AircraftVO player, float deltaTime) {
        SyncLifecycle(player);
        RefreshHitFeedback(player);
        UpdateUpgrade(player, deltaTime);
    }

    /**开始玩家飞机升级的蓄能、换装和完成表现。*/
    public void BeginUpgrade(AircraftVO player, int targetLevel) {
        pendingLevel = targetLevel;
        completionElapsed = 0f;
        nextCompletionEffectIndex = 0;
        upgradePhase = UpgradePhase.Charging;
        upgradePhaseRemaining = BattleConst.PlayerUpgradeChargeDuration;
        setUpgradeBlocked(true);
        setFiringEnabled(false);
        loopingUpgradeEffect = effectPresenter.PlayPlayerUpgrade(BattleConst.PlayerUpgradeChargeEffectId, getView(player), true, default, BattleConst.PlayerUpgradeMainEffectScale);
        RefreshHitFeedback(player);
    }

    /**进入死亡阶段时隐藏僚机并保留玩家死亡表现。*/
    public void OnDefeatStarted(AircraftVO player) {
        SetFormationVisible(player, false, false);
        RectTransform visual = getVisual(player);
        if (visual != null) {
            visual.gameObject.SetActive(true);
        }
    }

    /**开始复活时重置玩家飞机的视觉变换。*/
    public void OnRespawnStarted(AircraftVO player) {
        syncUnitView(player);
        RectTransform visual = getVisual(player);
        if (visual != null) {
            visual.anchoredPosition = Vector2.zero;
            visual.localEulerAngles = new Vector3(0f, 0f, baseRotation);
            visual.localScale = Vector3.one;
        }
        SetFormationVisible(player, true, true);
        RefreshHitFeedback(player);
    }

    /**完成复活时同步最终坐标与闪烁状态。*/
    public void OnRespawnCompleted(AircraftVO player) {
        syncUnitView(player);
        RefreshHitFeedback(player);
    }

    /**立即刷新玩家受击或无敌表现。*/
    public void RefreshHitFeedback(AircraftVO player) {
        RectTransform visual = getVisual(player);
        if (visual == null) {
            return;
        }
        if (player != null && (player.lifecycleState == PlayerLifecycleState.Alive || player.lifecycleState == PlayerLifecycleState.Respawning)) {
            int visibilityPhase = Mathf.FloorToInt(player.invincibleRemaining / player.invincibleFlashInterval);
            visual.gameObject.SetActive(player.invincibleRemaining <= 0f || visibilityPhase % 2 != 0);
        }
        float shakeProgress = BattleConst.PlayerHitShakeDuration <= 0f || player == null
            ? 1f
            : 1f - player.hitShakeRemaining / BattleConst.PlayerHitShakeDuration;
        float shakeOffset = player != null && player.hitShakeRemaining > 0f
            ? Mathf.Sin(shakeProgress * Mathf.PI * 2f) * BattleConst.PlayerHitShakeDistance
            : 0f;
        visual.anchoredPosition = new Vector2(shakeOffset, 0f);
        Image image = visual.GetComponent<Image>();
        if (image == null) {
            return;
        }
        Color color = baseColor;
        image.color = color;
    }

    /**清除尚未完成的升级表现状态。*/
    public void Clear() {
        StopLoopingUpgradeEffect();
        upgradePhase = UpgradePhase.None;
        upgradePhaseRemaining = 0f;
        pendingLevel = 0;
        completionElapsed = 0f;
        nextCompletionEffectIndex = 0;
        baseColor = Color.white;
        baseRotation = 0f;
        setUpgradeBlocked(false);
    }

    private void SyncLifecycle(AircraftVO player) {
        if (player == null) {
            return;
        }
        if (player.lifecycleState == PlayerLifecycleState.Respawning) {
            syncUnitView(player);
        }
    }

    private void UpdateUpgrade(AircraftVO player, float deltaTime) {
        if (upgradePhase == UpgradePhase.None || player == null) {
            return;
        }
        if (upgradePhase == UpgradePhase.Completing) {
            UpdateCompletionEffects(player, deltaTime);
            return;
        }
        upgradePhaseRemaining = Mathf.Max(0f, upgradePhaseRemaining - deltaTime);
        if (upgradePhaseRemaining > 0f) {
            return;
        }
        if (upgradePhase == UpgradePhase.Charging) {
            StopLoopingUpgradeEffect();
            effectPresenter.PlayPlayerUpgrade(BattleConst.PlayerUpgradeFlashEffectId, getView(player), false, default, BattleConst.PlayerUpgradeMainEffectScale);
            upgradePhase = UpgradePhase.Flashing;
            upgradePhaseRemaining = BattleConst.PlayerUpgradeFlashDuration;
            return;
        }
        if (upgradePhase == UpgradePhase.Flashing) {
            applyAircraftLevel(pendingLevel);
            setFiringEnabled(true);
            player.GrantInvincibility(BattleConst.PlayerUpgradeInvincibleDuration);
            effectPresenter.PlayPlayerUpgrade(BattleConst.PlayerUpgradeTransformEffectId, getView(player), false, default, BattleConst.PlayerUpgradeMainEffectScale);
            upgradePhase = UpgradePhase.Transforming;
            upgradePhaseRemaining = BattleConst.PlayerUpgradeTransformDuration;
            return;
        }
        BeginCompletionEffects(player);
    }

    /**开始错时播放升级完成粒子并解除升级保护。*/
    private void BeginCompletionEffects(AircraftVO player) {
        upgradePhase = UpgradePhase.Completing;
        completionElapsed = 0f;
        nextCompletionEffectIndex = 0;
        pendingLevel = 0;
        setUpgradeBlocked(false);
        RefreshHitFeedback(player);
        UpdateCompletionEffects(player, 0f);
    }

    /**按配置好的时间差和局部偏移依次播放完成粒子。*/
    private void UpdateCompletionEffects(AircraftVO player, float deltaTime) {
        completionElapsed += deltaTime;
        while (nextCompletionEffectIndex < BattleConst.PlayerUpgradeCompleteEffectDelays.Count && completionElapsed * 1000f >= BattleConst.PlayerUpgradeCompleteEffectDelays[nextCompletionEffectIndex]) {
            Vector2 offset = BattleConst.PlayerUpgradeCompleteEffectOffsets[nextCompletionEffectIndex];
            effectPresenter.PlayPlayerUpgrade(BattleConst.PlayerUpgradeCompleteEffectId, getView(player), false, offset);
            nextCompletionEffectIndex++;
        }
        if (nextCompletionEffectIndex >= BattleConst.PlayerUpgradeCompleteEffectDelays.Count) {
            upgradePhase = UpgradePhase.None;
            completionElapsed = 0f;
        }
    }

    private void StopLoopingUpgradeEffect() {
        if (loopingUpgradeEffect == null) {
            return;
        }
        loopingUpgradeEffect.Destroy();
        loopingUpgradeEffect = null;
    }

    private void SetFormationVisible(AircraftVO player, bool playerVisible, bool wingmenVisible) {
        SetVisualVisible(player, playerVisible);
        IReadOnlyList<AircraftVO> wingmen = getWingmen?.Invoke();
        if (wingmen == null) return;
        foreach (AircraftVO wingman in wingmen) SetVisualVisible(wingman, wingmenVisible);
    }

    private void SetVisualVisible(AircraftVO unit, bool visible) {
        RectTransform visual = getVisual(unit);
        if (visual != null) {
            visual.gameObject.SetActive(visible);
        }
    }
}
