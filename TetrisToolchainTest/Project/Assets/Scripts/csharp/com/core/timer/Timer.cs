using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Timer {

    /// <summary>
    /// 时间倍率
    /// </summary>
    public float scale;

    /// <summary>
    /// 当前时刻
    /// </summary>
    public float curTime;

    /// <summary>
    /// 延迟到下一帧执行的函数列表
    /// </summary>
    private List<TimeHandler> callLaterList = new List<TimeHandler>();

    /// <summary>
    /// 需要整理的调用者的列表
    /// </summary>
    /// <remarks>
    /// SyncTime过程中，由于：归属于某些调用者的函数会被动态清理，但不能因此立刻将它们从回调列表中删除（因为清理他们的逻辑也使由列表遍历触发的，此时删除元素=遍历列表过程中删除列表元素，会报错）
    /// 所以用这个列表存起来，等SyncTime结束再整理一遍，回调列表中空的元素删除，将列表彻底为空的caller从handlerMap删除
    /// </remarks>
    private List<object> needOrganizeCallerList;

    /// <summary>
    /// 计时器使用对象和对应延迟执行handler列表的map
    /// </summary>
    private Dictionary<object, List<TimeHandler>> handlerMap = new Dictionary<object, List<TimeHandler>>();

    public Timer() {
        scale = 1;
        curTime = 0;
    }

    /// <summary>
    /// 同步
    /// </summary>
    /// <param name="passTime">经过的物理毫秒时间</param>
    public void SyncTime(float passTime) {
        curTime += passTime * scale;
        //Debug.Log("curTime:" + curTime);
        for (int i = 0; i < handlerMap.Keys.Count; i++) {
            object obj = handlerMap.Keys.ElementAt(i);
            List<TimeHandler> handlerList = handlerMap[obj];
            if (handlerList.Count > 0) {
                for (int j = handlerList.Count - 1; j >= 0; j--) {
                    TimeHandler handler = handlerList[j];
                    if (handler != null && curTime >= handler.nextExecuteTimestep) {
                        if (!IsHandlerAlive(handler)) {
                            // 直接清掉这个 handler，避免再调用
                            ClearHandler(handler.caller, handler);
                            continue;
                        }
                        if (!handler.repeat) {
                            ClearHandler(handler.caller, handler);
                        }
                        handler.Run();
                        if (handler.repeat) {
                            handler.nextExecuteTimestep = Mathf.FloorToInt(curTime) + handler.loopGap;
                        }
                    }
                }
            }
        }
        if (needOrganizeCallerList != null) {
            for (int i = 0; i < needOrganizeCallerList.Count; i++) {
                object caller = needOrganizeCallerList[i];
                handlerMap.TryGetValue(caller, out List<TimeHandler> handlers);
                if (handlers != null) {
                    handlers.RemoveAll(item => item == null);//删除一遍空节点
                    if (handlers.Count == 0) { //删完以后长度为0，则该caller作为key也可以用handlerMap中删除
                        handlerMap.Remove(caller);
                    }
                }
            }
            needOrganizeCallerList = null;
        }

        for (int i = 0; i < callLaterList.Count; i++) {
            callLaterList[i].Run();
            callLaterList[i].Destory();
            callLaterList.RemoveAt(i);
        }
    }

    /**增加一个需要清除的调用者*/
    private void AddCleanCaller(object caller) {
        if (needOrganizeCallerList == null) {
            needOrganizeCallerList = new List<object>();
        }
        needOrganizeCallerList.Add(caller);
    }

    /// <summary>
    /// 获取某个延迟回调
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    private TimeHandler TryGetHandler(object caller, Delegate func) {
        if (handlerMap.ContainsKey(caller)) {
            List<TimeHandler> callbackList = handlerMap[caller];
            for (int i = 0; i < callbackList.Count; i++) {
                if (callbackList[i] != null && callbackList[i].callback.Equals(func)) {
                    return callbackList[i];
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 添加handler
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="func"></param>
    private void AddHandler(object caller, TimeHandler handler) {
        List<TimeHandler> callbackList;
        if (handlerMap.ContainsKey(caller)) {
            callbackList = handlerMap[caller];
        } else {
            callbackList = new List<TimeHandler>();
            handlerMap.Add(caller, callbackList);
        }
        callbackList.Add(handler);
    }

    /// <summary>
    ///  循环执行函数
    /// </summary>
    /// <param name="caller">调用对象</param>
    /// <param name="loopGap">循环间隔</param>
    /// <param name="func">函数</param>
    public void Loop(object caller, int loopGap, Action func) {
        if (loopGap <= 0) {
            Debug.LogError("loop函数的间隔非法，值为：" + loopGap);
            return;
        }
        //先尝试找出之前的TimeHandler
        TimeHandler handler = TryGetHandler(caller, func);
        if (handler == null) {
            //找不到就新创建一个handler
            handler = TimeHandler.Create(caller, func);
            handler.repeat = true;
            handler.loopGap = loopGap;
            AddHandler(caller, handler);
        }
        handler.nextExecuteTimestep = (int)(curTime + loopGap);
    }

    /// <summary>
    /// 延后一帧执行函数（无参数）
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="func"></param>
    public void CallLater(object caller, Action func) {
        bool findExistOne = false;
        for (int i = 0; i < callLaterList.Count; i++) {
            if (callLaterList[i].caller == caller && callLaterList[i].callback.Equals(func)) {
                findExistOne = true;
            }
        }
        if (!findExistOne) {
            callLaterList.Add(TimeHandler.Create(caller, func));
        }
    }

    /// <summary>
    /// 延后一帧执行函数（1个参数）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="caller"></param>
    /// <param name="func"></param>
    /// <param name="arg"></param>
    public void CallLater<T>(object caller, Action<T> func, T arg) {
        bool findExistOne = false;
        for (int i = 0; i < callLaterList.Count; i++) {
            if (callLaterList[i].caller == caller && callLaterList[i].callback.Equals(func)) {
                findExistOne = true;
            }
        }
        if (!findExistOne) {
            callLaterList.Add(TimeHandler.Create(caller, func, arg));
        }
    }

    /// <summary>
    /// 延后一帧执行函数（2个参数）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="X"></typeparam>
    /// <param name="caller"></param>
    /// <param name="func"></param>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    public void CallLater<T, X>(object caller, Action<T, X> func, T arg1, X arg2) {
        bool findExistOne = false;
        for (int i = 0; i < callLaterList.Count; i++) {
            if (callLaterList[i].caller == caller && callLaterList[i].callback.Equals(func)) {
                findExistOne = true;
            }
        }
        if (!findExistOne) {
            callLaterList.Add(TimeHandler.Create(caller, func, arg1, arg2));
        }
    }

    /// <summary>
    /// 延后一帧执行函数（3个参数）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="X"></typeparam>
    /// <typeparam name="Y"></typeparam>
    /// <param name="caller"></param>
    /// <param name="func"></param>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    /// <param name="arg3"></param>
    public void CallLater<T, X, Y>(object caller, Action<T, X, Y> func, T arg1, X arg2, Y arg3) {
        bool findExistOne = false;
        for (int i = 0; i < callLaterList.Count; i++) {
            if (callLaterList[i].caller == caller && callLaterList[i].callback.Equals(func)) {
                findExistOne = true;
            }
        }
        if (!findExistOne) {
            callLaterList.Add(TimeHandler.Create(caller, func, arg1, arg2, arg3));
        }
    }

    /// <summary>
    /// 创建延迟执行函数
    /// </summary>
    /// <param name="caller">调用对象</param>
    /// <param name="delay">延迟时长（单位：毫秒）</param>
    /// <param name="func">函数</param>
    /// <param name="coverBefore">是否覆盖该调用对象对该函数之前创建的延迟执行</param>
    public void Once(object caller, int delay, Action func, bool coverBefore = true) {
        if (delay == 0) {
            func();
            return;
        }
        //先尝试找出之前的TimeHandler
        TimeHandler handler = TryGetHandler(caller, func);
        if (handler == null || !coverBefore) {
            //如果找不到或不覆盖的情况，就必须新创建一个handler
            handler = TimeHandler.Create(caller, func);
            AddHandler(caller, handler);
        }
        handler.nextExecuteTimestep = Mathf.FloorToInt(curTime) + delay;
    }

    public void Once<T>(object caller, int delay, Action<T> func, T arg1, bool coverBefore = false) {
        if (delay == 0) {
            func(arg1);
            return;
        }
        //先尝试找出之前的TimeHandler
        TimeHandler handler = TryGetHandler(caller, func);
        if (handler == null || !coverBefore) {
            //如果找不到或不覆盖的情况，就必须新创建一个handler
            handler = TimeHandler.Create(caller, func, arg1);
            AddHandler(caller, handler);
        }
        handler.nextExecuteTimestep = Mathf.FloorToInt(curTime) + delay;
    }

    public void Once<T, X>(object caller, int delay, Action<T, X> func, T arg1, X arg2, bool coverBefore = false) {
        if (delay == 0) {
            func(arg1, arg2);
            return;
        }
        //先尝试找出之前的TimeHandler
        TimeHandler handler = TryGetHandler(caller, func);
        if (handler == null || !coverBefore) {
            //如果找不到或不覆盖的情况，就必须新创建一个handler
            handler = TimeHandler.Create(caller, func, arg1, arg2);
            AddHandler(caller, handler);
        }
        handler.nextExecuteTimestep = Mathf.FloorToInt(curTime) + delay;
    }

    public void Once<T, X, Y>(object caller, int delay, Action<T, X, Y> func, T arg1, X arg2, Y arg3, bool coverBefore = false) {
        if (delay == 0) {
            func(arg1, arg2, arg3);
            return;
        }
        //先尝试找出之前的TimeHandler
        TimeHandler handler = TryGetHandler(caller, func);
        if (handler == null || !coverBefore) {
            //如果找不到或不覆盖的情况，就必须新创建一个handler
            handler = TimeHandler.Create(caller, func, arg1, arg2, arg3);
            AddHandler(caller, handler);
        }
        handler.nextExecuteTimestep = Mathf.FloorToInt(curTime) + delay;
    }

    /// <summary>
    /// 清理延迟触发函数
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="func"></param>
    public void Clear(object caller, Action func) {
        ClearHandlerByFunc(caller, func);
    }

    /// <summary>
    /// 清理延迟触发函数
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="func"></param>
    public void Clear<T>(object caller, Action<T> func) {
        ClearHandlerByFunc(caller, func);
    }

    /// <summary>
    /// 清理延迟触发函数
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="func"></param>
    public void Clear<T, X>(object caller, Action<T, X> func) {
        ClearHandlerByFunc(caller, func);
    }

    /// <summary>
    /// 清理某对象的某个函数对应的回调
    /// </summary>
    /// <param name="caller">调用对象</param>
    /// <param name="func">函数</param>
    private void ClearHandlerByFunc(object caller, Delegate func) {
        if (handlerMap.ContainsKey(caller)) {
            List<TimeHandler> callbackList = handlerMap[caller];
            for (int i = callbackList.Count - 1; i >= 0; i--) {
                Handler handler = callbackList[i];
                if (handler != null && handler.callback.Equals(func)) {
                    ClearHandler(caller, callbackList[i]);
                    break;
                }
            }
            if (callbackList.Count == 0) { //清理完后如果该caller不再有其他延迟调用的函数，则清除掉key，否则handlerMap会无限增长
                AddCleanCaller(caller);
            }
            //} else {
            //    Debug.LogError("清理回调方法" + func.Method.Name + "时，找不到调用者：" + caller);
        }
    }

    /// <summary>
    /// 清除某个回调
    /// </summary>
    /// <param name="caller">调用对象</param>
    /// <param name="handler">回调handler</param>
    private void ClearHandler(object caller, TimeHandler handler) {
        if (handlerMap.ContainsKey(caller)) {
            List<TimeHandler> handlerList = handlerMap[caller];
            if (handlerList.Contains(handler)) {
                handlerList.Remove(handler);
            }
            //} else {
            //    Debug.LogError("清除回调时，调用者不存在");
        }
    }

    private bool IsHandlerAlive(TimeHandler handler) {
        // 1) 先看 caller
        if (handler.caller is UnityEngine.Object callerObj && callerObj == null) {
            return false; // 已被 Destroy
        }

        // 2) 再看 callback 的 Target（通常是 MonoBehaviour 实例）
        if (handler.callback != null) {
            var target = handler.callback.Target as UnityEngine.Object;
            if (target != null && target == null) {
                return false; // Unity 的假 null，说明也 Destroy 了
            }
        }

        return true;
    }

}
