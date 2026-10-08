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

    /**形状实际面积，用于组合区域内的均匀采样*/
    public double area => isCircle ? System.Math.PI * radius * radius : (double)size.x * size.y;

    public AircraftCollisionShapeVO(bool isCircle, Vector2 size, Vector2 centerOffset, float radius) {
        this.isCircle = isCircle;
        this.size = size;
        this.centerOffset = centerOffset;
        this.radius = radius;
    }

    /**判断局部坐标是否在当前形状内，包含边界*/
    public bool ContainsPoint(Vector2 point) {
        double x = (double)point.x - centerOffset.x;
        double y = (double)point.y - centerOffset.y;
        return isCircle ? x * x + y * y <= (double)radius * radius : System.Math.Abs(x) <= size.x * 0.5d && System.Math.Abs(y) <= size.y * 0.5d;
    }

    /**在形状实际面积内均匀生成局部坐标*/
    public Vector2 GetRandomPoint(System.Random random) {
        if (isCircle) {
            double angle = random.NextDouble() * System.Math.PI * 2d;
            double distance = System.Math.Sqrt(random.NextDouble()) * radius;
            return centerOffset + new Vector2((float)(System.Math.Cos(angle) * distance), (float)(System.Math.Sin(angle) * distance));
        }
        return centerOffset + new Vector2((float)((random.NextDouble() - 0.5d) * size.x), (float)((random.NextDouble() - 0.5d) * size.y));
    }

}
