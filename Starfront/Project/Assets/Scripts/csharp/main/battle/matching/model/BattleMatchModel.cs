using System.Threading;

/// <summary>单次匹配请求的权威状态，独立于页面表现与窗口生命周期。</summary>
internal sealed class BattleMatchModel
{
    internal readonly string Id;
    internal readonly BattleMatchPairVO Pair;
    internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
    internal BattleMatchPhase Phase = BattleMatchPhase.Searching;
    internal BattlePreparationProgressVO Progress = new BattlePreparationProgressVO(0, 0, null, 0, true);
    internal double SearchElapsed;
    internal double CountdownRemaining = 3;
    internal string Error;
    internal bool Transferred;
    internal bool ApplicationPaused;
    internal bool WasForeground;
    internal IBattleMatchPreparedBattle Prepared;
    internal BattleMatchRequest Request;

    internal BattleMatchModel(string id, BattleMatchPairVO pair)
    {
        Id = id;
        Pair = pair;
    }
}
