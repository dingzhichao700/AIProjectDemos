using System;

/// <summary>
/// 四套有序方案和已选用编号的持久化快照。
/// </summary>
/// <remarks>编号从1开始；0表示空位。草稿不写入本快照。</remarks>
[Serializable]
public sealed class TeamSchemeSaveVO {

    [Newtonsoft.Json.JsonRequired]
    public int schemaVersion = 1;

    [Newtonsoft.Json.JsonRequired]
    public long revision;

    [Newtonsoft.Json.JsonRequired]
    public int selected = 1;

    [Newtonsoft.Json.JsonRequired]
    public int[][] schemes = { new int[4], new int[4], new int[4], new int[4] };

}
