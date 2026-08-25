using cfg;
using UnityEngine;

/// <summary>
/// 敌机波次运行数据
/// </summary>
/// <remarks>
/// 保存单个普通敌机编队的敌机、阵型、运动方式和出生参数。
/// </remarks>
public sealed class EnemyWaveVO {

    /**本波使用的敌机配置*/
    public readonly EnemyConfigVO enemy;

    /**敌机种类*/
    public EnemyClass enemyClass => enemy.enemyClass;

    /**行动模式*/
    public readonly EnemyMotionType motionType;

    /**编队分布方式*/
    public readonly EnemyFormationType formationType;

    /**编队成员数量*/
    public readonly int count;

    /**编队中心入场坐标*/
    public readonly Vector2 spawnCenter;

    /**成员间横向间距*/
    public readonly float spacing;

    /**行动的主要水平方向*/
    public readonly float direction;

    public EnemyWaveVO(EnemyConfigVO enemy, EnemyMotionType motionType, EnemyFormationType formationType, int count, Vector2 spawnCenter, float spacing, float direction = 1f) {
        this.enemy = enemy;
        this.motionType = motionType;
        this.formationType = formationType;
        this.count = count;
        this.spawnCenter = spawnCenter;
        this.spacing = spacing;
        this.direction = Mathf.Sign(direction);
    }

}
