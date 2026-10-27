using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>检查阶段资源的递归依赖及同包资源带来的间接依赖。</summary>
public static class AddressablesPrefabDependencyChecker
{
    [MenuItem("Tools/Addressables/Check Prefab Dependencies")]
    public static void CheckPrefabDependencies()
    {
        ValidateOrThrow();
    }

    public static void ValidateOrThrow()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            throw new InvalidOperationException("AddressableAssetSettings not found.");
        }

        var roots = new HashSet<string>(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" })
            .Select(AssetDatabase.GUIDToAssetPath));
        foreach (var group in settings.groups)
        {
            if (group != null && (group.Name == "opening" || group.Name == "title"))
            {
                foreach (var entry in group.entries)
                {
                    roots.Add(entry.AssetPath);
                }
            }
        }

        int errors = 0;
        foreach (string root in roots.OrderBy(path => path))
        {
            string sourceGroup = ResolveGroup(settings, root);
            if (string.IsNullOrEmpty(sourceGroup))
            {
                continue;
            }

            var pending = new Queue<(string path, string chain)>();
            var visited = new HashSet<string>();
            pending.Enqueue((root, root));
            while (pending.Count > 0)
            {
                var item = pending.Dequeue();
                if (!visited.Add(item.path))
                {
                    continue;
                }

                string targetGroup = ResolveGroup(settings, item.path);
                if (IsViolation(sourceGroup, targetGroup, root, item.path))
                {
                    errors++;
                    Debug.LogError($"Addressables 阶段依赖违规 [{sourceGroup} -> {targetGroup}]\n{item.chain}");
                    continue;
                }

                foreach (string dependency in AssetDatabase.GetDependencies(item.path, false))
                {
                    if (dependency != item.path && !dependency.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Enqueue((dependency, item.chain + "\n  引用 -> " + dependency));
                    }
                }

                // 加载一个条目可能同时拉取同 Bundle 内其他条目的依赖。
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(item.path), true);
                var schema = entry?.parentGroup.GetSchema<BundledAssetGroupSchema>();
                if (schema == null || schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
                {
                    continue;
                }
                foreach (var sibling in entry.parentGroup.entries)
                {
                    if (schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel
                        && !entry.labels.SetEquals(sibling.labels))
                    {
                        continue;
                    }
                    pending.Enqueue((sibling.AssetPath, item.chain + "\n  同 Bundle -> " + sibling.AssetPath));
                }
            }
        }
        if (errors > 0)
        {
            throw new InvalidOperationException($"Addressables 阶段依赖检查失败：{errors} 条违规，已阻止构建。见上述引用链。");
        }
        Debug.Log($"Addressables 静态阶段依赖检查通过：{roots.Count} 个入口。动态地址及最终 Bundle 依赖仍需构建报告和运行验证。");
    }

    private static bool IsViolation(string source, string target, string sourcePath, string targetPath)
    {
        if (string.IsNullOrEmpty(target) || target == "fonts")
        {
            return false;
        }
        if (source == "opening" || source == "title")
        {
            // title 的预加载由流程显式发起，不能被 opening 的资源引用提前带入。
            return target != source;
        }
        if (source == target)
        {
            if (source == "scene" || source == "cutScene")
            {
                string sourceLabel = GetPathLabel(sourcePath, source);
                string targetLabel = GetPathLabel(targetPath, target);
                return sourceLabel != null && targetLabel != null && sourceLabel != targetLabel;
            }
            return false;
        }
        // 正式内容可复用此前已可用的登录资源和公共配置；场景可依赖 default。
        return target != "title" && target != "configData" && target != "default";
    }

    private static string ResolveGroup(AddressableAssetSettings settings, string path)
    {
        var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path), true);
        if (entry != null)
        {
            return entry.parentGroup == settings.DefaultGroup ? "default" : entry.parentGroup.Name;
        }
        // 未显式登记的隐式资源也不能绕过阶段检查。
        if (path.StartsWith("Assets/ConfigBin/", StringComparison.Ordinal))
        {
            return "configData";
        }
        if (path.StartsWith("Assets/Art/font/", StringComparison.Ordinal))
        {
            return "fonts";
        }
        return path.Split('/').FirstOrDefault(part => part == "opening" || part == "title"
            || part == "default" || part == "scene" || part == "cutScene");
    }

    private static string GetPathLabel(string path, string group)
    {
        var parts = path.Split('/');
        int index = Array.IndexOf(parts, group);
        return index >= 0 && index + 2 < parts.Length ? parts[index + 1] : null;
    }
}
