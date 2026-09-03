using cfg;
using cfg.resource;
using UnityEngine;

/// <summary>
/// 可拾取奖励数据
/// </summary>
/// <remarks>
/// 表示关卡内奖励场景元素，并负责随机移动、边缘反弹和离场逻辑。
/// </remarks>
internal sealed class RewardVO : SceneElementVO {
    public readonly int itemId;
    public readonly StageItemEffectType type;
    public readonly string resPath;
    public readonly float collisionRadius;
    public readonly int effectValue;
    public readonly int effectId;
    public readonly string pickupText;
    public readonly bool isNaturalSupply;
    private readonly float moveSpeed;
    private Vector2 moveDirection;
    private float bounceRemaining;
    private float warningElapsed;

    public bool isCollected { get; private set; }

    public float iconAlpha {
        get {
            if (bounceRemaining > BattleConst.RewardPickupWarningDuration) {
                return 1f;
            }
            float fadeProgress = Mathf.PingPong(warningElapsed / BattleConst.RewardPickupWarningFadeHalfCycleDuration, 1f);
            return Mathf.Lerp(1f, BattleConst.RewardPickupWarningMinAlpha, fadeProgress);
        }
    }

    public RewardVO(long id, Vector2 position, StageItemResource config, bool isNaturalSupply = false)
        : base(id, SceneElementFaction.NEUTRAL, TimerType.SCENE, position) {
        itemId = config.Id;
        type = config.EffectType;
        resPath = BattleConst.GetRaidenUnpackImagePath(config.Res);
        collisionRadius = config.CollisionRadius;
        effectValue = config.EffectValue;
        effectId = config.EffectId;
        pickupText = config.PickupText;
        moveSpeed = config.MoveSpeed;
        bounceRemaining = config.BounceDurationMs / 1000f;
        moveDirection = CreateInitialDirection();
        this.isNaturalSupply = isNaturalSupply;
    }

    public override void OnTimeUpdate(float deltaTime) {
        float currentMoveSpeed = isCollected ? BattleConst.RewardCollectedMoveSpeed : moveSpeed;
        position += moveDirection * currentMoveSpeed * deltaTime;
        if (isCollected) {
            return;
        }
        if (bounceRemaining <= BattleConst.RewardPickupWarningDuration) {
            warningElapsed += deltaTime;
        }
        if (bounceRemaining <= 0f) {
            return;
        }
        bounceRemaining = Mathf.Max(0f, bounceRemaining - deltaTime);
        ReflectAtViewportEdge();
    }

    /**进入已拾取表现状态，保留移动但不再参与拾取碰撞。*/
    public void MarkCollected() {
        isCollected = true;
    }

    /**判断道具的圆形范围是否已经完全离开战斗视窗。*/
    public bool IsOutsideViewport() {
        return position.x + collisionRadius < 0f || position.x - collisionRadius > BattleConst.BattleViewportWidth || position.y - collisionRadius > 0f || position.y + collisionRadius < -BattleConst.BattleViewportHeight;
    }

    /**随机生成横纵分量较均衡的左下或右下初始方向。*/
    private static Vector2 CreateInitialDirection() {
        bool moveLeft = UnityEngine.Random.value < 0.5f;
        float angle = moveLeft ? UnityEngine.Random.Range(215f, 235f) : UnityEngine.Random.Range(305f, 325f);
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

    /**反射碰到视窗边缘的移动方向，并将圆形道具校正回视窗内。*/
    private void ReflectAtViewportEdge() {
        float minX = collisionRadius;
        float maxX = BattleConst.BattleViewportWidth - collisionRadius;
        float minY = -BattleConst.BattleViewportHeight + collisionRadius;
        float maxY = -collisionRadius;
        Vector2 nextPosition = position;
        if (nextPosition.x < minX || nextPosition.x > maxX) {
            moveDirection.x = -moveDirection.x;
            nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        }
        if (nextPosition.y < minY || nextPosition.y > maxY) {
            moveDirection.y = -moveDirection.y;
            nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);
        }
        position = nextPosition;
    }
}
