using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗模块固定规则、资源约定与视窗边界
/// </summary>
public static class BattleConst {

    /**两轮开火之间的最小间隔，单位毫秒。*/
    public static int fireCooldownMinMs => ConfigValueHelper.GetInt(1001);

    /**同一轮开火中相邻子弹的最小发射间隔，单位毫秒。*/
    public static int shotIntervalMinMs => ConfigValueHelper.GetInt(1002);

    /**一轮开火生成的最少子弹实体数量。*/
    public static int shotCountMin => ConfigValueHelper.GetInt(1003);

    /**一轮开火生成的最多子弹实体数量。*/
    public static int shotCountMax => ConfigValueHelper.GetInt(1004);

    public const float BattleViewportWidth = 720f;

    public const float BattleViewportHeight = 1280f;
    public const float PlayerAreaTop = -BattleViewportHeight * 0.5f;
    public static Rect ViewportBounds => Rect.MinMaxRect(0f, -BattleViewportHeight, BattleViewportWidth, 0f);
    public static Vector2 ClampPlayerPosition(Vector2 position) {
        return new Vector2(Mathf.Clamp(position.x, 0f, BattleViewportWidth),
            Mathf.Clamp(position.y, -BattleViewportHeight, PlayerAreaTop));
    }
    public static bool IsRewardInPlayerArea(Vector2 position, float radius) {
        return position.y + radius <= PlayerAreaTop;
    }

    /**敌机机身与中线之间的缓冲区。*/
    public const float EnemyActivityBottom = PlayerAreaTop + 80f;

    public const float EnemyFormationBankAngle = 15f;

    /**自然补给在视窗上方区域生成时保留的边缘距离*/
    public const float NaturalSupplySpawnMargin = 90f;

    /**飞机散图统一朝上，敌方阵营在战斗中整体旋转为朝下。*/
    public const float EnemyAircraftVisualRotation = 180f;

    public const string RaidenUnpackImageDirectory = "Assets/Art/unpack/default/raiden/";

    public const string BulletLaunchEffectPrefix = "shootFire_";

    public const string BulletHitEffectPrefix = "hit_";

    public const string PngExtension = ".png";

    /**玩家子弹对象池的预热数量。*/
    public static int PlayerProjectilePoolCapacity => ConfigValueHelper.GetInt(6001);

    /**升级道具对象池的预热数量。*/
    public static int UpgradeDropPoolCapacity => ConfigValueHelper.GetInt(6002);

    /**玩家飞机进入关卡时的初始生命数量。*/
    public static int PlayerInitialLifeCount => ConfigValueHelper.GetInt(2001);

    /**敌机编队与视窗边缘之间的距离，单位像素。*/
    public static float EnemyFormationViewportPadding => ConfigValueHelper.GetFloat(3101);

    /**敌机编队允许使用的最大列数。*/
    public static int EnemyFormationMaxColumns => ConfigValueHelper.GetInt(3102);

    /**敌机编队成员的横向间距，单位像素。*/
    public static float EnemyFormationMemberGap => ConfigValueHelper.GetFloat(3103);

    /**敌机编队成员的纵向间距，单位像素。*/
    public static float EnemyFormationRowGap => ConfigValueHelper.GetFloat(3104);

    /**敌机斜向编队相邻列的纵向距离，单位像素。*/
    public static float EnemyDiagonalFormationVerticalGap => ConfigValueHelper.GetFloat(3105);

    /**敌机外观跟随移动方向转向时的平滑度。*/
    public static float EnemyVisualTurnSmoothness => ConfigValueHelper.GetFloat(3106);

    /**道具进入拾取警告状态前允许弹跳的时间，单位秒。*/
    public static float RewardPickupWarningDuration => ConfigValueHelper.GetFloat(4001);

    /**道具拾取警告淡入或淡出的半周期，单位秒。*/
    public static float RewardPickupWarningFadeHalfCycleDuration => ConfigValueHelper.GetFloat(4002);

    /**道具拾取警告闪烁时的最低透明度。*/
    public static float RewardPickupWarningMinAlpha => ConfigValueHelper.GetFloat(4003);

    /**道具循环效果相对道具显示尺寸的缩放。*/
    public static float RewardLoopEffectScale => ConfigValueHelper.GetFloat(4004);

    public const int RewardPickupEffectId = 12008;

    /**道具浮动文字的持续时间，单位秒。*/
    public static float RewardFloatingTextDuration => ConfigValueHelper.GetFloat(4101);

