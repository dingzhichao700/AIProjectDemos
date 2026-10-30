using System;
using cfg;
using UnityEngine;

/// <summary>可受击元素的权威生命数据，不持有表现对象。</summary>
public abstract class BattleCombatantVO : SceneElementVO {
    public BattleSceneCamp camp { get; }
    public double maxHp { get; }
    public double hp { get; private set; }
    public bool alive => !destroyed && hp > 0;

    protected BattleCombatantVO(long id, BattleSceneCamp camp, Vector2 position, double maxHp)
        : base(id, camp == BattleSceneCamp.FRIENDLY ? TimerType.PLAYER : TimerType.ENEMY, position) {
        if (maxHp <= 0 || double.IsNaN(maxHp) || double.IsInfinity(maxHp)) {
            throw new InvalidOperationException("战斗元素生命值必须配置为有限正数。");
        }
        this.camp = camp;
        this.maxHp = maxHp;
        hp = maxHp;
    }

    internal double ApplyDamage(double damage) {
        if (damage <= 0 || double.IsNaN(damage) || double.IsInfinity(damage)) {
            throw new ArgumentOutOfRangeException(nameof(damage));
        }
        if (!alive) {
            return 0;
        }
        double actual = Math.Min(hp, damage);
        hp -= actual;
        return actual;
    }
}
