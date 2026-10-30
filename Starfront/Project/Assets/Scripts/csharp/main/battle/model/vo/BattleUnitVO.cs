using System;
using System.Collections.Generic;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>
/// 作战单位的逻辑位置、行进路径与移动朝向。
/// </summary>
/// <remarks>由所属Timer推进，表现离屏不停止移动；战斗决策由所属场景战斗Model驱动。</remarks>
public sealed class BattleUnitVO : BattleCombatantVO {

    public UnitResource config { get; }
    /**离地高度，场景像素；position仍为地面投影，不改变移动/攻击资格。*/
    public float altitude { get; private set; }

    internal void SetAltitude(float value) {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        altitude = value;
    }

    public BattleUnitState state { get; internal set; } = BattleUnitState.Marching;
    public long targetId { get; internal set; }
    internal double scanRemainingMs;
    internal double pathRemainingMs;
    public IReadOnlyList<BattleSkillVO> skills { get; }
    public BattleSkillCastVO activeCast { get; internal set; }
    public double skillTimeMs { get; internal set; }

    public Vector2 heading { get; private set; }

    public double animationMs { get; private set; }

    public bool moving => !destroyed && waypoint < route.Count;

    private IReadOnlyList<Vector2> route;

    private int waypoint;
    private readonly List<Vector2> captureTrace = new List<Vector2>();
    internal IReadOnlyList<Vector2> territoryTrace => captureTrace;

    internal void ClearTerritoryTrace() {
        captureTrace.Clear();
    }



    public UnitVisualResource visual { get; }

    public BattleUnitVO(long id, UnitResource config, BattleSceneCamp camp, Vector2 position, IReadOnlyList<Vector2> route = null) : base(id, camp, position, config.MaxHp) {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        if (camp != BattleSceneCamp.FRIENDLY && camp != BattleSceneCamp.ENEMY) {
            throw new ArgumentOutOfRangeException(nameof(camp));
        }
        if (config.MoveSpeed <= 0 || float.IsNaN(config.MoveSpeed) || float.IsInfinity(config.MoveSpeed)) {
            throw new InvalidOperationException("单位移动速度未配置：" + config.Id);
        }
        visual = CfgManager.tables.UnitVisualObj.GetOrDefault(config.VisualId);
        if (visual == null || visual.NormalHeight <= 0 || float.IsNaN(visual.NormalHeight) || float.IsInfinity(visual.NormalHeight)) {
            throw new InvalidOperationException("单位形象缺失或常态高度非法：" + config.Id);
        }
        SetAltitude(config.DefaultAltitude);
        skills = BattleSkillModel.CreateSkills(config, CfgManager.tables);
        this.route = route == null ? new List<Vector2>().AsReadOnly() : new List<Vector2>(route).AsReadOnly();
        heading = camp == BattleSceneCamp.FRIENDLY ? Vector2.down : Vector2.up;
    }

    internal Vector2 nextRoutePoint => moving ? route[waypoint] : position;

    internal void SetRoute(IReadOnlyList<Vector2> path) {
        route = path ?? new List<Vector2>().AsReadOnly();
        waypoint = 0;
    }

    internal void StopMoving() {
        waypoint = route.Count;
    }

    internal void Face(Vector2 target) {
        Vector2 direction = target - position;
        if (direction.sqrMagnitude > .0001f) {
            heading = direction.normalized;
        }
    }

    /**消费距离预算而非一帧一个路点，长帧也不穿过障碍或超过目的地。*/
    public override void OnTimeUpdate(float deltaTime) {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }
        if (!moving || deltaTime == 0) {
            return;
        }
        if (config.CanCaptureTerritory && captureTrace.Count == 0) {
            captureTrace.Add(position);
        }
        double budget = deltaTime * (double)config.MoveSpeed;
        double travelled = 0;
        while (waypoint < route.Count && budget > 0) {
            Vector2 delta = route[waypoint] - position;
            float distance = delta.magnitude;
            if (distance <= .0001f) {
                position = route[waypoint++];
                continue;
            }
            heading = delta / distance;
            double step = Math.Min(distance, budget);
            position += heading * (float)step;
            if (config.CanCaptureTerritory) {
                captureTrace.Add(position);
            }
            travelled += step;
            budget -= step;
            if (step >= distance) {
                position = route[waypoint++];
            }
        }
        animationMs += travelled / config.MoveSpeed * 1000;
    }

}
