using UnityEngine;

/// <summary>
/// 发射器确定的一次子弹生成请求
/// </summary>
/// <remarks>
/// 使用值类型传递生成快照，避免每颗子弹额外分配请求对象；接收方只登记，不再改写发射规格。
/// </remarks>
internal readonly struct BulletLaunchVO {

    /**来源对象，仅提供归属、计时器及效果关联*/
    public readonly AircraftVO owner;

    /**已经计算完成的战场出生坐标*/
    public readonly Vector2 position;

    /**发射效果使用的配置偏移*/
    public readonly Vector2 launcherOffset;

    /**当前发射器已解析的有效子弹配置*/
    public readonly BulletConfigVO bullet;

    /**最终发射角度*/
    public readonly float direction;

    /**当前帧内需要补偿的飞行时间*/
    public readonly float inFrameElapsed;

    /**记录发射瞬间的规格，不在接收端重新查询配置*/
    public BulletLaunchVO(AircraftVO owner, Vector2 position, Vector2 launcherOffset, BulletConfigVO bullet, float direction, float inFrameElapsed) {
        this.owner = owner;
        this.position = position;
        this.launcherOffset = launcherOffset;
        this.bullet = bullet;
        this.direction = direction;
        this.inFrameElapsed = inFrameElapsed;
    }

}
