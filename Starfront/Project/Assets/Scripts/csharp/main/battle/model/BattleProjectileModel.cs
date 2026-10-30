using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cfg;
using UnityEngine;

/// <summary>锁定目标的独立弹道；按所属阵营Timer推进，线段扫掠防止高速穿透。</summary>
public sealed class BattleProjectileModel : IDisposable {
    private readonly BattleSceneModel scene;
    private readonly BattleCombatModel combat;
    private readonly Dictionary<long, BattleProjectileVO> active = new Dictionary<long, BattleProjectileVO>();
    public IReadOnlyDictionary<long, BattleProjectileVO> projectiles { get; }
    public event Action<BattleProjectileHitEvent> Hit;
    private long nextId;

    internal BattleProjectileModel(BattleSceneModel scene, BattleCombatModel combat) {
        this.scene = scene;
        this.combat = combat;
        projectiles = new ReadOnlyDictionary<long, BattleProjectileVO>(active);
    }

    internal void Launch(BattleUnitVO source, BattleCombatantVO target, BattleSkillCastVO cast, LaunchProjectileEffect effect, double elapsedMs) {
        var config = CfgManager.tables.ProjectileObj.Get(effect.ProjectileId);
        var point = source.config.FirePoints.Where(p => p.Action == cast.skill.config.Action && p.Direction == BattleAim.ResolveDirection(source.heading)).ElementAt(effect.FirePointIndex);
        var offset = new Vector2(source.heading.x < 0 ? -point.Offset.X : point.Offset.X, point.Offset.Y - source.altitude);
        var value = new BattleProjectileVO(++nextId, source, target, cast, config, effect.HitEffects.Cast<FixedDamageEffect>().Select(e => e.Amount).ToArray(), offset);
        active.Add(value.id, value);
        // 补偿的是本帧发射后的剩余时间，不能再推进整个帧长。
        Step(value, elapsedMs);
    }

    internal void Advance(BattleSceneCamp camp, double deltaMs) {
        foreach (var value in active.Values.Where(p => p.camp == camp).ToArray()) {
            if (scene.expired) {
                break;
            }
            Step(value, deltaMs);
        }
    }

    private void Step(BattleProjectileVO value, double deltaMs) {
        if (!scene.elements.TryGetValue(value.targetId, out var element) || !(element is BattleCombatantVO target) || !target.alive || target.camp == value.camp || target.camp == BattleSceneCamp.ABSOLUTE_NEUTRAL) {
            active.Remove(value.id);
            return;
        }
        double stepMs = Math.Min(deltaMs, value.config.LifetimeMs - value.ageMs);
        float radius = target is BattleUnitVO unit ? unit.config.HitRadius : ((BattleBuildingVO)target).config.HitRadius;
        var hitOffset = target is BattleUnitVO u ? u.config.HitOffset : ((BattleBuildingVO)target).config.HitOffset;
        Vector2 offset = new Vector2(hitOffset.X, hitOffset.Y - (target is BattleUnitVO airborne ? airborne.altitude : 0));
        Vector2 direction = target.position - value.position;
        float distance = direction.magnitude;
        float toContact = Mathf.Max(0, distance - radius);
        float travel = value.config.Speed * (float)(stepMs / 1000d);
        bool hit = travel >= toContact;
        float travelled = Mathf.Min(travel, toContact);
        value.position += direction.normalized * travelled;
        value.travelled += travelled;
        float remaining = Mathf.Max(0, toContact - travelled);
        float progress = value.travelled + remaining > 0 ? value.travelled / (value.travelled + remaining) : 1;
        Vector2 nextVisual = value.position + Vector2.Lerp(value.launchOffset, offset, progress);
        value.visualHeading = nextVisual - value.visualPosition;
        value.visualPosition = nextVisual;
        value.ageMs += stepMs;
        if (hit) {
            // 先移除再结算，死亡事件重入也不能使同颗子弹再次扣血。
            active.Remove(value.id);
            bool applied = false;
            foreach (double amount in value.damageAmounts) {
                applied |= combat.DamageFromProjectile(value, target, amount);
            }
            if (applied) {
                Hit?.Invoke(new BattleProjectileHitEvent(value, target.position + offset, Math.Max(0, deltaMs - toContact / value.config.Speed * 1000d)));
            }
        } else if (value.ageMs >= value.config.LifetimeMs) {
            active.Remove(value.id);
        }
    }

    public void Dispose() {
        active.Clear();
        Hit = null;
    }
}
