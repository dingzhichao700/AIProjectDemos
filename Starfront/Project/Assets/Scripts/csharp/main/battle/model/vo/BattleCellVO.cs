using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>逻辑格子，不保存图片、节点或镜头状态。</summary>
public sealed class BattleCellVO : SceneElementVO {
    public int configId { get; }
    public int q { get; }
    public int k { get; }
    public BattleSceneCamp camp { get; private set; }
    public bool capturable => groundPassable && camp != BattleSceneCamp.ABSOLUTE_NEUTRAL;

    internal bool TrySetCamp(BattleSceneCamp value) {
        if (!capturable || camp == value || (value != BattleSceneCamp.FRIENDLY && value != BattleSceneCamp.ENEMY)) {
            return false;
        }
        camp = value;
        return true;
    }
    public BattleSceneCellType type { get; }
    public CellUnlockResource unlockConfig { get; private set; }

    public int? unlockedCardId { get; private set; }

    internal void SetUnlockConfig(CellUnlockResource rule) {
        unlockConfig = rule;
    }

    internal void SetUnlockResult(int? cardId) {
        unlockedCardId = cardId;
        MarkUnlocked();
    }

    public bool unlocked { get; private set; }
    public bool groundPassable => type == BattleSceneCellType.NORMAL;
    public bool flyingPassable => true;

    public BattleCellVO(long id, BattleSceneCellResource config, Vector2 center) : base(id, TimerType.SCENE, center) {
        configId = config.Id;
        q = config.Q;
        k = config.K;
        camp = config.Camp;
        type = config.Type;
    }

    internal void MarkUnlocked() {
        if (!groundPassable || camp == BattleSceneCamp.ABSOLUTE_NEUTRAL) {
            throw new System.InvalidOperationException("障碍或中立格不能解锁。");
        }
        unlocked = true;
    }

    public override void OnTimeUpdate(float deltaTime) {
        // 静态格子当前没有每帧玩法行为。
    }
}
