using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>技能释放、冷却和生效时序的唯一执行入口。</summary>
public sealed class BattleSkillModel : IDisposable {
    private readonly BattleSceneModel scene;
    private readonly BattleCombatModel combat;
    private long nextCastId;
    public event Action<BattleSkillEvent> Changed;

    internal BattleSkillModel(BattleSceneModel scene, BattleCombatModel combat) {
        this.scene = scene;
        this.combat = combat;
    }

    internal static IReadOnlyList<BattleSkillVO> CreateSkills(UnitResource unit, Tables tables) {
        if (unit.SkillIds.Count == 0 || unit.SkillIds.Distinct().Count() != unit.SkillIds.Count || float.IsNaN(unit.SearchRange) || float.IsInfinity(unit.SearchRange)) {
            throw new InvalidOperationException("单位技能引用或索敌范围非法：" + unit.Id);
        }
        if (!Positive(unit.HitRadius) || !Finite(unit.HitOffset.X) || !Finite(unit.HitOffset.Y)) {
            throw new InvalidOperationException("单位命中配置非法：" + unit.Id);
        }
        var result = new List<BattleSkillVO>();
        foreach (int id in unit.SkillIds) {
            var config = tables.SkillObj.Get(id);
            if (config == null || config.Trigger != SkillTriggerType.AUTO_ATTACK || (config.TargetRule != SkillTargetRule.ENEMY_UNIT_THEN_BUILDING && config.TargetRule != SkillTargetRule.ENEMY_GROUND_UNIT_THEN_BUILDING) || config.CastRange <= 0 || float.IsNaN(config.CastRange) || float.IsInfinity(config.CastRange) || config.CastRange > unit.SearchRange || config.CooldownMs <= 0 || config.WindupMs < 0 || config.RecoveryMs < 0 || config.Effects.Count == 0 || !Enum.IsDefined(typeof(UnitActionType), config.Action)) {
                throw new InvalidOperationException("技能配置非法或尚未支持：" + id);
            }
            int previousDelay = -1;
            foreach (var effect in config.Effects) {
                int delay = effect is LaunchProjectileEffect delayed ? delayed.DelayMs : 0;
                if (delay < previousDelay) {
                    throw new InvalidOperationException("技能效果必须按延迟升序配置：" + id);
                }
                previousDelay = delay;
                if (effect is FixedDamageEffect damage) {
                    ValidateDamage(damage);
                } else if (effect is LaunchProjectileEffect launch) {
                    var projectile = tables.ProjectileObj.Get(launch.ProjectileId);
                    if (projectile == null || !Positive(projectile.Speed) || projectile.LifetimeMs <= 0 || projectile.Motion != ProjectileMotion.TRACK_TARGET || projectile.TargetLost != ProjectileTargetLost.DESTROY || launch.HitEffects.Count == 0) {
                        throw new InvalidOperationException("弹道配置非法：" + launch.ProjectileId);
                    }
                    foreach (var hit in launch.HitEffects) {
                        if (!(hit is FixedDamageEffect hitDamage)) {
                            throw new InvalidOperationException("当前弹道命中只支持固定伤害。");
                        }
                        ValidateDamage(hitDamage);
                    }
                    foreach (UnitAppearanceDirection direction in Enum.GetValues(typeof(UnitAppearanceDirection))) {
                        var points = unit.FirePoints.Where(p => p.Action == config.Action && p.Direction == direction).ToArray();
                        if (launch.DelayMs < 0 || launch.DelayMs > config.RecoveryMs || launch.FirePointIndex < 0 || points.Length <= launch.FirePointIndex || !Finite(points[launch.FirePointIndex].Offset.X) || !Finite(points[launch.FirePointIndex].Offset.Y)) {
                            throw new InvalidOperationException("单位发射挂点缺失或重复：" + unit.Id + "/" + direction);
                        }
                    }
                } else {
                    throw new InvalidOperationException("技能效果尚未支持：" + id);
                }
            }
            result.Add(new BattleSkillVO(config));
        }
        if (result.Count(s => s.config.Trigger == SkillTriggerType.AUTO_ATTACK) != 1) {
            throw new InvalidOperationException("首版单位必须且只能配置一个自动普攻：" + unit.Id);
        }
        return result.AsReadOnly();
    }

