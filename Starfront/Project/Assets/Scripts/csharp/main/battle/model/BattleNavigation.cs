using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在当前小规模六边形地图上查询地面最短格子路径。
/// </summary>
/// <remarks>只按地形通行，不把阵营、解锁、建筑或其他单位当成障碍。</remarks>
public sealed class BattleNavigation {

    private readonly BattleSceneModel model;

    public BattleNavigation(BattleSceneModel model) {
        this.model = model;
    }

    /**先完成当前路段，再沿相邻格心行进，重寻路不穿越障碍拐角。*/
    public List<Vector2> FindGroundPathFromPosition(Vector2 position, Vector2 destination, Vector2 nextRoutePoint, cfg.UnitMovementType movement = cfg.UnitMovementType.GROUND) {
        var start = model.FindCell(nextRoutePoint);
        var target = model.FindCell(destination);
        var path = FindPath(start, target, movement);
        if (path == null) {
            return null;
        }
        if ((start.position - position).sqrMagnitude > .0001f) {
            path.Insert(0, start.position);
        }
        if (path.Count == 0 || (path[path.Count - 1] - destination).sqrMagnitude > .0001f) {
            path.Add(destination);
        }
        return path;
    }

    /**返回包含目标、不包含起点的格心路径；不连通时返回null。*/
    public List<Vector2> FindGroundPath(BattleCellVO start, BattleCellVO target) {
        return FindPath(start, target, cfg.UnitMovementType.GROUND);
    }

    public List<Vector2> FindPath(BattleCellVO start, BattleCellVO target, cfg.UnitMovementType movement) {
        bool flying = movement == cfg.UnitMovementType.FLYING;
        if (start == null || target == null || (!flying && (!start.groundPassable || !target.groundPassable))) {
            return null;
        }
        var frontier = new Queue<BattleCellVO>();
        var parents = new Dictionary<long, BattleCellVO> { { start.id, null } };
        frontier.Enqueue(start);
        while (frontier.Count > 0) {
            var current = frontier.Dequeue();
            if (current.id == target.id) {
                var result = new List<Vector2>();
                while (current.id != start.id) {
                    result.Add(current.position);
                    current = parents[current.id];
                }
                result.Reverse();
                return result;
            }
            foreach (var neighbor in model.GetNeighbors(current)) {
                if ((flying || neighbor.groundPassable) && !parents.ContainsKey(neighbor.id)) {
                    parents.Add(neighbor.id, current);
                    frontier.Enqueue(neighbor);
                }
            }
        }
        return null;
    }

}
