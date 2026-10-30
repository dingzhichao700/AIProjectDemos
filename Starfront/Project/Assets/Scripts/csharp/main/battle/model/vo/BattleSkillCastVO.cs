public enum BattleSkillStage { Windup, Recovery, Completed, Cancelled }

/// <summary>一次释放锁定一个目标；时序由逻辑Timer决定。</summary>
public sealed class BattleSkillCastVO {
    public long id { get; }
    public BattleSkillVO skill { get; }
    public long casterId { get; }
    public long targetId { get; }
    public double startedAtMs { get; }
    public double impactAtMs => startedAtMs + skill.config.WindupMs;
    public double finishedAtMs => impactAtMs + skill.config.RecoveryMs;
    public int nextEffectIndex { get; internal set; }
    public BattleSkillStage stage { get; internal set; } = BattleSkillStage.Windup;

    internal BattleSkillCastVO(long id, BattleSkillVO skill, long casterId, long targetId, double startedAtMs) {
        this.id = id;
        this.skill = skill;
        this.casterId = casterId;
        this.targetId = targetId;
        this.startedAtMs = startedAtMs;
    }
}
