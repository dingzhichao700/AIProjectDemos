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

    /**解锁提示图标共享固定目录，文件名由常量表映射。*/
    public static string UnlockIconResource(int configId) {
        return "scene/battle/effects/" + UnlockIcons[configId.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    }

    public const int InitialSceneId = 1001;
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
