using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 从登录阶段进入正式内容的加载队列。
/// </summary>
public static class MainContentLoader {

    /**正式内容资源标签，由构建工具同步到 Default Group*/
    public const string ContentLabel = "main-content";

    /// <summary>
    /// 顺序准备正式内容，成功后交给 Main 入口。
    /// </summary>
    /// <param name="onComplete">进入成功回调</param>
    /// <param name="onFailed">加载失败回调</param>
    public static void Enter(Action onComplete, Action<Exception> onFailed) {
        var steps = new List<Func<Action<float>, Task>> {
            async progress => {
                await CfgManager.EnsureLoadedAsync();
                await HotUpdateBootstrap.LoadModuleAsync("Main");
                progress(1f);
            },
            progress => ResourceLoader.DownloadDependenciesAsync(ContentLabel, progress),
            async progress => {
                await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(ResourceConst.GetUIPath(UIEnum.HOME_PANEL), ResType.Prefab) }, null, progress);
            },
            async progress => {
                await HotUpdateBootstrap.EnterModuleAsync("Main");
                progress(1f);
            }
        };
        LoadingControl.ins.OpenQueue(steps, onComplete, onFailed);
    }

}
