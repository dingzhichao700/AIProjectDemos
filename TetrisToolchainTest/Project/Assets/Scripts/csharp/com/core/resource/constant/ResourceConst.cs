using System.Collections.Generic;

/// <summary>
/// 资源路径常量（baseline：无 b1 业务配置列表）。
/// </summary>
public static class ResourceConst
{
    public const string PATH_CONFIG = "Assets/ConfigBin/";

    const string ART_PATH = "Assets/Art/";

    public const string PATH_UI_REFERENCE = ART_PATH + "UIReference/";
    public const string PATH_UNPACK_IMAGE = ART_PATH + "unpack/";
    public const string PATH_ATLAS = ART_PATH + "atlas/";
    public const string PATH_ATLAS_SOURCE = ART_PATH + "atlasSource/";
    public const string PATH_FRAME_ANIMATION = ART_PATH + "frameAnimation/";
    public const string PATH_MATERIAL = ART_PATH + "material/";
    public const string PATH_AUDIO = ART_PATH + "audio/";

    public const string PATH_ATLAS_SOURCE_RICHTEXTIMAGE = PATH_ATLAS_SOURCE + "richTextImage/";

    // 图集（按五组分包；title 环节至少需要 TITLE）
    public const string PATH_ATLAS_TITLE = PATH_ATLAS + "title/title/";
    public const string PATH_ATLAS_COMMON = PATH_ATLAS + "default/common/";

    /// <summary>Title baseline：设置选项表。</summary>
    public static readonly List<string> ALL_CONFIG_LIST = new List<string>
    {
        "cfgobj_settingoptionobj"
    };

    public static readonly List<string> ALL_MATERIAL_LIST = new List<string>
    {
        "custom/matInstanceImage"
    };

    const string PATH_PREFAB = "Assets/Prefab/";

    public static string GetUIPath(string uiPath)
    {
        return PATH_PREFAB + uiPath + ".prefab";
    }

    public static string GetFrameAnimationPath(string animationName)
    {
        return PATH_FRAME_ANIMATION + animationName;
    }

    public static string GetAudio(string path)
    {
        return PATH_AUDIO + path;
    }

    /// <summary>baseline 占位：按 id 解析音效路径，待接 Luban 后实现。</summary>
    public static string GetAudioPathById(int audioId)
    {
        return string.Empty;
    }
}
