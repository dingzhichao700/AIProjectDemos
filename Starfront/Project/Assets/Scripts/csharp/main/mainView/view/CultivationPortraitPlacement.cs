using System;
using cfg.resource;

/// <summary>
/// 培养页专用立绘落位参数；不改变其他用途或Sprite原始锚点。
/// </summary>
public static class CultivationPortraitPlacement {

    /**校正只适用于审核时绑定的素材，换图后回到基准位置等待重新校准。*/
    public static float ReadOffsetY(CardArtResource art, string portraitAddress) {
        if (!MatchesSource(art, portraitAddress) || !art.CultivationOffsetY.HasValue) {
            return 0;
        }
        return art.CultivationOffsetY.Value;
    }

    /**横向校正与纵向校正使用同一素材身份，避免影响其他展示用途。*/
    public static float ReadOffsetX(CardArtResource art, string portraitAddress) {
        if (!MatchesSource(art, portraitAddress) || !art.CultivationOffsetX.HasValue) {
            return 0;
        }
        return art.CultivationOffsetX.Value;
    }

    /**以基准适配框为基础等比放大，不将所有单位限制成同一宽度。*/
    public static float ReadScale(CardArtResource art, string portraitAddress) {
        if (!MatchesSource(art, portraitAddress) || !art.CultivationScale.HasValue) {
            return 1;
        }
        double scale = art.CultivationScale.Value;
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) {
            return 1;
        }
        return (float)scale;
    }

    private static bool MatchesSource(CardArtResource art, string portraitAddress) {
        return art != null && !string.IsNullOrWhiteSpace(art.CultivationPortraitSource) && string.Equals(art.CultivationPortraitSource, portraitAddress, StringComparison.Ordinal);
    }

}
