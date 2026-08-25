using UnityEngine;

/// <summary>
/// 战斗逻辑层的碰撞检测与接触点计算。
/// </summary>
internal static class BattleCollisionSystem {

    /**检测两个矩形区域是否重叠*/
    public static bool Overlaps(RectTransform left, Vector2 leftSize,
        RectTransform right, Vector2 rightSize) {
        if (left == null || right == null) {
            return false;
        }
        Vector2 delta = left.anchoredPosition - right.anchoredPosition;
        Vector2 combinedHalfSize = (leftSize + rightSize) * 0.5f;
        return Mathf.Abs(delta.x) <= combinedHalfSize.x &&
               Mathf.Abs(delta.y) <= combinedHalfSize.y;
    }
    
    /**检测矩形与飞行物组合形状是否重叠*/
    public static bool Overlaps(RectTransform rectangle, Vector2 rectangleSize,
        RectTransform aircraft, AircraftCollisionVO collision) {
        if (rectangle == null || aircraft == null || collision == null) {
            return false;
        }
        return Overlaps(rectangle.anchoredPosition, rectangleSize,
            aircraft.anchoredPosition, collision);
    }

    /**使用纯逻辑坐标检测矩形与飞行物组合形状*/
    public static bool Overlaps(Vector2 rectangleCenter, Vector2 rectangleSize,
        Vector2 aircraftPosition, AircraftCollisionVO collision) {
        if (collision == null) {
            return false;
        }
        Vector2 boundsCenter = aircraftPosition + collision.boundsCenterOffset;
        if (!OverlapsAabb(rectangleCenter, rectangleSize, boundsCenter, collision.boundsSize)) {
            return false;
        }
        foreach (AircraftCollisionShapeVO shape in collision.shapes) {
            if (OverlapsRectangleShape(rectangleCenter, rectangleSize,
                aircraftPosition + shape.centerOffset, shape)) {
                return true;
            }
        }
        return false;
    }
    
    /**检测两个飞行物组合形状是否重叠*/
    public static bool Overlaps(RectTransform left, AircraftCollisionVO leftCollision,
        RectTransform right, AircraftCollisionVO rightCollision) {
        if (left == null || right == null || leftCollision == null || rightCollision == null) {
            return false;
        }
        return Overlaps(left.anchoredPosition, leftCollision,
            right.anchoredPosition, rightCollision);
    }

    /**使用纯逻辑坐标检测两个飞行物组合形状*/
    public static bool Overlaps(Vector2 leftPosition, AircraftCollisionVO leftCollision,
        Vector2 rightPosition, AircraftCollisionVO rightCollision) {
        if (leftCollision == null || rightCollision == null) {
            return false;
        }
        Vector2 leftBoundsCenter = leftPosition + leftCollision.boundsCenterOffset;
        Vector2 rightBoundsCenter = rightPosition + rightCollision.boundsCenterOffset;
        if (!OverlapsAabb(leftBoundsCenter, leftCollision.boundsSize,
            rightBoundsCenter, rightCollision.boundsSize)) {
            return false;
        }
        foreach (AircraftCollisionShapeVO leftShape in leftCollision.shapes) {
            Vector2 leftCenter = leftPosition + leftShape.centerOffset;
            foreach (AircraftCollisionShapeVO rightShape in rightCollision.shapes) {
                Vector2 rightCenter = rightPosition + rightShape.centerOffset;
                if (OverlapsShapes(leftCenter, leftShape, rightCenter, rightShape)) {
                    return true;
                }
            }
        }
        return false;
    }
    
    /**按形状类型检测两个基础形状*/
    private static bool OverlapsShapes(Vector2 leftCenter, AircraftCollisionShapeVO left,
        Vector2 rightCenter, AircraftCollisionShapeVO right) {
        if (!left.isCircle && !right.isCircle) {
            return OverlapsAabb(leftCenter, left.size, rightCenter, right.size);
        }
        if (left.isCircle && right.isCircle) {
            float radius = left.radius + right.radius;
            return (leftCenter - rightCenter).sqrMagnitude <= radius * radius;
        }
        return left.isCircle
            ? OverlapsRectangleCircle(rightCenter, right.size, leftCenter, left.radius)
            : OverlapsRectangleCircle(leftCenter, left.size, rightCenter, right.radius);
    }
    
    private static bool OverlapsRectangleShape(Vector2 rectangleCenter, Vector2 rectangleSize,
        Vector2 shapeCenter, AircraftCollisionShapeVO shape) {
        return shape.isCircle
            ? OverlapsRectangleCircle(rectangleCenter, rectangleSize, shapeCenter, shape.radius)
            : OverlapsAabb(rectangleCenter, rectangleSize, shapeCenter, shape.size);
    }
    
    private static bool OverlapsRectangleCircle(Vector2 rectangleCenter, Vector2 rectangleSize,
        Vector2 circleCenter, float radius) {
        Vector2 half = rectangleSize * 0.5f;
        Vector2 nearest = new Vector2(
            Mathf.Clamp(circleCenter.x, rectangleCenter.x - half.x, rectangleCenter.x + half.x),
            Mathf.Clamp(circleCenter.y, rectangleCenter.y - half.y, rectangleCenter.y + half.y));
        return (nearest - circleCenter).sqrMagnitude <= radius * radius;
    }
    
    private static bool OverlapsAabb(Vector2 leftCenter, Vector2 leftSize,
        Vector2 rightCenter, Vector2 rightSize) {
        Vector2 delta = leftCenter - rightCenter;
        Vector2 combinedHalfSize = (leftSize + rightSize) * 0.5f;
        return Mathf.Abs(delta.x) <= combinedHalfSize.x &&
               Mathf.Abs(delta.y) <= combinedHalfSize.y;
    }
    
