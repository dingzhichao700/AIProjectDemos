#if UNITY_EDITOR && GAMEFRAMEWORK_WECHAT
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;
using WeChatWASM;

/// <summary>
/// 在当前编辑器中执行微信小游戏构建。
/// </summary>
[InitializeOnLoad]
public static class FrameworkWeChatBuild {

    private const string RequestPath = "Library/AIUI/pending-wechat-build.json";
    private const string ResultPath = "Library/AIUI/wechat-build-result.json";
    private static bool running;

    [Serializable]
    private class BuildRequest {

        public string requestId;
        public string outputPath;
        public int processId;
        public string action;
    }

    [Serializable]
    private class BuildResult {

        public string requestId;
        public string status;
        public string outputPath;
        public string error;
    }

    /// <summary>
    /// 批处理构建入口，动作与输出由命令行显式提供。
    /// </summary>
    public static void RunBatch() {
        try {
            string[] arguments = Environment.GetCommandLineArgs();
            string ReadArgument(string key) {
                int index = Array.IndexOf(arguments, key);
                return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
            }
            string action = ReadArgument("-frameworkAction");
            if (Array.IndexOf(new[] { "prepare", "generate", "export", "content", "publish" }, action) < 0) {
                throw new InvalidOperationException("必须指定 -frameworkAction prepare|generate|export|content|publish。");
            }
            string requestId = Submit(action, ReadArgument("-frameworkOutput"));
            SessionState.SetString("Framework.BuildRequest", requestId);
            SessionState.SetString("Framework.BuildDeadline", DateTime.UtcNow.AddHours(1).Ticks.ToString());
            EditorApplication.update -= CheckBatch;
            EditorApplication.update += CheckBatch;
        } catch (Exception exception) {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Tools/Framework/WeChat/3 Export Base")]
    private static void ExportFromMenu() {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/wechat_" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
        Submit("export", output);
    }

    [MenuItem("Tools/Framework/WeChat/4 Build Content")]
    private static void ContentFromMenu() {
        Submit("content", null);
    }

    [MenuItem("Tools/Framework/WeChat/5 Publish Local Pointer")]
    private static void PublishFromMenu() {
        Submit("publish", null);
    }

    /**只提交到当前编辑器，已排队或忙碌时拒绝重复请求*/
    private static string Submit(string action, string output) {
        if (running || File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) {
            throw new InvalidOperationException("当前编辑器忙碌或已有微信构建请求。");
        }
        FrameworkPlatformConfig.Read().ValidateWeChat();
        Directory.CreateDirectory("Library/AIUI");
        var request = new BuildRequest { requestId = Guid.NewGuid().ToString("N"), action = action, outputPath = output, processId = System.Diagnostics.Process.GetCurrentProcess().Id };
        string temporary = RequestPath + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(request));
        File.Move(temporary, RequestPath);
        return request.requestId;
    }

    /**注册构建请求轮询*/
    static FrameworkWeChatBuild() {
        if (AssetDatabase.IsAssetImportWorkerProcess()) {
            return;
        }
        EditorApplication.update += Poll;
        if (!string.IsNullOrEmpty(SessionState.GetString("Framework.BuildRequest", string.Empty))) {
            EditorApplication.update += CheckBatch;
        }
        Directory.CreateDirectory("Library/AIUI");
        File.WriteAllText("Library/AIUI/wechat-build-ready.json", "{\"version\":1,\"processId\":" + System.Diagnostics.Process.GetCurrentProcess().Id + ",\"target\":\"" + EditorUserBuildSettings.activeBuildTarget + "\"}");
    }

    /**批处理只消费当前请求的终态，跨编译保留等待状态*/
    private static void CheckBatch() {
        string requestId = SessionState.GetString("Framework.BuildRequest", string.Empty);
        if (string.IsNullOrEmpty(requestId)) {
            return;
        }
        string deadline = SessionState.GetString("Framework.BuildDeadline", "0");
        if (DateTime.UtcNow.Ticks > long.Parse(deadline)) {
            Debug.LogError("微信构建等待超时：" + requestId);
            SessionState.EraseString("Framework.BuildRequest");
            EditorApplication.Exit(1);
            return;
        }
        if (!File.Exists(ResultPath)) {
            return;
        }
        var result = JsonUtility.FromJson<BuildResult>(File.ReadAllText(ResultPath));
        string[] terminal = { "prepared", "generated", "published-locally", "content-built", "exported", "failed" };
        if (result.requestId != requestId || Array.IndexOf(terminal, result.status) < 0) {
            return;
        }
        EditorApplication.update -= CheckBatch;
        SessionState.EraseString("Framework.BuildRequest");
        EditorApplication.Exit(result.status == "failed" ? 1 : 0);
    }

    /**编辑器空闲时消费一次构建请求*/
    private static void Poll() {
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) {
            return;
        }
        BuildRequest request = JsonUtility.FromJson<BuildRequest>(File.ReadAllText(RequestPath));
        File.Delete(RequestPath);
        running = true;
        try {
            if (string.IsNullOrEmpty(request.requestId) || request.processId != System.Diagnostics.Process.GetCurrentProcess().Id) {
                throw new InvalidOperationException("构建请求标识或编辑器 PID 不匹配。");
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) {
                throw new InvalidOperationException("请先切换为 WebGL 构建目标。");
            }
            if (request.action == "prepare") {
                WriteResult(request, "preparing-hybridclr", null);
                FrameworkHotUpdateBuild.Prepare();
                WriteResult(request, "prepared", null);
                return;
            }
            if (request.action == "generate") {
                WriteResult(request, "generating-hybridclr", null);
                FrameworkHotUpdateBuild.Generate();
                WriteResult(request, "generated", null);
                return;
            }
            if (request.action == "publish") {
                FrameworkHotUpdateBuild.Publish();
                WriteResult(request, "published-locally", null);
                return;
            }
            if (request.action == "content") {
                WriteResult(request, "building-content", null);
                FrameworkHotUpdateBuild.GenerateContent();
                AddressablesPrefabDependencyChecker.ValidateOrThrow();
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult update);
                if (!string.IsNullOrEmpty(update.Error)) {
                    throw new InvalidOperationException(update.Error);
                }
                FrameworkHotUpdateBuild.BuildCandidate();
                WriteResult(request, "content-built", null);
                return;
            }
            string output = Path.GetFullPath(request.outputPath);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build")) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Directory.Exists(output)) {
                throw new InvalidOperationException("输出必须是 build 下尚不存在的目录。");
            }
            Directory.CreateDirectory(output);
            AssetDatabase.SaveAssets();
            WriteResult(request, "building-addressables", null);
            AddressablesPrefabDependencyChecker.ValidateOrThrow();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult content);
            if (!string.IsNullOrEmpty(content.Error)) {
                throw new InvalidOperationException(content.Error);
            }
            var config = WXConvertCore.config;
            config.ProjectConf.Appid = FrameworkPlatformConfig.Read().appId;
            config.ProjectConf.assetLoadType = 1;
            config.CompileOptions.DeleteStreamingAssets = true;
            config.ProjectConf.CDN = new Uri(new Uri(FrameworkHotUpdateBuild.BootstrapUrl), ".").AbsoluteUri.TrimEnd('/' );
            config.ProjectConf.relativeDST = output.Replace('\\', '/');
            config.ProjectConf.DST = config.ProjectConf.relativeDST;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            WriteResult(request, "building-wechat", null);
            var result = WXConvertCore.DoExport(true);
            if (result != WXConvertCore.WXExportError.SUCCEED || !File.Exists(Path.Combine(output, "minigame/project.config.json"))) {
                throw new InvalidOperationException("微信导出未完成：" + result);
            }
            ApplyResourceCachePolicy(output);
            FrameworkHotUpdateBuild.BuildCandidate();
            WriteResult(request, "exported", null);
        } catch (Exception exception) {
            WriteResult(request, "failed", exception.ToString());
            Debug.LogException(exception);
        } finally {
            running = false;
        }
    }

    /// <summary>
    /// 写入真机版本资源缓存规则，保留开发工具兼容处理。
    /// </summary>
    /// <param name="output">微信导出目录</param>
    private static void ApplyResourceCachePolicy(string output) {
        string path = Path.Combine(output, "minigame/unity-namespace.js");
        string source = File.ReadAllText(path);
        Regex pattern = new Regex(@"^unityNamespace\.isCacheableFile = function \(path\) \{[\s\S]*?^\};", RegexOptions.Multiline);
        if (pattern.Matches(source).Count != 1) {
            throw new InvalidOperationException("微信 SDK 缓存入口发生变化，请检查开发工具兼容处理。");
        }
        string policyPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Editor/WeChatResourceCache.js.txt"));
        string policy = File.ReadAllText(policyPath).Trim();
        File.WriteAllText(path, pattern.Replace(source, match => policy));
    }

    /**记录本次请求的进度与结果*/
    private static void WriteResult(BuildRequest request, string status, string error) {
        File.WriteAllText(ResultPath, JsonUtility.ToJson(new BuildResult { requestId = request.requestId, status = status, outputPath = request.outputPath, error = error }, true));
    }
}
#endif
