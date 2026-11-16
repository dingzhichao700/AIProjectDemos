using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>进入本局时的玩家已选卡组及已激活等级快照，不含存档或表现引用。</summary>
public sealed class BattlePlayerDeckVO
{
    public IReadOnlyDictionary<int, int> cardLevels { get; }

    public BattlePlayerDeckVO(IEnumerable<KeyValuePair<int, int>> cards)
    {
        var snapshot = new Dictionary<int, int>();
        foreach (var card in cards)
        {
            if (card.Key == 0 || card.Value <= 0)
            {
                continue;
            }
            snapshot.Add(card.Key, card.Value);
        }
        cardLevels = new ReadOnlyDictionary<int, int>(snapshot);
    }

    public static BattlePlayerDeckVO CaptureCurrent()
    {
        var team = TeamEditControl.ins;
        var cards = new List<KeyValuePair<int, int>>();
        foreach (int id in team.ReadSelected(out _))
        {
            if (id != 0)
            {
                cards.Add(new KeyValuePair<int, int>(id, team.Query.Level(id)));
            }
        }
        return new BattlePlayerDeckVO(cards);
    }
}
