using cfg;
using UnityEngine;

/// <summary>
/// 飞行器着火点数据
/// </summary>
/// <remarks>
/// 保存碰撞区域内的局部坐标及创建时选定的特效，生命周期内保持不变。
/// </remarks>
internal sealed class AircraftFirePointVO {

    /**相对飞行器逻辑原点的坐标，与碰撞形状使用相同坐标系*/
    public readonly Vector2 localPosition;

    /**着火规模*/
    public readonly AircraftFireSize fireSize;

    /**循环播放的火焰特效ID*/
    public readonly int fireEffectId;

    /**定时生成的烟雾特效ID*/
    public readonly int smokeEffectId;

    /**创建不可变的着火点数据*/
    public AircraftFirePointVO(Vector2 localPosition, AircraftFireSize fireSize, int fireEffectId, int smokeEffectId) {
        this.localPosition = localPosition;
        this.fireSize = fireSize;
        this.fireEffectId = fireEffectId;
        this.smokeEffectId = smokeEffectId;
    }

}
