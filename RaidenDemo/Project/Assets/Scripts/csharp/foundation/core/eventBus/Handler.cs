using System;
using System.Collections.Generic;
using UnityEngine;

public class Handler {

    /// <summary>
    /// 池的最大实例数量
    /// </summary>
    private const int POOL_MAX = 500;

    /// <summary>
    /// Handler 对象池
    /// </summary>
    private static List<Handler> pool = new List<Handler>();

    /// <summary>
    /// 回调持有对象
    /// </summary>
    public object caller;

    /// <summary>
    /// 委托
    /// </summary>
    public Delegate callback;

    /// <summary>
    /// 委托的参数1
    /// </summary>
    public object param1;

    /// <summary>
    /// 委托的参数2
    /// </summary>
    public object param2;

    /// <summary>
    /// 委托的参数3
    /// </summary>
    public object param3;

    /// <summary>
    /// 委托的参数4
    /// </summary>
    public object param4;

    virtual public void Clear() {
        if (param1 != null) {
            param1 = null;
        }
        if (param2 != null) {
            param2 = null;
        }
        if (param3 != null) {
            param3 = null;
        }
        if (param4 != null) {
            param4 = null;
        }
    }

    /// <summary>
    /// 无参
    /// </summary>
    public void Run() {
        if (param4 != null) {
            callback.DynamicInvoke(param1, param2, param3, param4);
        } else if (param3 != null) {
            callback.DynamicInvoke(param1, param2, param3);
        } else if (param2 != null) {
            callback.DynamicInvoke(param1, param2);
        } else if (param1 != null) {
            callback.DynamicInvoke(param1);
        } else {
            callback.DynamicInvoke();
        }
    }

    public static Handler Create(object caller, Action func) {
        Handler handler = GetFromPool();
        handler.caller = caller;
        handler.callback = func;
        return handler;
    }

    public static Handler Create<T>(object caller, Action<T> func, T arg) {
        Handler handler = GetFromPool();
        handler.caller = caller;
        handler.callback = func;
        handler.param1 = arg;
        return handler;
    }

    public static Handler Create<T, X>(object caller, Action<T, X> func, T arg1, X arg2) {
        Handler handler = GetFromPool();
        handler.caller = caller;
        handler.callback = func;
        handler.param1 = arg1;
        handler.param2 = arg2;
        return handler;
    }

    public static Handler Create<T, X, Y>(object caller, Action<T, X, Y> func, T arg1, X arg2, Y arg3) {
        Handler handler = GetFromPool();
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
    public static Handler GetFromPool() {
        Handler handler;
        if (pool.Count > 0) {
            handler = pool[0];
            pool.RemoveAt(0);
            return handler;
        }
        handler = new Handler();
        return handler;
    }

    /// <summary>
    /// 归还到池中
    /// </summary>
    /// <param name="handler"></param>
    public static void ReturnToPool(Handler handler) {
        handler.Clear();
        pool.Add(handler);
        if (pool.Count > POOL_MAX) {
            Debug.LogWarning(string.Format("Handler数量超过{0}，请注意回收", POOL_MAX));
        }
    }

}

