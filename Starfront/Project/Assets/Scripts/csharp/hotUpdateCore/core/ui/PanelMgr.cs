using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 窗口管理
/// </summary>
public class PanelMgr : EventDispatcher {

    /// <summary>
    /// 缩放层Canvas
    /// </summary>
    public RectTransform viewportScale;

    /// <summary>
    /// 安全区域节点，用于需要避开刘海和系统手势区域的界面。
    /// </summary>
    public RectTransform safeAreaRoot;

    /// <summary>
    /// UI对象池节点
    /// </summary>
    public RectTransform uiPool;

    /**舞台宽度*/
    public int stageWidth;
    /**舞台高度*/
    public int stageHeight;

    /**窗口实际像素宽度比舞台像素的比例*/
    public float globalWidthRatio;
    /**窗口实际像素高度比舞台像素的比例*/
    public float globalHeightRatio;

    /**正在加载中的窗口列表*/
    private List<string> loadingPanelList = new List<string>();
    /**已打开的窗口字典<业务窗口标识,BasePanel>*/
    private Dictionary<string, BasePanel> openPanelMap = new Dictionary<string, BasePanel>();

    /**已经打开的常规窗口列表*/
    private List<BasePanel> openPanelInsList = new List<BasePanel>();
    /**请求版本使关闭或配置失效后的迟到加载不再打开。*/
    private readonly Dictionary<string, int> openVersions = new Dictionary<string, int>();

    private readonly Dictionary<BasePanel, long> drawOrder = new Dictionary<BasePanel, long>();

    private long nextDrawOrder;

    private int tipRequestVersion;

    /**普通交互窗口替换时使在途Tips请求失效，不受纯表现层与GM入口刷新影响。*/
    public int InteractiveWindowRevision { get; private set; }

    public event Action PanelsChanged;

    /**关闭动画仍参与遮挡，加载期间不能操作工具入口。*/
    public bool IsLoading => openPanelInsList.Exists(panel => panel.layer == PanelLayer.SCALE_LOADING);


    /**最顶层的窗口*/
    private BasePanel topPanel;

    private PointerEventData pointerEventData;
    private EventSystem eventSystem;

    private readonly Vector3[] viewportCorners = new Vector3[4];

    /**舞台在屏幕上的实际位置，用于窗口尺寸变化与输入换算*/
    private Rect stageScreenRect;

    private static PanelMgr _ins;
    public static PanelMgr ins {
        get {
            if (_ins == null) {
                _ins = new PanelMgr();
            }
            return _ins;
        }
    }

    public PanelMgr() {
    }

    public void Init() {
        eventSystem = EventSystem.current;
        KeyBoardControl.ins.OnAnyKeyDown(OnKeyDown);
    }

    /**处理当前窗口的操作按键操作相应*/
    private void OnKeyDown(KeyCode key) {
        if (topPanel != null) {
            PanelOperateEnum result = 0;
            switch (key) {
                case KeyCode.UpArrow:
                    result = PanelOperateEnum.Up;
                    break;
                case KeyCode.DownArrow:
                    result = PanelOperateEnum.Down;
                    break;
                case KeyCode.LeftArrow:
                    result = PanelOperateEnum.Left;
                    break;
                case KeyCode.RightArrow:
                    result = PanelOperateEnum.Right;
                    break;
                case KeyCode.Escape:
                    result = PanelOperateEnum.ESC;
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    result = PanelOperateEnum.SURE;
                    break;
                case KeyCode.Delete:
                    result = PanelOperateEnum.DELETE;
                    break;
                case KeyCode.O:
                    result = PanelOperateEnum.SWITCH_OBSTALCE_VISIBLE;
                    break;
            }
            if (result != 0) {
                if (topPanel.isOpened) {//正在关闭的话，不响应操作
                    topPanel.OnPanelOperate(result);
                }
            }
        }
    }

