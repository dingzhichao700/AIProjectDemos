using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>匹配界面的PlayerMatchingStateView表现与绑定；不维护业务权威状态。</summary>
public sealed class PlayerMatchingStateView : BaseView {

    /******************* UIComponent Define begin ************************/
    public PlayerMatchingHeaderView headerView;
    public PlayerMatchingTeamView friendlyView;
    public PlayerMatchingSearchView searchView;
    public TextMeshProUGUI txtStatus;
    public Button btnCancel;
    public PlayerMatchingTeamView enemyView;
    public PlayerMatchingProgressView progressView;
    public TextMeshProUGUI txtPercent;
    public Button btnRetry;
    public Button btnReturn;
    public TextMeshProUGUI txtExcluded;
    /******************* UIComponent Define finish ************************/

    private UnityEngine.Events.UnityAction cancelAction, retryAction, returnAction;

    public void Render(PlayerMatchingPresentation frame) {
        headerView.Render(frame.State); friendlyView.Render(frame.Friendly);
        if (enemyView != null) enemyView.Render(frame.Enemy);
        if (txtStatus != null) {
            PlayerMatchingTextBounds.Fit(txtStatus);
            if (progressView != null) txtStatus.text="准备战斗资源（"+frame.CompletedItems+"/"+frame.TotalItems+"）";
            else if (frame.State==PlayerMatchingUiState.Countdown) txtStatus.text=frame.CountdownSeconds.ToString();
            else if (frame.State==PlayerMatchingUiState.Searching) txtStatus.text=TimeSpan.FromSeconds(frame.SearchElapsedSeconds).ToString(@"mm\:ss");
            else txtStatus.text=frame.Message ?? (frame.State==PlayerMatchingUiState.Failed ? "战斗资源加载失败，请重试" : "正在打开战斗界面");
        }
        if (progressView != null) progressView.Render(frame.CurrentItemProgress);
        if (txtPercent != null) txtPercent.text=Mathf.RoundToInt(frame.CurrentItemProgress*100)+"%";
        if (txtExcluded != null) { PlayerMatchingTextBounds.Fit(txtExcluded,true); txtExcluded.text=frame.ExcludedReason ?? string.Empty; }
    }

    /**只持有本窗口监听；旧请求闭包携带原身份，不能指向新请求。*/
    public void BindActions(string requestId, Action<PlayerMatchingUiAction,string> dispatch) {
        UnbindActions();
        if (btnCancel != null) { cancelAction=()=>dispatch(PlayerMatchingUiAction.Cancel,requestId); btnCancel.onClick.AddListener(cancelAction); }
        if (btnRetry != null) { retryAction=()=>dispatch(PlayerMatchingUiAction.Retry,requestId); btnRetry.onClick.AddListener(retryAction); }
        if (btnReturn != null) { returnAction=()=>dispatch(PlayerMatchingUiAction.Return,requestId); btnReturn.onClick.AddListener(returnAction); }
    }

    public void UnbindActions() {
        if (btnCancel != null && cancelAction != null) btnCancel.onClick.RemoveListener(cancelAction);
        if (btnRetry != null && retryAction != null) btnRetry.onClick.RemoveListener(retryAction);
        if (btnReturn != null && returnAction != null) btnReturn.onClick.RemoveListener(returnAction);
        cancelAction=retryAction=returnAction=null;
    }

    public override void Clear() {
        UnbindActions(); friendlyView.Clear(); if (enemyView != null) enemyView.Clear();
        if (searchView != null) searchView.ResetVisual(); if (progressView != null) progressView.ResetVisual();
        if (txtStatus != null) txtStatus.text=string.Empty; if (txtPercent != null) txtPercent.text=string.Empty;
        if (txtExcluded != null) txtExcluded.text=string.Empty;
    }

}
