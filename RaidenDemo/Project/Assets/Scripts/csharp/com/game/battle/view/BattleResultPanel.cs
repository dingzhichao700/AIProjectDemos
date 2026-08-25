using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结算界面
/// </summary>
/// <remarks>
/// 展示关卡结算结果并处理结算后的界面操作。
/// </remarks>
public class BattleResultPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public Image imgEmblem;

    public TextMeshProUGUI txtTitle;

    public RectTransform grpVictoryDetails;

    public Image imgStars;

    public TextMeshProUGUI txtScore;

    public TextMeshProUGUI txtReward;

    public GameButton btnRetry;

    public GameButton btnReturn;
    /******************* UIComponent Define finish ************************/

    /**胜利标记资源路径*/
    private const string VictoryEmblemPath = "Assets/Art/atlas/default/raiden/battleResultVictoryEmblem";

    /**失败标记资源路径*/
    private const string DefeatEmblemPath = "Assets/Art/atlas/default/raiden/battleResultDefeatEmblem";

    /**发起结算的战斗界面*/
    private BattlePanel battlePanel;

    /**是否胜利*/
    private bool victory;

    /**本局得分*/
    private int score;

    /**本局奖励*/
    private int reward;

    public BattleResultPanel() {
        layer = PanelLayer.SCALE_PANEL_SECOND;
    }

    public override void OnOpen() {
        ReadResult();
        transform.Find("imgDim").GetComponent<Image>().raycastTarget = true;
        RefreshState();
        AddLis();
    }

    public override void OnClose() {
        RemoveLis();
        battlePanel = null;
    }

    private void ReadResult() {
        battlePanel = openParams != null && openParams.Length > 0
            ? openParams[0] as BattlePanel
            : null;
        victory = openParams != null && openParams.Length > 1 && openParams[1] is bool result && result;
        score = openParams != null && openParams.Length > 2 && openParams[2] is int scoreValue
            ? Mathf.Max(0, scoreValue)
            : 0;
        reward = openParams != null && openParams.Length > 3 && openParams[3] is int rewardValue
            ? Mathf.Max(0, rewardValue)
            : 0;
    }

    private void RefreshState() {
        UITools.SetImage(imgEmblem, victory ? VictoryEmblemPath : DefeatEmblemPath);
        txtTitle.text = victory ? "任务完成" : "战斗失败";
        grpVictoryDetails.gameObject.SetActive(victory);
        if (!victory) {
            return;
        }
        txtScore.text = score.ToString("000000");
        txtReward.text = $"× {reward}";
    }

    private void AddLis() {
        OnClick(btnRetry.gameObject, OnRetry);
        OnClick(btnReturn.gameObject, OnReturn);
    }

    private void RemoveLis() {
        OffClick(btnRetry.gameObject, OnRetry);
        OffClick(btnReturn.gameObject, OnReturn);
    }

    private void OnRetry() {
        BattlePanel activeBattle = battlePanel;
        Close();
        activeBattle?.RestartFromResult();
    }

    private void OnReturn() {
        BattlePanel activeBattle = battlePanel;
        Close();
        activeBattle?.ReturnFromResult();
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (operateCode == PanelOperateEnum.ESC) {
            OnReturn();
        }
    }

}
