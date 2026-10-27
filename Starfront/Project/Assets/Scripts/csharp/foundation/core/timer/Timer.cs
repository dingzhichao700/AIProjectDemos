using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理独立时间轴上的逐帧监听、延迟任务及下一帧任务。
/// </summary>
public class Timer {

    /// <summary>
    /// 保存逐帧监听的所属对象与回调。
    /// </summary>
    private sealed class TimeUpdateListener {

        public object caller;
        public Action<float> callback;

    }

    /**时间倍率，0 表示停止时间推进*/
    public float scale = 1f;
    /**当前时间，单位毫秒*/
    public float curTime;

    /**字典只负责查询；活动列表负责线性调度*/
    private readonly Dictionary<object, List<TimeHandler>> handlerMap = new Dictionary<object, List<TimeHandler>>();
    private readonly List<TimeHandler> activeHandlers = new List<TimeHandler>();
    private readonly List<TimeHandler> pendingHandlers = new List<TimeHandler>();
    private readonly HashSet<object> dirtyCallers = new HashSet<object>();
    private List<TimeHandler> callLaterList = new List<TimeHandler>();
    private List<TimeHandler> executingLaterList = new List<TimeHandler>();
    private readonly List<TimeUpdateListener> updateListeners = new List<TimeUpdateListener>();
    private bool isSyncing;

    /// <summary>
    /// 推进时间并执行本轮任务，结束后统一整理和回收。
    /// </summary>
    /// <param name="passTime">实际经过时间，单位毫秒。</param>
    /// <remarks>新增任务下一轮参与调度；循环每轮最多执行一次，不补跑错过的间隔。</remarks>
    public void SyncTime(float passTime) {
        if (isSyncing) {
            throw new InvalidOperationException("同一个 Timer 不允许重入 SyncTime。");
        }
        isSyncing = true;
        List<TimeHandler> temporary = executingLaterList;
        executingLaterList = callLaterList;
        callLaterList = temporary;
        try {
            float scaledPassTime = passTime * scale;
            curTime += scaledPassTime;
            DispatchTimeUpdate(scaledPassTime / 1000f);
            for (int i = 0; i < activeHandlers.Count; i++) {
                TimeHandler handler = activeHandlers[i];
                if (!CanExecute(handler) || curTime < handler.nextExecuteTimestep) {
                    continue;
                }
                if (handler.repeat) {
                    // 先安排下次时间，回调内覆盖或取消不会被本轮结果改写。
                    handler.nextExecuteTimestep = Mathf.FloorToInt(curTime) + handler.loopGap;
                } else {
                    Cancel(handler);
                }
                Execute(handler);
            }
            for (int i = 0; i < executingLaterList.Count; i++) {
                TimeHandler handler = executingLaterList[i];
                if (CanExecute(handler)) {
                    Cancel(handler);
                    Execute(handler);
                }
            }
        } finally {
            CompactCancelled();
            activeHandlers.AddRange(pendingHandlers);
            pendingHandlers.Clear();
            updateListeners.RemoveAll(listener => listener.callback == null);
            isSyncing = false;
        }
    }

    /// <summary>
    /// 添加逐帧监听，同一调用者与回调只注册一次。
    /// </summary>
    /// <param name="caller">监听所属对象。</param>
    /// <param name="callback">接收经过倍率处理的秒数。</param>
    public void AddUpdateListener(object caller, Action<float> callback) {
        if (caller == null || callback == null) {
            return;
        }
        foreach (TimeUpdateListener listener in updateListeners) {
            if (listener.caller == caller && listener.callback == callback) {
                return;
            }
        }
        updateListeners.Add(new TimeUpdateListener { caller = caller, callback = callback });
    }

    /**移除逐帧监听，调度期间只标记，避免改变当前遍历位置*/
    public void RemoveUpdateListener(object caller, Action<float> callback) {
        for (int i = updateListeners.Count - 1; i >= 0; i--) {
            TimeUpdateListener listener = updateListeners[i];
            if (listener.caller == caller && listener.callback == callback) {
                if (isSyncing) {
                    listener.callback = null;
                } else {
                    updateListeners.RemoveAt(i);
                }
            }
        }
    }

    /**只通知本轮开始时已有的监听者，单个回调异常不影响其他任务*/
    private void DispatchTimeUpdate(float deltaTime) {
        int count = updateListeners.Count;
        for (int i = 0; i < count; i++) {
            TimeUpdateListener listener = updateListeners[i];
            if (listener.callback == null || !IsAlive(listener.caller) || !IsAlive(listener.callback.Target)) {
                listener.callback = null;
                continue;
            }
            try {
                listener.callback(deltaTime);
            } catch (Exception exception) {
                Debug.LogException(exception);
            }
        }
    }

    /**识别已销毁的 Unity 对象；普通对象和静态回调不受影响*/
    private static bool IsAlive(object target) {
        return !(target is UnityEngine.Object unityObject) || unityObject != null;
    }

    /**跳过取消任务，并及时清理已经销毁的调用者或回调目标*/
    private bool CanExecute(TimeHandler handler) {
        if (handler.cancelled) {
            return false;
        }
        if (!IsAlive(handler.caller) || !IsAlive(handler.callback.Target)) {
            Cancel(handler);
            return false;
        }
        return true;
    }

