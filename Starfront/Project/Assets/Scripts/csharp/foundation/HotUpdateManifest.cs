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

