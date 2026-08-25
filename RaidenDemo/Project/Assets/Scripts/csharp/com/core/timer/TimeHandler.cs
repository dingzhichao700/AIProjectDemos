using System;
using System.Collections.Generic;
using UnityEngine;

public class TimeHandler : Handler {

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

    override public void Clear() {
        nextExecuteTimestep = 0;
        loopGap = 0;
        repeat = false;
        base.Clear();
    }

    public void Destory() {
        Clear();
        ReturnToPool(this);
    }

    new public static TimeHandler Create(object caller, Action func) {
        TimeHandler handler = GetFromPool();
        handler.caller = caller;
        handler.callback = func;
        return handler;
    }

    new public static TimeHandler Create<T>(object caller, Action<T> func, T arg) {
        TimeHandler handler = GetFromPool();
        handler.caller = caller;
        handler.callback = func;
        handler.param1 = arg;
        return handler;
    }

    new public static TimeHandler Create<T, X>(object caller, Action<T, X> func, T arg1, X arg2) {
        TimeHandler handler = GetFromPool();
        handler.caller = caller;
        handler.callback = func;
        handler.param1 = arg1;
        handler.param2 = arg2;
        return handler;
    }

    new public static TimeHandler Create<T, X, Y>(object caller, Action<T, X, Y> func, T arg1, X arg2, Y arg3) {
        TimeHandler handler = GetFromPool();
        handler.caller = caller;
        handler.callback = func;
        handler.param1 = arg1;
        handler.param2 = arg2;
        handler.param3 = arg3;
        return handler;
    }

    /// <summary>
    /// 从池里取一个实例
    /// </summary>
    /// <returns></returns>
    public static new TimeHandler GetFromPool() {
        TimeHandler handler;
        if (pool.Count > 0) {
            handler = pool[0];
            pool.RemoveAt(0);
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
        pool.Add(handler);
        if (pool.Count > POOL_MAX) {
            Debug.LogWarning(string.Format("TimeHandler数量超过{0}，请注意回收", POOL_MAX));
        }
    }

}
