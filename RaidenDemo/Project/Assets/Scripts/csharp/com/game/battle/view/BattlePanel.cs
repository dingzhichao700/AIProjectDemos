using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗主界面
/// </summary>
/// <remarks>
/// 组装战斗表现组件，并将战斗 Model 的权威数据同步到游戏视窗。
/// </remarks>
public class BattlePanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public RectTransform backgroundLayer;
    public RectTransform entityLayer;
    public RectTransform projectileLayer;
    public RectTransform effectLayer;
    public GameButton btnPause;
    public Image imgMissionProgressFill;
    public TextMeshProUGUI txtPlayerLife;
    public GameButton btnSkill;
    public GameButton btnUpgrade;
    public TextMeshProUGUI txtScore;
    public RectTransform barBossHealth;
    public Image imgBossHealthFill;
    public TextMeshProUGUI txtBossHealth;
    /******************* UIComponent Define finish ************************/

    /**本局战斗的权威数据模型*/
    private readonly BattleModel battleModel = new BattleModel();

    /**战斗图片表现对象池*/
    private readonly BattleVisualPool visualPool = new BattleVisualPool();

    /**三类战斗 Timer 的暂停与倍率恢复控制器*/
    private readonly BattleTimerPauseController timerPauseController =
        new BattleTimerPauseController();

    /**战斗配置统一查询入口*/
    private readonly BattleConfigProvider configProvider = new BattleConfigProvider();

    /**本局出战飞机及临时等级配置协调器*/
    private BattlePlayerConfigCoordinator playerConfigCoordinator;

    /**战斗表现与流程对象的统一装配入口*/
    private BattleCompositionRoot composition;

    /**战斗逻辑实体与表现节点映射*/
    private readonly BattleEntityViewManager entityViews = new BattleEntityViewManager();

    private BattleFormationPresenter formationPresenter => composition?.formationPresenter;
    private BattleEventPresenter eventPresenter => composition?.eventPresenter;
    private BattleNavigationCoordinator navigationCoordinator => composition?.navigationCoordinator;
    private BattleLifecycleCoordinator lifecycleCoordinator => composition?.lifecycleCoordinator;
    private BattleSetupCoordinator setupCoordinator => composition?.setupCoordinator;

    /**当前关卡 ID*/
    private int stageId;

    private AircraftVO playerUnit => formationPresenter?.player;

    /**战斗是否处于正常运行状态*/
    public bool isBattlePlaying => battleModel.isPlaying;

    /**返回当前关卡进入战斗前需要预加载的全部动态资源*/
    public static List<ResLoadInfo> GetStagePreloadList(int stageId) {
        return BattlePreloadCollector.GetStagePreloadList(stageId);
    }

    /**不打开加载界面，仅执行当前关卡资源预加载*/
    public static Task PreloadStageAssetsAsync(int stageId) {
        return BattlePreloadCollector.PreloadStageAssetsAsync(stageId);
    }

    public BattlePanel() {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    /**初始化单局战斗并启动时间流*/
    public override void OnOpen() {
        playerConfigCoordinator ??= new BattlePlayerConfigCoordinator(configProvider);
        composition ??= new BattleCompositionRoot(battleModel, configProvider, playerConfigCoordinator, timerPauseController, visualPool, entityViews, backgroundLayer, entityLayer, projectileLayer, effectLayer, imgMissionProgressFill, txtPlayerLife, txtScore, barBossHealth, imgBossHealthFill, txtBossHealth, SetPlayerPosition, ApplyPlayerAircraftLevel, CompleteBattle);
        lifecycleCoordinator.ResetForOpen(eventPresenter.Unbind, RemoveLis, timerPauseController.Resume, ClearRuntimeLayers, ResetPanelReferences);
        stageId = ReadStageId();
        btnSkill.gameObject.SetActive(false);
        btnUpgrade.gameObject.SetActive(false);
        setupCoordinator.Initialize(stageId);
        eventPresenter.RefreshHud();
        lifecycleCoordinator.Start(eventPresenter.Bind, AddLis);
        battleModel.StartBattle();
    }

    /**停止时间流并清理本局表现对象*/
    public override void OnClose() {
        lifecycleCoordinator.Shutdown(eventPresenter.Unbind, RemoveLis, timerPauseController.Resume, ClearRuntimeLayers, ResetPanelReferences);
    }

    private int ReadStageId() {
        if (openParams != null && openParams.Length > 0 && openParams[0] is int value) {
            return value;
        }
        return 1;
    }

    private void AddLis() {
        OnClick(btnPause.gameObject, OnPause);
        KeyBoardControl.ins.OnKeyDown(KeyCode.Minus, SlowDownSceneTimers);
        KeyBoardControl.ins.OnKeyDown(KeyCode.Equals, SpeedUpSceneTimers);
    }

    private void RemoveLis() {
        OffClick(btnPause.gameObject, OnPause);
        KeyBoardControl.ins.OffKeyDown(KeyCode.Minus, SlowDownSceneTimers);
        KeyBoardControl.ins.OffKeyDown(KeyCode.Equals, SpeedUpSceneTimers);
    }

    /**降低场景、玩家和敌方 Timer 倍率。*/
    private void SlowDownSceneTimers() {
        timerPauseController.AdjustScale(-BattleConst.SceneTimerScaleStep);
    }

    /**提高场景、玩家和敌方 Timer 倍率。*/
    private void SpeedUpSceneTimers() {
        timerPauseController.AdjustScale(BattleConst.SceneTimerScaleStep);
    }


    /**切换当前临时等级，并集中刷新所有已配置的战斗属性*/
    private void ApplyPlayerAircraftLevel(int level) {
        if (playerUnit == null || !playerConfigCoordinator.TrySetLevel(level)) {
            return;
        }
        PlayerAircraftBattleLevelVO levelConfig = playerConfigCoordinator.current;
        playerConfigCoordinator.ApplyBattleStats(battleModel, playerUnit);
        formationPresenter.ApplyPlayerVisual(levelConfig);
        eventPresenter.RefreshPlayer();
    }

    /**同步玩家输入产生的逻辑坐标与表现坐标。*/
    private void SetPlayerPosition(Vector2 position) {
        battleModel.SetPlayerPosition(position);
        formationPresenter.SyncUnit(playerUnit);
    }


    private void OnPause() {
        navigationCoordinator.Pause(this);
    }

    /**从暂停状态恢复当前战斗*/
    public void ResumeBattle() {
        navigationCoordinator.Resume();
    }

    /**清空本局运行对象，并以当前关卡编号重新开始*/
    public void RestartBattle() {
        navigationCoordinator.Restart(this, stageId, BattleFlowState.Paused);
    }

    /**结束当前战斗并返回关卡选择界面*/
    public void ExitBattle() {
        navigationCoordinator.Exit(this, stageId, BattleFlowState.Paused);
    }

    /**冻结战斗并打开胜利或失败结算，胜利时同步本次运行进度*/
    public void CompleteBattle(bool victory) {
        navigationCoordinator.Complete(this, stageId, victory);
    }

    /**从结算界面重新进入当前关卡*/
    public void RestartFromResult() {
        navigationCoordinator.Restart(this, stageId, BattleFlowState.Settling);
    }

    /**从结算界面返回关卡选择，并保留刚结算的关卡作为默认选择*/
    public void ReturnFromResult() {
        navigationCoordinator.Exit(this, stageId, BattleFlowState.Settling);
    }

    private void ClearRuntimeLayers() {
        ClearLayer(backgroundLayer);
        ClearLayer(entityLayer);
        ClearLayer(projectileLayer);
        ClearLayer(effectLayer);
    }

    private void ClearLayer(RectTransform layerRoot) {
        for (int i = layerRoot.childCount - 1; i >= 0; i--) {
            Destroy(layerRoot.GetChild(i).gameObject);
        }
    }

    /**仅重置 Panel 持有的本局引用；运行对象由生命周期协调器统一清理。*/
    private void ResetPanelReferences() {
        playerConfigCoordinator.Clear();
        formationPresenter?.Clear();
        if (barBossHealth != null) {
            barBossHealth.gameObject.SetActive(false);
        }
        eventPresenter?.Clear();
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            OnPause();
        }
#if UNITY_EDITOR
        else if (operateCode == PanelOperateEnum.SURE) {
            CompleteBattle(true);
        } else if (operateCode == PanelOperateEnum.DELETE) {
            CompleteBattle(false);
        }
#endif
    }

}
