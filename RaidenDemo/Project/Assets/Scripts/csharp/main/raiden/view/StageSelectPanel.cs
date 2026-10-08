using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 雷电战机关卡选择界面
/// </summary>
public class StageSelectPanel : BasePanel {

    /**加载界面背景路径*/
    private const string LoadingBackgroundPath = "Assets/Art/unpack/default/raiden/loadingBackground.png";

    /**进入战斗前随机展示的提示*/
    private static readonly string[] LoadingTips = { "移动战机，战机会持续自动射击。", "受击闪烁期间处于无敌状态，可以趁机调整位置。", "击破敌机有机会获得关卡内升级道具。" };

    /******************* UIComponent Define begin ************************/
    public RectTransform routeContainer;

    public RectTransform stageContainer;

    public StageSelectItem itemStageTemplate;

    public StagePathItem itemPathTemplate;

    public GameButton btnBack;

    public GameButton btnDeploy;
    /******************* UIComponent Define finish ************************/


    private sealed class StageRuntime {

        /**关卡编号*/
        public int id;

        /**是否已经解锁*/
        public bool unlocked;

        /**关卡显示实例*/
        public StageSelectItem item;

    }

    /**当前动态生成的关卡实例*/
    private readonly List<StageRuntime> stages = new List<StageRuntime>();

    /**当前动态生成的路线实例*/
    private readonly List<StagePathItem> paths = new List<StagePathItem>();

    /**当前选中的关卡编号*/
    private int selectedStage = 1;

    /**是否正在等待关卡资源加载*/
    private bool isDeploying;

    public StageSelectPanel() {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen() {
        selectedStage = openParams != null && openParams.Length > 0 && openParams[0] is int value ? value : 1;
        isDeploying = false;
        BuildChapter();
        OnClick(btnBack.gameObject, OnBack);
        OnClick(btnDeploy.gameObject, OnDeploy);
    }

    public override void OnClose() {
        OffClick(btnBack.gameObject, OnBack);
        OffClick(btnDeploy.gameObject, OnDeploy);
        ClearRuntimeContent();
        isDeploying = false;
    }

    // 坐标暂为 Demo 章节布局；关卡状态始终从运行时进度读取，避免把预览实例当成真实数据。
    private void BuildChapter() {
        ClearRuntimeContent();
        for (int stageId = 1; stageId <= RaidenControl.ins.stageCount; stageId++) {
            StageConfigVO config = RaidenControl.ins.GetStageConfig(stageId);
            StageProgressVO progress = RaidenControl.ins.model.GetStageProgress(stageId);
            CreateStage(stageId, config.selectPosition, progress.unlocked, progress.highestStar);
        }
        CreatePath(new Vector2(30, 1053), Center(1), RaidenControl.ins.IsStageUnlocked(1), "StartTo1");
        for (int id = 2; id <= RaidenControl.ins.stageCount; id++)
            CreatePath(Center(id - 1), Center(id), RaidenControl.ins.IsStageUnlocked(id), $"{id - 1}To{id}");

        StageRuntime selected = FindStage(selectedStage);
        if (selected == null || !selected.unlocked) selectedStage = 1;
        RefreshSelection();
    }

    private void CreateStage(int id, Vector2 screen, bool unlocked, int starCount) {
        StageSelectItem item = Instantiate(itemStageTemplate, stageContainer, false);
        item.name = $"itemStage{id}";
        item.gameObject.SetActive(true);
        RectTransform rect = item.Trans;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(140, 140);
        rect.anchoredPosition = new Vector2(screen.x, -(screen.y - 112));

        StageRuntime runtime = new StageRuntime { id = id, unlocked = unlocked, item = item };
        item.SetData(id, unlocked, starCount, () => SelectStage(runtime));
        stages.Add(runtime);
    }

    private void CreatePath(Vector2 fromScreen, Vector2 toScreen, bool bright, string name) {
        StagePathItem item = Instantiate(itemPathTemplate, routeContainer, false);
        item.name = $"itemPath{name}";
        item.gameObject.SetActive(true);
        item.SetState(bright);

        Vector2 from = fromScreen - new Vector2(0, 112);
        Vector2 to = toScreen - new Vector2(0, 112);
        Vector2 delta = to - from;
        // 路线只保留一个可拉伸段：长度、中心点和角度均由两个关卡端点实时推导。
        RectTransform rect = item.Trans;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(Mathf.Round(delta.magnitude), 20);
        rect.anchoredPosition = new Vector2(Mathf.Round((from.x + to.x) / 2), -Mathf.Round((from.y + to.y) / 2));
        rect.localEulerAngles = new Vector3(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        paths.Add(item);
    }

    private Vector2 Center(int id) {
        StageConfigVO config = RaidenControl.ins.GetStageConfig(id);
        return config.selectPosition + new Vector2(70, 70);
    }

    private StageRuntime FindStage(int id) {
        return stages.Find(stage => stage.id == id);
    }

    private void SelectStage(StageRuntime stage) {
        if (!stage.unlocked) {
            CommonUtil.OpenWarning("该关卡尚未解锁。", null);
            return;
        }
        selectedStage = stage.id;
        RefreshSelection();
    }

    private void RefreshSelection() {
        foreach (StageRuntime stage in stages) stage.item.SetSelected(stage.id == selectedStage);
        StageRuntime selected = FindStage(selectedStage);
        if (selected == null) return;

    }

    private void ClearRuntimeContent() {
        foreach (StageRuntime stage in stages) {
            if (stage.item == null) continue;
            stage.item.Clear();
            Destroy(stage.item.gameObject);
        }
        stages.Clear();

        foreach (StagePathItem path in paths) if (path != null) Destroy(path.gameObject);
        paths.Clear();
    }

    private void OnBack() {
        Close();
        PanelMgr.ins.OpenPanel(UIEnum.HOME_PANEL);
    }

    private void OnDeploy() {
        StageRuntime stage = FindStage(selectedStage);
        if (stage == null || !stage.unlocked) {
            CommonUtil.OpenWarning("请先选择可用关卡。", null);
            return;
        }
        if (isDeploying) return;
        isDeploying = true;
        // 加载界面拥有资源加载过程，关卡选择页只提供资源清单及成功、失败回调。
        LoadingControl.ins.OpenLoading(BattlePanel.GetStagePreloadList(selectedStage),
            OnStageLoaded, LoadingBackgroundPath, LoadingTips, OnStageLoadFailed);
    }

    private void OnStageLoaded() {
        if (!isOpened) return;
        int stage = selectedStage;
        Close();
        PanelMgr.ins.OpenPanel(UIEnum.BATTLE_PANEL, new object[] { stage });
    }

    private void OnStageLoadFailed(Exception exception) {
        Debug.LogError($"关卡资源预加载失败：{exception}");
        isDeploying = false;
        if (isOpened) CommonUtil.OpenWarning("关卡资源加载失败，请重试。", null);
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) OnBack();
    }

}
