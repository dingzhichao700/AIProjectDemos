using UnityEngine;

public static class ColorConst
{
    public static readonly Color BLACK = Parse("#000000");
    public static readonly Color WHITE = Parse("#FFFFFF");
    public static readonly Color RED = Parse("#ff5858");
    public static readonly Color RED_DARK = Parse("#9A2828");

    static Color Parse(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }
}