    /**道具浮动文字在持续时间内移动的距离，单位像素。*/
    public static float RewardFloatingTextDistance => ConfigValueHelper.GetFloat(4102);

    /**道具浮动文字开始淡出时的归一化进度。*/
    public static float RewardFloatingTextFadeStartProgress => ConfigValueHelper.GetFloat(4103);

    public const string RewardFloatingTextMaterialPath = "Assets/Art/font/materialPreset/fontBodyCommon_OutlineShadow";

    /**道具被收集后的初始移动速度，单位像素每秒。*/
    public static float RewardCollectedMoveSpeed => ConfigValueHelper.GetFloat(4201);

    /**道具被收集后的减速时间，单位秒。*/
    public static float RewardCollectedDecelerationDuration => ConfigValueHelper.GetFloat(4202);

    public static readonly Vector2 RewardFloatingTextPlayerOffset = new Vector2(0f, 120f);

    /**玩家飞机受击震动的持续时间，单位秒。*/
    public static float PlayerHitShakeDuration => ConfigValueHelper.GetFloat(2101);

    /**玩家飞机受击震动的最大距离，单位像素。*/
    public static float PlayerHitShakeDistance => ConfigValueHelper.GetFloat(2102);

    /**玩家飞机受击后的无敌时间，单位秒。*/
    public static float PlayerInvincibleDuration => ConfigValueHelper.GetFloat(2103);

    /**玩家飞机受击无敌期间的闪烁间隔，单位秒。*/
    public static float PlayerHitFlashInterval => ConfigValueHelper.GetFloat(2104);

    /**玩家飞机复活后的无敌时间，单位秒。*/
    public static float PlayerRespawnInvincibleDuration => ConfigValueHelper.GetFloat(2201);

    /**玩家飞机复活入场的持续时间，单位秒。*/
    public static float PlayerRespawnEnterDuration => ConfigValueHelper.GetFloat(2202);

    /**玩家飞机通用无敌状态的闪烁间隔，单位秒。*/
    public static float PlayerFlashInterval => ConfigValueHelper.GetFloat(2203);

    /**玩家飞机升级蓄力阶段的持续时间，单位秒。*/
    public static float PlayerUpgradeChargeDuration => ConfigValueHelper.GetFloat(2301);

    /**玩家飞机升级闪光阶段的持续时间，单位秒。*/
    public static float PlayerUpgradeFlashDuration => ConfigValueHelper.GetFloat(2302);

    /**玩家飞机升级变身阶段的持续时间，单位秒。*/
    public static float PlayerUpgradeTransformDuration => ConfigValueHelper.GetFloat(2303);

    /**玩家飞机升级完成后的无敌时间，单位秒。*/
    public static float PlayerUpgradeInvincibleDuration => ConfigValueHelper.GetFloat(2304);

    /**玩家飞机死亡表现结束后进入失败结算的等待时间，单位秒。*/
    public static float PlayerDefeatDelayAfterDeathPresentation => ConfigValueHelper.GetFloat(2401);

    /**玩家飞机升级主效果相对飞机显示尺寸的缩放。*/
    public static float PlayerUpgradeMainEffectScale => ConfigValueHelper.GetFloat(2305);

    public const int PlayerUpgradeChargeEffectId = 10001;

    public const int PlayerUpgradeFlashEffectId = 10002;

    public const int PlayerUpgradeTransformEffectId = 10003;

    public const int PlayerUpgradeCompleteEffectId = 10004;

    public static readonly IReadOnlyList<int> FixedStageEffectIds = Array.AsReadOnly(new[] { PlayerUpgradeChargeEffectId, PlayerUpgradeFlashEffectId, PlayerUpgradeTransformEffectId, PlayerUpgradeCompleteEffectId, RewardPickupEffectId });

    public static readonly IReadOnlyList<int> PlayerUpgradeCompleteEffectDelays = Array.AsReadOnly(new[] { 0, 40, 75, 110, 150 });

    public static readonly IReadOnlyList<Vector2> PlayerUpgradeCompleteEffectOffsets = Array.AsReadOnly(new[] { new Vector2(-34f, 18f), new Vector2(30f, 32f), new Vector2(-18f, -25f), new Vector2(38f, -18f), new Vector2(0f, 48f) });

    /**敌机机体与玩家飞机碰撞时造成的伤害。*/
    public static int EnemyContactDamage => ConfigValueHelper.GetInt(3001);

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

}