    private static bool Finite(double value) {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool Positive(double value) {
        return value > 0 && Finite(value);
    }

    private static void ValidateDamage(FixedDamageEffect effect) {
        if (!Positive(effect.Amount)) {
            throw new InvalidOperationException("技能伤害必须为正有限值。");
        }
    }

    private bool Registered(BattleCombatantVO value) {
        return value != null && value.alive && scene.elements.TryGetValue(value.id, out var current) && current == value;
    }

    public static bool CanTarget(BattleUnitVO source, BattleCombatantVO target) {
        var rule = source.skills.Single(s => s.config.Trigger == SkillTriggerType.AUTO_ATTACK).config.TargetRule;
        return rule != SkillTargetRule.ENEMY_GROUND_UNIT_THEN_BUILDING
            || !(target is BattleUnitVO unit) || unit.config.MovementType == UnitMovementType.GROUND;
    }

    private bool ValidTarget(BattleUnitVO unit, BattleCombatantVO target, BattleSkillVO skill) {
        return !scene.expired && Registered(unit) && Registered(target) && CanTarget(unit, target) && target.camp != unit.camp && target.camp != BattleSceneCamp.ABSOLUTE_NEUTRAL && Vector2.Distance(unit.position, target.position) <= skill.config.CastRange;
    }

    /// <summary>只在同一目标的连续攻击窗口补偿长帧；离开射程不积攒释放。</summary>
    internal void Advance(BattleUnitVO unit, BattleCombatantVO target, double deltaMs, bool continuous) {
        double previous = unit.skillTimeMs;
        unit.skillTimeMs += deltaMs;
        double cursor = continuous ? previous : unit.skillTimeMs;
        var skill = unit.skills.Single(s => s.config.Trigger == SkillTriggerType.AUTO_ATTACK);
        while (Registered(unit) && !scene.expired) {
            var cast = unit.activeCast;
            if (cast == null) {
                double start = Math.Max(cursor, skill.readyAtMs);
                if (start > unit.skillTimeMs || !ValidTarget(unit, target, skill)) {
                    return;
                }
                cast = new BattleSkillCastVO(++nextCastId, skill, unit.id, target.id, start);
                unit.activeCast = cast;
                skill.readyAtMs = start + skill.config.CooldownMs;
                unit.StopMoving();
                unit.Face(target.position);
                Emit(BattleSkillEventType.Started, cast, unit, start);
                combat.NotifyAttack(unit, target);
                if (unit.activeCast != cast || !Registered(unit) || scene.expired) {
                    return;
                }
            }
            if (cast.stage == BattleSkillStage.Windup) {
                if (target == null || target.id != cast.targetId || !ValidTarget(unit, target, cast.skill)) {
                    Cancel(unit);
                    return;
                }
                if (cast.impactAtMs > unit.skillTimeMs) {
                    return;
                }
                // 先推进阶段，避免伤害/死亡事件重入导致重复结算。
                cast.stage = BattleSkillStage.Recovery;
                Emit(BattleSkillEventType.Impact, cast, unit, cast.impactAtMs);
            }
            while (cast.nextEffectIndex < cast.skill.config.Effects.Count) {
                var effect = cast.skill.config.Effects[cast.nextEffectIndex];
                double due = cast.impactAtMs + (effect is LaunchProjectileEffect scheduled ? scheduled.DelayMs : 0);
                if (due > unit.skillTimeMs) {
                    break;
                }
                cast.nextEffectIndex++;
                if (!Registered(unit) || scene.expired || unit.activeCast != cast) {
                    return;
                }
                if (!Registered(target) || !CanTarget(unit, target)) {
                    continue;
                }
                if (effect is FixedDamageEffect damage) {
                    combat.Damage(unit, target, damage.Amount);
                } else if (effect is LaunchProjectileEffect launch) {
                    combat.projectiles.Launch(unit, target, cast, launch, unit.skillTimeMs - due);
                }
            }
            if (unit.activeCast != cast) {
                return;
            }
            if (cast.finishedAtMs > unit.skillTimeMs) {
                return;
            }
            cast.stage = BattleSkillStage.Completed;
            unit.activeCast = null;
            cursor = cast.finishedAtMs;
            Emit(BattleSkillEventType.Completed, cast, unit, cursor);
        }
    }

    internal void Cancel(BattleUnitVO unit) {
        var cast = unit.activeCast;
        if (cast == null) {
            return;
        }
        unit.activeCast = null;
        cast.stage = BattleSkillStage.Cancelled;
        Emit(BattleSkillEventType.Cancelled, cast, unit, unit.skillTimeMs);
    }

    private void Emit(BattleSkillEventType type, BattleSkillCastVO cast, BattleUnitVO unit, double timeMs) {
        Changed?.Invoke(new BattleSkillEvent(type, cast, unit.position, timeMs));
    }

    public void Dispose() {
        foreach (var unit in scene.elements.Values.OfType<BattleUnitVO>().ToArray()) {
            Cancel(unit);
        }
        Changed = null;
    }
}
