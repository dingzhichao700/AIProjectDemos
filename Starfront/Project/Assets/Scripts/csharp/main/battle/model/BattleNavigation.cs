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

    /**返回包含目标、不包含起点的格心路径；不连通时返回null。*/
    public List<Vector2> FindGroundPath(BattleCellVO start, BattleCellVO target) {
        if (start == null || target == null || !start.groundPassable || !target.groundPassable) {
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
                if (neighbor.groundPassable && !parents.ContainsKey(neighbor.id)) {
                    parents.Add(neighbor.id, current);
                    frontier.Enqueue(neighbor);
                }
            }
        }
        return null;
    }

}
