using System;
using System.Linq;
using cfg;
using UnityEngine;

/// <summary>复用场景分组Timer；移动分发结束后索敌、攻击并处理死亡。</summary>
public sealed class BattleCombatModel : IDisposable {
    private readonly BattleSceneModel scene;
    public event Action<BattleCombatEvent> Changed;
    public BattleSkillModel skills { get; }
    public BattleProjectileModel projectiles { get; }

    internal BattleCombatModel(BattleSceneModel scene) {
        this.scene = scene;
        skills = new BattleSkillModel(scene, this);
        projectiles = new BattleProjectileModel(scene, this);
        if (BattleConst.TargetScanMs <= 0 || BattleConst.RepathMs <= 0) {
            throw new InvalidOperationException("战斗查询周期必须为正。");
        }
        scene.playerTimeUpdated += OnPlayerTime;
        scene.enemyTimeUpdated += OnEnemyTime;
    }

    private void OnPlayerTime(float delta) {
        Advance(BattleSceneCamp.FRIENDLY, delta);
    }

    private void OnEnemyTime(float delta) {
        Advance(BattleSceneCamp.ENEMY, delta);
    }

    internal void Advance(BattleSceneCamp camp, float delta) {
        if (scene.expired || delta == 0) {
            return;
        }
        if (delta < 0 || float.IsNaN(delta) || float.IsInfinity(delta)) {
            throw new ArgumentOutOfRangeException(nameof(delta));
        }
        projectiles.Advance(camp, delta * 1000d);
        // 攻击可能移除敌人并释放名额；快照避免集合变更，也避免新兵当帧攻击。
        var units = scene.elements.Values.OfType<BattleUnitVO>().Where(u => u.alive && u.camp == camp).OrderBy(u => u.id).ToArray();
        foreach (var unit in units) {
            if (scene.expired) {
                return;
            }
            if (!unit.alive) {
                continue;
            }
            Tick(unit, delta * 1000d);
        }
    }

    private BattleCombatantVO GetTarget(long id) {
        return scene.elements.TryGetValue(id, out var value) && value is BattleCombatantVO target && target.alive ? target : null;
    }

    private void Tick(BattleUnitVO unit, double deltaMs) {
        unit.scanRemainingMs -= deltaMs;
        unit.pathRemainingMs -= deltaMs;
        if (unit.activeCast != null) {
            unit.StopMoving();
            skills.Advance(unit, GetTarget(unit.activeCast.targetId), deltaMs, true);
            return;
        }
        long previousTarget = unit.targetId;
        var normalAttack = unit.skills.Single(s => s.config.Trigger == SkillTriggerType.AUTO_ATTACK);
        var target = GetTarget(unit.targetId);
        if (target != null && (target.camp == unit.camp || !BattleSkillModel.CanTarget(unit, target) || Vector2.Distance(unit.position, target.position) > unit.config.SearchRange)) {
            target = null;
        }
        if (target == null) {
            bool lost = unit.targetId != 0;
            unit.targetId = 0;
            if (lost || unit.scanRemainingMs <= 0) {
                target = FindTarget(unit);
                unit.scanRemainingMs = BattleConst.TargetScanMs;
                unit.targetId = target?.id ?? 0;
                if (lost || target != null) {
                    unit.pathRemainingMs = 0;
                }
            }
        }
        if (target != null && Vector2.Distance(unit.position, target.position) <= normalAttack.config.CastRange) {
            bool continuous = unit.state == BattleUnitState.Attacking && previousTarget == target.id;
            unit.StopMoving();
            unit.Face(target.position);
            SetState(unit, BattleUnitState.Attacking);
            skills.Advance(unit, target, deltaMs, continuous);
            return;
        }
        unit.skillTimeMs += deltaMs;
        var destination = target ?? scene.buildings.Values.FirstOrDefault(b => b.alive && b.camp != unit.camp && b.config.Type == BuildingType.BASE);
        if (destination == null) {
            unit.StopMoving();
            SetState(unit, BattleUnitState.Waiting);
            return;
        }
        if (unit.pathRemainingMs <= 0) {
            var route = scene.navigation.FindGroundPathFromPosition(unit.position, destination.position, unit.nextRoutePoint, unit.config.MovementType);
            unit.SetRoute(route);
            unit.pathRemainingMs = BattleConst.RepathMs;
        }
        SetState(unit, unit.moving ? (target != null ? BattleUnitState.Chasing : BattleUnitState.Marching) : BattleUnitState.Waiting);
    }

    private BattleCombatantVO FindTarget(BattleUnitVO unit) {
        return scene.elements.Values.OfType<BattleCombatantVO>()
            .Where(t => BattleSkillModel.CanTarget(unit, t) && t.alive && t.camp != unit.camp && t.camp != BattleSceneCamp.ABSOLUTE_NEUTRAL && Vector2.Distance(unit.position, t.position) <= unit.config.SearchRange)
            .OrderBy(t => t is BattleUnitVO ? 0 : 1)
            .ThenBy(t => (t.position - unit.position).sqrMagnitude)
            .ThenBy(t => t.id)
            .FirstOrDefault(t => scene.navigation.FindPath(scene.FindCell(unit.position), scene.FindCell(t.position), unit.config.MovementType) != null);
    }

    internal bool Damage(BattleCombatantVO attacker, BattleCombatantVO target, double amount) {
        if (scene.expired || attacker == null || target == null || !attacker.alive || !target.alive || attacker.camp == target.camp || GetTarget(attacker.id) != attacker || GetTarget(target.id) != target) {
            return false;
        }
        return ApplyDamage(attacker.id, attacker.camp, target, amount);
    }

    internal bool DamageFromProjectile(BattleProjectileVO source, BattleCombatantVO target, double amount) {
        if (scene.expired || target == null || !target.alive || target.camp == source.camp || target.camp == BattleSceneCamp.ABSOLUTE_NEUTRAL || GetTarget(target.id) != target) {
            return false;
        }
        return ApplyDamage(source.sourceId, source.camp, target, amount);
    }

    private bool ApplyDamage(long sourceId, BattleSceneCamp sourceCamp, BattleCombatantVO target, double amount) {
        double actual = target.ApplyDamage(amount);
        Emit(BattleCombatEventType.Damaged, sourceId, target, actual);
        if (target.hp > 0) {
            return true;
        }
        if (target is BattleUnitVO dead) {
            skills.Cancel(dead);
            SetState(dead, BattleUnitState.Dead);
        }
        Emit(BattleCombatEventType.Died, sourceId, target, actual);
        scene.RemoveDefeated(target, sourceCamp);
        return true;
    }

    internal void NotifyAttack(BattleUnitVO unit, BattleCombatantVO target) {
        Emit(BattleCombatEventType.Attack, unit.id, target);
    }

    private void SetState(BattleUnitVO unit, BattleUnitState state) {
        if (unit.state != state) {
            unit.state = state;
            Emit(BattleCombatEventType.StateChanged, unit.id, unit);
        }
    }

    private void Emit(BattleCombatEventType type, long source, BattleCombatantVO target, double damage = 0) {
        Changed?.Invoke(new BattleCombatEvent(type, source, target, damage));
    }

    public void Dispose() {
        scene.playerTimeUpdated -= OnPlayerTime;
        scene.enemyTimeUpdated -= OnEnemyTime;
        skills.Dispose();
        projectiles.Dispose();
        Changed = null;
    }
}
