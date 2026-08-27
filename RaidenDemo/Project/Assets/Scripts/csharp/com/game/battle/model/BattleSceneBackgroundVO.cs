/// <summary>
/// 战斗场景背景数据
/// </summary>
/// <remarks>
/// 保存单个场景的远景、低空、中空与高空视差层配置。
/// </remarks>
internal sealed class BattleSceneBackgroundVO {

    public readonly string backgroundRes;
    public readonly float backgroundScrollSpeed;
    public readonly string lowRes;
    public readonly float lowScrollSpeed;
    public readonly string middleRes;
    public readonly float middleScrollSpeed;
    public readonly string highRes;
    public readonly float highScrollSpeed;

    public BattleSceneBackgroundVO(string backgroundRes, float backgroundScrollSpeed, string lowRes, float lowScrollSpeed, string middleRes, float middleScrollSpeed, string highRes, float highScrollSpeed) {
        this.backgroundRes = backgroundRes;
        this.backgroundScrollSpeed = backgroundScrollSpeed;
        this.lowRes = lowRes;
        this.lowScrollSpeed = lowScrollSpeed;
        this.middleRes = middleRes;
        this.middleScrollSpeed = middleScrollSpeed;
        this.highRes = highRes;
        this.highScrollSpeed = highScrollSpeed;
    }
}
