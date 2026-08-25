/// <summary>
/// 子弹发射器运行数据
/// </summary>
/// <remarks>
/// 保存飞机持有的发射器运行状态，并负责按配置推进发射节奏。
/// </remarks>
internal sealed class BulletLauncherVO {
    public readonly PlayerBulletLauncherVO config;
    public float fireCooldown;
    public float bulletCooldown;
    public int nextProjectileIndex;
    public int pendingProjectileCount;
    public BulletLauncherVO(PlayerBulletLauncherVO config) { this.config = config; }
    public void Reset() {
        fireCooldown = 0f; bulletCooldown = 0f;
        nextProjectileIndex = 0; pendingProjectileCount = 0;
    }
}