    public void Update() {
        int width = (int)viewportScale.rect.width;
        int height = (int)viewportScale.rect.height;
        viewportScale.GetWorldCorners(viewportCorners);
        Canvas canvas = viewportScale.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, viewportCorners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, viewportCorners[2]);
        Rect screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        if (stageWidth != width || stageHeight != height || stageScreenRect != screenRect) {
            stageWidth = width;
            stageHeight = height;
            stageScreenRect = screenRect;
            Debug.Log("stage size changed:" + stageWidth + "x" + stageHeight + ", windowSize:" + Screen.width + "x" + Screen.height + ", fullScreen:" + Screen.fullScreen);
            globalWidthRatio = screenRect.width > 0f ? stageWidth / screenRect.width : 0f;
            globalHeightRatio = screenRect.height > 0f ? stageHeight / screenRect.height : 0f;
            Dispatch(PanelEvent.WINDOW_RESIZE);
        }

        // 这里只负责现有Tips的外部关闭；具体入口切换及点击消费由后继消费者实现。
        if (Input.GetMouseButtonDown(0)) {
            BasePanel tip = openPanelInsList.FindLast(panel => panel is BaseTipPanel && !panel.isClosing);
            if (tip != null && tip.isOpened && eventSystem != null) {
                pointerEventData = new PointerEventData(eventSystem) { position = Input.mousePosition };
                var results = new List<RaycastResult>();
                eventSystem.RaycastAll(pointerEventData, results);
                if (!results.Exists(result => result.gameObject.transform.IsChildOf(tip.transform))) {
                    tip.Close();
                }
            }
        }
    }

    /**由项目视窗配置注入，在打开任何窗口前初始化。*/
    public Vector2 DesignSize { get; private set; }

    /**设置UI节点*/
    public void SetUIRoot(RectTransform viewportScale, RectTransform safeAreaRoot, RectTransform uiPool, Vector2 designSize) {
        DesignSize = designSize;
        this.viewportScale = viewportScale;
        this.safeAreaRoot = safeAreaRoot;
        this.uiPool = uiPool;
    }

    /**二象性操作一个窗口（已打开了就关闭，已关闭了就打开）*/
    public void OperatePanelDuality(string panelEnum) {
        BasePanel panel = GetPanel(panelEnum);
        if (panel != null) {
            if (panel.isOpening || panel.isOpened) {
                panel.Close();
            } else if (panel.isClosing) {
                OpenPanelInstance(panel, null, Rect.zero);
            }
        } else {
            OpenPanel(panelEnum);
        }
    }

    /// <summary>
    /// 根据窗口类型打开窗口
    /// </summary>
    /// <param name="panelEnum">窗口枚举</param>
    /// <param name="openParams">打开时传入的参数</param>
    /// <param name="fromStageRect">从舞台哪个区域打开</param>
    public void OpenPanel(string panelEnum, dynamic[] openParams = null, Rect fromStageRect = default) {
        OpenPanelRequest(panelEnum, openParams, fromStageRect, -1);
    }

    /**Tip请求使用全局版本，异源慢请求不能覆盖后来请求。*/
    private void OpenPanelRequest(string panelEnum, dynamic[] openParams, Rect fromStageRect, int tipVersion) {
        if (tipVersion >= 0 && tipVersion != tipRequestVersion) {
            return;
        }
        if (loadingPanelList.IndexOf(panelEnum) >= 0) {
            Debug.LogWarning("正在打开中，请勿重复打开窗口：" + panelEnum);
            return;
        }
        if (IsPanelOpen(panelEnum)) {
            Debug.LogWarning("窗口已打开，请勿重复打开窗口：" + panelEnum);
            return;
        }

        BasePanel retiring = openPanelInsList.Find(panel => panel.panelEnum == panelEnum && panel.isClosing);
        if (retiring != null) {
            int retryVersion = NextOpenVersion(panelEnum);
            void Retry() {
                if (openVersions[panelEnum] != retryVersion) {
                    return;
                }
                if (IsPanelVisible(panelEnum)) {
                    RookieEngine.timer.CallLater(this, Retry);
                    return;
                }
                OpenPanelRequest(panelEnum, openParams, fromStageRect, tipVersion);
            }
            RookieEngine.timer.CallLater(this, Retry);
            return;
        }
        int version = NextOpenVersion(panelEnum);
        BasePanel panelFromPool = GetPanelInsFromPool(panelEnum);
        if (panelFromPool != null) {
            OpenPanelInstance(panelFromPool, openParams, fromStageRect);
        } else {
            loadingPanelList.Add(panelEnum);
            UIFactory.LoadUIPrefab(panelEnum, (string fullPath) => {
                loadingPanelList.Remove(panelEnum);
                if (openVersions[panelEnum] != version || (tipVersion >= 0 && tipVersion != tipRequestVersion)) {
                    return;
                }

                BasePanel panelIns = UIFactory.GetUIInstance(panelEnum) as BasePanel;
                if (panelIns == null) {
                    Debug.LogError("打开窗口失败，Prefab 根节点缺少有效的 BasePanel/UIBinder：" + panelEnum +
                                   "，资源路径：" + ResourcePresentation.Policy.ResolveUI(panelEnum));
                    return;
                }
                panelIns.panelEnum = panelEnum;
                OpenPanelInstance(panelIns, openParams, fromStageRect);
            }, exception => {
                loadingPanelList.Remove(panelEnum);
                Debug.LogError("窗口加载失败：" + panelEnum + "，" + exception);
            });
        }

    }

    /// <summary>
    /// 打开Tip
    /// </summary>
    /// <param name="panelEnum">窗口枚举</param>
    /// <param name="sourceGo">点击哪个gameObject打开Tip</param>
    /// <param name="openParams">窗口打开参数</param>
    public void OpenTip(string panelEnum, GameObject sourceGo, dynamic[] openParams = null) {
        if (sourceGo == null || !(sourceGo.transform is RectTransform)) {
            return;
        }
        RectTransform sourceRect = sourceGo.transform as RectTransform;
        // 将来源控件的真实边界换算到提示层，包含竖屏容器的缩放与居中偏移。
        sourceRect.GetWorldCorners(viewportCorners);
        Vector3 topLeft = viewportScale.InverseTransformPoint(viewportCorners[1]);
        Vector3 bottomRight = viewportScale.InverseTransformPoint(viewportCorners[3]);
        Rect btnPosRect = new Rect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
        //Debug.Log("按钮的全局位置: " + btnPosRect);

        OpenPanelRequest(panelEnum, openParams, btnPosRect, ++tipRequestVersion);
    }

    /**打开窗口实例*/
    private void OpenPanelInstance(BasePanel panel, dynamic[] openParams, Rect fromStageRect) {
        if (!panel.CanOpen) {
            UIFactory.Recover(panel.panelEnum, panel);
            return;
        }
        if (panel.layer != PanelLayer.SCALE_TIP && panel.ReceivesPanelInput) {
            InteractiveWindowRevision++;
            tipRequestVersion++;
            foreach (BasePanel tip in openPanelInsList.ToArray()) {
                if (tip is BaseTipPanel && !tip.isClosing) {
                    tip.Close();
                }
            }
        }
        ClosePanelByLayer(panel.layer);
        if (!openPanelMap.ContainsKey(panel.panelEnum)) {
            openPanelMap.Add(panel.panelEnum, panel);
            openPanelInsList.Add(panel);
            drawOrder[panel] = ++nextDrawOrder;
            Transform parentTrans = viewportScale.transform;
            panel.transform.SetParent(parentTrans);
            panel.transform.localScale = new Vector3(1, 1, 1);
            panel.openParams = openParams;
            BaseTipPanel tipPanel = panel.GetComponent<BaseTipPanel>();
            if (tipPanel != null) {
                tipPanel.sourceStageRect = fromStageRect;
            } else {
                panel.transform.localPosition = new Vector3(0, 0, 0);
            }
            RefreshViewportMode();
            panel.Open();
            SortAllPanel();
        } else {
            Debug.LogError("重复打开窗口：" + panel.panelEnum);
        }
    }

    /**获取窗口实例*/
    public BasePanel GetPanel(string panelEnum) {
        openPanelMap.TryGetValue(panelEnum, out BasePanel panel);
        return panel;
    }

    /**获取某个层级的窗口实例*/
    public BasePanel GetPanelByLayer(int layer) {
        foreach (BasePanel panel in openPanelInsList) {
            if (panel.layer == layer) {
                return panel;
            }
        }
        return null;
    }

    /**根据窗口类型关闭窗口*/
    public void ClosePanelByType(string panelEnum) {
        NextOpenVersion(panelEnum);
        BasePanel panel = GetPanel(panelEnum);
        if (panel != null) {
            panel.Close();
        }
    }

    /**某窗口是否已打开*/
    public bool IsPanelOpen(string panelEnum) {
        return openPanelMap.ContainsKey(panelEnum);
    }

    /**关闭所有界面(加载界面除外)*/
    public void CloseAllPanel() {
        foreach (BasePanel panel in openPanelInsList.ToArray()) {
            if (!panel.KeepOpenOnCloseAll) {
                RookieEngine.timer.CallLater(this, () => { 
                    panel.Close();
                });
            }
        }
    }

    /**关闭某个层级的窗口*/
    public void ClosePanelByLayer(int layer) {
        foreach (BasePanel panel in openPanelInsList.ToArray()) {
            if (panel.layer == layer && !panel.isClosing) {
                panel.Close();
            }
        }
    }

    /**关闭只撤销业务打开状态；退场完成前保留绘制与焦点屏障。*/
    public void ClosePanel(BasePanel panel) {
        if (panel != null && openPanelMap.TryGetValue(panel.panelEnum, out BasePanel current) && current == panel) {
            openPanelMap.Remove(panel.panelEnum);
            NextOpenVersion(panel.panelEnum);
        }
    }

    private int NextOpenVersion(string panelEnum) {
        openVersions.TryGetValue(panelEnum, out int value);
        openVersions[panelEnum] = value + 1;
        return value + 1;
    }

    public void OnPanelCloseComplete(BasePanel panel) {
        if (panel != null) {
            openPanelInsList.Remove(panel);
            drawOrder.Remove(panel);
            UIFactory.Recover(panel.panelEnum, panel);
            SortAllPanel();
        }
    }

    /**尝试从窗口池中取一个窗口实例*/
    private BasePanel GetPanelInsFromPool(string panelEnum) {
        return UIFactory.TakePooledPanel(panelEnum);
    }

    /**常驻程序宿主也通过同一管理器排序与回收。*/
    public void OpenRuntimePanel(string panelEnum, BasePanel panel) {
        panel.panelEnum = panelEnum;
        OpenPanelInstance(panel, null, Rect.zero);
    }

    /**包含正在退场的窗口；入口隐藏必须等退场完成后恢复。*/
    public bool IsPanelVisible(string panelEnum) {
        return openPanelInsList.Exists(panel => panel.panelEnum == panelEnum);
    }

    /**由仍处于打开状态的界面共同决定舞台，不修改默认适配方式。*/
    private void RefreshViewportMode() {
        var viewport = UnityEngine.Object.FindObjectOfType<PortraitGameViewport>();
        if (viewport != null) {
            viewport.SetFullScreen(openPanelInsList.Exists(panel => panel.UsesFullScreenViewport));
        }
    }

    public void SortAllPanel() {
        RefreshViewportMode();
        openPanelInsList.Sort((p1, p2) => {
            int byLayer = p1.layer.CompareTo(p2.layer);
            return byLayer != 0 ? byLayer : drawOrder[p1].CompareTo(drawOrder[p2]);
        });
        for (int i = 0; i < openPanelInsList.Count; i++) {
            openPanelInsList[i].gameObject.transform.SetSiblingIndex(i);
        }
        // 最高交互窗口若仍在开关动画中，阻止按键穿透到下层。
        topPanel = openPanelInsList.FindLast(panel => panel.ReceivesPanelInput);
        PanelsChanged?.Invoke();
    }

}
