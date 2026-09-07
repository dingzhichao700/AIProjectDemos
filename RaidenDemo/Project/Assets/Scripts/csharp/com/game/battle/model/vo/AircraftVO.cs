using cfg;
using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 飞机场景元素数据
/// </summary>
/// <remarks>
/// 统一表示玩家飞机、僚机和各类敌机，并负责自身移动、发射与生命周期行为。
/// </remarks>
internal sealed class AircraftVO : SceneElementVO {
    public readonly List<BulletLauncherVO> bulletLaunchers = new List<BulletLauncherVO>();
    public readonly string semanticName;
    public readonly string appearancePath;
    public readonly bool isPlayer;
    public readonly EnemyClass enemyClass;
    public readonly Vector2 size;
    public readonly AircraftCollisionVO collision;
    public readonly EnemyMotionType motionType;
    public readonly float moveSpeed;
    public readonly int scoreValue;
    public readonly int formationIndex;
    public readonly int formationCount;
    public IReadOnlyList<ExplosionEffect> deathExplosions { get; private set; }
    public bool removeAfterDeathPresentation { get; private set; }
    public int maxHealth { get; private set; }
    public int health { get; private set; }
    public float horizontalDirection = 1f;
    public float motionTime;
    public float originX;
    public float motionDirection;
    private readonly EnemyFormationPathVO formationPath;
    private readonly EnemyWaveVO specialMotion;
    private AircraftVO followTarget;
    private Vector2 followOffset;
    private float followSpeed;
    private Action<BulletLaunchVO> projectileRequested;
    private bool firingEnabled;
    public bool isDying { get; private set; }
    public bool deathMovementActive { get; private set; }
    private float enemyVisibleTime;
    private Action playerRespawnCompleted;
    public PlayerLifecycleState lifecycleState { get; private set; } =
        PlayerLifecycleState.Alive;
    public float lifecycleRemaining { get; private set; }
    public float invincibleRemaining { get; private set; }
    public float invincibleFlashInterval { get; private set; } = BattleConst.PlayerFlashInterval;
    public float hitShakeRemaining { get; private set; }
    public bool showsSharedHealth => faction == SceneElementFaction.ENEMY && enemyClass != EnemyClass.NORMAL;
    public bool isBoss => faction == SceneElementFaction.ENEMY && enemyClass == EnemyClass.BOSS;
    public bool isLeavingFormation => formationPath != null && formationPath.isLeaving;

    public AircraftVO(long id, string semanticName, bool isPlayer, Vector2 position)
        : base(id, SceneElementFaction.PLAYER, TimerType.PLAYER, position) {
        this.semanticName = semanticName;
        this.isPlayer = isPlayer;
    }

    public AircraftVO(long id, Vector2 position, EnemyClass enemyClass,
        Vector2 size, AircraftCollisionVO collision, int maxHealth,
        EnemyMotionType motionType = EnemyMotionType.STATIONARY, float moveSpeed = 0f,
        int scoreValue = 0, int formationIndex = 0, int formationCount = 1,
        float motionDirection = 1f,
        string appearancePath = null, string semanticName = null,
        EnemyFormationPathVO formationPath = null, EnemyWaveVO specialMotion = null)
        : base(id, SceneElementFaction.ENEMY, TimerType.ENEMY, position) {
        this.semanticName = semanticName ?? $"enemyEntity{id}";
        this.appearancePath = appearancePath;
        this.enemyClass = enemyClass;
        this.size = size;
        this.collision = collision;
        this.maxHealth = maxHealth;
        this.motionType = motionType;
        this.moveSpeed = moveSpeed;
        this.scoreValue = scoreValue;
        this.formationIndex = formationIndex;
        this.formationCount = formationCount;
        this.motionDirection = Mathf.Sign(motionDirection);
        this.formationPath = formationPath;
        this.specialMotion = specialMotion;
        health = maxHealth;
        originX = position.x;
        firingEnabled = true;
        rotation = BattleConst.EnemyAircraftVisualRotation;
    }

