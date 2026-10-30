using UnityEngine;

public enum BattleUnitState { Marching, Chasing, Attacking, Waiting, Dead }
public enum BattleCombatEventType { StateChanged, Attack, Damaged, Died }

/// <summary>事件包含值快照，目标移除后表现仍可使用最后位置。</summary>
public sealed class BattleCombatEvent {
    public BattleCombatEventType type { get; }
    public long sourceId { get; }
    public long targetId { get; }
    public Vector2 position { get; }
    public double damage { get; }
    public double hp { get; }
    public double maxHp { get; }
    public BattleUnitState state { get; }

    internal BattleCombatEvent(BattleCombatEventType type, long sourceId, BattleCombatantVO target, double damage = 0) {
        this.type = type;
        this.sourceId = sourceId;
        targetId = target.id;
        position = target.position;
        this.damage = damage;
        hp = target.hp;
        maxHp = target.maxHp;
        state = target is BattleUnitVO unit ? unit.state : BattleUnitState.Waiting;
    }
}
