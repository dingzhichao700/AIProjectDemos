using System;
using System.Collections.Generic;
using UnityEngine;

public class LoadingControl {

    /**是否加载中*/
    private bool isLoading;

    /**当前的加载完成回调*/
    private Action loadingHandler;

    private static LoadingControl _ins;

    public static LoadingControl ins {
        get {
            if (_ins == null) {
                _ins = new LoadingControl();
            }
            return _ins;
        }
    }

    /// <summary>
    /// 打开加载界面
    /// </summary>
    /// <param name="preloadList">要加载的资源列表</param>
    /// <param name="action">加载完成的回调</param>
    public void OpenLoading(List<ResLoadInfo> preloadList, Action action) {
        if (isLoading) {
            Debug.LogError("错误！重复打开加载界面");
            return;
        }
        isLoading = true;
        loadingHandler = action;
        PanelMgr.ins.OpenPanel(UIEnum.LOADING_PANEL, new object[] { preloadList, (Action)OnLoadingComplete });
    }

    /**加载完成回调*/
    private void OnLoadingComplete() {
        //Debug.Log("加载完成");
        isLoading = false;
        if (loadingHandler!=null) {
            loadingHandler.Invoke();
            loadingHandler = null;
        }
    }

    /**关闭加载界面*/
    public void CloseLoading() {
        PanelMgr.ins.ClosePanelByType(UIEnum.LOADING_PANEL);
    }

}
