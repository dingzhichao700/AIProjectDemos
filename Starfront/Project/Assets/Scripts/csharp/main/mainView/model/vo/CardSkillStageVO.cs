/// <summary>
/// 单个技能阶的只读展示快照。
/// </summary>
/// <remarks>阶数、效果、条件与生效状态必须由正式配置和业务查询提供，视图不推导。</remarks>
public sealed class CardSkillStageVO {

    public int rank;

    public string effect;

    public string unlockCondition;

    public bool unlocked;

    public bool current;

}