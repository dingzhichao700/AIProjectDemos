using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 窗口管理
/// </summary>
public class PanelMgr : EventDispatcher {

    /// <summary>
    /// 固定像素层Canvas
    /// </summary>
    public RectTransform viewportConstant;

    /// <summary>
    /// 缩放层Canvas
    /// </summary>
    public RectTransform viewportScale;

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
    /**已打开的窗口字典<UIEnum枚举,BasePanel>*/
    private Dictionary<string, BasePanel> openPanelMap = new Dictionary<string, BasePanel>();

    /**已经打开的常规窗口列表*/
    private List<BasePanel> openPanelInsList = new List<BasePanel>();
    /**已经打开的Tip窗口列表*/
    private List<BaseTipPanel> openTipPanelInsList = new List<BaseTipPanel>();

    /**UI对象池*/
    private Dictionary<string, BasePanel> uiPoolMap = new Dictionary<string, BasePanel>();

    /**最顶层的窗口*/
    private BasePanel topPanel;

    private GraphicRaycaster raycaster;
    private PointerEventData pointerEventData;
    private EventSystem eventSystem;

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
        raycaster = viewportConstant.GetComponent<GraphicRaycaster>();
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
                if (!topPanel.isClosing) {//正在关闭的话，不响应操作
                    topPanel.OnPanelOperate(result);
                }
            }
        }
    }

    public void Update() {
        int width = (int)viewportScale.rect.width;
        int height = (int)viewportScale.rect.height;
        if (stageWidth != width || stageHeight != height) {
            stageWidth = width;
            stageHeight = height;
            //uiRoot.sizeDelta = new Vector2(width, height);
            Debug.Log("stage size changed:" + stageWidth + "x" + stageHeight + ", windowSize:" + Screen.width + "x" + Screen.height + ", fullScreen:" + Screen.fullScreen);
            globalWidthRatio = (float)stageWidth / Screen.width;
            globalHeightRatio = (float)stageHeight / Screen.height;
            Dispatch(PanelEvent.WINDOW_RESIZE);
        }

        //针对Tip层窗口的的点击检测
        if (Input.GetMouseButtonDown(0)) {
            if (openTipPanelInsList.Count > 0) {
                pointerEventData = new PointerEventData(eventSystem) {
                    position = Input.mousePosition
                };

                List<RaycastResult> results = new List<RaycastResult>();
                raycaster.Raycast(pointerEventData, results);

                for (int i = openTipPanelInsList.Count - 1; i >= 0; i--) {
                    bool isClickInTip = false;
                    GameObject target = openTipPanelInsList[i].gameObject;
                    foreach (var result in results) {
                        if (result.gameObject == target || result.gameObject.transform.IsChildOf(target.transform)) {
                            isClickInTip = true;
                            //Debug.Log($"点击到了{target}或它的子元素");
                        }
                    }
                    if (!isClickInTip) {
                        openTipPanelInsList[i].Close();
                    }
                }
            }
        }
    }

    /**设置UI节点*/
    public void SetUIRoot(RectTransform viewportScale, RectTransform viewportConstant, RectTransform uiPool) {
        this.viewportScale = viewportScale;
        this.viewportConstant = viewportConstant;
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
        if (loadingPanelList.IndexOf(panelEnum) >= 0) {
            Debug.LogWarning("正在打开中，请勿重复打开窗口：" + panelEnum);
            return;
        }
        if (IsPanelOpen(panelEnum)) {
            Debug.LogWarning("窗口已打开，请勿重复打开窗口：" + panelEnum);
            return;
        }

        BasePanel panelFromPool = GetPanelInsFromPool(panelEnum);
        if (panelFromPool != null) {
            OpenPanelInstance(panelFromPool, openParams, fromStageRect);
        } else {
            UIFactory.LoadUIPrefab(panelEnum, (string fullPath) => {
                loadingPanelList.Remove(panelEnum);

                BasePanel panelIns = UIFactory.GetUIInstance(panelEnum) as BasePanel;
                panelIns.panelEnum = panelEnum;
                OpenPanelInstance(panelIns, openParams, fromStageRect);
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
        if (loadingPanelList.IndexOf(panelEnum) >= 0) {
            Debug.LogWarning("正在打开中，请勿重复打开窗口：" + panelEnum);
            return;
        }
        if (IsPanelOpen(panelEnum)) {
            Debug.LogWarning("窗口已打开，请勿重复打开窗口：" + panelEnum);
            return;
        }

        RectTransform sourceRect = sourceGo.transform as RectTransform;
        // sourceGo的世界坐标 → Canvas 本地坐标
        Vector3 localPosToCanvas = (viewportConstant.transform as RectTransform).InverseTransformPoint(sourceRect.position);
        //Debug.Log("相对于Canvas的本地坐标: " + localPosToCanvas);
        Rect btnPosRect = new Rect(localPosToCanvas.x, localPosToCanvas.y, sourceRect.rect.width, sourceRect.rect.height);
        //Debug.Log("按钮的全局位置: " + btnPosRect);

        BasePanel panelFromPool = GetPanelInsFromPool(panelEnum);
        if (panelFromPool != null) {
            OpenPanelInstance(panelFromPool, openParams, btnPosRect);
        } else {
            loadingPanelList.Add(panelEnum);
            UIFactory.LoadUIPrefab(panelEnum, (string fullPath) => {
                loadingPanelList.Remove(panelEnum);
                BasePanel panelIns = UIFactory.GetUIInstance(panelEnum) as BasePanel;
                panelIns.panelEnum = panelEnum;
                OpenPanelInstance(panelIns, openParams, btnPosRect);
            });
        }
    }

    /**打开窗口实例*/
    private void OpenPanelInstance(BasePanel panel, dynamic[] openParams, Rect fromStageRect) {
        ClosePanelByLayer(panel.layer);
        if (!openPanelMap.ContainsKey(panel.panelEnum)) {
            openPanelMap.Add(panel.panelEnum, panel);
            openPanelInsList.Add(panel);
            Transform parentTrans = panel.layer >= 10000 ? viewportScale.transform : viewportConstant.transform;
            panel.transform.SetParent(parentTrans);
            panel.transform.localScale = new Vector3(1, 1, 1);
            panel.openParams = openParams;
            BaseTipPanel tipPanel = panel.GetComponent<BaseTipPanel>();
            if (tipPanel != null) {
                tipPanel.sourceStageRect = fromStageRect;
                openTipPanelInsList.Add(tipPanel);
            } else {
                panel.transform.localPosition = new Vector3(0, 0, 0);
            }
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
        foreach (string panelNum in openPanelMap.Keys) {
            BasePanel panel = openPanelMap[panelNum];
            if (panel.layer == layer) {
                return panel;
            }
        }
        return null;
    }

    /**根据窗口类型关闭窗口*/
    public void ClosePanelByType(string panelEnum) {
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
        foreach (BasePanel panel in openPanelInsList) {
            if(panel.panelEnum != UIEnum.LOADING_PANEL) {
                RookieEngine.timer.CallLater(this, () => { 
                    panel.Close();
                });
            }
        }
    }

    /**关闭某个层级的窗口*/
    public void ClosePanelByLayer(int layer) {
        BasePanel panel = GetPanelByLayer(layer);
        if (panel != null) {
            panel.Close();
        }
    }

    /**关闭窗口实例*/
    public void ClosePanel(BasePanel panel) {
        if (panel != null) {
            if (!uiPoolMap.ContainsKey(panel.panelEnum)) {
                uiPoolMap.Add(panel.panelEnum, panel);
            } else {
                Debug.LogWarning("重复关闭界面：" + panel.panelEnum);
            }
            if (openPanelMap.ContainsKey(panel.panelEnum)) {
                openPanelMap.Remove(panel.panelEnum);
            }
            BaseTipPanel tipPanel = panel.GetComponent<BaseTipPanel>();
            if (tipPanel != null) {
                if (openTipPanelInsList.Contains(tipPanel)) {
                    openTipPanelInsList.Remove(tipPanel);
                }
            } else {
                if (openPanelInsList.Contains(panel)) {
                    openPanelInsList.Remove(panel);
                }
            }
        }
    }

    public void OnPanelCloseComplete(BasePanel panel) {
        if (panel != null) {
            UIFactory.Recover(panel.panelEnum, panel);
            SortAllPanel();
        }
    }

    /**尝试从窗口池中取一个窗口实例*/
    private BasePanel GetPanelInsFromPool(string panelEnum) {
        BasePanel panel = null;
        if (uiPoolMap.ContainsKey(panelEnum)) {
            uiPoolMap.TryGetValue(panelEnum, out panel);
            uiPoolMap.Remove(panelEnum);
        }
        return panel;
    }

    /// <summary>
    /// 排序所有打开中的窗口
    /// </summary>
    public void SortAllPanel() {
        openPanelInsList.Sort((p1, p2) => {
            return p1.layer - p2.layer;
        });
        for (int i = 0; i < openPanelInsList.Count; i++) {
            openPanelInsList[i].gameObject.transform.SetSiblingIndex(i);
        }
        openTipPanelInsList.Sort((p1, p2) => {
            return p1.layer - p2.layer;
        });
        for (int i = 0; i < openTipPanelInsList.Count; i++) {
            openTipPanelInsList[i].gameObject.transform.SetSiblingIndex(i);
        }

        //设置下当前顶层窗口
        if (openTipPanelInsList.Count > 0) {
            topPanel = openTipPanelInsList[openTipPanelInsList.Count - 1];
        } else if (openPanelInsList.Count > 0) {
            topPanel = openPanelInsList[openPanelInsList.Count - 1];
        } else {
            topPanel = null;
        }
    }

}
