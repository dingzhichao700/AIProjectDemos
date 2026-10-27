using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可复用的时间任务，调度期间取消后延迟回收。
/// </summary>
public class TimeHandler : Handler {

    /**取消后不再执行，待本轮遍历结束统一回收*/
    internal bool cancelled;
    /**是否属于下一帧批次*/
    internal bool isLater;

    /// <summary>
    /// 下次执行时间
    /// </summary>
    public int nextExecuteTimestep;

    /// <summary>
    /// 重复执行时间间隔
    /// </summary>
    public int loopGap;

    /// <summary>
    /// 是否重复执行
    /// </summary>
    public bool repeat;

    /// <summary>
    /// TimeHandler 对象池
    /// </summary>
    private static List<TimeHandler> pool = new List<TimeHandler>();

    /// <summary>
    /// 池的最大实例数量
    /// </summary>
    private const int POOL_MAX = 500;

    /**重置调度状态及回调引用*/
    public override void Clear() {
        cancelled = false;
        isLater = false;
        nextExecuteTimestep = 0;
        loopGap = 0;
        repeat = false;
        base.Clear();
    }

    /**归还任务池，保留原有接口名称*/
    public void Destory() {
        ReturnToPool(this);
    }

    /**创建并绑定时间任务回调*/
    public new static TimeHandler Create(object caller, Action func) {
        TimeHandler handler = GetFromPool();
        handler.caller = caller;
        handler.SetCallback(func);
        return handler;
    }

    /**创建并绑定时间任务回调*/
    public new static TimeHandler Create<T>(object caller, Action<T> func, T arg) {
        TimeHandler handler = GetFromPool();
        handler.caller = caller;
        handler.SetCallback(func, arg);
        return handler;
    }

    /**创建并绑定时间任务回调*/
    public new static TimeHandler Create<T, X>(object caller, Action<T, X> func, T arg1, X arg2) {
        TimeHandler handler = GetFromPool();
        handler.caller = caller;
        handler.SetCallback(func, arg1, arg2);
        return handler;
    }

    /**创建并绑定时间任务回调*/
    public new static TimeHandler Create<T, X, Y>(object caller, Action<T, X, Y> func, T arg1, X arg2, Y arg3) {
        TimeHandler handler = GetFromPool();
        handler.caller = caller;
        handler.SetCallback(func, arg1, arg2, arg3);
        return handler;
    }

    /// <summary>
    /// 从池里取一个实例
    /// </summary>
    /// <returns></returns>
    public static new TimeHandler GetFromPool() {
        TimeHandler handler;
        if (pool.Count > 0) {
            int index = pool.Count - 1;
            handler = pool[index];
            pool.RemoveAt(index);
            return handler;
        }
        handler = new TimeHandler();
        return handler;
    }

    /// <summary>
    /// 归还到池中
    /// </summary>
    /// <param name="handler"></param>
    public static void ReturnToPool(TimeHandler handler) {
        handler.Clear();
        if (pool.Count < POOL_MAX) {
            pool.Add(handler);
        }
    }

}
