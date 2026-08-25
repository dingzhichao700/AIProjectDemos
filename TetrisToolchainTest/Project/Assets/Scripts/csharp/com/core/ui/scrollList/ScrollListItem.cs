/// <summary>
/// 滑动列表item基类
/// </summary>
public class ScrollListItem : BaseView {

    protected int listIndex;
    protected object data;

    /**初始化完成接口*/
    override public void OnInit() {
        OnInitListItem();
        if (data != null) {
            OnSetData(listIndex, data);
        }
    }

    protected virtual void OnInitListItem() { }

    /// <summary>
    /// 设置数据
    /// </summary>
    /// <param name="index">在列表中的序号</param>
    /// <param name="value">数据</param>
    public void SetData(int index, object value) {
        this.listIndex = index;
        this.data = value;
        if (isInit) {
            OnSetData(index, data);
        }
    }

    /**数据设置后调度，给上层复写*/
    protected virtual void OnSetData(int index, object value) { }

}
