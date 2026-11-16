using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>将真实匹配请求映射为本页显示输入；不维护权威匹配、进度或倒计时。</summary>
public sealed class PlayerMatchingBusinessSource : IPlayerMatchingPresentationSource, IDisposable {
    private readonly BattleMatchRequest request;
    private readonly PlayerMatchingDisplayAssetLease assets;
    private readonly Sprite portrait;
    private readonly string portraitAddress;
    private readonly Dictionary<BattleMatchPlayerVO, PlayerMatchingTeamDisplay> readyTeams = new Dictionary<BattleMatchPlayerVO, PlayerMatchingTeamDisplay>();
    private readonly HashSet<BattleMatchPlayerVO> loadingTeams = new HashSet<BattleMatchPlayerVO>();
    private readonly HashSet<BattleMatchPlayerVO> failedTeams = new HashSet<BattleMatchPlayerVO>();
    private bool disposed, active, suppressEnd, entrySignalled;
    private Exception displayError;

    public PlayerMatchingPresentation Current { get; private set; }
    public BattleMatchRequest Request => request;
    public event Action<PlayerMatchingPresentation> Changed;
    public event Action<PlayerMatchingBusinessSource> Ended, Entering, ClosedFromView;
    public event Action<PlayerMatchingBusinessSource, BattleMatchRequest> Retrying;
    public event Action<PlayerMatchingBusinessSource, Exception> DisplayFailed;

    public PlayerMatchingBusinessSource(BattleMatchRequest request, PlayerMatchingDisplayAssetLease assets, Sprite portrait, string portraitAddress) {
        this.request=request ?? throw new ArgumentNullException(nameof(request));
        this.assets=assets ?? throw new ArgumentNullException(nameof(assets));
        this.portrait=portrait;
        this.portraitAddress=portraitAddress;
        request.Changed+=OnChanged;
        try { Refresh(); }
        catch { Dispose(); throw; }
    }

    /**页面实际打开之后才允许消费移交或结束通知。*/
    public void Activate() { active=true; Refresh(); }
    public void SetApplicationPaused(bool paused) { if (!disposed) request.SetApplicationPaused(paused); }

    public void Dispatch(PlayerMatchingUiAction action, string requestId) {
        if (disposed || requestId!=request.Id) return;
        switch (action) {
            case PlayerMatchingUiAction.Cancel: request.CancelSearch(); break;
            case PlayerMatchingUiAction.Return: request.ReturnAfterFailure(); break;
            case PlayerMatchingUiAction.Retry:
                suppressEnd=true;
                try {
                    BattleMatchRequest next=request.Retry();
                    if (next!=null) Retrying?.Invoke(this,next);
                } finally { suppressEnd=false; }
                break;
            case PlayerMatchingUiAction.Closed:
                // Panel已先清空Sprite和监听；此时才允许业务归还本请求资源。
                suppressEnd=true;
                try { request.Close(); ClosedFromView?.Invoke(this); }
                finally { Dispose(); }
                break;
        }
    }

    private void OnChanged(BattleMatchStateVO state) {
        if (disposed || state.RequestId!=request.Id) return;
        if (state.Phase==BattleMatchPhase.Cancelled || state.Phase==BattleMatchPhase.Closed) {
            if (active && !suppressEnd) Ended?.Invoke(this);
            return;
        }
        var frame=Map(state);
        if (disposed) return;
        Current=frame;
        Changed?.Invoke(Current);
        if (active && state.Phase==BattleMatchPhase.Entering && loadingTeams.Count==0 && !entrySignalled) {
            entrySignalled=true;
            if (displayError!=null) DisplayFailed?.Invoke(this,displayError);
            else Entering?.Invoke(this);
        }
    }

    public void Refresh() { if (!disposed) OnChanged(request.Current); }

