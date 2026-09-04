using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家编队表现
/// </summary>
/// <remarks>
/// 创建并同步玩家飞机与僚机的场景表现。
/// </remarks>
internal sealed class BattleFormationPresenter {

    private readonly RectTransform entityLayer;
    private readonly BattleModel model;
    private readonly BattleVisualPool visualPool;
    private readonly BattleEntityViewManager entityViews;
    private readonly List<AircraftVO> wingmanUnits = new List<AircraftVO>();
    private WingmanConfigVO wingmanConfig;

    public AircraftVO player { get; private set; }
    public IReadOnlyList<AircraftVO> wingmen => wingmanUnits;

    public BattleFormationPresenter(RectTransform entityLayer, BattleModel model, BattleVisualPool visualPool, BattleEntityViewManager entityViews) {
        this.entityLayer = entityLayer;
        this.model = model;
        this.visualPool = visualPool;
        this.entityViews = entityViews;
    }

    /**创建玩家飞机逻辑对象及对应 View。*/
    public AircraftVO CreatePlayer(PlayerAircraftBattleLevelVO config) {
        RectTransform root = visualPool.Create("playerEntity", entityLayer, config.displaySize, BattleConst.PlayerStart, config.appearancePath);
        player = model.CreatePlayerAircraft("playerEntity", true, BattleConst.PlayerStart);
        entityViews.BindUnit(player.id, root);
        return player;
    }

    public void ConfigureWingman(WingmanConfigVO config) {
        wingmanConfig = config;
        model.ConfigureWingman(config);
    }

    /**同步玩家飞机等级配置对应的尺寸与外观。*/
    public void ApplyPlayerVisual(PlayerAircraftBattleLevelVO config) {
        if (player == null || config == null) return;
        RectTransform view = GetView(player);
        RectTransform visual = GetVisual(player);
        if (view != null) view.sizeDelta = config.displaySize;
        if (visual != null) visual.sizeDelta = config.displaySize;
        Image image = visual != null ? visual.GetComponent<Image>() : null;
        if (image == null) return;
        BattlePreloadCollector.RequireUnpackImagePreloaded(config.appearancePath);
        UITools.SetImage(image, config.appearancePath, true);
    }

    /**数量未满时创建下一槽位僚机 View。*/
    public AircraftVO ApplyWingmanReward() {
        AircraftVO wingman = model.ApplyWingmanReward(out bool created);
        if (!created || wingman == null || wingmanConfig == null) return wingman;
        RectTransform view = visualPool.Create(wingman.semanticName, entityLayer, wingmanConfig.displaySize, wingman.position, wingmanConfig.appearancePath);
        entityViews.BindUnit(wingman.id, view);
        wingmanUnits.Add(wingman);
        return wingman;
    }

    /**同步玩家和现有僚机的逻辑坐标。*/
    public void Sync() {
        SyncUnit(player);
        foreach (AircraftVO wingman in wingmanUnits) SyncUnit(wingman);
    }

    public RectTransform GetView(AircraftVO unit) {
        return unit != null ? entityViews.GetUnit(unit.id) : null;
    }

    public RectTransform GetVisual(AircraftVO unit) {
        RectTransform view = GetView(unit);
        return view != null ? view.Find("imgVisual") as RectTransform : null;
    }

    public void SyncUnit(AircraftVO unit) {
        RectTransform view = GetView(unit);
        if (view != null) view.anchoredPosition = unit.position;
    }

    /**玩家死亡时回收全部僚机表现。*/
    public void ClearWingmen() {
        foreach (AircraftVO wingman in wingmanUnits) visualPool.Recycle(entityViews.RemoveUnit(wingman.id));
        wingmanUnits.Clear();
    }

    /**清除本局编队逻辑引用。*/
    public void Clear() {
        player = null;
        wingmanConfig = null;
        wingmanUnits.Clear();
    }
}
