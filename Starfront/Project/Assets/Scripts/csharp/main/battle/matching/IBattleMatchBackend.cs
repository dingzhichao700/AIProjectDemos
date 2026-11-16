using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>本地匹配的战斗准备边界；允许隔离验证，不包含UI或联网协议。</summary>
public interface IBattleMatchBackend
{
    BattleMatchPairVO CaptureLocal(int sceneId);
    Task<IBattleMatchPreparedBattle> PrepareAsync(BattleMatchPairVO pair,
        Action<BattlePreparationProgressVO> progress, CancellationToken cancellation);
}

public interface IBattleMatchPreparedBattle : IDisposable
{
    bool TryCommit();
    void AbortEntry();
}
