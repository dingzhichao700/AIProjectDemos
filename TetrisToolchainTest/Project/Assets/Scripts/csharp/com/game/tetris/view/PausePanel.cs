using UnityEngine;

/// <summary>
/// 暂停界面
/// </summary>
public class PausePanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public GameButton btnResume;
    public GameButton btnRestart;
    public GameButton btnQuitToTitle;
    public GameButton btnAchievements;
    /******************* UIComponent Define finish ************************/

    public PausePanel() { layer = PanelLayer.SCALE_PANEL_FIRST + 1; }

    /// <summary>打开暂停界面并注册按钮事件。</summary>
    public override void OnOpen() {
        NormalizeLayout();
        ResolveButtonBindings();
        if (btnResume != null) OnClick(btnResume.gameObject, OnResumeClicked);
        if (btnRestart != null) OnClick(btnRestart.gameObject, OnRestartClicked);
        if (btnQuitToTitle != null) OnClick(btnQuitToTitle.gameObject, OnQuitToTitleClicked);
        if (btnAchievements != null) OnClick(btnAchievements.gameObject, OnAchievementsClicked);
    }

    /// <summary>关闭暂停界面并移除按钮事件，避免重复注册。</summary>
    public override void OnClose() {
        if (btnResume != null) OffClick(btnResume.gameObject, OnResumeClicked);
        if (btnRestart != null) OffClick(btnRestart.gameObject, OnRestartClicked);
        if (btnQuitToTitle != null) OffClick(btnQuitToTitle.gameObject, OnQuitToTitleClicked);
        if (btnAchievements != null) OffClick(btnAchievements.gameObject, OnAchievementsClicked);
    }

    /// <summary>将根节点适配到当前 Canvas，保持横屏全屏布局。</summary>
    private void NormalizeLayout() {
        RectTransform root = transform as RectTransform;
        if (root == null || root.parent == null) return;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one; root.localRotation = Quaternion.identity;
    }

    /// <summary>恢复预制体未写入时的按钮引用。</summary>
    private void ResolveButtonBindings() {
        if (btnResume == null) btnResume = FindButton("btnResume");
        if (btnRestart == null) btnRestart = FindButton("btnRestart");
        if (btnQuitToTitle == null) btnQuitToTitle = FindButton("btnQuitToTitle");
        if (btnAchievements == null) btnAchievements = FindButton("btnAchievements");
    }

    private GameButton FindButton(string objectName) {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) if (all[i].name == objectName) return all[i].GetComponent<GameButton>();
        return null;
    }

    /// <summary>关闭暂停界面并恢复游戏计时。</summary>
    private void OnResumeClicked() {
        Close();
        TetrisGamePanel game = PanelMgr.ins.GetPanel(UIEnum.TETRIS_PANEL) as TetrisGamePanel;
        if (game != null) game.ResumeFromPause();
    }

    /// <summary>复用当前游戏面板并重新开始对局。</summary>
    private void OnRestartClicked() {
        TetrisGamePanel game = PanelMgr.ins.GetPanel(UIEnum.TETRIS_PANEL) as TetrisGamePanel;
        Close();
        if (game != null) game.RestartFromPause();
    }

    /// <summary>结束当前对局并返回标题界面。</summary>
    private void OnQuitToTitleClicked() {
        Close(); PanelMgr.ins.ClosePanelByType(UIEnum.TETRIS_PANEL); PanelMgr.ins.OpenPanel(UIEnum.TITLE_PANEL);
    }

    /// <summary>成就界面的预留入口。</summary>
    private void OnAchievementsClicked() {
        Debug.Log("[Tetris] Achievements panel is not implemented yet.");
    }

}
