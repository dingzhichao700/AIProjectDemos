using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HybridCLR;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

/// <summary>
/// 从远程资源目录初始化资源系统并加载业务程序集。
/// </summary>
public static class HotUpdateBootstrap {

    /**Catalog 中的可替换资源根地址*/
    public const string BundleRoot = "http://raiden-content.invalid/";

    /**当前底座选定的业务发布清单*/
    private static HotUpdateManifest activeManifest;

    /**已完成装载的业务程序集*/
    private static readonly HashSet<string> loadedModules = new HashSet<string>();

    /**正在装载的业务程序集任务*/
    private static readonly Dictionary<string, Task> loadingModules = new Dictionary<string, Task>();

    /**限制启动时元数据下载并发及尚未加载的字节数组数量*/
    private const int AOT_DOWNLOAD_CONCURRENCY = 3;

    /// <summary>
    /// 按底座版本选择发布内容，补充元数据并装载登录程序集。
    /// </summary>
    public static async Task InitializeAsync() {
        var startupWatch = System.Diagnostics.Stopwatch.StartNew();
#if UNITY_EDITOR
        // 编辑器使用已编译的程序集，避免重复加载同名 DLL。
        await LoadTiming.MeasureAsync("Addressables.Initialize", async () => await Addressables.InitializeAsync().Task);
#else
        TextAsset boot = Resources.Load<TextAsset>("RaidenHotUpdateBase");
        if (boot == null) {
            throw new InvalidOperationException("缺少热更新底座版本，请使用热更新构建入口。");
        }
        HotUpdateBootConfig config = JsonUtility.FromJson<HotUpdateBootConfig>(boot.text);
        Resources.UnloadAsset(boot);
        if (config == null || string.IsNullOrEmpty(config.baseVersion) || string.IsNullOrEmpty(config.bootstrapUrl)) {
            throw new InvalidOperationException("热更新启动配置无效。");
        }
        string baseVersion = config.baseVersion;
        HotUpdateIndex index = JsonUtility.FromJson<HotUpdateIndex>(await DownloadTextAsync(config.bootstrapUrl, true));
        HotUpdateBaseEntry selected = null;
        foreach (HotUpdateBaseEntry item in index?.bases ?? Array.Empty<HotUpdateBaseEntry>()) {
            if (item.baseVersion == baseVersion) {
                if (selected != null) {
                    throw new InvalidOperationException("热更新入口存在重复底座版本。");
                }
                selected = item;
            }
        }
        if (selected == null || string.IsNullOrEmpty(selected.manifestUrl)) {
            throw new InvalidOperationException("当前底座尚未发布兼容的业务版本：" + baseVersion);
        }
        string pointerUrl = new Uri(new Uri(config.bootstrapUrl), selected.manifestUrl).AbsoluteUri;
        HotUpdateManifest manifest = JsonUtility.FromJson<HotUpdateManifest>(await DownloadTextAsync(pointerUrl, true));
        if (manifest == null || manifest.baseVersion != baseVersion || manifest.aot == null || manifest.assemblies == null || manifest.assemblies.Length == 0 || string.IsNullOrEmpty(manifest.release) || manifest.release.IndexOfAny(new[] { '/', '\\', '.' }) >= 0) {
            throw new InvalidOperationException("热更新清单与当前底座不匹配。");
        }
        if (manifest.resourceRoot != "releases/" + manifest.release + "/" || manifest.bundleRoot != "bundles/") {
            throw new InvalidOperationException("发布清单的资源目录结构无效。");
        }
        string releaseRoot = new Uri(new Uri(pointerUrl), manifest.resourceRoot).AbsoluteUri.TrimEnd('/') + "/";
        string bundleRoot = new Uri(new Uri(pointerUrl), manifest.bundleRoot).AbsoluteUri;
        string localRoot = Addressables.RuntimePath.TrimEnd('/') + "/";
        LoadTiming.version = baseVersion + "/" + manifest.release;
        LoadTiming.Report("Startup.Manifest", startupWatch);
        Addressables.InternalIdTransformFunc = location => {
            string path = location.InternalId;
            if (path.StartsWith(localRoot, StringComparison.Ordinal)) {
                return releaseRoot + path.Substring(localRoot.Length);
            }
            if (path.StartsWith(BundleRoot, StringComparison.Ordinal)) {
                return bundleRoot + path.Substring(BundleRoot.Length);
            }
            return path;
        };
        await LoadTiming.MeasureAsync("Addressables.Initialize", async () => await Addressables.InitializeAsync().Task);
        long aotDownloadMs = 0;
        long aotLoadMs = 0;
        for (int offset = 0; offset < manifest.aot.Length; offset += AOT_DOWNLOAD_CONCURRENCY) {
            int count = Math.Min(AOT_DOWNLOAD_CONCURRENCY, manifest.aot.Length - offset);
            var aotWatch = System.Diagnostics.Stopwatch.StartNew();
            Task<byte[]>[] downloads = new Task<byte[]>[count];
            for (int i = 0; i < count; i++) {
                downloads[i] = LoadBytesAsync("hotupdate/aot/" + manifest.aot[offset + i]);
            }
            byte[][] metadata = await Task.WhenAll(downloads);
            aotDownloadMs += aotWatch.ElapsedMilliseconds;
            aotWatch.Restart();
            // 下载并行，元数据仍按清单顺序加载；全部完成后才允许装载业务 DLL。
            for (int i = 0; i < count; i++) {
                LoadImageErrorCode result = RuntimeApi.LoadMetadataForAOTAssembly(metadata[i], HomologousImageMode.SuperSet);
                if (result != LoadImageErrorCode.OK) {
                    throw new InvalidOperationException("补充 AOT 元数据失败：" + manifest.aot[offset + i] + "，" + result);
                }
            }
            aotLoadMs += aotWatch.ElapsedMilliseconds;
        }
        LoadTiming.Report("Startup.AOTReady", startupWatch, $"count={manifest.aot.Length} concurrency={AOT_DOWNLOAD_CONCURRENCY} downloadMs={aotDownloadMs} metadataLoadMs={aotLoadMs}");
        activeManifest = manifest;
        await LoadModuleAsync("Login");
        LoadTiming.Report("Startup.LoginReady", startupWatch);
        Debug.Log("[HotUpdate] 登录程序集就绪，底座 " + baseVersion + "，业务发布 " + manifest.release);
#endif
    }

