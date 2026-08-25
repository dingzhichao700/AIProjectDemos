using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIEventListener : EventTrigger {
    public List<UnityAction> clickActions;
    public List<UnityAction<PointerEventData>> clickActionsWithParam;
    public List<UnityAction<PointerEventData>> downActions;
    public List<UnityAction<PointerEventData>> upActions;
    public List<UnityAction<PointerEventData>> dragActions;
    public List<UnityAction<PointerEventData>> beginDragActions;
    public List<UnityAction<PointerEventData>> endDragActions;
    public List<UnityAction<PointerEventData>> scrollActions;
    public List<UnityAction<PointerEventData>> enterActions;
    public List<UnityAction<PointerEventData>> exitActions;

    private ScrollRect parentScrollRect;

    public override void OnPointerClick(PointerEventData eventData) {
        base.OnPointerClick(eventData);
        if (clickActions != null) {
            for (int i = 0; i < clickActions.Count; i++) {
                clickActions[i].Invoke();
            }
        }
        if (clickActionsWithParam != null) {
            for (int i = 0; i < clickActionsWithParam.Count; i++) {
                clickActionsWithParam[i].Invoke(eventData);
            }
        }
    }

    public override void OnPointerDown(PointerEventData eventData) {
        base.OnPointerDown(eventData);
        if (downActions != null) {
            for (int i = 0; i < downActions.Count; i++) {
                downActions[i].Invoke(eventData);
            }
        }
    }

    public override void OnPointerUp(PointerEventData eventData) {
        base.OnPointerUp(eventData);
        if (upActions != null) {
            for (int i = 0; i < upActions.Count; i++) {
                upActions[i].Invoke(eventData);
            }
        }
    }

    public override void OnBeginDrag(PointerEventData eventData) {
        //base.OnBeginDrag(eventData);
        if (beginDragActions != null) {
            for (int i = 0; i < beginDragActions.Count; i++) {
                beginDragActions[i].Invoke(eventData);
            }
        }
        if (!parentScrollRect)
            parentScrollRect = GetComponentInParent<ScrollRect>();

        if (parentScrollRect)
            parentScrollRect.OnBeginDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData) {
        base.OnEndDrag(eventData);
        if (endDragActions != null) {
            for (int i = 0; i < endDragActions.Count; i++) {
                endDragActions[i].Invoke(eventData);
            }
        }
        if (!parentScrollRect)
            parentScrollRect = GetComponentInParent<ScrollRect>();

        if (parentScrollRect)
            parentScrollRect.OnEndDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData) {
        base.OnDrag(eventData);
        if (dragActions != null) {
            for (int i = 0; i < dragActions.Count; i++) {
                dragActions[i].Invoke(eventData);
            }
        } else {
            //当前对象没有对拖动建立监听，就把拖动事件透传一下，往往是按钮被嵌套在ScrollList中需要用到，否则拖不动列表
            PassEvent(eventData, ExecuteEvents.dragHandler, this.gameObject.transform);
        }
    }

    public override void OnScroll(PointerEventData eventData) {
        base.OnScroll(eventData);
        if (scrollActions != null) {
            for (int i = 0; i < scrollActions.Count; i++) {
                scrollActions[i].Invoke(eventData);
            }
        }
    }

    /**进入*/
    public override void OnPointerEnter(PointerEventData eventData) {
        base.OnPointerEnter(eventData);
        if (enterActions != null) {
            for (int i = 0; i < enterActions.Count; i++) {
                enterActions[i].Invoke(eventData);
            }
        }
    }

    /**离开*/
    public override void OnPointerExit(PointerEventData eventData) {
        base.OnPointerEnter(eventData);
        if (exitActions != null) {
            for (int i = 0; i < exitActions.Count; i++) {
                exitActions[i].Invoke(eventData);
            }
        }

    }

    /**添加点击监听*/
    public void AddClickAction(UnityAction action) {
        if (clickActions == null) {
            clickActions = new List<UnityAction>();
        }
        if (!clickActions.Contains(action)) {
            clickActions.Add(action);
        }
    }

    /**移除点击监听*/
    public void RemoveClickAction(UnityAction action) {
        if (clickActions != null) {
            if (clickActions.Contains(action)) {
                clickActions.Remove(action);
            }
        }
    }

    /**添加点击监听（带返回参数的）*/
    public void AddClickAction(UnityAction<PointerEventData> action) {
        if (clickActionsWithParam == null) {
            clickActionsWithParam = new List<UnityAction<PointerEventData>>();
        }
        if (!clickActionsWithParam.Contains(action)) {
            clickActionsWithParam.Add(action);
        }
    }

    /**移除点击监听*/
    public void RemoveClickAction(UnityAction<PointerEventData> action) {
        if (clickActionsWithParam != null) {
            if (clickActionsWithParam.Contains(action)) {
                clickActionsWithParam.Remove(action);
            }
        }
    }

    /**添加按下监听*/
    public void AddDownAction(UnityAction<PointerEventData> action) {
        if (downActions == null) {
            downActions = new List<UnityAction<PointerEventData>>();
        }
        if (!downActions.Contains(action)) {
            downActions.Add(action);
        }
    }

    /**移除按下监听*/
    public void RemoveDownAction(UnityAction<PointerEventData> action) {
        if (downActions != null) {
            if (downActions.Contains(action)) {
                downActions.Remove(action);
            }
        }
    }

    /**添加松开监听*/
    public void AddUpAction(UnityAction<PointerEventData> action) {
        if (upActions == null) {
            upActions = new List<UnityAction<PointerEventData>>();
        }
        if (!upActions.Contains(action)) {
            upActions.Add(action);
        }
    }

    /**移除松开监听*/
    public void RemoveUpAction(UnityAction<PointerEventData> action) {
        if (upActions != null) {
            if (upActions.Contains(action)) {
                upActions.Remove(action);
            }
        }
    }

    /**添加拖动监听*/
    public void AddDragAction(UnityAction<PointerEventData> action) {
        if (dragActions == null) {
            dragActions = new List<UnityAction<PointerEventData>>();
        }
        if (!dragActions.Contains(action)) {
            dragActions.Add(action);
        }
    }

    /**移除拖动监听*/
    public void RemoveDragAction(UnityAction<PointerEventData> action) {
        if (dragActions != null) {
            if (dragActions.Contains(action)) {
                dragActions.Remove(action);
            }
        }
    }

    /**添加开始拖动监听*/
    public void AddBeginDragAction(UnityAction<PointerEventData> action) {
        if (beginDragActions == null) {
            beginDragActions = new List<UnityAction<PointerEventData>>();
        }
        if (!beginDragActions.Contains(action)) {
            beginDragActions.Add(action);
        }
    }

    /**移除开始拖动监听*/
    public void RemoveBeginDragAction(UnityAction<PointerEventData> action) {
        if (beginDragActions != null) {
            if (beginDragActions.Contains(action)) {
                beginDragActions.Remove(action);
            }
        }
    }

    /**添加结束拖动监听*/
    public void AddEndDragAction(UnityAction<PointerEventData> action) {
        if (endDragActions == null) {
            endDragActions = new List<UnityAction<PointerEventData>>();
        }
        if (!endDragActions.Contains(action)) {
            endDragActions.Add(action);
        }
    }

    /**移除结束拖动监听*/
    public void RemoveEndDragAction(UnityAction<PointerEventData> action) {
        if (endDragActions != null) {
            if (endDragActions.Contains(action)) {
                endDragActions.Remove(action);
            }
        }
    }

    /**添加滚动监听*/
    public void AddScrollAction(UnityAction<PointerEventData> action) {
        if (scrollActions == null) {
            scrollActions = new List<UnityAction<PointerEventData>>();
        }
        if (!scrollActions.Contains(action)) {
            scrollActions.Add(action);
        }
    }

    /**移除滚动监听*/
    public void RemoveScrollAction(UnityAction<PointerEventData> action) {
        if (scrollActions != null) {
            if (scrollActions.Contains(action)) {
                scrollActions.Remove(action);
            }
        }
    }

    /**添加进入监听*/
    public void AddEnterAction(UnityAction<PointerEventData> action) {
        if (enterActions == null) {
            enterActions = new List<UnityAction<PointerEventData>>();
        }
        if (!enterActions.Contains(action)) {
            enterActions.Add(action);
        }
    }

    /**移除进入监听*/
    public void RemoveEnterAction(UnityAction<PointerEventData> action) {
        if (enterActions != null) {
            if (enterActions.Contains(action)) {
                enterActions.Remove(action);
            }
        }
    }

    /**添加离开监听*/
    public void AddExitAction(UnityAction<PointerEventData> action) {
        if (exitActions == null) {
            exitActions = new List<UnityAction<PointerEventData>>();
        }
        if (!exitActions.Contains(action)) {
            exitActions.Add(action);
        }
    }

    /**移除离开监听*/
    public void RemoveExitAction(UnityAction<PointerEventData> action) {
        if (exitActions != null) {
            if (exitActions.Contains(action)) {
                exitActions.Remove(action);
            }
        }
    }

    /// <summary>
    /// 透传消息
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">鼠标操作消息</param>
    /// <param name="function">执行函数</param>
    /// <param name="current"></param>
    private void PassEvent<T>(PointerEventData data, ExecuteEvents.EventFunction<T> function, Transform current)
        where T : IEventSystemHandler {
        GameObject nextGo = ExecuteEvents.GetEventHandler<T>(current.parent.gameObject);
        //Debug.Log(current.name + "  " + nextGo.name);
        ExecuteEvents.Execute(nextGo, data, function);
    }

    public static UIEventListener GetListener(GameObject go) {
        UIEventListener listener = go.GetComponent<UIEventListener>();
        //获取gameobject
        if (listener == null)
            listener = go.AddComponent<UIEventListener>();

        return listener;
    }
}

