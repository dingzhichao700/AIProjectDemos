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
    public readonly string resPath;
    public readonly Vector2 displaySize;
    public readonly Vector2 hitSize;
    public readonly Vector2 hitPivot;
    public readonly float speed;
    public readonly BulletMotionType motionType;
    public readonly bool rotate;
    public readonly float rotationSpeed;
    public Vector2 velocity;
    public Vector2 previousPosition;
    public AircraftVO trackingTarget;
    public float trackingDelayRemaining;
    public bool trackingActivated;
    public readonly float trackingTurnSpeed;
    private Func<Vector2, AircraftVO> targetFinder;
    private Predicate<AircraftVO> targetValidator;

    public BulletVO(long id, Vector2 position, AircraftVO owner,
        PlayerBulletLauncherVO launcher, float direction)
        : base(id, owner.faction, owner.timerType, position) {
        this.owner = owner;
        weaponLevel = owner.effectiveLevel;
        damage = launcher.damage;
        hitEffectId = launcher.hitEffectId;
        resPath = launcher.projectilePath;
        displaySize = launcher.projectileSize;
        hitSize = launcher.hitSize;
        hitPivot = launcher.hitPivot;
        speed = launcher.speed;
        motionType = launcher.motionType;
        rotate = launcher.rotate;
        rotationSpeed = launcher.rotationSpeed;
        trackingDelayRemaining = launcher.trackingDelayMs / 1000f;
        trackingTurnSpeed = launcher.trackingTurnSpeed;
        float radians = direction * Mathf.Deg2Rad;
        velocity = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * speed;
        previousPosition = position;
        rotation = rotate ? 0f : direction - 90f;
    }

    public BulletVO(long id, Vector2 position, Vector2 velocity,
        Vector2 hitSize, int damage, int hitEffectId)
        : base(id, SceneElementFaction.ENEMY, TimerType.ENEMY, position) {
        this.velocity = velocity;
        this.hitSize = hitSize;
        this.damage = damage;
        this.hitEffectId = hitEffectId;
        hitPivot = new Vector2(0.5f, 0.5f);
        speed = velocity.magnitude;
        motionType = BulletMotionType.STRAIGHT;
        previousPosition = position;
        rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
    }

    public override void OnTimeUpdate(float deltaTime) {
        UpdateTracking(deltaTime);
        if (rotate) {
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
