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

    /**读取关卡引用的视差背景配置。*/
    public BattleSceneBackgroundVO GetSceneBackground(int sceneId) {
        cfg.resource.SceneBgResource config = CfgManager.tables.SceneBgObj.GetOrDefault(sceneId);
        if (config == null) {
            throw new System.InvalidOperationException($"场景背景 {sceneId} 不存在");
        }
        return new BattleSceneBackgroundVO(config.BackgroundRes, config.BackgroundScrollSpeed, config.LowRes, config.LowScrollSpeed, config.MiddleRes, config.MiddleScrollSpeed, config.HighRes, config.HighScrollSpeed);
    }

    public PlayerAircraftVO GetSelectedPlayerAircraft() {
        return RaidenControl.ins.GetSelectedPlayerAircraft();
    }

    public WingmanConfigVO GetSelectedWingman() {
        return RaidenControl.ins.GetSelectedWingman();
    }

    public PlayerAircraftBattleLevelVO GetPlayerAircraftLevel(int aircraftId, int level) {
        return RaidenControl.ins.GetPlayerAircraftBattleLevel(aircraftId, level);
    }
}
