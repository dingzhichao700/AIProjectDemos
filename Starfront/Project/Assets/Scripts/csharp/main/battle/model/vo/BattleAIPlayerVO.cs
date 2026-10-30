using cfg.resource;

/// <summary>单局电脑玩家的决策时钟与诊断计数，不保存视觉对象或修改源配置。</summary>
public sealed class BattleAIPlayerVO {
    public BattleCampVO owner { get; }
    public BattleAIProfileResource profile { get; }
    public double remainingMs { get; internal set; }
    public int decisionCount { get; internal set; }
    public int unlockCount { get; internal set; }

    internal BattleAIPlayerVO(BattleCampVO owner, BattleAIProfileResource profile) {
        this.owner = owner;
        this.profile = profile;
        remainingMs = profile.InitialDelayMs;
    }
}
