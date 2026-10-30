using cfg;
using UnityEngine;

/// <summary>只供命中特效显示的值快照；不包含可写战斗对象。</summary>
public sealed class BattleProjectileHitEvent {
    public readonly long projectileId;
    public readonly BattleSceneCamp camp;
    public readonly Vector2 position;
    public readonly string resource;
    public readonly double elapsedSinceHitMs;
    internal BattleProjectileHitEvent(BattleProjectileVO projectile, Vector2 point, double elapsedSinceHitMs) {
        projectileId = projectile.id;
        camp = projectile.camp;
        position = point;
        resource = projectile.config.ImpactResource;
        this.elapsedSinceHitMs = elapsedSinceHitMs;
    }
}
