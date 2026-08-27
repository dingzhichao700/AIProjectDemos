using cfg;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 敌机运行配置数据
/// </summary>
/// <remarks>
/// 保存敌机类型、外观、碰撞、血量、移动、发射和计分配置。
/// </remarks>
public sealed class EnemyConfigVO {

    public readonly int id;
    public readonly EnemyClass enemyClass;
    public readonly int baseHealth;
    public readonly string appearancePath;
    public readonly Vector2 displaySize;
    public readonly AircraftCollisionVO collision;
    public readonly float moveSpeed;
    public readonly float fireInterval;
    public readonly EnemyFireType fireType;
    public readonly int score;
    public readonly int poolCapacity;
    public readonly BulletConfigVO bullet;
    public readonly IReadOnlyList<ExplosionEffect> deathExplosions;
    public readonly bool removeAfterDeathPresentation;

    public EnemyConfigVO(int id, EnemyClass enemyClass, int baseHealth, string appearancePath, Vector2 displaySize, AircraftCollisionVO collision, float moveSpeed, float fireInterval, EnemyFireType fireType, int score, int poolCapacity, BulletConfigVO bullet, IReadOnlyList<ExplosionEffect> deathExplosions, bool removeAfterDeathPresentation) {
        this.id = id;
        this.enemyClass = enemyClass;
        this.baseHealth = baseHealth;
        this.appearancePath = appearancePath;
        this.displaySize = displaySize;
        this.collision = collision;
        this.moveSpeed = moveSpeed;
        this.fireInterval = fireInterval;
        this.fireType = fireType;
        this.score = score;
        this.poolCapacity = poolCapacity;
        this.bullet = bullet;
        this.deathExplosions = deathExplosions;
        this.removeAfterDeathPresentation = removeAfterDeathPresentation;
    }

}
