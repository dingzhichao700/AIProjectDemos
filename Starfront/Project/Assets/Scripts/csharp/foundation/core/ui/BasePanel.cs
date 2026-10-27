using UnityEngine;

/// <summary>
/// 窗口状态枚举
/// </summary>
public enum PanelState {
    /**正在打开*/
    STATE_OPENING,
    /**已打开*/
    STATE_OPEN,
    /**正在关闭*/
    STATE_CLOSING,
}

public class BasePanel : BaseView {

    /// <summary>
    /// 窗口枚举
    /// </summary>
    /// <remarks>参考UIEnum</remarks>
    public string panelEnum;

    /**窗口层级，参考PanelLayer*/
    public int layer;
    /**打开音效id*/
    protected int openAudioId;
    /**关闭音效id*/
    protected int closeAudioId;

    public RectTransform trans => (transform as RectTransform);

    public object[] openParams;

    /**状态*/
    private PanelState state;

    public void Open() {
        transform.localPosition = Vector3.zero;
        state = PanelState.STATE_OPENING;
        if (openAudioId != 0) {
            AudioManager.ins.PlaySoundById(AudioBusType.HINT, openAudioId);
        }
        PlayOpen();
    }

    /// <summary>
    /// 播放打开表现
    /// </summary>
    /// <remarks>
    /// 上层表现打开动画的话，就复写这个接口，并自行调用PlayOpenComplete
    /// 否则底层这里会直接调用PlayOpenComplete，当做打开流程已经走完
    /// </remarks>
    protected virtual void PlayOpen() {
        PlayOpenComplete();
    }

    /**打开表现播放完成*/
    protected void PlayOpenComplete() {
        state = PanelState.STATE_OPEN;
        OnOpen();
        OnPostOpen();
    }

    public virtual void OnOpen() { }

    /**OnOpen执行结束后调度*/
    public virtual void OnPostOpen() { }

    public virtual void Close() {
        PanelMgr.ins.ClosePanel(this);
        state = PanelState.STATE_CLOSING;
        if (closeAudioId != 0) {
            AudioManager.ins.PlaySoundById(AudioBusType.HINT, closeAudioId);
        }
        PlayClose();
    }

    /// <summary>
    /// 播放关闭表现
    /// </summary>
    /// <remarks>
    /// 上层表现关闭动画的话，就复写这个接口，并自行调用PlayCloseComplete
    /// 否则底层这里会直接调用PlayCloseComplete，当做关闭流程已经走完
    /// </remarks>
    protected virtual void PlayClose() {
        PlayCloseComplete();
    }

    /**打开表现播放完成*/
    protected void PlayCloseComplete() {
        OnClose();
        PanelMgr.ins.OnPanelCloseComplete(this);
    }

    public virtual void OnClose() { }

    /**窗口操作(必须是public)*/
    public virtual void OnPanelOperate(PanelOperateEnum operateCode) { }

    /**是否正在打开*/
    public bool isOpening => state == PanelState.STATE_OPENING;
    /**是否已打开*/
    public bool isOpened => state == PanelState.STATE_OPEN;
    /**是否正在打开*/
    public bool isClosing => state == PanelState.STATE_CLOSING;

}
