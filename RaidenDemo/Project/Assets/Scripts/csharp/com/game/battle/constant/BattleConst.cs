using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 雷电战机原型战斗参数，后续由 Luban 配置替换
/// </summary>
public static class BattleConst {

    public const float BattleViewportWidth = 720f;

    public const float BattleViewportHeight = 1280f;

    /**保留已确认版本的临时测试值：自然补给首次延迟，单位秒；后续由 Luban 配置替换*/
    public const float NaturalSupplyFirstDelay = 0.2f;

    /**自然补给生成间隔，单位秒；后续由 Luban 配置替换*/
    public const float NaturalSupplyInterval = 10f;

    /**自然补给在视窗上方区域生成时保留的边缘距离*/
    public const float NaturalSupplySpawnMargin = 90f;

    /**飞机散图统一朝上，敌方阵营在战斗中整体旋转为朝下。*/
    public const float EnemyAircraftVisualRotation = 180f;

    public const string RaidenUnpackImageDirectory = "Assets/Art/unpack/default/raiden/";

    public const string BulletLaunchEffectPrefix = "shootFire_";

    public const string BulletHitEffectPrefix = "hit_";

    public const string PngExtension = ".png";

    public const int PlayerProjectilePoolCapacity = 12;

    public const int UpgradeDropPoolCapacity = 2;

    public const int PlayerInitialLifeCount = 3;

    public const float EnemySpawnInterval = 0.4f;

    public const float EnemyWaveInterval = 1.5f;

    public const float EnemyMoveSpeed = 150f;

    public const float EnemyFormationViewportPadding = 20f;

    public const int EnemyFormationMaxColumns = 5;

    public const float EnemyFormationMemberGap = 20f;

    public const float EnemyFormationRowGap = 30f;

    public const float EnemyDiagonalHorizontalRatio = 0.55f;

    public const float EnemySnakeAmplitude = 105f;

    public const float EnemySnakeFrequency = 3.2f;

    public const float EnemyFormationTurnAmplitude = 150f;

    public const float EnemyFormationTurnDuration = 2.6f;

    public const float EnemyDiagonalFormationVerticalGap = 55f;

    public const float EnemyStraightDriftAmplitude = 48f;

    public const float EnemyStraightDriftDuration = 2.8f;

    public const float EnemyStraightEntryAccelerationDuration = 1.2f;

    public const float EnemyStraightEntrySpeedRatio = 0.65f;

    public const float EnemyStraightCruiseSpeedRatio = 1.15f;

    public const float EnemyHorizontalPassHeight = -320f;

    public const float EnemyHorizontalPassVerticalAmplitude = 18f;

    public const float EnemyHorizontalPassVerticalFrequency = 1.4f;

    public const float EnemyVisualTurnSmoothness = 10f;

    public const float EnemyVisualMaxBankAngle = 45f;

    public const float EnemyHorizontalVisualMaxBankAngle = 90f;


    public const float RewardPickupWarningDuration = 8f;

    public const float RewardPickupWarningFadeHalfCycleDuration = 0.2f;

    public const float RewardPickupWarningMinAlpha = 0.2f;

    public const float RewardLoopEffectScale = 1.25f;

    public const int RewardPickupEffectId = 12008;

    public const float RewardFloatingTextDuration = 0.9f;

    public const float RewardFloatingTextDistance = 80f;

    public const float RewardFloatingTextFadeStartProgress = 0.4f;

    public const string RewardFloatingTextMaterialPath = "Assets/Art/font/materialPreset/fontBodyCommon_OutlineShadow";

    public const float RewardCollectedMoveSpeed = 100f;

    public const float RewardCollectedDecelerationDuration = 1f;

    public static readonly Vector2 RewardFloatingTextPlayerOffset = new Vector2(0f, 120f);

    public const float EliteMoveSpeed = 90f;

    public const float BossMoveSpeed = 70f;

    public const float PlayerHitShakeDuration = 0.1f;

    public const float PlayerHitShakeDistance = 5f;

    public const float PlayerInvincibleDuration = 1f;

    public const float PlayerHitFlashInterval = 0.2f;

    public const float PlayerRespawnInvincibleDuration = 2f;

    public const float PlayerRespawnEnterDuration = 0.8f;

    public const float PlayerFlashInterval = 0.05f;

    public const float PlayerUpgradeChargeDuration = 1f;

    public const float PlayerUpgradeFlashDuration = 0.15f;

    public const float PlayerUpgradeTransformDuration = 0.3f;

    public const float PlayerUpgradeInvincibleDuration = 1f;

    public const float BattleResultDelayAfterDeathPresentation = 1.5f;

    public const float PlayerUpgradeMainEffectScale = 1.5f;

    public const int PlayerUpgradeChargeEffectId = 10001;

    public const int PlayerUpgradeFlashEffectId = 10002;

    public const int PlayerUpgradeTransformEffectId = 10003;

    public const int PlayerUpgradeCompleteEffectId = 10004;

    public static readonly IReadOnlyList<int> FixedStageEffectIds = Array.AsReadOnly(new[] { PlayerUpgradeChargeEffectId, PlayerUpgradeFlashEffectId, PlayerUpgradeTransformEffectId, PlayerUpgradeCompleteEffectId, RewardPickupEffectId });

    public static readonly IReadOnlyList<int> PlayerUpgradeCompleteEffectDelays = Array.AsReadOnly(new[] { 0, 40, 75, 110, 150 });

    public static readonly IReadOnlyList<Vector2> PlayerUpgradeCompleteEffectOffsets = Array.AsReadOnly(new[] { new Vector2(-34f, 18f), new Vector2(30f, 32f), new Vector2(-18f, -25f), new Vector2(38f, -18f), new Vector2(0f, 48f) });

    public const int EnemyContactDamage = 25;

    public const int EnemyScore = 100;

    public const int EliteScore = 1000;

    public const int BossScore = 3000;

    public const float PlayerHealthFillMaxWidth = 237f;

    public static readonly Vector2 UpgradeDropSize = new Vector2(72, 72);

    public static readonly Vector2 BossHealthFillSize = new Vector2(410, 18);

    public static readonly Vector2 EliteHealthBarSize = new Vector2(144, 20);

    public const float EliteHealthBarVerticalGap = 12f;

    public static readonly Vector2 PlayerStart = new Vector2(360, -1010);

    public static readonly Vector2 PlayerRespawnStart = new Vector2(360, -1420);

    public const string SceneBackgroundImageDirectory = "Assets/Art/unpack/default/raiden/sceneBg/";

    public const string EliteHealthBarBackgroundPath = "Assets/Art/unpack/default/raiden/hud/elite_health_bar_bg.png";

    public const string EliteHealthBarFillPath = "Assets/Art/unpack/default/raiden/hud/elite_health_bar_fill.png";

    /**根据雷电模块位图资源名生成 Addressables 资源路径*/
    public static string GetRaidenUnpackImagePath(string resourceName) {
        return RaidenUnpackImageDirectory + resourceName + PngExtension;
    }

    /**根据场景背景资源名生成 Addressables 资源路径*/
    public static string GetSceneBackgroundImagePath(string resourceName) {
        return SceneBackgroundImageDirectory + resourceName + PngExtension;
    }

    /**计算原型阶段的单位生命上限，正式数值将由 Luban 配置替换*/
    public static int GetMaxHealth(bool isPlayer, int level) {
        return isPlayer ? 100 + (level - 1) * 20 : 40 + (level - 1) * 10;
    }

}
