using cfg;
using System;
using UnityEngine;

/// <summary>
/// 子弹场景元素数据
/// </summary>
/// <remarks>
/// 统一表示玩家与敌方子弹，并由所属 Timer 推进自身飞行和追踪行为。
/// </remarks>
internal sealed class BulletVO : SceneElementVO {

    public readonly AircraftVO owner;
    public readonly int weaponLevel;
    public readonly int damage;
    public readonly int hitEffectId;
    public readonly int launchEffectId;
    public readonly int appearanceEffectId;
    public readonly float launchRotation;
    public readonly Vector2 launcherOffset;
    public readonly string resPath;
    public readonly Vector2 displaySize;
    public readonly float collisionRadius;
    public readonly float speed;
    public readonly BulletMotionType motionType;
    public readonly float rotationSpeed;
    public Vector2 velocity;
    public Vector2 previousPosition;
    public AircraftVO trackingTarget;
    public float trackingDelayRemaining;
    public bool trackingActivated;
    public readonly float trackingTurnSpeed;
    private Func<Vector2, AircraftVO> targetFinder;
    private Predicate<AircraftVO> targetValidator;

    /**只应用发射快照，子弹等级不再从所属飞机等级推导*/
    public BulletVO(long id, BulletLaunchVO launch) : base(id, launch.owner.faction, launch.owner.timerType, launch.position) {
        owner = launch.owner;
        BulletConfigVO bullet = launch.bullet;
        float direction = launch.direction;
        weaponLevel = bullet.level;
        damage = bullet.damage;
        hitEffectId = bullet.hitEffectId;
        launchEffectId = bullet.launchEffectId;
        appearanceEffectId = bullet.appearanceEffectId;
        launcherOffset = launch.launcherOffset;
        resPath = bullet.appearancePath;
        displaySize = bullet.displaySize;
        collisionRadius = bullet.collisionRadius;
        speed = bullet.speed;
        motionType = bullet.motionType;
        rotationSpeed = bullet.rotationSpeed;
        trackingDelayRemaining = bullet.trackingDelayMs / 1000f;
        trackingTurnSpeed = bullet.trackingTurnSpeed;
        float radians = direction * Mathf.Deg2Rad;
        velocity = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * speed;
        previousPosition = position;
        launchRotation = direction - 90f;
        rotation = rotationSpeed != 0f ? 0f : launchRotation;
    }

    public override void OnTimeUpdate(float deltaTime) {
        UpdateTracking(deltaTime);
        if (rotationSpeed != 0f) {
            rotation += rotationSpeed * deltaTime;
        } else if (velocity.sqrMagnitude > 0.0001f) {
            rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
        }
        previousPosition = position;
        position += velocity * deltaTime;
    }
    public void ConfigureTracking(Func<Vector2, AircraftVO> finder,
        Predicate<AircraftVO> validator) {
        targetFinder = finder; targetValidator = validator;
    }
    private void UpdateTracking(float dt) {
        if (motionType != BulletMotionType.TRACKING) return;
        if (!trackingActivated) {
            trackingDelayRemaining -= dt;
            if (trackingDelayRemaining <= 0f) {
                trackingActivated = true;
                trackingTarget = targetFinder?.Invoke(position);
            }
        }
        if (trackingTarget == null) return;
        if (targetValidator != null && !targetValidator(trackingTarget)) {
            trackingTarget = null; return;
        }
        Vector2 offset = trackingTarget.position - position;
        if (offset.sqrMagnitude <= 0.0001f) return;
        Vector3 next = Vector3.RotateTowards(velocity.normalized, offset.normalized,
            trackingTurnSpeed * Mathf.Deg2Rad * dt, 0f);
        velocity = new Vector2(next.x, next.y) * speed;
    }
}
