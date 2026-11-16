using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>匹配界面的只读显示阶段；由业务适配器提供，不在界面推导匹配结果。</summary>
public enum PlayerMatchingUiState { Searching, Preparing, Countdown, Failed, EmptyDeck, ExcludedCards, Entering, PartialDeck }

/// <summary>界面发出的意图；是否取消、重试或进入由业务负责人裁决。</summary>
public enum PlayerMatchingUiAction { Cancel, Retry, Return, Closed }

/// <summary>只读卡牌显示数据，不包含准入、编组或成长修改行为。</summary>
public sealed class PlayerMatchingCardDisplay {
    public readonly Sprite Artwork, Quality, Profession;
    public readonly string Name;
    public readonly int Cost, Level;

    public PlayerMatchingCardDisplay(Sprite artwork, Sprite quality, Sprite profession, string name, int cost, int level) {
        Artwork=artwork; Quality=quality; Profession=profession; Name=name ?? string.Empty; Cost=cost; Level=level;
    }
}

/// <summary>一方的只读显示快照；复制列表，避免界面持有可变编组。</summary>
public sealed class PlayerMatchingTeamDisplay {
    public readonly string Name, Identity;
    public readonly Sprite Portrait;
    public readonly IReadOnlyList<PlayerMatchingCardDisplay> Cards;

    public PlayerMatchingTeamDisplay(string name, string identity, Sprite portrait, IList<PlayerMatchingCardDisplay> cards) {
        if (cards == null || cards.Count > 4) throw new ArgumentException("本界面只接受0至4个实际卡牌显示项");
        Name=name ?? string.Empty; Identity=identity ?? string.Empty; Portrait=portrait;
        var copy=new PlayerMatchingCardDisplay[cards.Count]; cards.CopyTo(copy,0); Cards=Array.AsReadOnly(copy);
    }
}

/// <summary>表现层输入，不拥有请求状态、资源加载、倒计时或战斗规则。</summary>
public sealed class PlayerMatchingPresentation {
    public readonly string RequestId, CurrentItemId, Message, ExcludedReason;
    public readonly PlayerMatchingUiState State;
    public readonly PlayerMatchingTeamDisplay Friendly, Enemy;
    public readonly int CompletedItems, TotalItems, CountdownSeconds;
    public readonly float CurrentItemProgress, SearchElapsedSeconds;

    public PlayerMatchingPresentation(string requestId, PlayerMatchingUiState state, PlayerMatchingTeamDisplay friendly, PlayerMatchingTeamDisplay enemy, int completedItems, int totalItems, string currentItemId, float currentItemProgress, int countdownSeconds, float searchElapsedSeconds, string message = null, string excludedReason = null) {
        if (string.IsNullOrEmpty(requestId) || friendly == null) throw new ArgumentException("缺少请求身份或我方显示快照");
        if (completedItems < 0 || totalItems < completedItems || float.IsNaN(currentItemProgress) || float.IsInfinity(currentItemProgress)) throw new ArgumentException("显示进度数据无效");
        if (state != PlayerMatchingUiState.Searching && state != PlayerMatchingUiState.Failed && enemy == null) throw new ArgumentException("匹配后必须提供对手显示快照");
        RequestId=requestId; State=state; Friendly=friendly; Enemy=enemy; CompletedItems=completedItems; TotalItems=totalItems;
        CurrentItemId=currentItemId ?? string.Empty; CurrentItemProgress=Mathf.Clamp01(currentItemProgress);
        CountdownSeconds=countdownSeconds; SearchElapsedSeconds=Mathf.Max(0,searchElapsedSeconds); Message=message; ExcludedReason=excludedReason;
    }
}
