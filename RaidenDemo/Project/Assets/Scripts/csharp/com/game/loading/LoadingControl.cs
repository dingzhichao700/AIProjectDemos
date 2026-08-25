using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一次通用加载操作的输入参数与完成回调。
/// </summary>
public sealed class LoadingRequest {

    /**待加载资源列表*/
    public readonly List<ResLoadInfo> resources;

    /**加载完成回调*/
    public readonly Action onComplete;

    /**加载失败回调*/
    public readonly Action<Exception> onFailed;

    /**可选背景资源路径*/
    public readonly string backgroundPath;

    /**可选提示文本列表*/
    public readonly IReadOnlyList<string> tips;

    public LoadingRequest(List<ResLoadInfo> resources, Action onComplete,
        string backgroundPath, IReadOnlyList<string> tips, Action<Exception> onFailed) {
        this.resources = resources ?? new List<ResLoadInfo>();
        this.onComplete = onComplete;
        this.backgroundPath = backgroundPath;
        this.tips = tips;
        this.onFailed = onFailed;
    }

}

/// <summary>
/// 通用加载界面的调用入口，负责限制并发加载并恢复调用状态。
/// </summary>
public class LoadingControl {

    /**是否已有加载任务正在执行*/
    private bool isLoading;

    /**单例实例*/
    private static LoadingControl instance;

    /**通用加载控制器单例*/
    public static LoadingControl ins => instance ??= new LoadingControl();

    /// <summary>
    /// 打开加载界面，加载指定资源后执行回调；背景图会自动并入同一次加载任务。
    /// </summary>
    /// <param name="preloadList">需要加载的资源列表</param>
    /// <param name="onComplete">全部加载成功后的回调</param>
    /// <param name="backgroundPath">可选的加载界面背景路径</param>
    /// <param name="tips">可选的随机提示列表</param>
    /// <param name="onFailed">加载失败后的回调</param>
    public void OpenLoading(List<ResLoadInfo> preloadList, Action onComplete,
        string backgroundPath = null, IReadOnlyList<string> tips = null,
        Action<Exception> onFailed = null) {
        if (isLoading) {
            Debug.LogError("LoadingPanel is already open");
            return;
        }
        isLoading = true;
        LoadingRequest request = new LoadingRequest(preloadList,
            () => Finish(onComplete), backgroundPath, tips,
            exception => Fail(exception, onFailed));
        PanelMgr.ins.OpenPanel(UIEnum.LOADING_PANEL, new object[] { request });
    }

    private void Finish(Action handler) {
        isLoading = false;
        handler?.Invoke();
    }

    private void Fail(Exception exception, Action<Exception> handler) {
        isLoading = false;
        handler?.Invoke(exception);
    }

    /**主动终止当前加载界面的展示状态*/
    public void CloseLoading() {
        isLoading = false;
        PanelMgr.ins.ClosePanelByType(UIEnum.LOADING_PANEL);
    }

}
