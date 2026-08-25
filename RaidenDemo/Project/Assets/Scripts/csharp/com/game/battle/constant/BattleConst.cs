using UnityEngine;

/// <summary>
/// 雷电战机原型战斗参数，后续由 Luban 配置替换
/// </summary>
public static class BattleConst {

    public const string RaidenUnpackImageDirectory = "Assets/Art/unpack/default/raiden/";

    public const string PngExtension = ".png";

    public const int PlayerProjectilePoolCapacity = 12;

    public const int UpgradeDropPoolCapacity = 2;

    public const int HealthDropHealAmount = 30;

    public const int LifeDropAddCount = 1;

    public const int PlayerInitialLifeCount = 3;

    public const int ElitePoolCapacity = 2;

    public const int BossPoolCapacity = 1;

    public const float BackgroundScrollSpeed = 120f;

    public const float WingmanFollowSpeed = 12f;

    public const float FireInterval = 0.24f;

    public const float PlayerLaserSpeed = 1050f;

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

    public const float PlayerFlashInterval = 0.12f;

    public const float PlayerUpgradePresentationDuration = 1.5f;


    public const float EnemyDefeatDuration = 0.28f;

    public const int EnemyContactDamage = 25;

    public const int EnemyScore = 100;

    public const int EliteScore = 1000;

    public const int BossScore = 3000;

    public const float PlayerHealthFillMaxWidth = 237f;

    public static readonly Vector2 PlayerSize = new Vector2(242, 238);

    public static readonly Vector2 WingmanSize = new Vector2(59, 136);

    public static readonly Vector2 PlayerLaserSize = new Vector2(20, 215);

    public static readonly Vector2 UpgradeDropSize = new Vector2(72, 72);

    public static readonly Vector2 EliteInterceptorSize = new Vector2(193, 180);

    public static readonly Vector2 BossStage01Size = new Vector2(362, 330);

    public static readonly Vector2 BossHealthBgSize = new Vector2(420, 28);

    public static readonly Vector2 BossHealthFillSize = new Vector2(410, 18);

    public static readonly Vector2 PlayerHitSize = new Vector2(72, 96);

    public static readonly Vector2 PlayerLaserHitSize = new Vector2(14, 120);

    public static readonly Vector2 EliteInterceptorHitSize = new Vector2(150, 130);

    public static readonly Vector2 BossStage01HitSize = new Vector2(290, 250);

    public static readonly Vector2 UpgradeDropHitSize = new Vector2(60, 60);

    public static readonly Vector2 PlayerStart = new Vector2(360, -1010);

    public static readonly Vector2 PlayerRespawnStart = new Vector2(360, -1420);

    public static readonly Vector2 WingmanLeftOffset = new Vector2(-145, -10);

    public static readonly Vector2 WingmanRightOffset = new Vector2(145, -10);

    public const string BackgroundPath = "Assets/Art/unpack/default/raiden/battleBackground.png";

    public const string WingmanPath = "Assets/Art/unpack/default/raiden/wingman/self/battleWingman.png";

    public const string PlayerLaserPath = "Assets/Art/unpack/default/raiden/bullet/self/battlePlayerLaser.png";

    public const string HealthDropPath = "Assets/Art/unpack/default/raiden/battleHealthDrop.png";

    public const string UpgradeDropPath = "Assets/Art/unpack/default/raiden/battleUpgradeDrop.png";

    public const string WingmanUpgradeDropPath = "Assets/Art/unpack/default/raiden/battleWingmanUpgradeDrop.png";

    public const string LifeDropPath = "Assets/Art/unpack/default/raiden/battleLifeDrop.png";

    public const string EliteInterceptorPath = "Assets/Art/unpack/default/raiden/aircraft/enemy/battleEliteInterceptor.png";

    public const string BossStage01Path = "Assets/Art/unpack/default/raiden/aircraft/enemy/battleBossStage01.png";

    /**根据雷电模块位图资源名生成 Addressables 资源路径*/
    public static string GetRaidenUnpackImagePath(string resourceName) {
        return RaidenUnpackImageDirectory + resourceName + PngExtension;
    }

    /**计算原型阶段的单位生命上限，正式数值将由 Luban 配置替换*/
    public static int GetMaxHealth(bool isPlayer, int level) {
        return isPlayer ? 100 + (level - 1) * 20 : 40 + (level - 1) * 10;
    }

    /**计算原型阶段的子弹伤害，正式数值将由 Luban 配置替换*/
    public static int GetProjectileDamage(bool isPlayer, int level) {
        return isPlayer ? 10 + (level - 1) * 3 : 5 + (level - 1) * 2;
    }

}
