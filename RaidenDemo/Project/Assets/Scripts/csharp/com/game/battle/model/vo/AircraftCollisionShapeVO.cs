using UnityEngine;

/// <summary>
/// 飞机碰撞形状数据
/// </summary>
/// <remarks>
/// 保存单个圆形或矩形碰撞形状的运行时只读参数。
/// </remarks>
public sealed class AircraftCollisionShapeVO {

    public readonly bool isCircle;
    public readonly Vector2 size;
    public readonly Vector2 centerOffset;
    public readonly float radius;

    public AircraftCollisionShapeVO(bool isCircle, Vector2 size, Vector2 centerOffset, float radius) {
        this.isCircle = isCircle;
        this.size = size;
        this.centerOffset = centerOffset;
        this.radius = radius;
    }
}
