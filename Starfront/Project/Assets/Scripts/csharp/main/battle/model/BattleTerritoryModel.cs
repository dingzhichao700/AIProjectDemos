using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

/// <summary>汇总双方逻辑足迹与驻留，占领空地；建筑击毁使用致命伤害的阵营快照。</summary>
public sealed class BattleTerritoryModel {
    private readonly BattleSceneModel scene;
    private readonly Dictionary<long, int> claims = new Dictionary<long, int>();
    private readonly HashSet<long> protectedCells = new HashSet<long>();
    public event Action Changed;

    internal BattleTerritoryModel(BattleSceneModel scene) {
        this.scene = scene;
    }

    /**在sceneTimer结算上一轮双方移动，先汇总再变更，不依赖单位遍历先后。*/
    internal void Resolve() {
        if (scene.expired) {
            return;
        }
        claims.Clear();
        protectedCells.Clear();
        foreach (var building in scene.buildings.Values) {
            if (building.alive) {
                protectedCells.Add(building.cell.id);
            }
        }
        foreach (var element in scene.elements.Values) {
            if (!(element is BattleUnitVO unit)) {
                continue;
            }
            if (unit.alive && unit.config.CanCaptureTerritory) {
                Claim(scene.FindCell(unit.position), unit.camp);
                var trace = unit.territoryTrace;
                for (int i = 1; i < trace.Count; i++) {
                    Trace(trace[i - 1], trace[i], unit.camp);
                }
            }
            unit.ClearTerritoryTrace();
        }
        bool changed = false;
        foreach (var pair in claims) {
            if (pair.Value == 1 || pair.Value == 2) {
                changed |= scene.GetCell(pair.Key).TrySetCamp(pair.Value == 1 ? BattleSceneCamp.FRIENDLY : BattleSceneCamp.ENEMY);
            }
        }
        if (changed) {
            Changed?.Invoke();
            scene.NotifyInformationChanged();
        }
    }

    private void Claim(BattleCellVO cell, BattleSceneCamp camp) {
        if (cell == null || !cell.capturable || protectedCells.Contains(cell.id)) {
            return;
        }
        int mask = camp == BattleSceneCamp.FRIENDLY ? 1 : 2;
        claims.TryGetValue(cell.id, out int previous);
        claims[cell.id] = previous | mask;
    }

    private void Trace(Vector2 start, Vector2 end, BattleSceneCamp camp) {
        if (start == end) {
            return;
        }
        // 快速路径：绝大多数帧没有跨格；凸六边形内的线段不可能经过其他格。
        var from = scene.FindCell(start);
        var to = scene.FindCell(end);
        if (from != null && from == to) {
            Claim(from, camp);
            return;
        }
        foreach (var cell in scene.cells.Values) {
            if (cell.capturable && IntersectsInterior(start - cell.position, end - cell.position)) {
                Claim(cell, camp);
            }
        }
    }

    // 对标准六边形六个半平面裁剪线段；只触碰边角不算进入。
    private static bool IntersectsInterior(Vector2 a, Vector2 b) {
        float low = 0, high = 1;
        float w = BattleConst.CellWidth / 2, h = BattleConst.CellHeight / 2;
        float slope = BattleConst.CellWidth / (2 * BattleConst.CellHeight);
        return Clip(a.y, b.y, h, ref low, ref high)
            && Clip(-a.y, -b.y, h, ref low, ref high)
            && Clip(a.x + slope * a.y, b.x + slope * b.y, w, ref low, ref high)
            && Clip(a.x - slope * a.y, b.x - slope * b.y, w, ref low, ref high)
            && Clip(-a.x + slope * a.y, -b.x + slope * b.y, w, ref low, ref high)
            && Clip(-a.x - slope * a.y, -b.x - slope * b.y, w, ref low, ref high)
            && high - low > .000001f;
    }

    private static bool Clip(float a, float b, float limit, ref float low, ref float high) {
        limit -= .0001f;
        float delta = b - a;
        if (Mathf.Abs(delta) < .000001f) {
            return a <= limit;
        }
        float t = (limit - a) / delta;
        if (delta > 0) {
            high = Mathf.Min(high, t);
        } else {
            low = Mathf.Max(low, t);
        }
        return low <= high;
    }

    internal void CaptureDestroyedBuilding(BattleBuildingVO building, BattleSceneCamp sourceCamp) {
        if (scene.expired || building.config.Type == BuildingType.BASE || building.hp > 0
            || sourceCamp == building.camp || !building.cell.TrySetCamp(sourceCamp)) {
            return;
        }
        Changed?.Invoke();
        scene.NotifyInformationChanged();
    }

    internal void Dispose() {
        Changed = null;
        claims.Clear();
        protectedCells.Clear();
    }
}
