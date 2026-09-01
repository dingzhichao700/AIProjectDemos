using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>
/// 可拾取奖励数据
/// </summary>
/// <remarks>
/// 表示关卡内奖励场景元素，并负责自身的下落和吸附移动逻辑。
/// </remarks>
internal sealed class RewardVO : SceneElementVO {
    public readonly int itemId;
    public readonly StageItemType type;
    public readonly string resPath;
    public readonly float collisionRadius;
    public readonly int effectValue;
    public readonly int effectId;
    public readonly bool isNaturalSupply;
    private readonly float moveSpeed;
    private readonly float swayAmplitude;
    private readonly float swayAngularSpeed;
    private float swayElapsed;
    private float swayPhase;

    public RewardVO(long id, Vector2 position, StageItemResource config, bool isNaturalSupply = false)
        : base(id, SceneElementFaction.NEUTRAL, TimerType.SCENE, position) {
        itemId = config.Id;
        type = config.Type;
        resPath = BattleConst.GetRaidenUnpackImagePath(config.Res);
        collisionRadius = config.CollisionRadius;
        effectValue = config.EffectValue;
        effectId = config.EffectId;
        moveSpeed = config.MoveSpeed;
        swayAmplitude = config.SwayAmplitude;
        swayAngularSpeed = Mathf.PI * 2f / Mathf.Max(0.001f, config.SwayPeriodMs / 1000f);
        ResetSwayPath(position.x);
        this.isNaturalSupply = isNaturalSupply;
    }

    public override void OnTimeUpdate(float deltaTime) {
        swayElapsed += deltaTime;
        float swayCenterX = BattleConst.BattleViewportWidth * 0.5f;
        float swayOffset = Mathf.Sin(swayPhase + swayElapsed * swayAngularSpeed) * swayAmplitude;
        position = new Vector2(swayCenterX + swayOffset, position.y - moveSpeed * deltaTime);
    }

    /**从当前横坐标平滑接入覆盖整个视窗的摆动轨迹。*/
    private void ResetSwayPath(float currentX) {
        float swayCenterX = BattleConst.BattleViewportWidth * 0.5f;
        float normalizedX = Mathf.Clamp((currentX - swayCenterX) / Mathf.Max(0.001f, swayAmplitude), -1f, 1f);
        float phase = Mathf.Asin(normalizedX);
        swayPhase = UnityEngine.Random.value < 0.5f ? phase : Mathf.PI - phase;
        swayElapsed = 0f;
    }
}