    /**检测子弹命中并计算视觉接触点*/
    public static bool TryGetProjectileContactPoint(BulletVO projectile,
        Vector2 targetPosition, AircraftCollisionVO collision, out Vector2 contactPoint) {
        contactPoint = Vector2.zero;
        if (projectile == null || collision == null) {
            return false;
        }
        Vector2 projectileCenter = GetProjectileCollisionCenter(projectile);
        Vector2 projectileHalfSize = GetProjectileCollisionHalfSize(projectile);
        Vector2 boundsCenter = targetPosition + collision.boundsCenterOffset;
        Vector2 delta = projectileCenter - boundsCenter;
        Vector2 combinedHalfSize = projectileHalfSize + collision.boundsSize * 0.5f;
        if (Mathf.Abs(delta.x) > combinedHalfSize.x ||
            Mathf.Abs(delta.y) > combinedHalfSize.y) {
            return false;
        }
        foreach (AircraftCollisionShapeVO shape in collision.shapes) {
            Vector2 shapeCenter = targetPosition + shape.centerOffset;
            Vector2 shapeDelta = projectileCenter - shapeCenter;
            Vector2 shapeCombinedHalf = projectileHalfSize + shape.size * 0.5f;
            if (Mathf.Abs(shapeDelta.x) > shapeCombinedHalf.x ||
                Mathf.Abs(shapeDelta.y) > shapeCombinedHalf.y) {
                continue;
            }
            if (shape.isCircle) {
                Vector2 nearest = new Vector2(
                    Mathf.Clamp(shapeCenter.x, projectileCenter.x - projectileHalfSize.x, projectileCenter.x + projectileHalfSize.x),
                    Mathf.Clamp(shapeCenter.y, projectileCenter.y - projectileHalfSize.y, projectileCenter.y + projectileHalfSize.y));
                Vector2 fromCenter = nearest - shapeCenter;
                if (fromCenter.sqrMagnitude > shape.radius * shape.radius) {
                    continue;
                }
                Vector2 direction = fromCenter.sqrMagnitude > 0.0001f ? fromCenter.normalized : -projectile.velocity.normalized;
                contactPoint = shapeCenter + direction * shape.radius;
                return true;
            }
            Vector2 centerOffset = projectileCenter - projectile.position;
            Vector2 previousCenter = projectile.previousPosition + centerOffset;
            Vector2 expandedMin = shapeCenter - shapeCombinedHalf;
            Vector2 expandedMax = shapeCenter + shapeCombinedHalf;
            Vector2 entryCenter = projectileCenter;
            if (TryGetSegmentAabbEntry(previousCenter, projectileCenter,
                expandedMin, expandedMax, out Vector2 segmentEntry)) {
                entryCenter = segmentEntry;
            }
            Vector2 half = shape.size * 0.5f;
            contactPoint = new Vector2(
                Mathf.Clamp(entryCenter.x, shapeCenter.x - half.x, shapeCenter.x + half.x),
                Mathf.Clamp(entryCenter.y, shapeCenter.y - half.y, shapeCenter.y + half.y));
            return true;
        }
        return false;
    }
    
    private static Vector2 GetProjectileCollisionHalfSize(BulletVO projectile) {
        float rotation = projectile.rotation;
        float radians = rotation * Mathf.Deg2Rad;
        float sin = Mathf.Abs(Mathf.Sin(radians));
        float cos = Mathf.Abs(Mathf.Cos(radians));
        return new Vector2(
            (cos * projectile.hitSize.x + sin * projectile.hitSize.y) * 0.5f,
            (sin * projectile.hitSize.x + cos * projectile.hitSize.y) * 0.5f);
    }
    
    /**计算移动线段进入矩形边界时的位置*/
    private static bool TryGetSegmentAabbEntry(Vector2 start, Vector2 end,
        Vector2 boundsMin, Vector2 boundsMax, out Vector2 entryPoint) {
        entryPoint = start;
        Vector2 direction = end - start;
        float enter = 0f;
        float exit = 1f;
        for (int axis = 0; axis < 2; axis++) {
            float origin = axis == 0 ? start.x : start.y;
            float delta = axis == 0 ? direction.x : direction.y;
            float minimum = axis == 0 ? boundsMin.x : boundsMin.y;
            float maximum = axis == 0 ? boundsMax.x : boundsMax.y;
            if (Mathf.Abs(delta) < 0.0001f) {
                if (origin < minimum || origin > maximum) {
                    return false;
                }
                continue;
            }
            float first = (minimum - origin) / delta;
            float second = (maximum - origin) / delta;
            if (first > second) {
                (first, second) = (second, first);
            }
            enter = Mathf.Max(enter, first);
            exit = Mathf.Min(exit, second);
            if (enter > exit) {
                return false;
            }
        }
        entryPoint = start + direction * Mathf.Clamp01(enter);
        return true;
    }
    
    private static Vector2 GetProjectileCollisionCenter(BulletVO projectile) {
        float rotation = projectile.rotation;
        float radians = rotation * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        Vector2 localCenter = new Vector2(
            (0.5f - projectile.hitPivot.x) * projectile.hitSize.x,
            (0.5f - projectile.hitPivot.y) * projectile.hitSize.y);
        Vector2 rotatedCenter = new Vector2(
            localCenter.x * cos - localCenter.y * sin,
            localCenter.x * sin + localCenter.y * cos);
        return projectile.position + rotatedCenter;
    }
    
    
}
