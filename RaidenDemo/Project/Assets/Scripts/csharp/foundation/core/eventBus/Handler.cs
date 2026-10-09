using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 保存类型化回调及参数，支持对象池复用。
/// </summary>
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

    /**按创建时的参数类型直接调用，避免反射及空参数推断*/
    protected Action<Handler> invoke;

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

    /**清空回调与参数引用，供对象池复用*/
    public virtual void Clear() {
        caller = null;
        callback = null;
        invoke = null;
        param1 = param2 = param3 = param4 = null;
    }

    /**执行创建时绑定的类型化回调*/
    public void Run() {
        invoke(this);
    }

    /**绑定无参回调*/
    protected void SetCallback(Action func) {
        callback = func;
        invoke = handler => ((Action)handler.callback)();
    }

    /**绑定一个参数的回调，允许参数为 null*/
    protected void SetCallback<T>(Action<T> func, T arg) {
        callback = func;
        param1 = arg;
        invoke = handler => ((Action<T>)handler.callback)((T)handler.param1);
    }

    /**绑定两个参数的回调*/
    protected void SetCallback<T, X>(Action<T, X> func, T arg1, X arg2) {
        callback = func;
        param1 = arg1;
        param2 = arg2;
        invoke = handler => ((Action<T, X>)handler.callback)((T)handler.param1, (X)handler.param2);
    }

    /**绑定三个参数的回调*/
    protected void SetCallback<T, X, Y>(Action<T, X, Y> func, T arg1, X arg2, Y arg3) {
        callback = func;
        param1 = arg1;
        param2 = arg2;
        param3 = arg3;
        invoke = handler => ((Action<T, X, Y>)handler.callback)((T)handler.param1, (X)handler.param2, (Y)handler.param3);
    }

    /**创建并绑定回调参数*/
    public static Handler Create(object caller, Action func) {
        Handler handler = GetFromPool();
        handler.caller = caller;
        handler.SetCallback(func);
        return handler;
    }

    /**创建并绑定回调参数*/
    public static Handler Create<T>(object caller, Action<T> func, T arg) {
        Handler handler = GetFromPool();
        handler.caller = caller;
        handler.SetCallback(func, arg);
        return handler;
    }

    /**创建并绑定回调参数*/
    public static Handler Create<T, X>(object caller, Action<T, X> func, T arg1, X arg2) {
        Handler handler = GetFromPool();
        handler.caller = caller;
        handler.SetCallback(func, arg1, arg2);
        return handler;
    }

    /**创建并绑定回调参数*/
    public static Handler Create<T, X, Y>(object caller, Action<T, X, Y> func, T arg1, X arg2, Y arg3) {
        Handler handler = GetFromPool();
        handler.caller = caller;
        handler.SetCallback(func, arg1, arg2, arg3);
        return handler;
    }

    /// <summary>
    /// 从池里取一个实例
    /// </summary>
    /// <returns></returns>
    public static Handler GetFromPool() {
        Handler handler;
        if (pool.Count > 0) {
            int index = pool.Count - 1;
            handler = pool[index];
            pool.RemoveAt(index);
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
        if (pool.Count < POOL_MAX) {
            pool.Add(handler);
        }
    }

}

