using System;
using System.Collections.Generic;
using UnityEngine;

public class EventDispatcher {

    //定义一个字典 来存放事件对应的委托列表
    private Dictionary<string, List<Delegate>> eventMap = new Dictionary<string, List<Delegate>>();

    public EventDispatcher() {
    }

    /// <summary>
    /// 根据消息类型获取对应的委托列表
    /// </summary>
    /// <param name="eventType"></param>
    /// <returns></returns>
    private List<Delegate> GetCallBackByEvent(string eventType) {
        List<Delegate> funcList;
        if (eventMap.ContainsKey(eventType)) {
            funcList = eventMap[eventType];
        } else {
            funcList = new List<Delegate>();
            eventMap.Add(eventType, funcList);
        }
        return funcList;
    }

    /// <summary>
    /// 添加监听
    /// </summary>
    /// <arg name="eventType">消息类型</arg>
    /// <arg name="func">触发方法</arg>
    public void On(string eventType, Action func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        funcList.Add(func);

        //当前事件码和委托是否一致
        //如果不一致,是不能绑定在一起的
        //先把事件码传进去,接收值是 Delegate
        //这句代码是先把事件码拿出来
        //Delegate d = eventMap[eventType];
        ////d为空或d 的参数如果和callBack参数不一样
        //if (d != null && d.GetType() != callBack.GetType())
        //{
        //    //抛出异常
        //    throw new Exception(string.Format("尝试为事件{0}添加不同事件的委托,当前事件所对应的委托是{1},要添加的委托类型{2}", eventType, d.GetType(), callBack.GetType()));
        //}
    }

    /// <summary>
    /// 添加监听（带1个参数）
    /// </summary>
    /// <arg name="eventType">消息类型</arg>
    /// <arg name="func">触发方法</arg>
    public void On<T>(string eventType, Action<T> func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        funcList.Add(func);
    }

    /// <summary>
    /// 添加监听（带2个参数）
    /// </summary>
    /// <arg name="eventType">消息类型</arg>
    /// <arg name="func">触发方法</arg>
    public void On<T, X>(string eventType, Action<T, X> func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        funcList.Add(func);
    }

    /// <summary>
    /// 添加监听（带3个参数）
    /// </summary>
    /// <arg name="eventType">消息类型</arg>
    /// <arg name="func">触发方法</arg>
    public void On<T, X, Y>(string eventType, Action<T, X, Y> func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        funcList.Add(func);
    }

    /// <summary>
    /// 添加监听（带4个参数）
    /// </summary>
    /// <arg name="eventType">消息类型</arg>
    /// <arg name="func">触发方法</arg>
    public void On<T, X, Y, Z>(string eventType, Action<T, X, Y, Z> func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        funcList.Add(func);
    }

    /// <summary>
    /// 移除监听
    /// </summary>
    /// <arg name="eventType"></arg>
    /// <arg name="func"></arg>
    public void Off(string eventType, Action func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        if (funcList.IndexOf(func) >= 0) {
            funcList.RemoveAt(funcList.IndexOf(func));
            //} else {
            //    Debug.LogWarning("移除不存在的监听：" + eventType);
        }
    }

    /// <summary>
    /// 移除监听（1个参数）
    /// </summary>
    /// <arg name="eventType"></arg>
    /// <arg name="func"></arg>
    public void Off<T>(string eventType, Action<T> func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        if (funcList.IndexOf(func) >= 0) {
            funcList.RemoveAt(funcList.IndexOf(func));
        } else {
            Debug.LogWarning("移除不存在的监听：" + eventType);
        }
    }

    /// <summary>
    /// 移除监听（2个参数）
    /// </summary>
    /// <arg name="eventType"></arg>
    /// <arg name="func"></arg>
    public void Off<T, X>(string eventType, Action<T, X> func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        if (funcList.IndexOf(func) >= 0) {
            funcList.RemoveAt(funcList.IndexOf(func));
        } else {
            Debug.LogWarning("移除不存在的监听：" + eventType);
        }
    }

    /// <summary>
    /// 移除监听（3个参数）
    /// </summary>
    /// <arg name="eventType"></arg>
    /// <arg name="func"></arg>
    public void Off<T, X, Y>(string eventType, Action<T, X, Y> func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        if (funcList.IndexOf(func) >= 0) {
            funcList.RemoveAt(funcList.IndexOf(func));
        } else {
            Debug.LogWarning("移除不存在的监听：" + eventType);
        }
    }

    /// <summary>
    /// 移除监听（4个参数）
    /// </summary>
    /// <arg name="eventType"></arg>
    /// <arg name="func"></arg>
    public void Off<T, X, Y, Z>(string eventType, Action<T, X, Y, Z> func) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        if (funcList.IndexOf(func) >= 0) {
            funcList.RemoveAt(funcList.IndexOf(func));
        } else {
            Debug.LogWarning("移除不存在的监听：" + eventType);
        }
    }

    public void Dispatch(string eventType) {
        if (eventMap.ContainsKey(eventType)) {
            List<Delegate> funcList = GetCallBackByEvent(eventType);
            for (int i = funcList.Count - 1; i >= 0; i--) {
                (funcList[i] as Action)();
            }
        }
    }

    public void Dispatch<T>(string eventType, T arg1) {
        if (eventMap.ContainsKey(eventType)) {
            List<Delegate> funcList = GetCallBackByEvent(eventType);
            if (funcList != null && funcList.Count > 0) {
                for (int i = funcList.Count - 1; i >= 0; i--) {
                    (funcList[i] as Action<T>)(arg1);
                }
            }
        }
    }

    public void Dispatch<T, X>(string eventType, T arg1, X arg2) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        if (funcList != null && funcList.Count > 0) {
            for (int i = funcList.Count - 1; i >= 0; i--) {
                (funcList[i] as Action<T, X>)(arg1, arg2);
            }
        }
    }

    public void Dispatch<T, X, Y>(string eventType, T arg1, X arg2, Y arg3) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        if (funcList != null && funcList.Count > 0) {
            for (int i = funcList.Count - 1; i >= 0; i--) {
                (funcList[i] as Action<T, X, Y>).Invoke(arg1, arg2, arg3);
            }
        }
    }
    public void Dispatch<T, X, Y, Z>(string eventType, T arg1, X arg2, Y arg3, Z arg4) {
        List<Delegate> funcList = GetCallBackByEvent(eventType);
        if (funcList != null && funcList.Count > 0) {
            for (int i = funcList.Count - 1; i >= 0; i--) {
                (funcList[i] as Action<T, X, Y, Z>).Invoke(arg1, arg2, arg3, arg4);
            }
        }
    }

    /// <summary>
    /// 清理
    /// 会清除所有消息监听
    /// </summary>
    public virtual void Clear() {
        foreach (var key in eventMap.Keys) {
            List<Delegate> list = eventMap[key];
            list.Clear();
        }
        eventMap.Clear();
    }

    /// <summary>
    /// 销毁
    /// </summary>
    public virtual void Destroy() {
        Clear();
    }

}
