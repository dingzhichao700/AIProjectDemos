using System;

/// <summary>一个实例对应一个不可复用的请求身份，页面只提交意图。</summary>
public sealed class BattleMatchRequest
{
    private readonly BattleMatchControl owner;
    internal readonly BattleMatchModel Model;
    public string Id => Model.Id;
    public BattleMatchStateVO Current => owner.Read(Model);
    public event Action<BattleMatchStateVO> Changed;

    internal BattleMatchRequest(BattleMatchControl owner, BattleMatchModel model)
    {
        this.owner = owner;
        Model = model;
    }

    public bool CancelSearch() => owner.CancelSearch(Model);
    public BattleMatchRequest Retry() => owner.Retry(Model);
    public bool ReturnAfterFailure() => owner.ReturnAfterFailure(Model);
    public void Close() => owner.Close(Model);
    public bool TryTakeBattle() => owner.TryTakeBattle(Model);
    public void ReportEntryFailure(string reason) => owner.ReportEntryFailure(Model, reason);
    public void SetApplicationPaused(bool paused) => owner.SetApplicationPaused(Model, paused);

    internal void Publish(BattleMatchStateVO state, Action<Exception> observerError)
    {
        if (Changed == null)
        {
            return;
        }
        foreach (Action<BattleMatchStateVO> handler in Changed.GetInvocationList())
        {
            try
            {
                handler(state);
            }
            catch (Exception exception)
            {
                observerError(exception);
            }
        }
    }
}
