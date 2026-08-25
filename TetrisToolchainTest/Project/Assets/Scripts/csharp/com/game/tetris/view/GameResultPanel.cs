using TMPro;

using UnityEngine;

public class GameResultPanel : BasePanel
{
    /******************* UIComponent Define begin ************************/
    public GameButton btnRetry;
    public GameButton btnQuitToTitle;
    public TextMeshProUGUI txtScore;
    public TextMeshProUGUI txtLines;
    public TextMeshProUGUI txtLevel;
    /******************* UIComponent Define finish ************************/

    private int resultScore;
    private int resultLines;
    private int resultLevel;

    public GameResultPanel() { layer = PanelLayer.SCALE_PANEL_FIRST + 1; }

    public override void OnOpen() {
        NormalizeLayout();
        ReadResultParams();
        RefreshLabels();
        if (btnRetry != null) OnClick(btnRetry.gameObject, OnRetryClicked);
        if (btnQuitToTitle != null) OnClick(btnQuitToTitle.gameObject, OnQuitToTitleClicked);
    }

    private void NormalizeLayout() {
        RectTransform root = transform as RectTransform;
        if (root == null || root.parent == null) return;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        NormalizeButtonPivot(btnRetry);
        NormalizeButtonPivot(btnQuitToTitle);
    }

    private static void NormalizeButtonPivot(GameButton button) {
        if (button == null) return;
        RectTransform rect = button.transform as RectTransform;
        if (rect == null || rect.pivot == new Vector2(0.5f, 0.5f)) return;
        Vector2 oldPivot = rect.pivot;
        Vector2 size = rect.rect.size;
        rect.anchoredPosition += new Vector2((0.5f - oldPivot.x) * size.x, (oldPivot.y - 0.5f) * size.y);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    public override void OnClose() {
        if (btnRetry != null) OffClick(btnRetry.gameObject, OnRetryClicked);
        if (btnQuitToTitle != null) OffClick(btnQuitToTitle.gameObject, OnQuitToTitleClicked);
    }

    private void ReadResultParams() {
        if (openParams == null || openParams.Length < 3) return;
        resultScore = ToInt(openParams[0]);
        resultLines = ToInt(openParams[1]);
        resultLevel = ToInt(openParams[2]);
    }

    private static int ToInt(object value) {
        return value == null ? 0 : System.Convert.ToInt32(value);
    }

    private void RefreshLabels() {
        if (txtScore != null) txtScore.text = resultScore.ToString();
        if (txtLines != null) txtLines.text = resultLines.ToString();
        if (txtLevel != null) txtLevel.text = resultLevel.ToString();
    }

    private void OnRetryClicked() {
        TetrisGamePanel game = PanelMgr.ins.GetPanel(UIEnum.TETRIS_PANEL) as TetrisGamePanel;
        PanelMgr.ins.ClosePanelByType(UIEnum.GAME_RESULT_PANEL);
        if (game != null) game.RestartFromPause();
        else PanelMgr.ins.OpenPanel(UIEnum.TETRIS_PANEL);
    }

    private void OnQuitToTitleClicked() {
        PanelMgr.ins.ClosePanelByType(UIEnum.GAME_RESULT_PANEL);
        PanelMgr.ins.ClosePanelByType(UIEnum.TETRIS_PANEL);
        PanelMgr.ins.OpenPanel(UIEnum.TITLE_PANEL);
    }
}
