using cfg;
using UnityEngine;

/// <summary>
/// 各类飞行单位共用的子弹发射器配置数据
/// </summary>
/// <remarks>
/// 保存飞机在关卡内使用的单个子弹发射器配置。
/// </remarks>
public sealed class BulletLauncherConfigVO {

    /**战场坐标系中的发射偏移*/
    public readonly Vector2 offset;

    /**关联的子弹配置类型*/
    public readonly int bulletType;

    /**发射器配置的基础子弹等级*/
    public readonly int bulletLevel;

    /**每轮发射数量*/
    public readonly int bulletCount;

    /**本轮最后一颗子弹生成后，到下一轮开始的等待时间，单位秒*/
    public readonly float fireInterval;

    /**轮内相邻子弹间隔，单位毫秒；零表示同时发射*/
    public readonly int bulletIntervalMs;

    /**战场坐标系中的基准发射角度*/
    public readonly float direction;

    /**相对基准方向的散布方式*/
    public readonly BulletSpreadType spreadType;

    /**整轮子弹覆盖的散布角度*/
    public readonly float spreadAngle;

    /**保存配置映射，不根据持有单位或所属阵营改写参数*/
    public BulletLauncherConfigVO(Vector2 offset, int bulletType, int bulletLevel, int bulletCount, float fireInterval, int bulletIntervalMs, float direction, BulletSpreadType spreadType, float spreadAngle) {
        this.offset = offset;
        this.bulletType = bulletType;
        this.bulletLevel = bulletLevel;
        this.bulletCount = bulletCount;
        this.fireInterval = fireInterval;
        this.bulletIntervalMs = bulletIntervalMs;
        this.direction = direction;
        this.spreadType = spreadType;
        this.spreadAngle = spreadAngle;
    }

}
