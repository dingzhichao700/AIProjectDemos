using System.Collections.Generic;

/// <summary>
/// 历史无Manifest工程的兼容分包常量；正式项目按ResourcePackRouting的唯一CLI报告归组。
/// </summary>
public static class AddressAblesConst
{
    public static readonly HashSet<string> PACK_TOGETHER_GROUPS = new HashSet<string>
    {
        "opening",
        "title",
        "default"
    };

    public static readonly HashSet<string> PACK_BY_LABEL_GROUPS = new HashSet<string>
    {
        "cutScene",
        "scene"
    };
}
