using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 高于普通窗口和确认框的全局滚动提示层。
/// </summary>
/// <remarks>常驻显示宿主，不占页面布局或输入焦点；可见数与条目池容量跟随全局提示常量。</remarks>
public sealed class GlobalToastPanel : BasePanel {

    /******************* UIComponent Define begin ************************/
    public RectTransform boxMessages;
    public RectTransform itemTemplate;
    /******************* UIComponent Define finish ************************/

    private readonly List<GlobalToastItem> active = new List<GlobalToastItem>();

    private readonly Stack<GlobalToastItem> pool = new Stack<GlobalToastItem>();

    public int ActiveCount => active.Count;

    public int PoolCount => pool.Count;

    public IReadOnlyList<GlobalToastItem> ActiveItems => active;

    public override bool KeepOpenOnCloseAll => true;

    public override bool ReceivesPanelInput => false;

    public GlobalToastPanel() {
        layer = PanelLayer.SCALE_TOAST;
    }

    public override void OnOpen() {
        GlobalToast.Attach(this);
        RookieEngine.timer.AddUpdateListener(this, Advance);
        Pump();
    }

    public override void OnClose() {
        RookieEngine.timer.RemoveUpdateListener(this, Advance);
        ResetItems();
        GlobalToast.Detach(this);
    }

    private void OnDestroy() {
        RookieEngine.timer.RemoveUpdateListener(this, Advance);
        GlobalToast.Detach(this);
    }

    public bool Contains(string message, bool warning) {
        foreach (var item in active) {
            if (item.Message == message && item.Warning == warning) {
                return true;
            }
        }
        return false;
    }

    /**打开和普通页面切换共享同一宿主；从在途队列按顺序填充可见槽位。*/
    public void Pump() {
        int limit = GlobalToast.MaxVisible;
        // 配置重载缩小容量时先回收最旧条目，保持新条在下的顺序。
        while (active.Count > limit) {
            var oldest = active[0];
            active.RemoveAt(0);
            oldest.ResetForPool();
            pool.Push(oldest);
        }
        TrimPool(limit);
        while (active.Count < limit && GlobalToast.TryTake(out string text, out bool warning)) {
            GlobalToastItem item;
            if (pool.Count > 0) {
                item = pool.Pop();
                item.gameObject.SetActive(true);
            } else {
                var go = Instantiate(itemTemplate.gameObject, boxMessages, false);
                go.name = "toastItem";
                go.SetActive(true);
                item = go.GetComponent<GlobalToastItem>();
            }
            item.Present(text, warning);
            active.Add(item);
        }
        LayoutItems(0);
    }

    private void LayoutItems(float seconds) {
        for (int i = 0; i < active.Count; i++) {
            float maximumY = i == 0 ? float.PositiveInfinity : active[i - 1].Trans.anchoredPosition.y - 56;
            active[i].Advance(seconds, 120 + (active.Count - 1 - i) * 56, maximumY);
        }
    }

    private void Advance(float seconds) {
        LayoutItems(seconds);
        for (int i = active.Count - 1; i >= 0; i--) {
            if (active[i].Age >= GlobalToast.Lifetime) {
                var item = active[i];
                active.RemoveAt(i);
                item.ResetForPool();
                pool.Push(item);
            }
        }
        Pump();
    }

    public void ResetItems() {
        foreach (var item in active) {
            item.ResetForPool();
            pool.Push(item);
        }
        active.Clear();
        TrimPool(GlobalToast.MaxVisible);
    }

    /**容量调整不让复用池持续保留超过当前可见上限的对象。*/
    private void TrimPool(int limit) {
        while (pool.Count > limit) {
            Destroy(pool.Pop().gameObject);
        }
    }

}
