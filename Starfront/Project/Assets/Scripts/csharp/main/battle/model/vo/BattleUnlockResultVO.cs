using cfg;
using UnityEngine;

/// <summary>成功付费解锁的只读结果；不包含表现对象，不参与后续抽取。</summary>
public sealed class BattleUnlockResultVO {
    public long cellId { get; }
    public int? cardId { get; }
    public BattleSceneCamp camp { get; }
    public Vector2 position { get; }

    public BattleUnlockResultVO(BattleCellVO cell, BattleSceneCamp actor) {
        cellId = cell.id;
        cardId = cell.unlockedCardId;
        camp = actor;
        position = cell.position;
    }
}
