using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

/// <summary>
/// 战斗碰撞数据管理
/// </summary>
/// <remarks>
/// 统一结算玩家子弹、敌方子弹、敌机机体与玩家之间的有效接触和伤害。
/// </remarks>
internal sealed class BattleCombatModel {

    public void Resolve(PlayerAircraftUnitVO player, AircraftCollisionVO playerCollision, List<BulletVO> playerProjectiles, List<EnemyAircraftVO> enemies, List<BulletVO> enemyProjectiles, Func<BulletVO, bool> removePlayerProjectile, Func<EnemyAircraftVO, bool, bool> resolveEnemy, Action<EnemyAircraftVO> enemyHealthChanged, Action<BulletVO, EnemyAircraftVO, Vector2> playerProjectileHitEnemy, Func<BulletVO, bool> removeEnemyProjectile, Action<BulletVO, Vector2> enemyProjectileHitPlayer, Action playerStatusChanged, Action playerDefeatStarted) {
        if (player == null || playerCollision == null) {
            return;
        }
        ResolvePlayerProjectiles(playerProjectiles, enemies, removePlayerProjectile, resolveEnemy, enemyHealthChanged, playerProjectileHitEnemy);
        ResolveEnemyProjectiles(player, playerCollision, enemyProjectiles, removeEnemyProjectile, enemyProjectileHitPlayer, playerStatusChanged, playerDefeatStarted);
        ResolveEnemyBodies(player, playerCollision, enemies, resolveEnemy, playerStatusChanged, playerDefeatStarted);
    }

    private static void ResolvePlayerProjectiles(List<BulletVO> projectiles, List<EnemyAircraftVO> enemies, Func<BulletVO, bool> removeProjectile, Func<EnemyAircraftVO, bool, bool> resolveEnemy, Action<EnemyAircraftVO> enemyHealthChanged, Action<BulletVO, EnemyAircraftVO, Vector2> hitEnemy) {
        for (int projectileIndex = projectiles.Count - 1; projectileIndex >= 0; projectileIndex--) {
            BulletVO projectile = projectiles[projectileIndex];
            for (int enemyIndex = enemies.Count - 1; enemyIndex >= 0; enemyIndex--) {
                EnemyAircraftVO enemy = enemies[enemyIndex];
                if (!BattleCollisionSystem.TryGetProjectileContactPoint(projectile, enemy.position, enemy.collision, out Vector2 contactPoint)) {
                    continue;
                }
                hitEnemy?.Invoke(projectile, enemy, contactPoint);
                removeProjectile(projectile);
                if (enemy.TakeDamage(projectile.damage)) {
                    resolveEnemy(enemy, true);
                } else {
                    enemyHealthChanged?.Invoke(enemy);
                }
                break;
            }
        }
    }

    private static void ResolveEnemyProjectiles(PlayerAircraftUnitVO player, AircraftCollisionVO playerCollision, List<BulletVO> projectiles, Func<BulletVO, bool> removeProjectile, Action<BulletVO, Vector2> hitPlayer, Action playerChanged, Action defeatStarted) {
        for (int i = projectiles.Count - 1; i >= 0; i--) {
            BulletVO projectile = projectiles[i];
            if (!BattleCollisionSystem.TryGetProjectileContactPoint(projectile, player.position, playerCollision, out Vector2 contactPoint)) {
                continue;
            }
            if (player.TryTakeDamage(projectile.damage)) {
                removeProjectile(projectile);
                hitPlayer?.Invoke(projectile, contactPoint);
                NotifyAcceptedDamage(player, playerChanged, defeatStarted);
            }
        }
    }

    private static void ResolveEnemyBodies(PlayerAircraftUnitVO player, AircraftCollisionVO playerCollision, List<EnemyAircraftVO> enemies, Func<EnemyAircraftVO, bool, bool> resolveEnemy, Action playerChanged, Action defeatStarted) {
        for (int i = enemies.Count - 1; i >= 0; i--) {
            EnemyAircraftVO enemy = enemies[i];
            if (!BattleCollisionSystem.Overlaps(enemy.position, enemy.collision, player.position, playerCollision)) {
                continue;
            }
            if (player.TryTakeDamage(BattleConst.EnemyContactDamage)) {
                NotifyAcceptedDamage(player, playerChanged, defeatStarted);
                if (enemy.enemyClass == EnemyClass.NORMAL) {
                    resolveEnemy(enemy, false);
                }
            }
        }
    }

    private static void NotifyAcceptedDamage(PlayerAircraftUnitVO player, Action playerChanged, Action defeatStarted) {
        playerChanged?.Invoke();
        if (player.lifecycleState == PlayerLifecycleState.Dying) {
            defeatStarted?.Invoke();
        }
    }
}
