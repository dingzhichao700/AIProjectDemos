using System;

/// <summary>
/// 与底座版本匹配的远程程序集清单。
/// </summary>
[Serializable]
public class HotUpdateManifest {

    public string baseVersion;
    public string release;
    public string resourceRoot;
    public string bundleRoot;
    public string[] aot;
    public string[] assemblies;

}

/// <summary>
/// 包内热更新启动配置。
/// </summary>
[Serializable]
public class HotUpdateBootConfig {

    public string baseVersion;
    public string bootstrapUrl;

}

/// <summary>
/// 项目入口中的底座版本映射。
/// </summary>
[Serializable]
public class HotUpdateIndex {

    public HotUpdateBaseEntry[] bases;

}

/// <summary>
/// 指定底座对应的发布清单地址。
/// </summary>
[Serializable]
public class HotUpdateBaseEntry {

    public string baseVersion;
    public string manifestUrl;

}
