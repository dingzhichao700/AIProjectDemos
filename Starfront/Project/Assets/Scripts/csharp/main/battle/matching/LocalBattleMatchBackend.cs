using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cfg;

/// <summary>只复用既有本地快照、准入及BattleControl准备链，不生成联网资料。</summary>
public sealed class LocalBattleMatchBackend : IBattleMatchBackend
{
    public BattleMatchPairVO CaptureLocal(int sceneId)
    {
        var team = TeamEditControl.ins;
        var saved = team.ReadSelected(out _).Where(id => id != 0)
            .Select(id => new KeyValuePair<int, int>(id, team.Query.Level(id))).ToArray();
        return CapturePair(sceneId, saved, saved);
    }

    /// <summary>测试或已有独立对手输入可各自给定快照；不表示实现远端匹配。</summary>
    public BattleMatchPairVO CapturePair(int sceneId, IEnumerable<KeyValuePair<int, int>> friendly,
        IEnumerable<KeyValuePair<int, int>> enemy)
    {
        var tables = CfgManager.tables;
        BattleMatchPlayerVO Create(BattleSceneCamp camp, IEnumerable<KeyValuePair<int, int>> source)
        {
            var saved = source.Where(p => p.Key != 0).ToArray();
            if (saved.Length > BattleConst.DeckCardLimit || saved.Select(p => p.Key).Distinct().Count() != saved.Length)
            {
                throw new InvalidOperationException("匹配卡组超出容量或包含重复卡牌");
            }
            var deck = new BattlePlayerDeckVO(saved);
            var roster = BattleRoster.CreateCamp(tables, sceneId, camp, deck);
            var ignored = new Dictionary<int, string>(roster.ignoredCards);
            foreach (var pair in saved.Where(p => p.Value <= 0))
            {
                ignored[pair.Key] = "资格：卡牌未激活";
            }
            var cards = saved.Select(pair =>
            {
                var card = tables.CardObj.Get(pair.Key);
                return new BattleMatchCardVO(card.Id, card.ArtId, card.Name, pair.Value,
                    card.ConstructionCost, (int)card.Quality, card.TagId);
            }).ToArray();
            return new BattleMatchPlayerVO(camp == BattleSceneCamp.FRIENDLY ? BattleConst.FriendlyName : BattleConst.EnemyName,
                camp == BattleSceneCamp.ENEMY, cards,
                cards.Where(c => roster.cardIds.Contains(c.CardId) && tables.CardObj.Get(c.CardId).Content is UnitCardContent), ignored);
        }
        return new BattleMatchPairVO(sceneId, Create(BattleSceneCamp.FRIENDLY, friendly),
            Create(BattleSceneCamp.ENEMY, enemy));
    }

    public async Task<IBattleMatchPreparedBattle> PrepareAsync(BattleMatchPairVO pair,
        Action<BattlePreparationProgressVO> progress, CancellationToken cancellation)
    {
        return await BattleControl.ins.PrepareMatchAsync(pair, progress, cancellation);
    }
}
