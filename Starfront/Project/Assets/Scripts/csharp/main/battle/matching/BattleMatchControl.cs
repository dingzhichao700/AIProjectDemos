using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>本地电脑模拟匹配；权威阶段、竞态和倒计时不放在界面里。</summary>
public sealed class BattleMatchControl : IDisposable
{
    private static BattleMatchControl instance;
    public static BattleMatchControl ins => instance ?? (instance = new BattleMatchControl(
        new LocalBattleMatchBackend(), RookieEngine.timer, () => UnityEngine.Application.isFocused,
        exception => UnityEngine.Debug.LogException(exception)));

    private readonly IBattleMatchBackend backend;
    private readonly Timer timer;
    private readonly Func<bool> foreground;
    private readonly Action<Exception> observerError;
    private readonly int threadId;
    private BattleMatchModel active;
    private Task preparationTail = Task.CompletedTask;
    private bool disposed;
    private bool retrying;

    public BattleMatchControl(IBattleMatchBackend backend, Timer timer, Func<bool> foreground,
        Action<Exception> observerError = null)
    {
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
        this.foreground = foreground ?? throw new ArgumentNullException(nameof(foreground));
        this.observerError = observerError ?? (_ => { });
        threadId = Thread.CurrentThread.ManagedThreadId;
        timer.AddUpdateListener(this, Tick);
    }

    public BattleMatchRequest BeginLocalSimulation(int sceneId)
    {
        CheckThread();
        if (disposed || retrying || active != null)
        {
            throw new InvalidOperationException("匹配请求尚未关闭，不能重复开始");
        }
        return Begin(backend.CaptureLocal(sceneId));
    }

    private BattleMatchRequest Begin(BattleMatchPairVO pair)
    {
        var model = new BattleMatchModel(Guid.NewGuid().ToString("N"), pair);
        model.WasForeground = foreground();
        model.Request = new BattleMatchRequest(this, model);
        active = model;
        return model.Request;
    }

    internal BattleMatchStateVO Read(BattleMatchModel model)
    {
        CheckThread();
        return new BattleMatchStateVO(model);
    }

    private void CheckThread()
    {
        if (Thread.CurrentThread.ManagedThreadId != threadId)
        {
            throw new InvalidOperationException("匹配接口及事件必须在创建它的Unity主线程调用");
        }
    }

    private bool IsCurrent(BattleMatchModel model) => !disposed && ReferenceEquals(active, model);

    private void Publish(BattleMatchModel model)
    {
        model.Request.Publish(new BattleMatchStateVO(model), observerError);
    }

    private void Tick(float delta)
    {
        CheckThread();
        var model = active;
        if (model == null || model.ApplicationPaused || !foreground())
        {
            if (model != null)
            {
                model.WasForeground = false;
            }
            return;
        }
        if (!model.WasForeground)
        {
            model.WasForeground = true;
            return;
        }
        if (delta < 0 || float.IsNaN(delta) || float.IsInfinity(delta))
        {
            return;
        }
        if (model.Phase == BattleMatchPhase.Searching)
        {
            model.SearchElapsed += delta;
            model.Phase = BattleMatchPhase.Preparing;
            Publish(model);
            if (IsCurrent(model) && model.Phase == BattleMatchPhase.Preparing)
            {
                preparationTail = PrepareAfterAsync(model, preparationTail);
            }
        }
        else if (model.Phase == BattleMatchPhase.Countdown)
        {
            int before = (int)Math.Ceiling(model.CountdownRemaining);
            model.CountdownRemaining = Math.Max(0, model.CountdownRemaining - delta);
            if (model.CountdownRemaining == 0)
            {
                model.Phase = BattleMatchPhase.Entering;
                Publish(model);
            }
            else if (before != (int)Math.Ceiling(model.CountdownRemaining))
            {
                Publish(model);
            }
        }
    }

