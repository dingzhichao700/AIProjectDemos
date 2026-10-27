using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 自定义视图基类
/// </summary>
public class BaseView : MonoBehaviour {

    /**是否初始化完成*/
    public bool isInit = false;

    private RectTransform trans;

    public RectTransform Trans {
        get {
            if (!trans) {
                trans = GetComponent<RectTransform>();
            }
            return trans;
        }
    }

    /**初始化完成接口*/
    public virtual void OnInit() { }

    /******************************************************************************监听 开始******************************************************************************/

    /**添加点击监听*/
    public void OnClick(GameObject go, UnityAction action) {
        UIEventListener.GetListener(go).AddClickAction(action);
    }

    /**移除点击监听*/
    public void OffClick(GameObject go, UnityAction action) {
        UIEventListener.GetListener(go).RemoveClickAction(action);
    }

    /**添加点击监听(带参数)*/
    public void OnClick(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).AddClickAction(action);
    }

    /**移除点击监听(带参数)*/
    public void OffClick(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).RemoveClickAction(action);
    }

    /**添加按下监听*/
    public void OnDown(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).AddDownAction(action);
    }

    /**移除按下监听*/
    public void OffDown(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).RemoveDownAction(action);
    }

    /**添加松开监听*/
    public void OnUp(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).AddUpAction(action);
    }

    /**移除按下监听*/
    public void OffUp(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).RemoveUpAction(action);
    }

    /**添加拖动监听*/
    public void OnDrag(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).AddDragAction(action);
    }

    /**移除拖动监听*/
    public void OffDrag(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).RemoveDragAction(action);
    }

    /**添加开始拖动监听*/
    public void OnBeginDrag(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).AddBeginDragAction(action);
    }

    /**移除开始拖动监听*/
    public void OffBeginDrag(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).RemoveBeginDragAction(action);
    }

    /**添加结束拖动监听*/
    public void OnEndDrag(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).AddEndDragAction(action);
    }

    /**移除结束拖动监听*/
    public void OffEndDrag(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).RemoveEndDragAction(action);
    }

    /**添加滚动监听*/
    public void OnScroll(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).AddScrollAction(action);
    }

    /**移除滚动监听*/
    public void OffScroll(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).RemoveScrollAction(action);
    }

    /**添加进入监听*/
    public void OnEnter(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).AddEnterAction(action);
    }

    /**移除进入监听*/
    public void OffEnter(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).RemoveEnterAction(action);
    }

    /**添加离开监听*/
    public void OnExit(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).AddExitAction(action);
    }

    /**移除离开监听*/
    public void OffExit(GameObject go, UnityAction<PointerEventData> action) {
        UIEventListener.GetListener(go).RemoveExitAction(action);
    }
    /******************************************************************************监听 结束******************************************************************************/

    protected GraphicRaycaster raycaster => FindObjectOfType<GraphicRaycaster>();

    /// <summary>
    /// 在某个容器中播一个一次性特效
    /// </summary>
    /// <param name="transform">特效容器</param>
    /// <param name="path">特效路径</param>
    /// <param name="pos">位置</param>
    /// <param name="scale">缩放</param>
    /// <param name="handler">播放完成回调</param>
    /// <param name="timerType">使用的计时器类型</param>
    protected void PlayOnceEffect(RectTransform transform, string path, Vector2 pos, float scale = 1, Handler handler = null, TimerType timerType = TimerType.COMMON) {
        FrameAnimationView effectView = FrameAnimationView.GetInstance();
        effectView.trans.SetParent(transform);
        effectView.trans.anchoredPosition = pos;
        effectView.Play(path, false, handler, true, scale, 1, 1, timerType);
    }

    /**清理函数（给子类实现）*/
    public virtual void Clear() {}

    /**销毁函数（给子类实现）*/
    public virtual void Destroy() {
        Clear();
    }

}
