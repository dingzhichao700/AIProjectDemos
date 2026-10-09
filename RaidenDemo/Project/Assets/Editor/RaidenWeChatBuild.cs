#if UNITY_EDITOR
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
public static class RaidenWeChatBuild {

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

    /**注册构建请求轮询*/
    static RaidenWeChatBuild() {
        if (AssetDatabase.IsAssetImportWorkerProcess()) {
            return;
        }
        EditorApplication.update += Poll;
        Directory.CreateDirectory("Library/AIUI");
        File.WriteAllText("Library/AIUI/wechat-build-ready.json", "{\"version\":1,\"processId\":" + System.Diagnostics.Process.GetCurrentProcess().Id + ",\"target\":\"" + EditorUserBuildSettings.activeBuildTarget + "\"}");
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
                RaidenHotUpdateBuild.Prepare();
                WriteResult(request, "prepared", null);
                return;
            }
            if (request.action == "generate") {
                WriteResult(request, "generating-hybridclr", null);
                RaidenHotUpdateBuild.Generate();
                WriteResult(request, "generated", null);
                return;
            }
            if (request.action == "publish") {
                RaidenHotUpdateBuild.Publish();
                WriteResult(request, "published-locally", null);
                return;
            }
            if (request.action == "content") {
                WriteResult(request, "building-content", null);
                RaidenHotUpdateBuild.GenerateContent();
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult update);
                if (!string.IsNullOrEmpty(update.Error)) {
                    throw new InvalidOperationException(update.Error);
                }
                RaidenHotUpdateBuild.BuildCandidate();
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
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult content);
            if (!string.IsNullOrEmpty(content.Error)) {
                throw new InvalidOperationException(content.Error);
            }
            var config = WXConvertCore.config;
            config.ProjectConf.CDN = new Uri(new Uri(RaidenHotUpdateBuild.BootstrapUrl), ".").AbsoluteUri.TrimEnd('/' );
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
            RaidenHotUpdateBuild.BuildCandidate();
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
        string policyPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Tools/WeChatResourceCache.js"));
        string policy = File.ReadAllText(policyPath).Trim();
        File.WriteAllText(path, pattern.Replace(source, match => policy));
    }

    /**记录本次请求的进度与结果*/
    private static void WriteResult(BuildRequest request, string status, string error) {
        File.WriteAllText(ResultPath, JsonUtility.ToJson(new BuildResult { requestId = request.requestId, status = status, outputPath = request.outputPath, error = error }, true));
    }
}
#endif
