using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>匹配界面的PlayerMatchingPanel表现与绑定；不维护业务权威状态。</summary>
public sealed class PlayerMatchingPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public PlayerMatchingStateView state_search;
    public PlayerMatchingStateView state_preparing;
    public PlayerMatchingStateView state_countdown;
    public PlayerMatchingStateView state_failure;
    public PlayerMatchingStateView state_empty;
    public PlayerMatchingStateView state_excluded;
    public PlayerMatchingStateView state_entering;
    public PlayerMatchingStateView state_partial;
    /******************* UIComponent Define finish ************************/

    public override bool UsesFullScreenViewport => true;
    public override string[] FullBleedChildren => new[] {"boxBackground","imgBackgroundTint","txtTip"};
    private IPlayerMatchingPresentationSource source;
    private Action<PlayerMatchingPresentation> sourceChanged;
    private int sourceVersion;
    private bool applicationPaused, applicationUnfocused;
    private string requestId;
    private Action<PlayerMatchingUiAction,string> command;
    private PlayerMatchingPresentation current;
    private PlayerMatchingStateView activeView;
    private PlayerMatchingStateView[] views;

    public PlayerMatchingPanel() { layer=PanelLayer.SCALE_PANEL_FIRST; }

    public override void OnInit() {
        views=new[] { state_search,state_preparing,state_countdown,state_failure,state_empty,state_excluded,state_entering,state_partial };
        foreach (BaseView view in GetComponentsInChildren<BaseView>(true)) if (view != this) view.InitializeAfterBinding();
    }

    protected override void OnPrepareOpen() {
        trans.anchorMin=Vector2.zero; trans.anchorMax=Vector2.one;
        trans.offsetMin=Vector2.zero; trans.offsetMax=Vector2.zero;
        var tip=transform.Find("txtTip");
        if (tip!=null) PlayerMatchingTextBounds.Fit(tip.GetComponent<TextMeshProUGUI>());
    }

    public override void OnOpen() {
        if (openParams != null && openParams.Length>0) {
            if (openParams[0] is IPlayerMatchingPresentationSource input) { BindSource(input); return; }
            if (openParams[0] is PlayerMatchingPresentation frame) {
                BindPresentation(frame,openParams.Length>1 ? openParams[1] as Action<PlayerMatchingUiAction,string> : null);
                return;
            }
        }
        ReleasePresentation(true);
        throw new ArgumentException("打开匹配界面必须提供当前请求的显示输入");
    }

    /**绑定只读显示输入和业务意图回调；不启动匹配、资源下载或战斗。*/
    public void BindPresentation(PlayerMatchingPresentation frame, Action<PlayerMatchingUiAction,string> dispatch) {
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        if (!isInit) InitializeAfterBinding();
        ReleasePresentation(true); requestId=frame.RequestId; command=dispatch; Present(frame);
    }

    /**订阅真实业务的显示适配器；切换/关闭先退订，延迟通知不能写入后继请求。*/
    public void BindSource(IPlayerMatchingPresentationSource input) {
        if (input == null || input.Current == null) throw new ArgumentException("缺少当前请求显示输入");
        var initial=input.Current;
        BindPresentation(initial,input.Dispatch);
        source=input;
        int version=++sourceVersion;
        sourceChanged=frame=> {
            if (version!=sourceVersion || !ReferenceEquals(source,input)) return;
            Present(frame);
        };
        AddLis();
        // 先订阅再读一次最新值，覆盖绑定期间业务同步刷新的显示数据。
        Present(input.Current);
        SyncApplicationPause();
    }

    private void AddLis() { source.Changed+=sourceChanged; }

    private void RemoveLis() {
        sourceVersion++;
        var previousSource=source; var previousChanged=sourceChanged;
        source=null; sourceChanged=null;
        if (previousSource != null && previousChanged != null) previousSource.Changed-=previousChanged;
    }

    public bool Present(PlayerMatchingPresentation frame) {
        if (frame == null || string.IsNullOrEmpty(requestId) || frame.RequestId!=requestId) return false;
        int index=(int)frame.State;
        if (index<0 || index>=views.Length) throw new ArgumentOutOfRangeException(nameof(frame.State));
        var selected=views[index];
        if (activeView != selected) {
            if (activeView != null) activeView.UnbindActions();
            foreach (var view in views) view.gameObject.SetActive(view==selected);
            activeView=selected; activeView.BindActions(requestId,DispatchIntent);
        }
        current=frame; activeView.Render(frame); return true;
    }

    private void DispatchIntent(PlayerMatchingUiAction action, string sourceRequest) {
        if (current == null || sourceRequest!=requestId) return;
        if (action==PlayerMatchingUiAction.Cancel && current.State!=PlayerMatchingUiState.Searching) return;
        if ((action==PlayerMatchingUiAction.Retry || action==PlayerMatchingUiAction.Return) && current.State!=PlayerMatchingUiState.Failed) return;
        command?.Invoke(action,sourceRequest);
    }

    public void TickPresentation(float deltaTime) {
        if (current != null && current.State==PlayerMatchingUiState.Searching && activeView.searchView != null) activeView.searchView.TickVisual(Mathf.Max(0,deltaTime));
    }

    public override void OnPanelOperate(PanelOperateEnum operation) {
        if (operation!=PanelOperateEnum.ESC || current==null) return;
        if (current.State==PlayerMatchingUiState.Searching) DispatchIntent(PlayerMatchingUiAction.Cancel,requestId);
        else if (current.State==PlayerMatchingUiState.Failed) DispatchIntent(PlayerMatchingUiAction.Return,requestId);
    }

    private void OnApplicationPause(bool paused) { applicationPaused=paused; SyncApplicationPause(); }
    private void OnApplicationFocus(bool focused) { applicationUnfocused=!focused; SyncApplicationPause(); }
    private void SyncApplicationPause() {
        if (source is PlayerMatchingBusinessSource business) business.SetApplicationPaused(applicationPaused || applicationUnfocused);
    }

    public override void Clear() { ReleasePresentation(true); }
    private void Update() { TickPresentation(Time.unscaledDeltaTime); }
    public override void OnClose() { ReleasePresentation(true); }
    private void OnDestroy() { ReleasePresentation(true); }

    /**先清除显示引用与监听，再通知业务释放其持有的资源；不直接释放共用Sprite。*/
    private void ReleasePresentation(bool notify) {
        RemoveLis();
        string previous=requestId; var handler=command; requestId=null; command=null; current=null; activeView=null;
        if (views != null) foreach (var view in views) if (view != null) { view.Clear(); view.gameObject.SetActive(false); }
        if (notify && !string.IsNullOrEmpty(previous)) handler?.Invoke(PlayerMatchingUiAction.Closed,previous);
    }

}
