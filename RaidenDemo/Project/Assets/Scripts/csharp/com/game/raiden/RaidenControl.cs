using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 雷电模块入口，负责组织模块数据并处理关卡进度业务
/// </summary>
public sealed class RaidenControl {

    /**单例实例*/
    private static RaidenControl instance;

    /**雷电模块入口单例*/
    public static RaidenControl ins => instance ??= new RaidenControl();

    /**雷电模块数据管理*/
    public RaidenModel model { get; }

    /**当前关卡配置总数*/
    public int stageCount => model.stageCount;

    public int availableStarCount => model.availableStarCount;

    public int selectedAircraftId => model.selectedAircraftId;

    public int selectedWingmanId => model.selectedWingmanId;

    public int defaultAircraftLevel => model.defaultAircraftLevel;

    /**初始化模块数据*/
    private RaidenControl() {
        model = new RaidenModel();
    }

    /**获取指定关卡配置*/
    public StageConfigVO GetStageConfig(int stageId) {
        return model.GetStageConfig(stageId);
    }

    public List<PlayerAircraftVO> GetAllPlayerAircraft() {
        return model.GetAllPlayerAircraft();
    }

    public PlayerAircraftVO GetSelectedPlayerAircraft() {
        return model.GetPlayerAircraft(selectedAircraftId);
    }

    /**获取当前出战僚机配置。*/
    public WingmanConfigVO GetSelectedWingman() {
        return model.GetWingmanConfig(selectedWingmanId);
    }

    /**获取指定玩家飞机类型*/
    public PlayerAircraftVO GetPlayerAircraft(int aircraftId) {
        return model.GetPlayerAircraft(aircraftId);
    }

    /**获取指定玩家飞机等级的关卡内战斗配置*/
    public PlayerAircraftBattleLevelVO GetPlayerAircraftBattleLevel(int aircraftId, int level) {
        return model.GetPlayerAircraftBattleLevel(aircraftId, level);
    }

    /**获取当前出战飞机指定等级的关卡内战斗配置*/
    public PlayerAircraftBattleLevelVO GetSelectedPlayerAircraftBattleLevel(int level) {
        return model.GetPlayerAircraftBattleLevel(selectedAircraftId, level);
    }

    public bool IsPlayerAircraftUnlocked(int aircraftId) {
        return model.IsPlayerAircraftUnlocked(aircraftId);
    }

    public bool UnlockPlayerAircraft(int aircraftId) {
        return model.TryUnlockPlayerAircraft(aircraftId);
    }

    public bool SelectPlayerAircraft(int aircraftId) {
        return model.TrySelectPlayerAircraft(aircraftId);
    }

    /**判断指定关卡当前是否允许进入*/
    public bool IsStageUnlocked(int stageId) {
        StageProgressVO progress = model.GetStageProgress(stageId);
        return progress != null && progress.unlocked;
    }

    /// <summary>
    /// 结算已解锁关卡，只提升历史最佳成绩，并在首次或重复通关后保持下一关解锁
    /// </summary>
    /// <param name="stageId">需要结算的关卡编号</param>
    /// <param name="score">本次关卡得分</param>
    public void CompleteStage(int stageId, int score) {
        StageProgressVO progress = model.GetStageProgress(stageId);
        if (progress == null || !progress.unlocked) {
            return;
        }

        int safeScore = Mathf.Max(0, score);
        progress.passed = true;
        progress.highestScore = Mathf.Max(progress.highestScore, safeScore);
        progress.highestStar = Mathf.Max(progress.highestStar, CalculateStageStar(stageId, safeScore));

        StageProgressVO next = model.GetStageProgress(stageId + 1);
        if (next != null) {
            next.unlocked = true;
        }
    }

    /// <summary>
    /// 按 Luban 关卡配置的分数边界计算星级
    /// </summary>
    /// <param name="stageId">用于读取星级边界的关卡编号</param>
    /// <param name="score">本次关卡得分</param>
    /// <returns>一至三星的关卡评价</returns>
    public int CalculateStageStar(int stageId, int score) {
        StageConfigVO config = GetStageConfig(stageId);
        if (config == null) {
            return 1;
        }
        if (score >= config.threeStarScore) {
            return 3;
        }
        if (score >= config.twoStarScore) {
            return 2;
        }
        return 1;
    }

    /**重置雷电模块的当前运行数据*/
    public void Reset() {
        model.Reset();
    }

}
