using cfg;
using UnityEngine;

public enum BattleSkillEventType { Started, Impact, Completed, Cancelled }

/// <summary>表现层只消费快照，不从动作回调触发效果。</summary>
public sealed class BattleSkillEvent {
    public readonly BattleSkillEventType type;
    public readonly long castId;
    public readonly int skillId;
    public readonly long casterId;
    public readonly long targetId;
    public readonly UnitActionType action;
    public readonly Vector2 position;
    public readonly double timeMs;

    internal BattleSkillEvent(BattleSkillEventType type, BattleSkillCastVO cast, Vector2 position, double timeMs) {
        this.type = type;
        castId = cast.id;
        skillId = cast.skill.config.Id;
        casterId = cast.casterId;
        targetId = cast.targetId;
        action = cast.skill.config.Action;
        this.position = position;
        this.timeMs = timeMs;
    }
}
