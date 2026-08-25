using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>
/// 预制违规跨Group或Label检测脚本
/// </summary>
public static class AddressablesPrefabDependencyChecker {

    private const string PREFAB_ROOT = "Assets/Prefab";

    [MenuItem("Tools/Addressables/Check Prefab Dependencies")]
    public static void CheckPrefabDependencies() {

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) {
            Debug.LogError("AddressableAssetSettings not found.");
            return;
        }

        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_ROOT });

        int errorCount = 0;

        foreach (var prefabGuid in prefabGuids) {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);

            if (!TryGetLogicalGroupAndLabel(prefabPath, out var prefabGroup, out var prefabLabel))
                continue;

            var dependencies = AssetDatabase.GetDependencies(prefabPath, true);

            foreach (var depPath in dependencies) {
                if (depPath == prefabPath || depPath.EndsWith(".cs"))
                    continue;

                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(depPath));

                if (entry == null)
                    continue; // 非 Addressables 资源不检测

                if (!TryGetLogicalGroupAndLabel(depPath, out var depGroup, out var depLabel))
                    continue;

                if (IsViolation(prefabGroup, prefabLabel, depGroup, depLabel)) {
                    errorCount++;

                    Debug.LogError(
                        $"❌ Addressables Dependency Violation\n" +
                        $"Prefab: {prefabPath} [{prefabGroup}/{prefabLabel}]\n" +
                        $"Depends On: {depPath} [{depGroup}/{depLabel}]",
                        AssetDatabase.LoadAssetAtPath<Object>(prefabPath)
                    );
                }
            }
        }

        Debug.Log($"🔍 Prefab dependency check finished. Errors: {errorCount}");
    }

    #region Rules

    private static bool IsViolation( string prefabGroup, string prefabLabel,string depGroup,string depLabel) {
        // default 可以被任何人依赖
        if (depGroup == "default")
            return false;

        // Group 不同 → 违规
        if (prefabGroup != depGroup)
            return true;

        // 同 Group 但 Label 不同（且 Prefab 有 Label）→ 违规
        if (!string.IsNullOrEmpty(prefabLabel)
            && !string.IsNullOrEmpty(depLabel)
            && prefabLabel != depLabel)
            return true;

        return false;
    }

    #endregion

    #region Group / Label Resolve

    private static bool TryGetLogicalGroupAndLabel(string assetPath,out string group,out string label) {
        group = null;
        label = null;

        // Prefab & Art 统一按路径规则解析
        // Assets/Prefab/scene/scene1/xxx.prefab
        // Assets/Art/atlas/scene/scene1/xxx.asset

        var parts = assetPath.Split('/');

        for (int i = 0; i < parts.Length; i++) {
            if (parts[i] is "opening" or "title" or "default"
                or "cutScene" or "scene") {
                group = parts[i];

                // Pack Together Group 没有 Label
                if (group is "opening" or "title" or "default")
                    return true;

                // Label = 下一级目录名
                if (i + 1 < parts.Length)
                    label = parts[i + 1];

                return true;
            }
        }

        return false;
    }

    #endregion
}
