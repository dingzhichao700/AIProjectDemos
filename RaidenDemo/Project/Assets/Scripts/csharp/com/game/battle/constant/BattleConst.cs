using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 雷电战机原型战斗参数，后续由 Luban 配置替换
/// </summary>
public static class BattleConst {

    /**飞机散图统一朝上，敌方阵营在战斗中整体旋转为朝下。*/
    public const float EnemyAircraftVisualRotation = 180f;

    public const string RaidenUnpackImageDirectory = "Assets/Art/unpack/default/raiden/";

    public const string BulletFrameAnimationDirectory = "default/bullet/";

    public const string BulletBodyEffectPrefix = "body_";

    public const string BulletLaunchEffectPrefix = "shoot_";

    public const string BulletHitEffectPrefix = "hit_";

    public const string PngExtension = ".png";

    public const int PlayerProjectilePoolCapacity = 12;

    public const int UpgradeDropPoolCapacity = 2;

    public const int HealthDropHealAmount = 30;

    public const int LifeDropAddCount = 1;

    public const int PlayerInitialLifeCount = 3;

    public const float WingmanFollowSpeed = 12f;

    public const float FireInterval = 0.24f;

    public const float EnemySpawnInterval = 0.4f;

    public const float EnemyWaveInterval = 1.5f;

    public const float EnemyMoveSpeed = 150f;

    public const float EnemyFireInterval = 1.4f;

    public const float UpgradeDropSpeed = 120f;

    public const float RewardDropAttractRadius = 180f;

    public const float RewardDropAttractSpeed = 720f;

    public const float NaturalSupplyFirstDelay = 12f;

    public const float NaturalSupplyInterval = 15f;

    public const float NaturalSupplySpawnMargin = 90f;

    public const float EliteMoveSpeed = 90f;

    public const float BossMoveSpeed = 70f;

    public const float PlayerHitShakeDuration = 0.18f;

    public const float PlayerHitShakeDistance = 14f;

    public const float PlayerInvincibleDuration = 2f;

    public const float PlayerRespawnInvincibleDuration = 2f;

    public const float PlayerDefeatPresentationDuration = 2f;

    public const float PlayerRespawnEnterDuration = 0.8f;

    public const float PlayerFlashInterval = 0.05f;

    public const float PlayerUpgradeChargeDuration = 1f;

    public const float PlayerUpgradeFlashDuration = 0.15f;

    public const float PlayerUpgradeTransformDuration = 0.3f;

    public const float PlayerUpgradeInvincibleDuration = 1f;

    public const float BossVictoryDelayAfterDeathPresentation = 0.5f;

    public const float PlayerUpgradeMainEffectScale = 1.5f;

    public const float SceneTimerScaleMin = 0.1f;

    public const float SceneTimerScaleMax = 2f;

    public const float SceneTimerScaleStep = 0.1f;

    public const int PlayerUpgradeChargeEffectId = 10001;

    public const int PlayerUpgradeFlashEffectId = 10002;

    public const int PlayerUpgradeTransformEffectId = 10003;

    public const int PlayerUpgradeCompleteEffectId = 10004;

    public static readonly IReadOnlyList<int> FixedStageEffectIds = Array.AsReadOnly(new[] { PlayerUpgradeChargeEffectId, PlayerUpgradeFlashEffectId, PlayerUpgradeTransformEffectId, PlayerUpgradeCompleteEffectId });

    public static readonly IReadOnlyList<int> PlayerUpgradeCompleteEffectDelays = Array.AsReadOnly(new[] { 0, 40, 75, 110, 150 });

    public static readonly IReadOnlyList<Vector2> PlayerUpgradeCompleteEffectOffsets = Array.AsReadOnly(new[] { new Vector2(-34f, 18f), new Vector2(30f, 32f), new Vector2(-18f, -25f), new Vector2(38f, -18f), new Vector2(0f, 48f) });

    public const int EnemyContactDamage = 25;

    public const int EnemyScore = 100;

    public const int EliteScore = 1000;

    public const int BossScore = 3000;

    public const float PlayerHealthFillMaxWidth = 237f;

    public static readonly Vector2 WingmanSize = new Vector2(59, 136);

    public static readonly Vector2 UpgradeDropSize = new Vector2(72, 72);

    public static readonly Vector2 BossHealthFillSize = new Vector2(410, 18);

    public static readonly Vector2 UpgradeDropHitSize = new Vector2(60, 60);

    public static readonly Vector2 PlayerStart = new Vector2(360, -1010);

    public static readonly Vector2 PlayerRespawnStart = new Vector2(360, -1420);

    public static readonly Vector2 WingmanLeftOffset = new Vector2(-145, -10);

    public static readonly Vector2 WingmanRightOffset = new Vector2(145, -10);

    public const string SceneBackgroundImageDirectory = "Assets/Art/unpack/default/raiden/sceneBg/";

    public const string WingmanPath = "Assets/Art/unpack/default/raiden/wingman/self/battleWingman.png";

    public const string HealthDropPath = "Assets/Art/unpack/default/raiden/battleHealthDrop.png";

    public const string UpgradeDropPath = "Assets/Art/unpack/default/raiden/battleUpgradeDrop.png";

    public const string WingmanUpgradeDropPath = "Assets/Art/unpack/default/raiden/battleWingmanUpgradeDrop.png";

    public const string LifeDropPath = "Assets/Art/unpack/default/raiden/battleLifeDrop.png";

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