    /**直接执行类型化回调，异常隔离到当前任务*/
    private static void Execute(TimeHandler handler) {
        try {
            handler.Run();
        } catch (Exception exception) {
            Debug.LogException(exception);
        }
    }

    /**按调用者、回调及队列类型查找未取消任务*/
    private TimeHandler TryGetHandler(object caller, Delegate callback, bool isLater = false) {
        if (handlerMap.TryGetValue(caller, out List<TimeHandler> handlers)) {
            foreach (TimeHandler handler in handlers) {
                if (!handler.cancelled && handler.isLater == isLater && handler.callback.Equals(callback)) {
                    return handler;
                }
            }
        }
        return null;
    }

    /**登记查询索引与所属调度队列*/
    private void AddHandler(TimeHandler handler) {
        if (!handlerMap.TryGetValue(handler.caller, out List<TimeHandler> handlers)) {
            handlers = new List<TimeHandler>();
            handlerMap.Add(handler.caller, handlers);
        }
        handlers.Add(handler);
        if (handler.isLater) {
            callLaterList.Add(handler);
        } else if (isSyncing) {
            pendingHandlers.Add(handler);
        } else {
            activeHandlers.Add(handler);
        }
    }

    /**取消后保留数据到遍历结束，防止执行中的对象被池复用*/
    private void Cancel(TimeHandler handler) {
        if (handler != null && !handler.cancelled) {
            handler.cancelled = true;
            dirtyCallers.Add(handler.caller);
        }
    }

    /**先移除索引，再线性压缩各队列并回收取消任务*/
    private void CompactCancelled() {
        foreach (object caller in dirtyCallers) {
            List<TimeHandler> handlers = handlerMap[caller];
            handlers.RemoveAll(handler => handler.cancelled);
            if (handlers.Count == 0) {
                handlerMap.Remove(caller);
            }
        }
        dirtyCallers.Clear();
        Compact(activeHandlers);
        Compact(pendingHandlers);
        Compact(executingLaterList);
        Compact(callLaterList);
    }

    /**保持剩余任务的注册顺序，避免逐个 RemoveAt 搬移数组*/
    private static void Compact(List<TimeHandler> handlers) {
        int writeIndex = 0;
        for (int i = 0; i < handlers.Count; i++) {
            TimeHandler handler = handlers[i];
            if (handler.cancelled) {
                TimeHandler.ReturnToPool(handler);
            } else {
                handlers[writeIndex++] = handler;
            }
        }
        handlers.RemoveRange(writeIndex, handlers.Count - writeIndex);
    }

    /**验证注册参数，避免无效任务进入索引*/
    private static void Validate(object caller, Delegate callback) {
        if (caller == null) {
            throw new ArgumentNullException(nameof(caller));
        }
        if (callback == null) {
            throw new ArgumentNullException(nameof(callback));
        }
    }

    /// <summary>
    /// 注册循环任务，重复注册会更新间隔并重新计时。
    /// </summary>
    /// <param name="caller">调用对象。</param>
    /// <param name="loopGap">循环间隔，单位毫秒，必须大于 0。</param>
    /// <param name="func">执行回调。</param>
    public void Loop(object caller, int loopGap, Action func) {
        Validate(caller, func);
        if (loopGap <= 0) {
            Debug.LogError("loop函数的间隔非法，值为：" + loopGap);
            return;
        }
        Cancel(TryGetHandler(caller, func));
        TimeHandler handler = TimeHandler.Create(caller, func);
        handler.repeat = true;
        handler.loopGap = loopGap;
        handler.nextExecuteTimestep = Mathf.FloorToInt(curTime) + loopGap;
        AddHandler(handler);
    }

    /**登记一次性任务，覆盖时取消旧任务并使用本次参数*/
    private void ScheduleOnce(TimeHandler handler, int delay, bool coverBefore) {
        if (coverBefore) {
            Cancel(TryGetHandler(handler.caller, handler.callback));
        }
        handler.nextExecuteTimestep = Mathf.FloorToInt(curTime) + delay;
        AddHandler(handler);
    }

    /// <summary>
    /// 延迟执行一次回调，零延迟立即执行。
    /// </summary>
    /// <param name="caller">调用对象。</param>
    /// <param name="delay">延迟毫秒数。</param>
    /// <param name="func">执行回调。</param>
    /// <param name="coverBefore">是否覆盖同一调用者的同名回调任务。</param>
    public void Once(object caller, int delay, Action func, bool coverBefore = true) {
        Validate(caller, func);
        if (delay == 0) {
            func();
            return;
        }
        ScheduleOnce(TimeHandler.Create(caller, func), delay, coverBefore);
    }

    /// <summary>
    /// 下一轮同步时执行，同一调用者与回调只保留一个待执行任务。
    /// </summary>
    public void CallLater(object caller, Action func) {
        Validate(caller, func);
        if (TryGetHandler(caller, func, true) != null) {
            return;
        }
        TimeHandler handler = TimeHandler.Create(caller, func);
        handler.isLater = true;
        AddHandler(handler);
    }

