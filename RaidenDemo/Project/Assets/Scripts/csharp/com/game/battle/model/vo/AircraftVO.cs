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
    public int persistentLevel { get; private set; } = 1;
    public int stageBonusLevel { get; private set; }
    public int effectiveLevel => Mathf.Max(1, persistentLevel + stageBonusLevel);
    public int maxHealth { get; private set; }
    public int health { get; private set; }
    public float horizontalDirection = 1f;
    public float motionTime;
    public float originX;
    public float motionDirection;
    private AircraftVO followTarget;
    private Vector2 followOffset;
    private float followSpeed;
    private Action<AircraftVO, PlayerBulletLauncherVO, int> projectileRequested;
    private bool firingEnabled;
    private Action playerDefeatPresentationCompleted;
    private Action playerRespawnCompleted;
    public PlayerLifecycleState lifecycleState { get; private set; } =
        PlayerLifecycleState.Alive;
    public float lifecycleRemaining { get; private set; }
    public float invincibleRemaining { get; private set; }
    public float hitShakeRemaining { get; private set; }
    public bool showsSharedHealth => faction == SceneElementFaction.ENEMY && enemyClass != EnemyClass.NORMAL;
    public bool isBoss => faction == SceneElementFaction.ENEMY && enemyClass == EnemyClass.BOSS;

    public AircraftVO(long id, string semanticName, bool isPlayer, Vector2 position)
        : base(id, SceneElementFaction.PLAYER, TimerType.PLAYER, position) {
        this.semanticName = semanticName;
        this.isPlayer = isPlayer;
        ApplyLevels(1, 0);
    }

    public AircraftVO(long id, Vector2 position, EnemyClass enemyClass,
        Vector2 size, AircraftCollisionVO collision, int maxHealth,
        EnemyMotionType motionType = EnemyMotionType.STRAIGHT, float moveSpeed = 0f,
        int scoreValue = 0, int formationIndex = 0, int formationCount = 1,
        float motionDirection = 1f,
        string appearancePath = null, string semanticName = null)
        : base(id, SceneElementFaction.ENEMY, TimerType.ENEMY, position) {
        this.semanticName = semanticName ?? $"enemyEntity{id}";
        this.appearancePath = appearancePath;
        this.enemyClass = enemyClass; this.size = size; this.collision = collision;
        this.maxHealth = maxHealth; this.motionType = motionType;
        this.moveSpeed = moveSpeed > 0f ? moveSpeed : BattleConst.EnemyMoveSpeed;
        this.scoreValue = scoreValue > 0 ? scoreValue : BattleConst.EnemyScore;
        this.formationIndex = formationIndex; this.formationCount = formationCount;
        this.motionDirection = Mathf.Sign(motionDirection);
        health = maxHealth; originX = position.x; firingEnabled = true;
    }

    public override void OnTimeUpdate(float deltaTime) {
        if (faction == SceneElementFaction.ENEMY) {
            UpdateEnemy(deltaTime);
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
        followTarget = target; followOffset = offset; followSpeed = Mathf.Max(0f, speed);
    }
    public void ConfigureFiring(Action<AircraftVO, PlayerBulletLauncherVO, int> handler) {
        projectileRequested = handler;
    }
    public void SetFiringEnabled(bool enabled) => firingEnabled = enabled;
    public void ConfigurePlayerLifecycle(Action defeatCompleted, Action respawnCompleted) {
        playerDefeatPresentationCompleted = defeatCompleted;
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
            lifecycleRemaining = BattleConst.PlayerDefeatPresentationDuration;
            invincibleRemaining = 0f;
            hitShakeRemaining = 0f;
            firingEnabled = false;
        } else {
            invincibleRemaining = BattleConst.PlayerInvincibleDuration;
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
        hitShakeRemaining = 0f;
        firingEnabled = false;
    }
    public void GrantInvincibility(float duration) {
        invincibleRemaining = Mathf.Max(invincibleRemaining, duration);
        hitShakeRemaining = 0f;
    }
    public void ResetLaunchers() { foreach (BulletLauncherVO launcher in bulletLaunchers) launcher.Reset(); }
    public void SetPosition(Vector2 value) => position = value;
    public void ApplyLevels(int persistent, int stageBonus) {
        persistentLevel = Mathf.Max(1, persistent); stageBonusLevel = Mathf.Max(0, stageBonus);
        int previous = maxHealth; maxHealth = BattleConst.GetMaxHealth(isPlayer, effectiveLevel);
        health = previous <= 0 ? maxHealth : Mathf.Clamp(health + maxHealth - previous, 0, maxHealth);
    }
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
            if (lifecycleRemaining <= 0f) {
                playerDefeatPresentationCompleted?.Invoke();
            }
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
        if (isBoss) UpdateBoss(deltaTime);
        else if (enemyClass == EnemyClass.ELITE) UpdateElite(deltaTime);
        else UpdateNormal(deltaTime);
    }
    private void UpdateNormal(float dt) {
        motionTime += dt; float distance = moveSpeed * dt; Vector2 next = position;
        if (motionType == EnemyMotionType.DIAGONAL) next += new Vector2(motionDirection * distance * 0.7f, -distance);
        else if (motionType == EnemyMotionType.SNAKE) { next.y -= distance; next.x = originX + Mathf.Sin(motionTime * 3.2f) * 105f; }
        else if (motionType == EnemyMotionType.FORMATION_TURN) {
            next.y -= distance; float member = formationIndex - (formationCount - 1) * 0.5f;
            float progress = Mathf.Clamp01(motionTime / 2.6f);
            next.x = originX + motionDirection * Mathf.Sin(progress * Mathf.PI) * (130f + Mathf.Abs(member) * 18f);
        } else next.y -= distance;
        position = next;
    }
    private void UpdateElite(float dt) {
        Vector2 next = position;
        if (next.y > -300f) next += Vector2.down * (BattleConst.EnemyMoveSpeed * dt);
        else { next.x += horizontalDirection * BattleConst.EliteMoveSpeed * dt;
            if (next.x <= size.x * 0.5f || next.x >= 720f - size.x * 0.5f) { horizontalDirection *= -1f; next.x = Mathf.Clamp(next.x, size.x * 0.5f, 720f - size.x * 0.5f); } }
        position = next;
    }
    private void UpdateBoss(float dt) {
        Vector2 next = position;
        if (next.y > -260f) { next.y = Mathf.Max(-260f, next.y - BattleConst.BossMoveSpeed * dt); if (next.y <= -260f) motionTime = 0f; }
        else { motionTime += dt; next.x = originX + Mathf.Sin(motionTime * 0.85f) * 150f; }
        position = next;
    }
    private void UpdateLaunchers(float dt) {
        if (!firingEnabled || projectileRequested == null) return;
        foreach (BulletLauncherVO launcher in bulletLaunchers) {
            launcher.fireCooldown -= dt; UpdatePending(launcher, dt);
            if (launcher.fireCooldown > 0f || launcher.pendingProjectileCount > 0) continue;
            launcher.fireCooldown += launcher.config.fireInterval; launcher.nextProjectileIndex = 0;
            launcher.pendingProjectileCount = launcher.config.bulletCount; launcher.bulletCooldown = 0f;
            UpdatePending(launcher, 0f);
        }
    }
    private void UpdatePending(BulletLauncherVO launcher, float dt) {
        if (launcher.pendingProjectileCount <= 0) return;
        launcher.bulletCooldown -= dt; float interval = launcher.config.bulletIntervalMs / 1000f;
        while (launcher.pendingProjectileCount > 0 && launcher.bulletCooldown <= 0f) {
            projectileRequested(this, launcher.config, launcher.nextProjectileIndex++);
            launcher.pendingProjectileCount--;
            if (interval > 0f) launcher.bulletCooldown += interval;
        }
    }
}
