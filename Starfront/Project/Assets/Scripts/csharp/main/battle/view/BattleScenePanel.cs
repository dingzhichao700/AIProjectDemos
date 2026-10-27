using UnityEngine;

/// <summary>战斗场景窗口，仅负责表现和输入转发；没有扣费或建造规则。</summary>
public sealed class BattleScenePanel : BasePanel {
    /******************* UIComponent Define begin ************************/
    /******************* UIComponent Define finish ************************/

    private BattleSceneView sceneView;
    public string lastInteractionHint { get; private set; }

    public BattleScenePanel() {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen() {
        sceneView = GetComponent<BattleSceneView>();
        sceneView.Bind(BattleControl.ins.model);
        lastInteractionHint = string.Empty;
        AddLis();
        PanelMgr.ins.OpenPanel(UIEnum.BATTLE_INFO_PANEL, new dynamic[] { BattleControl.ins.model });
        BattleControl.ins.model.StartTimeFlow();
    }

    private void AddLis() {
        sceneView.CellClicked += OnCellClicked;
        BattleControl.ins.Finished += OnBattleFinished;
        BattleControl.ins.UnlockRejected += OnUnlockRejected;
    }

    private void RemoveLis() {
        sceneView.CellClicked -= OnCellClicked;
        BattleControl.ins.Finished -= OnBattleFinished;
        BattleControl.ins.UnlockRejected -= OnUnlockRejected;
    }

    private void OnBattleFinished() {
        if (isOpened) {
            Close();
            PanelMgr.ins.OpenPanel(UIEnum.MAIN_PANEL);
        }
    }

    private void OnCellClicked(long id) {
        if (BattleControl.ins.TryUnlockCell(id, out _)) {
            lastInteractionHint = string.Empty;
        }
    }

    private void OnUnlockRejected(long id, string reason) {
        lastInteractionHint = reason;
        sceneView.ShowInteractionHint(id, reason);
        Debug.Log("格子交互：" + id + "，" + reason);
    }

    public override void OnClose() {
        PanelMgr.ins.GetPanel(UIEnum.BATTLE_INFO_PANEL)?.Close();
        RemoveLis();
        sceneView.Unbind();
        BattleControl.ins.End();
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            Close();
            PanelMgr.ins.OpenPanel(UIEnum.MAIN_PANEL);
        }
    }
}
