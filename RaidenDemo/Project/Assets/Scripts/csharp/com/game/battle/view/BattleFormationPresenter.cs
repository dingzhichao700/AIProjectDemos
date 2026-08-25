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

    public AircraftVO player { get; private set; }
    public AircraftVO leftWingman { get; private set; }
    public AircraftVO rightWingman { get; private set; }

    public BattleFormationPresenter(RectTransform entityLayer, BattleModel model,
        BattleVisualPool visualPool, BattleEntityViewManager entityViews) {
        this.entityLayer = entityLayer;
        this.model = model;
        this.visualPool = visualPool;
        this.entityViews = entityViews;
    }

    /**创建玩家飞机逻辑对象及对应 View。*/
    public AircraftVO CreatePlayer(PlayerAircraftBattleLevelVO config) {
        RectTransform root = visualPool.Create("playerEntity", entityLayer,
            config.displaySize, BattleConst.PlayerStart, config.appearancePath);
        player = model.CreatePlayerAircraft("playerEntity", true, BattleConst.PlayerStart);
        entityViews.BindUnit(player.id, root);
        return player;
    }

    /**同步玩家飞机等级配置对应的尺寸与外观。*/
    public void ApplyPlayerVisual(PlayerAircraftBattleLevelVO config) {
        if (player == null || config == null) {
            return;
        }
        RectTransform view = GetView(player);
        RectTransform visual = GetVisual(player);
        if (view != null) {
            view.sizeDelta = config.displaySize;
        }
        if (visual != null) {
            visual.sizeDelta = config.displaySize;
        }
        Image image = visual != null ? visual.GetComponent<Image>() : null;
        if (image != null) {
            UITools.SetImage(image, config.appearancePath, true);
        }
    }

    /**按逻辑奖励结果创建一架尚未存在的僚机 View。*/
    public AircraftVO ApplyWingmanReward() {
        AircraftVO wingman = model.ApplyWingmanReward(out bool created, out bool isLeft);
        if (!created) {
            return wingman;
        }
        RectTransform view = visualPool.Create(wingman.semanticName, entityLayer,
            BattleConst.WingmanSize, wingman.position, BattleConst.WingmanPath);
        entityViews.BindUnit(wingman.id, view);
        if (isLeft) {
            leftWingman = wingman;
        } else {
            rightWingman = wingman;
        }
        return wingman;
    }

    /**同步玩家和现有僚机的逻辑坐标。*/
    public void Sync() {
        SyncUnit(player);
        SyncUnit(leftWingman);
        SyncUnit(rightWingman);
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
        if (view != null) {
            view.anchoredPosition = unit.position;
        }
    }

    /**清除本局编队逻辑引用。*/
    public void Clear() {
        player = null;
        leftWingman = null;
        rightWingman = null;
    }
}