    /// <summary>
    /// 按需装载业务程序集，合并并发请求并允许失败后重试。
    /// </summary>
    /// <param name="moduleName">程序集名称，不含扩展名</param>
    public static async Task LoadModuleAsync(string moduleName) {
        if (loadedModules.Contains(moduleName)) {
            return;
        }
        if (loadingModules.TryGetValue(moduleName, out Task pending)) {
            await pending;
            return;
        }
        Task task = LoadModuleCoreAsync(moduleName);
        loadingModules.Add(moduleName, task);
        try {
            await task;
            loadedModules.Add(moduleName);
        } finally {
            loadingModules.Remove(moduleName);
        }
    }

    /**校验发布清单并装载指定业务程序集*/
    private static async Task LoadModuleCoreAsync(string moduleName) {
        var moduleWatch = System.Diagnostics.Stopwatch.StartNew();
#if UNITY_EDITOR
        await Task.CompletedTask;
        if (Type.GetType(moduleName + "Entry, " + moduleName) == null) {
            throw new InvalidOperationException("业务入口不存在：" + moduleName);
        }
#else
        string fileName = moduleName + ".dll";
        if (activeManifest == null || Array.IndexOf(activeManifest.assemblies, fileName) < 0) {
            throw new InvalidOperationException("发布清单未包含业务程序集：" + fileName);
        }
        byte[] bytes = await LoadBytesAsync("hotupdate/code/" + fileName);
        long downloadMs = moduleWatch.ElapsedMilliseconds;
        Assembly assembly = Assembly.Load(bytes);
        LoadTiming.Report("Module." + moduleName, moduleWatch, $"downloadMs={downloadMs} assemblyLoadMs={moduleWatch.ElapsedMilliseconds - downloadMs}");
        Debug.Log("[HotUpdate] 已加载 " + assembly.GetName().Name + "，发布版本 " + activeManifest.release);
#endif
    }

    /// <summary>
    /// 调用已加载模块的公共入口。
    /// </summary>
    /// <param name="moduleName">程序集名称</param>
    public static async Task EnterModuleAsync(string moduleName) {
        await LoadModuleAsync(moduleName);
        Type type = Type.GetType(moduleName + "Entry, " + moduleName, true);
        if (!(Activator.CreateInstance(type) is IGameModuleEntry entry)) {
            throw new InvalidOperationException("业务入口未实现 IGameModuleEntry：" + moduleName);
        }
        await entry.InitializeAsync();
    }

    /// <summary>
    /// 从 Addressables 读取程序集数据并释放资源句柄。
    /// </summary>
    private static async Task<byte[]> LoadBytesAsync(string address) {
        var handle = Addressables.LoadAssetAsync<TextAsset>(address);
        try {
            TextAsset asset = await handle.Task;
            if (asset == null) {
                throw new InvalidOperationException("程序集资源为空：" + address);
            }
            return asset.bytes;
        } finally {
            Addressables.Release(handle);
        }
    }

    /// <summary>
    /// 获取当前底座对应的发布清单。
    /// </summary>
    private static async Task<string> DownloadTextAsync(string url, bool refresh = false) {
        if (refresh) {
            url += (url.Contains("?") ? "&" : "?") + "request=" + Guid.NewGuid().ToString("N");
        }
        using (UnityWebRequest request = UnityWebRequest.Get(url)) {
            request.timeout = 30;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) {
                await Task.Yield();
            }
            if (request.result != UnityWebRequest.Result.Success) {
                throw new InvalidOperationException("下载发布清单失败：" + url + "，" + request.error);
            }
            return request.downloadHandler.text;
        }
    }

}
