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
    /// <remarks>窗口标识由业务模块定义</remarks>
    public string panelEnum;

    /// <summary>由具体窗口决定是否在批量关闭时保留。</summary>
    public virtual bool KeepOpenOnCloseAll => false;

    /**实例打开前的最终守卫，异步加载完成后再次检查。*/
    public virtual bool CanOpen => true;

    /**无交互的表现宿主可退出窗口键盘焦点竞争；默认保留原有窗口行为。*/
    public virtual bool ReceivesPanelInput => true;

    /**显式选择全屏舞台，默认保持原有设计区适配。*/
    public virtual bool UsesFullScreenViewport => false;

    /**完整面板默认等比容纳安全区；场景及自布局窗口显式接管。*/
    public virtual bool FitContentToSafeArea => true;

    /**仅明确声明的背景保留在原根节点，避免按名称猜测装饰图用途。*/
    public virtual string[] FullBleedChildren => System.Array.Empty<string>();

    /**完整界面沿用项目设计基准；局部弹窗显式声明自身内容尺寸。*/
    public virtual Vector2 SafeContentDesignSize => PanelMgr.ins.DesignSize;

    /**内容在可用安全区中的对齐，默认居中；完整页面可显式选择靠底。*/
    public virtual Vector2 SafeContentAlignment => new Vector2(.5f, .5f);

    /**全屏页保持false；局部窗口显式选择统一过渡。*/
    public virtual bool UsesWindowTransition => false;
    protected virtual RectTransform WindowTransitionContent => null;
    protected virtual UnityEngine.UI.Image WindowTransitionMask => null;
    protected virtual void OnPrepareOpen() { }
    private PanelWindowMotion windowMotion;
    private bool closeCompleted;
    private PanelSafeContent safeContent;

    public RectTransform SafeContentRoot => safeContent != null ? safeContent.content : trans;

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
        closeCompleted = false;
        if (openAudioId != 0) {
            AudioManager.ins.PlaySoundById(AudioBusType.HINT, openAudioId);
        }
        if (FitContentToSafeArea) {
            if (safeContent == null) {
                safeContent = gameObject.AddComponent<PanelSafeContent>();
            }
            safeContent.Initialize(FullBleedChildren, SafeContentDesignSize, SafeContentAlignment);
        }
        if (UsesWindowTransition) {
            if (windowMotion == null) {
                windowMotion = gameObject.AddComponent<PanelWindowMotion>();
            }
            windowMotion.Configure(this, WindowTransitionContent, WindowTransitionMask);
        }
        OnPrepareOpen();
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
        if (UsesWindowTransition) {
            windowMotion.Open(PlayOpenComplete);
        } else {
            PlayOpenComplete();
        }
    }

    /**打开表现播放完成*/
    protected void PlayOpenComplete() {
        if (!isOpening) {
            return;
        }
        state = PanelState.STATE_OPEN;
        OnOpen();
        OnPostOpen();
    }

    public virtual void OnOpen() { }

    /**OnOpen执行结束后调度*/
    public virtual void OnPostOpen() { }

    public virtual void Close() {
        if (isClosing) {
            return;
        }
        PanelMgr.ins.ClosePanel(this);
        state = PanelState.STATE_CLOSING;
        PanelMgr.ins.SortAllPanel();
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
        if (UsesWindowTransition && windowMotion != null) {
            windowMotion.Close(PlayCloseComplete);
        } else {
            PlayCloseComplete();
        }
    }

    /**打开表现播放完成*/
    protected void PlayCloseComplete() {
        if (!isClosing || closeCompleted) {
            return;
        }
        closeCompleted = true;
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
