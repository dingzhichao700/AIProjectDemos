using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cfg;

public enum BattleMatchPhase { Searching, Preparing, Countdown, Entering, Failed, Cancelled, Closed }
public enum BattleMatchAvatarKind { DefaultPlayer, DefaultComputer }

/// <summary>本请求锁定的卡牌展示事实，不持有图片或界面对象。</summary>
public sealed class BattleMatchCardVO
{
    public int CardId { get; }
    public int ArtId { get; }
    public string Name { get; }
    public int Level { get; }
    public int ConstructionCost { get; }
    public int Quality { get; }
    public int TagId { get; }

    public BattleMatchCardVO(int cardId, int artId, string name, int level, int cost, int quality, int tagId)
    {
        CardId = cardId;
        ArtId = artId;
        Name = name ?? string.Empty;
        Level = level;
        ConstructionCost = cost;
        Quality = quality;
        TagId = tagId;
    }
}

public sealed class BattleMatchPlayerVO
{
    public string Name { get; }
    public bool IsComputer { get; }
    public BattleMatchAvatarKind AvatarKind { get; }
    public BattlePlayerDeckVO SavedDeck { get; }
    public IReadOnlyList<BattleMatchCardVO> SavedCards { get; }
    public IReadOnlyList<BattleMatchCardVO> EffectiveCards { get; }
    public IReadOnlyDictionary<int, string> IgnoredCards { get; }

    public BattleMatchPlayerVO(string name, bool computer, IEnumerable<BattleMatchCardVO> saved,
        IEnumerable<BattleMatchCardVO> effective, IDictionary<int, string> ignored)
    {
        Name = name ?? string.Empty;
        IsComputer = computer;
        AvatarKind = computer ? BattleMatchAvatarKind.DefaultComputer : BattleMatchAvatarKind.DefaultPlayer;
        SavedCards = Array.AsReadOnly(saved.ToArray());
        EffectiveCards = Array.AsReadOnly(effective.ToArray());
        SavedDeck = new BattlePlayerDeckVO(SavedCards.Select(c => new KeyValuePair<int, int>(c.CardId, c.Level)));
        IgnoredCards = new ReadOnlyDictionary<int, string>(new Dictionary<int, string>(ignored));
    }
}

public sealed class BattleMatchPairVO
{
    public int SceneId { get; }
    public BattleMatchPlayerVO Friendly { get; }
    public BattleMatchPlayerVO Enemy { get; }
    public IReadOnlyDictionary<BattleSceneCamp, BattlePlayerDeckVO> Decks { get; }

    public BattleMatchPairVO(int sceneId, BattleMatchPlayerVO friendly, BattleMatchPlayerVO enemy)
    {
        SceneId = sceneId;
        Friendly = friendly ?? throw new ArgumentNullException(nameof(friendly));
        Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        Decks = new ReadOnlyDictionary<BattleSceneCamp, BattlePlayerDeckVO>(
            new Dictionary<BattleSceneCamp, BattlePlayerDeckVO> {
                { BattleSceneCamp.FRIENDLY, new BattlePlayerDeckVO(friendly.SavedDeck.cardLevels) },
                { BattleSceneCamp.ENEMY, new BattlePlayerDeckVO(enemy.SavedDeck.cardLevels) }
            });
    }
}

/// <summary>项数仅供文字，当前项进度仅供当前项条；没有估算时间进度。</summary>
public sealed class BattlePreparationProgressVO
{
    public int CompletedItems { get; }
    public int TotalItems { get; }
    public string CurrentItemId { get; }
    public float CurrentItemProgress { get; }
    public bool CurrentItemProgressIsBinary { get; }

    public BattlePreparationProgressVO(int completed, int total, string itemId, float progress, bool binary)
    {
        if (completed < 0 || total < completed || float.IsNaN(progress) || float.IsInfinity(progress)
            || progress < 0 || progress > 1)
        {
            throw new ArgumentException("实际准备进度无效");
        }
        CompletedItems = completed;
        TotalItems = total;
        CurrentItemId = itemId ?? string.Empty;
        CurrentItemProgress = progress;
        CurrentItemProgressIsBinary = binary;
    }
}

public sealed class BattleMatchStateVO
{
    public string RequestId { get; }
    public BattleMatchPhase Phase { get; }
    public int SceneId { get; }
    public BattleMatchPlayerVO Friendly { get; }
    public BattleMatchPlayerVO Enemy { get; }
    public BattlePreparationProgressVO Progress { get; }
    public double SearchElapsedSeconds { get; }
    public int CountdownSeconds { get; }
    public string Error { get; }
    public bool BattleTransferred { get; }

    internal BattleMatchStateVO(BattleMatchModel model)
    {
        RequestId = model.Id;
        Phase = model.Phase;
        SceneId = model.Pair.SceneId;
        Friendly = model.Pair.Friendly;
        Enemy = model.Phase == BattleMatchPhase.Searching ? null : model.Pair.Enemy;
        Progress = model.Progress;
        SearchElapsedSeconds = model.SearchElapsed;
        CountdownSeconds = model.Phase == BattleMatchPhase.Countdown
            ? (int)Math.Ceiling(Math.Max(0, model.CountdownRemaining)) : 0;
        Error = model.Error;
        BattleTransferred = model.Transferred;
    }
}
