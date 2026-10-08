/// <summary>
/// 单个来源对子弹发射器属性提供的万分比修正
/// </summary>
/// <remarks>
/// 相同来源在同一乘区内覆盖旧值，不负责持续时间等状态生命周期。
/// </remarks>
internal readonly struct BulletLauncherModifierVO {

    /**修正来源的稳定唯一 ID。*/
    public readonly int sourceId;

    /**两轮开火间隔修正，单位万分比。*/
    public readonly int fireCooldownRate;

    /**每轮发射数量修正，单位万分比。*/
    public readonly int shotCountRate;

    /**轮内相邻子弹发射间隔修正，单位万分比。*/
    public readonly int shotIntervalRate;

    public BulletLauncherModifierVO(int sourceId, int fireCooldownRate, int shotCountRate, int shotIntervalRate) {
        this.sourceId = sourceId;
        this.fireCooldownRate = fireCooldownRate;
        this.shotCountRate = shotCountRate;
        this.shotIntervalRate = shotIntervalRate;
    }
}
