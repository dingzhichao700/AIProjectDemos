using UnityEngine;

/// <summary>
/// 单次飞机爆炸表现数据
/// </summary>
internal sealed class AircraftExplosionVO {

    /**相对死亡开始时刻的播放延迟，单位毫秒*/
    public readonly int delayMs;

    /**相对飞机逻辑原点的位置*/
    public readonly Vector2 localPosition;

    /**爆炸特效ID*/
    public readonly int effectId;

    /// <summary>
    /// 创建单次飞机爆炸表现。
    /// </summary>
    /// <param name="delayMs">播放延迟</param>
    /// <param name="localPosition">局部坐标</param>
    /// <param name="effectId">爆炸特效ID</param>
    public AircraftExplosionVO(int delayMs, Vector2 localPosition, int effectId) {
        this.delayMs = delayMs;
        this.localPosition = localPosition;
        this.effectId = effectId;
    }

}
