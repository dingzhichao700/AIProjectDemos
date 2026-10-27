using System;
using System.Collections.Generic;

/// <summary>
/// 单机永久解锁记录。
/// </summary>
/// <remarks>不重复保存配置条件或其他模块的玩家进度。</remarks>
[Serializable]
public sealed class FunctionSaveVO {

    public List<int> unlockedIds = new List<int>();

}
