/// <summary>
/// 战斗配置查询
/// </summary>
/// <remarks>
/// 集中提供战斗模块所需的关卡、飞机、敌机和敌弹配置。
/// </remarks>
internal sealed class BattleConfigProvider {

    public int defaultAircraftLevel => RaidenControl.ins.defaultAircraftLevel;

    public StageConfigVO GetStage(int stageId) {
        return RaidenControl.ins.GetStageConfig(stageId) ?? RaidenControl.ins.GetStageConfig(1);
    }

    public EnemyConfigVO GetEliteEnemy() {
        return RaidenControl.ins.model.GetEnemyConfig(4);
    }

    public EnemyConfigVO GetBossEnemy() {
        return RaidenControl.ins.model.GetEnemyConfig(5);
    }

    public EnemyBulletConfigVO GetDefaultEnemyBullet() {
        return RaidenControl.ins.model.GetEnemyBulletConfig(1);
    }

    public PlayerAircraftVO GetSelectedPlayerAircraft() {
        return RaidenControl.ins.GetSelectedPlayerAircraft();
    }

    public PlayerAircraftBattleLevelVO GetPlayerAircraftLevel(int aircraftId, int level) {
        return RaidenControl.ins.GetPlayerAircraftBattleLevel(aircraftId, level);
    }
}
