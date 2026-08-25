using System;

/// <summary>
/// 本地游戏数据。
/// </summary>
[Serializable]
public class SaveGameDataVO : SerializableSaveData {

    /// <summary>
    /// 传奇方块的历史最高分。
    /// </summary>
    public int tetrisHighScore;

}
