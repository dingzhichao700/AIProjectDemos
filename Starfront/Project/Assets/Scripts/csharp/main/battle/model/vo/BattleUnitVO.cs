using System;
using System.Collections.Generic;
using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>
/// 作战单位的逻辑位置、行进路径与移动朝向。
/// </summary>
/// <remarks>由所属Timer推进，表现离屏不停止移动；生命与攻击尚未接入。</remarks>
public sealed class BattleUnitVO : SceneElementVO {

    public UnitResource config { get; }

    public BattleSceneCamp camp { get; }

    public Vector2 heading { get; private set; }

    public double animationMs { get; private set; }

    public bool moving => !destroyed && waypoint < route.Count;

    private readonly IReadOnlyList<Vector2> route;

    private int waypoint;

    public BattleUnitVO(long id, UnitResource config, BattleSceneCamp camp, Vector2 position, IReadOnlyList<Vector2> route = null) : base(id, camp == BattleSceneCamp.FRIENDLY ? TimerType.PLAYER : TimerType.ENEMY, position) {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        if (camp != BattleSceneCamp.FRIENDLY && camp != BattleSceneCamp.ENEMY) {
            throw new ArgumentOutOfRangeException(nameof(camp));
        }
        if (config.MoveSpeed <= 0 || float.IsNaN(config.MoveSpeed) || float.IsInfinity(config.MoveSpeed)) {
            throw new InvalidOperationException("单位移动速度未配置：" + config.Id);
        }
        this.camp = camp;
        this.route = route == null ? new List<Vector2>().AsReadOnly() : new List<Vector2>(route).AsReadOnly();
        heading = camp == BattleSceneCamp.FRIENDLY ? Vector2.down : Vector2.up;
    }

    /**消费距离预算而非一帧一个路点，长帧也不穿过障碍或超过目的地。*/
    public override void OnTimeUpdate(float deltaTime) {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }
        if (!moving || deltaTime == 0) {
            return;
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
            travelled += step;
            budget -= step;
            if (step >= distance) {
                position = route[waypoint++];
            }
        }
        animationMs += travelled / config.MoveSpeed * 1000;
    }

}
