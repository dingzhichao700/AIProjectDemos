using System;

/// <summary>
/// 指定底座对应的发布清单地址。
/// </summary>
[Serializable]
public class HotUpdateBaseEntry {

    public string baseVersion;
    public string manifestUrl;

}