    private PlayerMatchingTeamDisplay Team(BattleMatchPlayerVO player) {
        if (player==null) return null;
        if (readyTeams.TryGetValue(player,out PlayerMatchingTeamDisplay team)) return team;
        if (!failedTeams.Contains(player) && loadingTeams.Add(player)) LoadTeam(player);
        if (readyTeams.TryGetValue(player,out team)) return team;
        var cards=new List<PlayerMatchingCardDisplay>();
        foreach (var card in player.EffectiveCards) cards.Add(new PlayerMatchingCardDisplay(null,assets.GetQualityFrame(card.Quality),null,card.Name,card.ConstructionCost,card.Level));
        return new PlayerMatchingTeamDisplay(player.Name,PlayerMatchingDisplayAssetLease.Identity(player),portrait,cards);
    }

    private async void LoadTeam(BattleMatchPlayerVO player) {
        try {
            // 即使图片已缓存，也先退出当前业务通知栈，避免同步完成时重入移交/关闭。
            await Task.Yield();
            if (disposed) return;
            var team=await assets.BuildTeamAsync(player,portraitAddress);
            if (!disposed) readyTeams[player]=team;
        } catch (OperationCanceledException) {
            if (!disposed) { displayError=new InvalidOperationException("匹配显示资源未就绪"); failedTeams.Add(player); }
        } catch (Exception exception) {
            if (!disposed) { displayError=exception; failedTeams.Add(player); }
        } finally {
            loadingTeams.Remove(player);
        }
        if (!disposed) Refresh();
    }

    private PlayerMatchingPresentation Map(BattleMatchStateVO state) {
        var phase=PlayerMatchingUiState.Preparing;
        switch (state.Phase) {
            case BattleMatchPhase.Searching: phase=PlayerMatchingUiState.Searching; break;
            case BattleMatchPhase.Preparing:
                if (state.Friendly.IgnoredCards.Count>0 || (state.Enemy!=null && state.Enemy.IgnoredCards.Count>0)) phase=PlayerMatchingUiState.ExcludedCards;
                else if (state.Friendly.EffectiveCards.Count==0 && state.Enemy!=null && state.Enemy.EffectiveCards.Count==0) phase=PlayerMatchingUiState.EmptyDeck;
                else if (state.Friendly.EffectiveCards.Count<4 || (state.Enemy!=null && state.Enemy.EffectiveCards.Count<4)) phase=PlayerMatchingUiState.PartialDeck;
                break;
            case BattleMatchPhase.Countdown: phase=PlayerMatchingUiState.Countdown; break;
            case BattleMatchPhase.Entering: phase=PlayerMatchingUiState.Entering; break;
            case BattleMatchPhase.Failed: phase=PlayerMatchingUiState.Failed; break;
            default: throw new InvalidOperationException("终态不再生成界面显示帧");
        }
        var progress=state.Progress;
        return new PlayerMatchingPresentation(state.RequestId,phase,Team(state.Friendly),Team(state.Enemy),
            progress?.CompletedItems ?? 0,progress?.TotalItems ?? 0,progress?.CurrentItemId,progress?.CurrentItemProgress ?? 0,
            state.CountdownSeconds,(float)state.SearchElapsedSeconds,state.Phase==BattleMatchPhase.Failed ? "战斗准备失败，请重试" : null,Ignored(state));
    }

    private static string Ignored(BattleMatchStateVO state) {
        var reasons=new HashSet<string>();
        foreach (var player in new[] {state.Friendly,state.Enemy}) {
            if (player==null) continue;
            foreach (string reason in player.IgnoredCards.Values) {
                if (reason.StartsWith("资格",StringComparison.Ordinal)) reasons.Add("未激活");
                else if (reason.StartsWith("配置",StringComparison.Ordinal)) reasons.Add("暂未开放战斗");
                else if (reason.StartsWith("资源",StringComparison.Ordinal)) reasons.Add("战斗资源未齐备");
                else reasons.Add("本局暂不可用");
            }
        }
        // 各方实际忽略张数已放在身份条；这里只显示可读分类，完整逐卡原因仍保留业务VO。
        return reasons.Count==0 ? string.Empty : "本局忽略："+string.Join("、",reasons);
    }

    public void Dispose() {
        if (disposed) return;
        disposed=true;
        request.Changed-=OnChanged;
        Current=null;
        Changed=null; Ended=null; Entering=null; ClosedFromView=null; Retrying=null; DisplayFailed=null;
        readyTeams.Clear();
    }
}
