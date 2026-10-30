using System.Collections.Generic;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>离开施法者独立存活的弹道；伤害在发射时快照，偏移不参与命中。</summary>
public sealed class BattleProjectileVO {
    public long id { get; }
    public long sourceId { get; }
    public long targetId { get; }
    public long castId { get; }
    public int skillId { get; }
    public BattleSceneCamp camp { get; }
    public ProjectileResource config { get; }
    public IReadOnlyList<double> damageAmounts { get; }
    public Vector2 position { get; internal set; }
    public Vector2 visualPosition { get; internal set; }
    public Vector2 visualHeading { get; internal set; }
    public double ageMs { get; internal set; }
    internal readonly Vector2 launchOffset;
    internal float travelled;

    internal BattleProjectileVO(long id, BattleUnitVO source, BattleCombatantVO target, BattleSkillCastVO cast, ProjectileResource config, double[] damageAmounts, Vector2 offset) {
        this.id = id;
        sourceId = source.id;
        targetId = target.id;
        castId = cast.id;
        skillId = cast.skill.config.Id;
        camp = source.camp;
        this.config = config;
        this.damageAmounts = System.Array.AsReadOnly(damageAmounts);
        position = source.position;
        launchOffset = offset;
        visualPosition = position + offset;
        visualHeading = target.position - position;
    }
}
