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
            if (runtimeShape.size.x <= 0f || runtimeShape.size.y <= 0f) {
                throw new InvalidOperationException("飞机碰撞形状尺寸必须大于 0");
            }
            Vector2 half = runtimeShape.size * 0.5f;
            min = Vector2.Min(min, runtimeShape.centerOffset - half);
            max = Vector2.Max(max, runtimeShape.centerOffset + half);
            shapes.Add(runtimeShape);
        }
        return new AircraftCollisionVO(shapes, (min + max) * 0.5f, max - min);
    }
}
