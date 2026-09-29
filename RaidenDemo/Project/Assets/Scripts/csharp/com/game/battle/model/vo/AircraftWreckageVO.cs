using UnityEngine;

/// <summary>
/// 飞机地面残骸数据
/// </summary>
/// <remarks>
/// 作为独立的中立场景元素随地面层移动，离开回收范围后由战斗模型移除。
/// </remarks>
internal sealed class AircraftWreckageVO : SceneElementVO {

    /**撞击坑图片路径*/
    public readonly string hitHolePath;

    /**坠机单位的战损外观路径*/
    public readonly string damagedAppearancePath;

    /**坠机单位正常高度下的显示尺寸*/
    public readonly Vector2 aircraftDisplaySize;

    /**战损外观的随机旋转角度*/
    public readonly float aircraftRotation;

    /**残骸跟随的地面层滚动速度*/
    public readonly float groundScrollSpeed;

    /// <summary>
    /// 创建飞机地面残骸。
    /// </summary>
    /// <param name="id">场景元素ID</param>
    /// <param name="position">坠地位置</param>
    /// <param name="hitHolePath">撞击坑图片路径</param>
    /// <param name="damagedAppearancePath">战损外观图片路径</param>
    /// <param name="aircraftDisplaySize">飞机原始显示尺寸</param>
    /// <param name="aircraftRotation">残骸随机角度</param>
    /// <param name="groundScrollSpeed">地面层滚动速度</param>
    public AircraftWreckageVO(long id, Vector2 position, string hitHolePath, string damagedAppearancePath, Vector2 aircraftDisplaySize, float aircraftRotation, float groundScrollSpeed)
        : base(id, SceneElementFaction.NEUTRAL, TimerType.SCENE, position) {
        this.hitHolePath = hitHolePath;
        this.damagedAppearancePath = damagedAppearancePath;
        this.aircraftDisplaySize = aircraftDisplaySize;
        this.aircraftRotation = aircraftRotation;
        this.groundScrollSpeed = groundScrollSpeed;
    }

    /**随地面背景向后滚动*/
    public override void OnTimeUpdate(float deltaTime) {
        position += Vector2.down * groundScrollSpeed * deltaTime;
    }

}