    private async Task PrepareAfterAsync(BattleMatchModel model, Task previous)
    {
        IBattleMatchPreparedBattle prepared = null;
        try
        {
            // 底层不能中止的加载必须先收尾，旧请求绝不能回收新局资源。
            try
            {
                await previous;
            }
            catch (Exception)
            {
                // 前序请求的失败已经在其自身状态中报告。
            }
            if (!IsCurrent(model) || model.Cancellation.IsCancellationRequested)
            {
                return;
            }
            prepared = await backend.PrepareAsync(model.Pair, progress =>
            {
                CheckThread();
                if (IsCurrent(model) && model.Phase == BattleMatchPhase.Preparing
                    && !model.Cancellation.IsCancellationRequested)
                {
                    model.Progress = progress;
                    Publish(model);
                }
            }, model.Cancellation.Token);
            CheckThread();
            if (!IsCurrent(model) || model.Cancellation.IsCancellationRequested)
            {
                prepared?.Dispose();
                return;
            }
            if (prepared == null || model.Progress.TotalItems == 0
                || model.Progress.CompletedItems != model.Progress.TotalItems)
            {
                throw new InvalidOperationException("准备结果不完整，不能开始倒计时");
            }
            model.Prepared = prepared;
            prepared = null;
            model.CountdownRemaining = 3;
            model.Phase = BattleMatchPhase.Countdown;
            Publish(model);
        }
        catch (Exception error)
        {
            prepared?.Dispose();
            CheckThread();
            if (IsCurrent(model) && model.Phase != BattleMatchPhase.Closed
                && model.Phase != BattleMatchPhase.Cancelled)
            {
                model.Error = error is OperationCanceledException ? "准备已取消" : error.Message;
                model.Phase = BattleMatchPhase.Failed;
                Publish(model);
            }
        }
    }

    internal bool CancelSearch(BattleMatchModel model)
    {
        CheckThread();
        if (!IsCurrent(model) || model.Phase != BattleMatchPhase.Searching)
        {
            return false;
        }
        model.Phase = BattleMatchPhase.Cancelled;
        active = null;
        model.Cancellation.Cancel();
        Publish(model);
        return true;
    }

    internal BattleMatchRequest Retry(BattleMatchModel model)
    {
        CheckThread();
        if (!IsCurrent(model) || model.Phase != BattleMatchPhase.Failed)
        {
            throw new InvalidOperationException("只有当前失败请求可重试");
        }
        var pair = model.Pair;
        retrying = true;
        try
        {
            Close(model);
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(BattleMatchControl));
            }
            return Begin(pair);
        }
        finally
        {
            retrying = false;
        }
    }

    internal bool ReturnAfterFailure(BattleMatchModel model)
    {
        CheckThread();
        if (!IsCurrent(model) || model.Phase != BattleMatchPhase.Failed)
        {
            return false;
        }
        Close(model);
        return true;
    }

    internal void Close(BattleMatchModel model)
    {
        CheckThread();
        if (model.Phase == BattleMatchPhase.Closed)
        {
            return;
        }
        model.Phase = BattleMatchPhase.Closed;
        if (ReferenceEquals(active, model))
        {
            active = null;
        }
        model.Cancellation.Cancel();
        model.Prepared?.Dispose();
        if (!model.Transferred)
        {
            model.Prepared = null;
        }
        Publish(model);
    }

    internal bool TryTakeBattle(BattleMatchModel model)
    {
        CheckThread();
        if (!IsCurrent(model) || model.Phase != BattleMatchPhase.Entering || model.Transferred
            || model.Prepared == null)
        {
            return false;
        }
        try
        {
            if (!model.Prepared.TryCommit())
            {
                throw new InvalidOperationException("准备结果已失效，不能进入战斗");
            }
            model.Transferred = true;
            return true;
        }
        catch (Exception error)
        {
            ReportEntryFailure(model, error.Message);
            return false;
        }
    }

    internal void ReportEntryFailure(BattleMatchModel model, string reason)
    {
        CheckThread();
        if (model.Transferred)
        {
            model.Prepared?.AbortEntry();
            model.Transferred = false;
        }
        if (!IsCurrent(model) || model.Phase != BattleMatchPhase.Entering)
        {
            return;
        }
        model.Prepared?.Dispose();
        model.Prepared = null;
        model.Error = string.IsNullOrWhiteSpace(reason) ? "战斗入口打开失败" : reason;
        model.Phase = BattleMatchPhase.Failed;
        Publish(model);
    }

    internal void SetApplicationPaused(BattleMatchModel model, bool paused)
    {
        CheckThread();
        if (IsCurrent(model))
        {
            model.ApplicationPaused = paused;
            if (paused)
            {
                model.WasForeground = false;
            }
        }
    }

    public void Dispose()
    {
        CheckThread();
        if (disposed)
        {
            return;
        }
        disposed = true;
        if (active != null)
        {
            Close(active);
        }
        timer.RemoveUpdateListener(this, Tick);
    }
}