    /**取消该调用者的匹配回调，包括下一帧任务*/
    public void Clear(object caller, Action func) {
        ClearHandlerByFunc(caller, func);
    }

    /// <summary>
    /// 延迟执行一次回调，零延迟立即执行。
    /// </summary>
    /// <param name="caller">调用对象。</param>
    /// <param name="delay">延迟毫秒数。</param>
    /// <param name="func">执行回调。</param>
    /// <param name="arg1">回调参数。</param>
    /// <param name="coverBefore">是否覆盖同一调用者的同名回调任务。</param>
    public void Once<T>(object caller, int delay, Action<T> func, T arg1, bool coverBefore = false) {
        Validate(caller, func);
        if (delay == 0) {
            func(arg1);
            return;
        }
        ScheduleOnce(TimeHandler.Create(caller, func, arg1), delay, coverBefore);
    }

    /// <summary>
    /// 下一轮同步时执行，同一调用者与回调只保留一个待执行任务。
    /// </summary>
    public void CallLater<T>(object caller, Action<T> func, T arg1) {
        Validate(caller, func);
        if (TryGetHandler(caller, func, true) != null) {
            return;
        }
        TimeHandler handler = TimeHandler.Create(caller, func, arg1);
        handler.isLater = true;
        AddHandler(handler);
    }

    /**取消该调用者的匹配回调，包括下一帧任务*/
    public void Clear<T>(object caller, Action<T> func) {
        ClearHandlerByFunc(caller, func);
    }

    /// <summary>
    /// 延迟执行一次回调，零延迟立即执行。
    /// </summary>
    /// <param name="caller">调用对象。</param>
    /// <param name="delay">延迟毫秒数。</param>
    /// <param name="func">执行回调。</param>
    /// <param name="arg1">回调参数。</param>
    /// <param name="arg2">回调参数。</param>
    /// <param name="coverBefore">是否覆盖同一调用者的同名回调任务。</param>
    public void Once<T, X>(object caller, int delay, Action<T, X> func, T arg1, X arg2, bool coverBefore = false) {
        Validate(caller, func);
        if (delay == 0) {
            func(arg1, arg2);
            return;
        }
        ScheduleOnce(TimeHandler.Create(caller, func, arg1, arg2), delay, coverBefore);
    }

    /// <summary>
    /// 下一轮同步时执行，同一调用者与回调只保留一个待执行任务。
    /// </summary>
    public void CallLater<T, X>(object caller, Action<T, X> func, T arg1, X arg2) {
        Validate(caller, func);
        if (TryGetHandler(caller, func, true) != null) {
            return;
        }
        TimeHandler handler = TimeHandler.Create(caller, func, arg1, arg2);
        handler.isLater = true;
        AddHandler(handler);
    }

    /**取消该调用者的匹配回调，包括下一帧任务*/
    public void Clear<T, X>(object caller, Action<T, X> func) {
        ClearHandlerByFunc(caller, func);
    }

    /// <summary>
    /// 延迟执行一次回调，零延迟立即执行。
    /// </summary>
    /// <param name="caller">调用对象。</param>
    /// <param name="delay">延迟毫秒数。</param>
    /// <param name="func">执行回调。</param>
    /// <param name="arg1">回调参数。</param>
    /// <param name="arg2">回调参数。</param>
    /// <param name="arg3">回调参数。</param>
    /// <param name="coverBefore">是否覆盖同一调用者的同名回调任务。</param>
    public void Once<T, X, Y>(object caller, int delay, Action<T, X, Y> func, T arg1, X arg2, Y arg3, bool coverBefore = false) {
        Validate(caller, func);
        if (delay == 0) {
            func(arg1, arg2, arg3);
            return;
        }
        ScheduleOnce(TimeHandler.Create(caller, func, arg1, arg2, arg3), delay, coverBefore);
    }

    /// <summary>
    /// 下一轮同步时执行，同一调用者与回调只保留一个待执行任务。
    /// </summary>
    public void CallLater<T, X, Y>(object caller, Action<T, X, Y> func, T arg1, X arg2, Y arg3) {
        Validate(caller, func);
        if (TryGetHandler(caller, func, true) != null) {
            return;
        }
        TimeHandler handler = TimeHandler.Create(caller, func, arg1, arg2, arg3);
        handler.isLater = true;
        AddHandler(handler);
    }

    /**取消该调用者的匹配回调，包括下一帧任务*/
    public void Clear<T, X, Y>(object caller, Action<T, X, Y> func) {
        ClearHandlerByFunc(caller, func);
    }

    /**取消全部匹配项，调度外可立即整理，调度内延迟回收*/
    private void ClearHandlerByFunc(object caller, Delegate callback) {
        if (caller == null || callback == null || !handlerMap.TryGetValue(caller, out List<TimeHandler> handlers)) {
            return;
        }
        foreach (TimeHandler handler in handlers) {
            if (handler.callback.Equals(callback)) {
                Cancel(handler);
            }
        }
        if (!isSyncing) {
            CompactCancelled();
        }
    }

}
