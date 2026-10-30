using cfg;

public enum BattleEndReason { BaseDestroyed, TimeExpired }

/// <summary>一局只生成一次的结算快照；中立枚举表示平局，不改写阵营。</summary>
public sealed class BattleResultVO {
    public BattleSceneCamp winner { get; }
    public bool draw => winner == BattleSceneCamp.ABSOLUTE_NEUTRAL;
    public BattleEndReason reason { get; }
    public int friendlyTerritory { get; }
    public int enemyTerritory { get; }

    internal BattleResultVO(BattleSceneCamp winner, BattleEndReason reason, int friendlyTerritory, int enemyTerritory) {
        this.winner = winner;
        this.reason = reason;
        this.friendlyTerritory = friendlyTerritory;
        this.enemyTerritory = enemyTerritory;
    }
}
