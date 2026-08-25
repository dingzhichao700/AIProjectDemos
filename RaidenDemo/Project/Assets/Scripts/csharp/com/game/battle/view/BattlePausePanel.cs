using UnityEngine.UI;

/// <summary>
/// 战斗暂停界面
/// </summary>
/// <remarks>
/// 展示暂停状态并处理继续战斗或退出关卡操作。
/// </remarks>
public class BattlePausePanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public GameButton btnContinue;

    public GameButton btnRestart;

    public GameButton btnExit;
    /******************* UIComponent Define finish ************************/

    /**当前暂停的战斗界面*/
    private BattlePanel battlePanel;

    public BattlePausePanel() {
        layer = PanelLayer.SCALE_PANEL_SECOND;
    }

    public override void OnOpen() {
        battlePanel = ReadBattlePanel();
        transform.Find("imgDim").GetComponent<Image>().raycastTarget = true;
        AddLis();
    }

    public override void OnClose() {
        RemoveLis();
        battlePanel = null;
    }

    private BattlePanel ReadBattlePanel() {
        if (openParams != null && openParams.Length > 0 && openParams[0] is BattlePanel value) {
            return value;
        }
        return null;
    }

    private void AddLis() {
        OnClick(btnContinue.gameObject, OnContinue);
        OnClick(btnRestart.gameObject, OnRestart);
        OnClick(btnExit.gameObject, OnExit);
    }

    private void RemoveLis() {
        OffClick(btnContinue.gameObject, OnContinue);
        OffClick(btnRestart.gameObject, OnRestart);
        OffClick(btnExit.gameObject, OnExit);
    }

    private void OnContinue() {
        battlePanel?.ResumeBattle();
        Close();
    }

    private void OnRestart() {
        BattlePanel activeBattle = battlePanel;
        Close();
        activeBattle?.RestartBattle();
    }

    private void OnExit() {
        BattlePanel activeBattle = battlePanel;
        Close();
        activeBattle?.ExitBattle();
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            OnContinue();
        }
    }

}
