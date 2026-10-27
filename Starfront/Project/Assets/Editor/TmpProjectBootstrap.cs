#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 导出时自动导入 TMP Essential Resources（等同 Window → TextMesh Pro → Import TMP Essential Resources）。
/// </summary>
public static class TmpProjectBootstrap
{
    const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    public static void EnsureEssentials(bool log = true)
    {
        if (File.Exists(TmpSettingsPath))
        {
            if (log)
            {
                Debug.Log("[export] TMP Essential Resources already present.");
            }
            return;
        }

        var packagePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Packages/com.unity.textmeshpro"));
        var unityPackage = Path.Combine(packagePath, "Package Resources", "TMP Essential Resources.unitypackage");
        if (!File.Exists(unityPackage))
        {
            throw new FileNotFoundException(
                "TMP Essential Resources.unitypackage not found. Is com.unity.textmeshpro installed?",
                unityPackage);
        }

        if (log)
        {
            Debug.Log("[export] Importing TMP Essential Resources...");
        }

        var importDone = false;
        void OnImportComplete(string packageName)
        {
            if (packageName == "TMP Essential Resources")
            {
                importDone = true;
            }
        }

        AssetDatabase.importPackageCompleted += OnImportComplete;
        try
        {
            AssetDatabase.ImportPackage(unityPackage, false);

            var deadline = Environment.TickCount + 120_000;
            while (!importDone && !File.Exists(TmpSettingsPath))
            {
                if (Environment.TickCount > deadline)
                {
                    break;
                }

                Thread.Sleep(100);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            AssetDatabase.importPackageCompleted -= OnImportComplete;
        }

        if (!File.Exists(TmpSettingsPath))
        {
            throw new InvalidOperationException(
                "TMP Essential Resources import did not create TMP Settings.asset. " +
                "Ensure Templates include Assets/TextMesh Pro/Resources/TMP Settings.asset.");
        }

        if (log)
        {
            Debug.Log("[export] TMP Essential Resources imported.");
        }
    }
}
#endif
