using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家飞机表现
/// </summary>
/// <remarks>
/// 集中同步玩家飞机的受击、升级、死亡与复活表现。
/// </remarks>
internal sealed class BattlePlayerPresenter {

    private readonly Func<AircraftVO, RectTransform> getVisual;
    private readonly Action<AircraftVO> syncUnitView;
    private readonly Func<AircraftVO> getLeftWingman;
    private readonly Func<AircraftVO> getRightWingman;
    private readonly Action<int> applyAircraftLevel;
    private readonly Action<bool> setUpgradeBlocked;
    private Color baseColor = Color.white;
    private float baseRotation;
    private float upgradeRemaining;
    private int pendingLevel;

    public BattlePlayerPresenter(Func<AircraftVO, RectTransform> getVisual,
        Action<AircraftVO> syncUnitView, Func<AircraftVO> getLeftWingman,
        Func<AircraftVO> getRightWingman, Action<int> applyAircraftLevel,
        Action<bool> setUpgradeBlocked) {
        this.getVisual = getVisual;
        this.syncUnitView = syncUnitView;
        this.getLeftWingman = getLeftWingman;
        this.getRightWingman = getRightWingman;
        this.applyAircraftLevel = applyAircraftLevel;
        this.setUpgradeBlocked = setUpgradeBlocked;
    }

    /**记录玩家飞机表现的原始颜色和角度。*/
    public void Initialize(AircraftVO player) {
        RectTransform visual = getVisual(player);
        Image image = visual != null ? visual.GetComponent<Image>() : null;
        baseColor = image != null ? image.color : Color.white;
        baseRotation = visual != null ? visual.localEulerAngles.z : 0f;
        upgradeRemaining = 0f;
        pendingLevel = 0;
    }

    /**推进玩家生命周期、受击和升级表现。*/
    public void Update(AircraftVO player, float deltaTime) {
        SyncLifecycle(player);
        RefreshHitFeedback(player);
        UpdateUpgrade(player, deltaTime);
    }

    /**开始升级闪动并在演出期间赋予无敌。*/
    public void BeginUpgrade(AircraftVO player, int targetLevel) {
        pendingLevel = targetLevel;
        upgradeRemaining = BattleConst.PlayerUpgradePresentationDuration;
        setUpgradeBlocked(true);
        player.GrantInvincibility(upgradeRemaining);
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
        if (player != null && player.invincibleRemaining > 0f) {
            int phase = Mathf.FloorToInt(player.invincibleRemaining /
                BattleConst.PlayerFlashInterval);
            color.a *= phase % 2 == 0 ? 0.25f : 1f;
        }
        image.color = color;
    }

    /**清除尚未完成的升级表现状态。*/
    public void Clear() {
        upgradeRemaining = 0f;
        pendingLevel = 0;
        baseColor = Color.white;
        baseRotation = 0f;
        setUpgradeBlocked(false);
    }

    private void SyncLifecycle(AircraftVO player) {
        if (player == null) {
            return;
        }
        if (player.lifecycleState == PlayerLifecycleState.Dying) {
            RectTransform visual = getVisual(player);
            if (visual == null) {
                return;
            }
            float duration = BattleConst.PlayerDefeatPresentationDuration;
            float progress = duration <= 0f
                ? 1f
                : 1f - player.lifecycleRemaining / duration;
            visual.localEulerAngles = new Vector3(0f, 0f,
                baseRotation + progress * 540f);
            visual.localScale = Vector3.one * Mathf.Max(0.05f, 1f - progress);
            Image image = visual.GetComponent<Image>();
            if (image != null) {
                Color color = baseColor;
                color.a *= 1f - progress;
                image.color = color;
            }
        } else if (player.lifecycleState == PlayerLifecycleState.Respawning) {
            syncUnitView(player);
        }
    }

    private void UpdateUpgrade(AircraftVO player, float deltaTime) {
        RectTransform visual = getVisual(player);
        if (upgradeRemaining <= 0f || visual == null) {
            return;
        }
        upgradeRemaining = Mathf.Max(0f, upgradeRemaining - deltaTime);
        float duration = BattleConst.PlayerUpgradePresentationDuration;
        float progress = duration <= 0f ? 1f : 1f - upgradeRemaining / duration;
        visual.localScale = Vector3.one *
            (1f + Mathf.Sin(progress * Mathf.PI * 8f) * 0.06f);
        Image image = visual.GetComponent<Image>();
        if (image != null) {
            Color current = image.color;
            Color tint = Color.Lerp(baseColor,
                new Color(0.45f, 1f, 1f, baseColor.a), 0.45f);
            tint.a = current.a;
            image.color = tint;
        }
        if (upgradeRemaining > 0f) {
            return;
        }
        int targetLevel = pendingLevel;
        pendingLevel = 0;
        setUpgradeBlocked(false);
        visual.localScale = Vector3.one;
        applyAircraftLevel(targetLevel);
        RefreshHitFeedback(player);
    }

    private void SetFormationVisible(AircraftVO player, bool playerVisible,
        bool wingmenVisible) {
        SetVisualVisible(player, playerVisible);
        SetVisualVisible(getLeftWingman(), wingmenVisible);
        SetVisualVisible(getRightWingman(), wingmenVisible);
    }

    private void SetVisualVisible(AircraftVO unit, bool visible) {
        RectTransform visual = getVisual(unit);
        if (visual != null) {
            visual.gameObject.SetActive(visible);
        }
    }
}
