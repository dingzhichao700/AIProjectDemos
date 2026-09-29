using cfg;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 飞机复合碰撞数据
/// </summary>
/// <remarks>
/// 管理飞机的多个碰撞形状，并缓存整体包围盒用于碰撞粗筛。
/// </remarks>
public sealed class AircraftCollisionVO {

    public readonly IReadOnlyList<AircraftCollisionShapeVO> shapes;
    public readonly Vector2 boundsCenterOffset;
    public readonly Vector2 boundsSize;

    private AircraftCollisionVO(IReadOnlyList<AircraftCollisionShapeVO> shapes, Vector2 boundsCenterOffset, Vector2 boundsSize) {
        this.shapes = shapes;
        this.boundsCenterOffset = boundsCenterOffset;
        this.boundsSize = boundsSize;
    }

    public static AircraftCollisionVO Create(IReadOnlyList<Shape> source) {
        if (source == null || source.Count == 0) {
            throw new InvalidOperationException("飞机配置缺少碰撞形状");
        }
        List<AircraftCollisionShapeVO> shapes = new List<AircraftCollisionShapeVO>(source.Count);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Shape shape in source) {
            AircraftCollisionShapeVO runtimeShape;
            if (shape is RectangleShape rectangle) {
                Vector2 size = new Vector2(rectangle.Rect.X, rectangle.Rect.Y);
                Vector2 pivot = new Vector2(rectangle.Pivot.X, rectangle.Pivot.Y);
                runtimeShape = new AircraftCollisionShapeVO(false, size, new Vector2((0.5f - pivot.x) * size.x, (0.5f - pivot.y) * size.y), 0f);
            } else if (shape is CircleShape circle) {
                float radius = circle.Radius;
                runtimeShape = new AircraftCollisionShapeVO(true, Vector2.one * radius * 2f, Vector2.zero, radius);
            } else {
                throw new InvalidOperationException("飞机配置包含未知碰撞形状");
            }
            if (runtimeShape.size.x <= 0f || runtimeShape.size.y <= 0f || float.IsNaN(runtimeShape.size.x) || float.IsNaN(runtimeShape.size.y) || float.IsInfinity(runtimeShape.size.x) || float.IsInfinity(runtimeShape.size.y)) {
                throw new InvalidOperationException("飞机碰撞形状尺寸必须大于 0");
            }
            if (float.IsNaN(runtimeShape.centerOffset.x) || float.IsNaN(runtimeShape.centerOffset.y) || float.IsInfinity(runtimeShape.centerOffset.x) || float.IsInfinity(runtimeShape.centerOffset.y)) {
                throw new InvalidOperationException("飞机碰撞形状中心必须为有限坐标");
            }
            Vector2 half = runtimeShape.size * 0.5f;
            min = Vector2.Min(min, runtimeShape.centerOffset - half);
            max = Vector2.Max(max, runtimeShape.centerOffset + half);
            shapes.Add(runtimeShape);
        }
        return new AircraftCollisionVO(shapes, (min + max) * 0.5f, max - min);
    }

    /**判断局部坐标是否位于实际组合碰撞区域内*/
    public bool ContainsPoint(Vector2 point) {
        foreach (AircraftCollisionShapeVO shape in shapes) {
            if (shape.ContainsPoint(point)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 在组合碰撞区域内均匀取点。
    /// </summary>
    /// <param name="random">随机数来源</param>
    /// <returns>相对飞行器逻辑原点的坐标</returns>
    /// <remarks>
    /// 按形状面积抽样，再按重叠形状数量拒绝重复权重，使相交区域与其他区域具有相同的采样密度。
    /// </remarks>
    public Vector2 GetRandomPoint(System.Random random) {
        if (random == null) {
            throw new ArgumentNullException(nameof(random));
        }
        double totalArea = 0d;
        foreach (AircraftCollisionShapeVO shape in shapes) {
            totalArea += shape.area;
        }
        while (true) {
            double remainingArea = random.NextDouble() * totalArea;
            AircraftCollisionShapeVO selected = shapes[shapes.Count - 1];
            foreach (AircraftCollisionShapeVO shape in shapes) {
                remainingArea -= shape.area;
                if (remainingArea < 0d) {
                    selected = shape;
                    break;
                }
            }
            Vector2 point = selected.GetRandomPoint(random);
            int overlapCount = 0;
            foreach (AircraftCollisionShapeVO shape in shapes) {
                if (shape.ContainsPoint(point)) {
                    overlapCount++;
                }
            }
            if (overlapCount > 0 && random.NextDouble() * overlapCount < 1d) {
                return point;
            }
        }
    }

}
