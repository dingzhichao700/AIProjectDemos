using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// 保存工程实例的平台及发布参数，不携带模板作者的机器信息。
/// </summary>
[Serializable]
public sealed class FrameworkPlatformConfig {

    public string platform = "WeChat";
    public string bootstrapUrl;
    public string cdnRoot;
    public string appId;

    /**从工程配置读取平台参数*/
    public static FrameworkPlatformConfig Read() {
        const string path = "ProjectSettings/FrameworkPlatform.json";
        return File.Exists(path) ? JsonUtility.FromJson<FrameworkPlatformConfig>(File.ReadAllText(path)) : new FrameworkPlatformConfig();
    }

    /**微信导出前确认调用者提供了项目身份与发布位置*/
    public void ValidateWeChat() {
        if (platform != "WeChat" || string.IsNullOrWhiteSpace(appId)) {
            throw new InvalidOperationException("请在 ProjectSettings/FrameworkPlatform.json 配置 WeChat 平台与 AppID。");
        }
        GetBootstrapUrl();
        GetCdnRoot();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) {
            throw new InvalidOperationException("微信构建要求 WebGL 目标。");
        }
    }

    /**返回明确配置的项目资源入口*/
    public string GetBootstrapUrl() {
        if (!Uri.TryCreate(bootstrapUrl, UriKind.Absolute, out Uri uri) || (uri.Scheme != "http" && uri.Scheme != "https") || !uri.AbsolutePath.EndsWith("/bootstrap.json")) {
            throw new InvalidOperationException("bootstrapUrl 必须为项目 bootstrap.json 的完整 HTTP(S) 地址。");
        }
        return uri.AbsoluteUri;
    }

    /**解析项目专用的本地候选发布根目录*/
    public string GetCdnRoot() {
        if (string.IsNullOrWhiteSpace(cdnRoot)) {
            throw new InvalidOperationException("请配置 cdnRoot，作为本项目的候选发布目录。");
        }
        return Path.GetFullPath(cdnRoot);
    }

    /// <summary>
    /// 普通工程保持本地资源；微信工程采用清单管理的远端资源策略。
    /// </summary>
    public static void ApplyAddressables() {
        bool weChat = Read().platform == "WeChat";
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", "ServerData/[BuildTarget]");
        settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", HotUpdateBootstrap.BundleRoot.TrimEnd('/'));
        settings.BuildRemoteCatalog = false;
        settings.DisableCatalogUpdateOnStartup = weChat;
        foreach (var group in settings.groups) {
            var schema = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
            if (schema == null) {
                continue;
            }
            schema.BuildPath.SetVariableByName(settings, weChat ? "Remote.BuildPath" : "Local.BuildPath");
            schema.LoadPath.SetVariableByName(settings, weChat ? "Remote.LoadPath" : "Local.LoadPath");
            EditorUtility.SetDirty(schema);
        }
        EditorUtility.SetDirty(settings);
    }

}