    public override void OnTimeUpdate(float deltaTime) {
        if (faction == SceneElementFaction.ENEMY) {
            if (!isDying || deathMovementActive) {
                UpdateEnemy(deltaTime);
            }
            UpdateLaunchers(deltaTime);
            return;
        }
        if (followTarget != null && !followTarget.destroyed) {
            float t = 1f - Mathf.Exp(-followSpeed * deltaTime);
            position = Vector2.Lerp(position, followTarget.position + followOffset, t);
        }
        if (isPlayer) {
            UpdatePlayerLifecycle(deltaTime);
        }
        UpdateLaunchers(deltaTime);
    }
    public void ConfigureFollow(AircraftVO target, Vector2 offset, float speed) {
        followTarget = target;
        followOffset = offset;
        followSpeed = Mathf.Max(0f, speed);
    }
    public void ConfigureFiring(Action<BulletLaunchVO> handler) {
        projectileRequested = handler;
    }
    public void SetFiringEnabled(bool enabled) => firingEnabled = enabled;
    public void BeginEnemyDeathPresentation() {
        if (faction == SceneElementFaction.ENEMY) {
            firingEnabled = false;
            isDying = true;
            deathMovementActive = true;
        }
    }
    /**死亡行为由实体决定，表现层只报告最后一次爆炸已开始。*/
    public void OnLastDeathExplosionStarted() {
        deathMovementActive = isBoss && !removeAfterDeathPresentation;
    }
    public void ConfigurePlayerLifecycle(Action respawnCompleted) {
        playerRespawnCompleted = respawnCompleted;
    }
    public bool TryTakePlayerDamage(int value) {
        if (!isPlayer || lifecycleState != PlayerLifecycleState.Alive ||
            invincibleRemaining > 0f) {
            return false;
        }
        bool defeated = TakeDamage(value);
        if (defeated) {
            lifecycleState = PlayerLifecycleState.Dying;
            lifecycleRemaining = 0f;
            invincibleRemaining = 0f;
            hitShakeRemaining = 0f;
            firingEnabled = false;
        } else {
            invincibleRemaining = BattleConst.PlayerInvincibleDuration;
            invincibleFlashInterval = BattleConst.PlayerHitFlashInterval;
            hitShakeRemaining = BattleConst.PlayerHitShakeDuration;
        }
        return true;
    }
    public void BeginRespawn() {
        RestoreFullHealth();
        position = BattleConst.PlayerRespawnStart;
        lifecycleState = PlayerLifecycleState.Respawning;
        lifecycleRemaining = BattleConst.PlayerRespawnEnterDuration;
        invincibleRemaining = BattleConst.PlayerRespawnInvincibleDuration;
        invincibleFlashInterval = BattleConst.PlayerFlashInterval;
        hitShakeRemaining = 0f;
        firingEnabled = false;
    }
    public void GrantInvincibility(float duration) {
        invincibleRemaining = Mathf.Max(invincibleRemaining, duration);
        invincibleFlashInterval = BattleConst.PlayerFlashInterval;
        hitShakeRemaining = 0f;
    }
    public void ResetLaunchers() {
        foreach (BulletLauncherVO launcher in bulletLaunchers) {
            launcher.Reset();
        }
    }
    public void SetPosition(Vector2 value) => position = value;
    public void ApplyPlayerAircraftStats(int configuredMaxHealth) {
        int previous = maxHealth; maxHealth = Mathf.Max(1, configuredMaxHealth);
        health = previous <= 0 ? maxHealth : Mathf.Clamp(health + maxHealth - previous, 0, maxHealth);
    }
    public void ConfigureDeathPresentation(IReadOnlyList<ExplosionEffect> explosions, bool removeAfterPresentation) {
        deathExplosions = explosions;
        removeAfterDeathPresentation = removeAfterPresentation;
    }
    public bool TakeDamage(int value) { health = Mathf.Max(0, health - Mathf.Max(0, value)); return health <= 0; }
    public int Heal(int value) { int previous = health; health = Mathf.Min(maxHealth, health + Mathf.Max(0, value)); return health - previous; }
    public void RestoreFullHealth() => health = maxHealth;

    private void UpdatePlayerLifecycle(float deltaTime) {
        invincibleRemaining = Mathf.Max(0f, invincibleRemaining - deltaTime);
        hitShakeRemaining = Mathf.Max(0f, hitShakeRemaining - deltaTime);
        if (lifecycleState == PlayerLifecycleState.Alive) {
            return;
        }
        lifecycleRemaining = Mathf.Max(0f, lifecycleRemaining - deltaTime);
        if (lifecycleState == PlayerLifecycleState.Dying) {
            return;
        }
        float duration = BattleConst.PlayerRespawnEnterDuration;
        float progress = duration <= 0f
            ? 1f
            : 1f - lifecycleRemaining / duration;
        position = Vector2.Lerp(BattleConst.PlayerRespawnStart,
            BattleConst.PlayerStart, Mathf.Clamp01(progress));
        if (lifecycleRemaining > 0f) {
            return;
        }
        position = BattleConst.PlayerStart;
        lifecycleState = PlayerLifecycleState.Alive;
        firingEnabled = true;
        ResetLaunchers();
        playerRespawnCompleted?.Invoke();
    }

