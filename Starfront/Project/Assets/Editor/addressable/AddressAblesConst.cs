using System.Collections.Generic;

/// <summary>
/// Addressables 分包常量（baseline：五组，无 region）。
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
