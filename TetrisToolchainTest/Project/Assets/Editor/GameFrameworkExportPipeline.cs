#if UNITY_EDITOR
using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 供 Generate-GameFrameworkProject.ps1 通过 Unity -batchmode -executeMethod 调用。
/// 导出器先单独完成 Unity 导入编译，再调用 ExportBaseline：
/// TMP Essentials + Addressables Settings + 五组 + Assign entries + BuildPlayerContent。
/// </summary>
public static class GameFrameworkExportPipeline
{
    const int CompileTimeoutMs = 180_000;

    public static void InitAddressablesSettings() => RunPhase(initOnly: true);
    public static void BuildAddressablesContent() => RunPhase(initOnly: false, buildOnly: true);
    public static void ExportBaseline() => RunPhase(initOnly: false, buildOnly: false);
    public static void WireGameEntranceSceneOnly() => RunWireOnly();

    static void RunPhase(bool initOnly, bool buildOnly = false)
    {
        try
        {
            WaitForEditorIdle();

            if (!buildOnly)
            {
                RunInitSettings();
                Debug.Log("[export] Init settings completed (TMP + Addressables).");
            }

            if (!initOnly)
            {
                RunBuildContent();
                WriteMarker(true);
                Debug.Log("[export] Addressables player content built.");
            }

            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[export] Failed: {ex}");
            if (!initOnly)
            {
                WriteMarker(false);
            }

            EditorApplication.Exit(1);
        }
    }

    static void WaitForEditorIdle()
    {
        var deadline = Environment.TickCount + CompileTimeoutMs;
        while (EditorApplication.isUpdating || EditorApplication.isCompiling)
        {
            if (Environment.TickCount - deadline > 0)
            {
                throw new TimeoutException("Editor compile/import did not finish in time.");
            }

            Thread.Sleep(100);
        }
    }

    static void RunInitSettings()
    {
        Debug.Log("[export] Step 0: Import TMP Essential Resources (if missing)...");
        TmpProjectBootstrap.EnsureEssentials(log: true);
        WaitForEditorIdle();

        Debug.Log("[export] Step 1: Create / link Addressables Settings...");
        AddressablesProjectBootstrap.EnsureReady(log: true);
        FrameworkSetup.EnsureAddressablesOnly();

        Debug.Log("[export] Step 1a: Enable Input System player backend...");
        FrameworkSetup.EnsureInputSystemPlayerSettings();

        Debug.Log("[export] Step 1b: Wire GameEntrance scene references...");
        GameEntranceSceneWiring.TryWireGameEntranceScene(true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void RunWireOnly()
    {
        try
        {
            WaitForEditorIdle();
            if (!GameEntranceSceneWiring.TryWireGameEntranceScene(true))
            {
                throw new Exception("Wire GameEntrance scene failed.");
            }

            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[export] Wire failed: {ex}");
            EditorApplication.Exit(1);
        }
    }

    static void RunBuildContent()
    {
        Debug.Log("[export] Step 2: Assign Game Addressables (Build Game Addressables)...");
        AddressablesAutoBuilder.AssignGameAddressables();

        Debug.Log("[export] Step 3: BuildPlayerContent...");
        AddressablesAutoBuilder.BuildPlayerContent();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void WriteMarker(bool success)
    {
        var path = "Assets/AddressableAssetsData/.export-baseline-complete.txt";
        System.IO.File.WriteAllText(path, success ? "ok" : "failed");
        AssetDatabase.ImportAsset(path);
    }
}
#endif
