using UnityEngine;

/// <summary>
/// 可拾取奖励数据
/// </summary>
/// <remarks>
/// 表示关卡内奖励场景元素，并负责自身的下落和吸附移动逻辑。
/// </remarks>
internal sealed class RewardVO : SceneElementVO {
    public readonly BattleRewardType type;
    public readonly bool isNaturalSupply;
    public bool isAttracting;
    public AircraftVO target;
    public RewardVO(long id, Vector2 position, BattleRewardType type,
        bool isNaturalSupply = false)
        : base(id, SceneElementFaction.NEUTRAL, TimerType.SCENE, position) {
        this.type = type; this.isNaturalSupply = isNaturalSupply;
    }
    public override void OnTimeUpdate(float deltaTime) {
        bool canAttract = target != null && !target.destroyed;
        if (canAttract && !isAttracting) {
            Vector2 delta = target.position - position;
            float radius = BattleConst.RewardDropAttractRadius;
            isAttracting = delta.sqrMagnitude <= radius * radius;
        }
        if (canAttract && isAttracting) {
            position = Vector2.MoveTowards(position, target.position,
                BattleConst.RewardDropAttractSpeed * deltaTime);
        } else {
            isAttracting = false;
            position += Vector2.down * (BattleConst.UpgradeDropSpeed * deltaTime);
        }
    }
}
