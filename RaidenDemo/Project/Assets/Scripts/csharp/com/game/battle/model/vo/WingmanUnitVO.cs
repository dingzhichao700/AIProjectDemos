using UnityEngine;

/// <summary>
/// 僚机单位数据
/// </summary>
/// <remarks>
/// 根据自身移动速度跟随所属玩家飞机并保持编队偏移。
/// </remarks>
internal sealed class WingmanUnitVO : FlyingUnitVO {

    /**僚机当前跟随的玩家飞机*/
    private PlayerAircraftUnitVO followTarget;

    /**僚机相对玩家飞机的编队偏移*/
    private Vector2 followOffset;

    /// <summary>
    /// 创建僚机单位。
    /// </summary>
    /// <param name="id">实体ID</param>
    /// <param name="semanticName">语义名称</param>
    /// <param name="position">初始坐标</param>
    public WingmanUnitVO(long id, string semanticName, Vector2 position)
        : base(id, semanticName, SceneElementFaction.PLAYER, TimerType.PLAYER, position) {
    }

    /**设置跟随目标*/
    public void ConfigureFollow(PlayerAircraftUnitVO target, Vector2 offset) {
        followTarget = target;
        followOffset = offset;
    }

    /// <summary>
    /// 更新跟随位置。
    /// </summary>
    /// <param name="deltaTime">距上次更新经过的时间（秒）</param>
    protected override void UpdateMovement(float deltaTime) {
        if (followTarget != null && !followTarget.destroyed) {
            position = Vector2.MoveTowards(position, followTarget.position + followOffset, moveSpeed * deltaTime);
        }
    }

}
