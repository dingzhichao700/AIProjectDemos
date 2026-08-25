/// <summary>
/// 单个关卡在当前游戏运行周期内的进度
/// </summary>
public sealed class StageProgressVO {

    /**关卡编号，从 1 开始*/
    public int stageId;

    /**当前运行周期内是否已经解锁*/
    public bool unlocked;

    /**当前运行周期内是否至少通关一次*/
    public bool passed;

    /**当前运行周期内的历史最高得分*/
    public int highestScore;

    /**当前运行周期内的历史最高星级*/
    public int highestStar;

}
