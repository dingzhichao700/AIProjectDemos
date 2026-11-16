using System;
using cfg;
using cfg.resource;

/// <summary>
/// 建筑自行推进所属生产周期，产兵成功后才重置进度。
/// </summary>
/// <remarks>人口与出生登记交给场景Model；等待期间不累计补发批次。</remarks>
public sealed class BattleBuildingVO : BattleCombatantVO {

    public BuildingResource config { get; }
    public int sourceCardId { get; }
    public int cardLevel { get; }
    public int productionIntervalMs { get; }
    public int currencyCount { get; }
    public System.Collections.Generic.IReadOnlyList<int> skillIds { get; }

    public BattleCellVO cell { get; }

    public BattleCampVO owner { get; }

    public CurrencyProductionResource currencyProduction { get; }

    public UnitProductionResource unitProduction { get; }

    public double productionElapsedMs { get; private set; }

    public double productionProgress => productionElapsedMs / productionIntervalMs;

    private readonly Func<BattleBuildingVO, bool> produceUnit;

    internal BattleBuildingVO(long id, BuildingResource building, BattleCellVO cell, BattleCampVO owner, CurrencyProductionResource production, UnitProductionResource units = null, Func<BattleBuildingVO, bool> produceUnit = null, CardBuildingLevelResource growth = null) : base(id, owner.camp, cell.position, growth == null ? building.MaxHp : growth.MaxHp) {
        bool currencyValid = production != null && production.BuildingId == building.Id && production.Interval > 0 && production.Count > 0;
        bool unitValid = units != null && units.BuildingId == building.Id && units.Interval > 0 && units.Count == 1 && produceUnit != null;
        if (currencyValid == unitValid || (building.Type == BuildingType.UNIT_PRODUCER ? !unitValid : !currencyValid)) {
            throw new InvalidOperationException("建筑生产配置缺失或非法：" + building.Id);
        }
        if (growth != null && growth.BuildingId != building.Id) {
            throw new InvalidOperationException("建筑成长引用不匹配");
        }
        sourceCardId = growth == null ? 0 : growth.CardId;
        cardLevel = growth == null ? 0 : growth.Level;
        productionIntervalMs = growth == null ? (production != null ? production.Interval : units.Interval) : (production != null ? growth.CurrencyIntervalMs.Value : growth.UnitIntervalMs.Value);
        currencyCount = production == null ? 0 : growth == null ? production.Count : growth.CurrencyCount.Value;
        skillIds = new System.Collections.Generic.List<int>(growth == null ? new int[0] : (System.Collections.Generic.IEnumerable<int>)growth.SkillIds).AsReadOnly();
        if (productionIntervalMs <= 0 || (production != null && currencyCount <= 0)) {
            throw new InvalidOperationException("建筑实际生产参数非法");
        }
        config = building;
        this.cell = cell;
        this.owner = owner;
        currencyProduction = production;
        unitProduction = units;
        this.produceUnit = produceUnit;
    }

    public override void OnTimeUpdate(float deltaTime) {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }
        if (destroyed || deltaTime == 0) {
            return;
        }
        if (currencyProduction != null) {
            double elapsed = productionElapsedMs + deltaTime * 1000d;
            long cycles = checked((long)Math.Floor(elapsed / productionIntervalMs));
            productionElapsedMs = elapsed - cycles * (double)productionIntervalMs;
            if (cycles > 0) {
                owner.AddCurrency(checked(cycles * currencyCount));
            }
            return;
        }
        // 已等待的周期最多保留一份。恢复成功后从零开始，不把等待时间计入下一轮。
        if (productionElapsedMs >= productionIntervalMs) {
            TryResumeProduction();
            return;
        }
        double available = productionElapsedMs + deltaTime * 1000d;
        while (available >= productionIntervalMs) {
            productionElapsedMs = productionIntervalMs;
            if (!produceUnit(this)) {
                return;
            }
            available -= productionIntervalMs;
        }
        productionElapsedMs = available;
    }

    /**人口空出时允许立即消费已完成的一份周期；不会提前完成未满周期。*/
    internal bool TryResumeProduction() {
        if (destroyed || unitProduction == null || productionElapsedMs < productionIntervalMs || !produceUnit(this)) {
            return false;
        }
        productionElapsedMs = 0;
        return true;
    }

}