    private void UpdateEnemy(float deltaTime) {
        if (isBoss) {
            UpdateBoss(deltaTime);
        } else if (enemyClass == EnemyClass.ELITE) {
            UpdateElite(deltaTime);
        } else {
            UpdateNormal(deltaTime);
        }
    }
    private void UpdateNormal(float dt) {
        if (formationPath != null) {
            Vector2 previousFormationPosition = position;
            position = formationPath.GetMemberPosition(formationIndex);
            UpdateEnemyVisualRotation(position - previousFormationPosition, dt);
            return;
        }
        throw new InvalidOperationException("普通敌机必须使用编队路径。");
    }

    /**根据实际移动切线平滑调整敌机机身方向。*/
    private void UpdateEnemyVisualRotation(Vector2 movement, float deltaTime) {
        if (movement.sqrMagnitude <= 0.0001f) {
            return;
        }
        float rawRotation = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg - 90f;
        float maxBankAngle = BattleConst.EnemyFormationBankAngle;
        float bank = Mathf.Clamp(Mathf.DeltaAngle(BattleConst.EnemyAircraftVisualRotation,
            rawRotation), -maxBankAngle, maxBankAngle);
        float targetRotation = BattleConst.EnemyAircraftVisualRotation + bank;
        float smooth = 1f - Mathf.Exp(-BattleConst.EnemyVisualTurnSmoothness * deltaTime);
        rotation = Mathf.LerpAngle(rotation, targetRotation, smooth);
    }
    private float GetSpecialStationY() {
        return Mathf.Lerp(BattleConst.EnemyActivityBottom + size.y * 0.5f,
            -BattleConst.EnemyFormationViewportPadding - size.y * 0.5f, specialMotion.stationHeightRatio);
    }
    private void UpdateElite(float dt) {
        float targetY = GetSpecialStationY();
        Vector2 next = position;
        if (next.y > targetY) {
            next.y = Mathf.MoveTowards(next.y, targetY, moveSpeed * dt);
        } else {
            float minX = size.x * 0.5f + BattleConst.EnemyFormationViewportPadding;
            float maxX = BattleConst.BattleViewportWidth - minX;
            float targetX = horizontalDirection > 0f ? maxX : minX;
            next.x = Mathf.MoveTowards(next.x, targetX, moveSpeed * dt);
            if (Mathf.Approximately(next.x, targetX)) {
                horizontalDirection *= -1f;
            }
        }
        position = next;
    }
    private void UpdateBoss(float dt) {
        float targetY = GetSpecialStationY();
        Vector2 next = position;
        if (next.y > targetY) {
            next.y = Mathf.MoveTowards(next.y, targetY, moveSpeed * dt);
            motionTime = 0f;
        } else {
            motionTime += dt;
            float margin = size.x * 0.5f + BattleConst.EnemyFormationViewportPadding;
            float amplitude = Mathf.Max(0f, Mathf.Min(specialMotion.patrolAmplitude, Mathf.Min(originX - margin,
                BattleConst.BattleViewportWidth - margin - originX)));
            float frequency = amplitude > 0f ? moveSpeed / amplitude : 0f;
            next.x = originX + Mathf.Sin(motionTime * frequency) * amplitude;
        }
        position = next;
    }

    /**飞机只提供生命周期许可和空间上下文，发射规格及冷却由各发射器自行维护*/
    private void UpdateLaunchers(float dt) {
        if (!firingEnabled || projectileRequested == null || destroyed) {
            return;
        }
        if (faction == SceneElementFaction.ENEMY && formationPath != null && !formationPath.canFire) {
            return;
        }
        if (faction == SceneElementFaction.ENEMY && formationPath == null) {
            bool fullyVisible = position.y + size.y * 0.5f <= 0f &&
                position.y - size.y * 0.5f >= BattleConst.EnemyActivityBottom &&
                position.x - size.x * 0.5f >= 0f &&
                position.x + size.x * 0.5f <= BattleConst.BattleViewportWidth;
            if (!fullyVisible) {
                enemyVisibleTime = 0f;
                return;
            }
            enemyVisibleTime += dt;
            if (enemyVisibleTime < specialMotion.prepareDuration) {
                return;
            }
        }
        foreach (BulletLauncherVO launcher in bulletLaunchers) {
            launcher.Update(dt, this, projectileRequested);
        }
    }

}
