using UnityEngine;

/// <summary>
/// 飞机坠落状态
/// </summary>
/// <remarks>
/// 保存坠落阶段的平面速度、高度加速度与旋转加速度，并在抵达地面高度时结束。
/// </remarks>
internal sealed class AircraftFallingState {

    /**坠落阶段的平面速度*/
    public Vector2 velocity { get; private set; }

    /**坠落阶段的旋转角速度*/
    public float angularSpeed { get; private set; }

    /**坠落阶段的旋转角加速度*/
    public float angularAcceleration { get; private set; }

    /**最近一次更新产生的旋转角度*/
    public float rotationDelta { get; private set; }

    /**当前飞行高度*/
    public float height { get; private set; }

    /**当前高度下降速度*/
    public float heightSpeed { get; private set; }

    /**是否已经降至地面高度*/
    public bool landed { get; private set; }

    /**初始化一次坠落*/
    public void Begin(float initialHeight, Vector2 fallingVelocity, float fallingAngularAcceleration) {
        height = initialHeight;
        heightSpeed = 0f;
        velocity = fallingVelocity;
        angularSpeed = 0f;
        angularAcceleration = fallingAngularAcceleration;
        rotationDelta = 0f;
        landed = false;
    }

    /**推进坠落位置和高度*/
    public Vector2 Update(float deltaTime) {
        if (landed || deltaTime <= 0f) {
            return Vector2.zero;
        }
        int acceleration = BattleAircraftDeathConst.FallingHeightAcceleration;
        if (acceleration <= 0) {
            throw new System.InvalidOperationException("飞机高度下降加速度必须大于0");
        }
        float fallingDistance = heightSpeed * deltaTime + acceleration * deltaTime * deltaTime * 0.5f;
        heightSpeed += acceleration * deltaTime;
        rotationDelta = angularSpeed * deltaTime + angularAcceleration * deltaTime * deltaTime * 0.5f;
        angularSpeed += angularAcceleration * deltaTime;
        height = Mathf.Max(BattleAircraftDeathConst.GroundHeight, height - fallingDistance);
        landed = height <= BattleAircraftDeathConst.GroundHeight;
        return velocity * deltaTime;
    }

    /**清除坠落状态*/
    public void Clear() {
        velocity = Vector2.zero;
        angularSpeed = 0f;
        angularAcceleration = 0f;
        rotationDelta = 0f;
        height = BattleAircraftDeathConst.NormalHeight;
        heightSpeed = 0f;
        landed = false;
    }

}
