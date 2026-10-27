using cfg;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 统一管理由 Addressables 预加载的 Luban JSON 配置。
/// </summary>
public static class CfgManager {
    static Tables _tables;

    /**正在执行的全量初始化任务*/
    private static Task loadingTask;

    /// <summary>
    /// 全量加载配置并初始化，复用已完成或正在执行的请求。
    /// </summary>
    public static async Task EnsureLoadedAsync() {
        if (_tables != null) {
            return;
        }
        if (loadingTask == null) {
            loadingTask = LoadAllAsync();
        }
        try {
            await loadingTask;
        } finally {
            loadingTask = null;
        }
    }

    /**加载全部配置数据后统一解析表引用*/
    private static async Task LoadAllAsync() {
        var resources = new List<ResLoadInfo>();
        foreach (string name in ResourceConst.ALL_CONFIG_LIST) {
            resources.Add(new ResLoadInfo(ResourceConst.PATH_CONFIG + name, ResType.Json));
        }
        // 各表下载互相独立，全部就绪后才构造含跨表引用的 Tables。
        await LoadTiming.MeasureAsync($"Config.Load count={resources.Count} concurrency=4", () => ResourceLoader.LoadListAsync(resources, maxConcurrent: 4));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Init();
        LoadTiming.Report("Config.TablesInit", watch);
    }

    public static Tables tables {
        get {
            if (_tables == null) {
                throw new InvalidOperationException("CfgManager 尚未初始化");
            }

            return _tables;
        }
    }

    /**使用已预加载的 JSON 初始化 Luban 配置表*/
    public static void Init() {
        ConfigValueHelper.ClearCache();
        _tables = null;
        _tables = new Tables(LoadJson);
    }

    public static void Clear() {
        ConfigValueHelper.ClearCache();
        _tables = null;
        foreach (string cfgName in ResourceConst.ALL_CONFIG_LIST) {
            ResourceManager.Release(ResourceConst.PATH_CONFIG + cfgName);
        }
    }

    static JSONNode LoadJson(string file) {
        string path = ResourceConst.PATH_CONFIG + file;
        JSONNode json = ResourceManager.GetJsonNode(path);
        if (json == null) {
            throw new InvalidOperationException($"Luban 配置尚未通过 Addressables 预加载：{path}.json");
        }

        return json;
    }
}
