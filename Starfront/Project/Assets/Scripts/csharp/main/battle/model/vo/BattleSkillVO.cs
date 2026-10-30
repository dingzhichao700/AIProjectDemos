using cfg.resource;

/// <summary>每个单位独占技能状态；配置对象只读共享。</summary>
public sealed class BattleSkillVO {
    public SkillResource config { get; }
    public double readyAtMs { get; internal set; }

    internal BattleSkillVO(SkillResource config) {
        this.config = config;
    }
}
