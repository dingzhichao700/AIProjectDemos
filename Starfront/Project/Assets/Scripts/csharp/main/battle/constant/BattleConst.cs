using UnityEngine;

/// <summary>已确认的标准格子几何；场景大小、原点、布局均读取配置。</summary>
public static class BattleConst {
    // 已审核的场景生产圆环素材，尺寸读取Sprite，不参与玩法数值。
    public const string ProductionBackgroundResource = "scene/battle/appearance/Production_BG";
    public const string ProductionFillResource = "scene/battle/appearance/Production_Fill";
    /**对局倒计时时长，单位秒。*/
    public static int DurationSeconds => ConfigValueHelper.GetInt(1000);
    /**首版己方显示名称。*/
    public static string FriendlyName => ConfigValueHelper.GetString(1001);
    /**首版敌方显示名称。*/
    public static string EnemyName => ConfigValueHelper.GetString(1002);
    /**解锁内容配置ID到图标文件名的映射，相对固定特效目录。*/
    public static System.Collections.Generic.IReadOnlyDictionary<string, string> UnlockIcons => ConfigValueHelper.GetMap<string>(1003);

    /**解锁提示光效的unpack相对路径。*/
    public static string UnlockGlowResource => ConfigValueHelper.GetString(1004);

    /**纯表现呼吸周期，单位秒。*/
    public static float UnlockPulseSeconds => ConfigValueHelper.GetFloat(1005);

    /**光效呼吸的最低不透明度。*/
    public static float UnlockMinAlpha => ConfigValueHelper.GetFloat(1006);

    /**交互文字反馈显示时长，单位秒。*/
    public static float InteractionHintSeconds => ConfigValueHelper.GetFloat(1007);

    /**费用图标的unpack相对路径。*/
    public static string UnlockCurrencyResource => ConfigValueHelper.GetString(1008);

    /**解锁卡牌样板尺寸、弹出/停留/淡出毫秒与布局。*/
    public static System.Collections.Generic.IReadOnlyDictionary<string, float> CardRevealLayout => ConfigValueHelper.GetMap<float>(1009);
    /**样板品质色，键为Quality枚举名，Background为内底色。*/
    public static System.Collections.Generic.IReadOnlyDictionary<string, string> CardRevealColors => ConfigValueHelper.GetMap<string>(1010);

    /**解锁提示图标共享固定目录，文件名由常量表映射。*/
    public static string UnlockIconResource(int configId) {
        return "scene/battle/effects/" + UnlockIcons[configId.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    }

    /**索敌查询周期，毫秒。*/
    public static int TargetScanMs => ConfigValueHelper.GetInt(1100);
    /**移动目标重寻路周期，毫秒。*/
    public static int RepathMs => ConfigValueHelper.GetInt(1101);
    /**受击闪烁时长，秒。*/
    public static float HitFlashSeconds => ConfigValueHelper.GetFloat(1102);
    /**常态头顶至血条下边缘间距，场景像素。*/
    public static float UnitHealthGap => ConfigValueHelper.GetFloat(1103);
    /**建筑血条相对格心上移，像素。*/
    public static float BuildingHealthOffset => ConfigValueHelper.GetFloat(1104);
    /**血条背景资源，按单位/建筑区分。*/
    public static System.Collections.Generic.IReadOnlyDictionary<string, string> HealthBackgrounds => ConfigValueHelper.GetMap<string>(1105);
    /**己方血条填充资源。*/
    public static System.Collections.Generic.IReadOnlyDictionary<string, string> FriendlyHealthFills => ConfigValueHelper.GetMap<string>(1106);
    /**敌方血条填充资源。*/
    public static System.Collections.Generic.IReadOnlyDictionary<string, string> EnemyHealthFills => ConfigValueHelper.GetMap<string>(1107);

    /**阴影从水平向左朝下旋转的角度，范围(0,180)，单位度。*/
    public static float ShadowAngle => ConfigValueHelper.GetFloat(1108);
    /**阴影投影长度系数，有限正数。*/
    public static float ShadowLength => ConfigValueHelper.GetFloat(1109);
    /**阴影不透明度，范围[0,1]。*/
    public static float ShadowAlpha => ConfigValueHelper.GetFloat(1110);

    /**尸体完成死亡动画后保留时长，单位毫秒。*/
    public static int DeathHoldMs => ConfigValueHelper.GetInt(1111);
    /**尸体及阴影同步淡出时长，单位毫秒。*/
    public static int DeathFadeMs => ConfigValueHelper.GetInt(1112);

    public const int InitialSceneId = 1002;
    public const float CellWidth = 107f;
    public const float CellHeight = 93f;
    public const float ColumnStep = 80.25f;
    public const float RowStep = 46.5f;

    public static Vector2 CellCenter(float originX, float originY, int q, int k) {
        return new Vector2(originX + ColumnStep * q, originY + RowStep * k);
    }

    public static bool Contains(Vector2 local) {
        float x = Mathf.Abs(local.x);
        float y = Mathf.Abs(local.y);
        return y <= CellHeight / 2 && x <= CellWidth / 2 - y * CellWidth / (2 * CellHeight);
    }
}
