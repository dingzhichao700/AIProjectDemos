#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>存在 marker 时执行一次 PackAtlasSourceAssetPath（与 UI工具 菜单相同逻辑）。</summary>
[InitializeOnLoad]
static class AtlasPackPendingRunner
{
    const string Marker = "Assets/Art/atlas/.pack-request.txt";

    static AtlasPackPendingRunner()
    {
        EditorApplication.delayCall += TryRun;
    }

    static void TryRun()
    {
        if (!File.Exists(Marker))
        {
            return;
        }

        string request = File.ReadAllText(Marker).Trim();
        File.Delete(Marker);
        if (File.Exists(Marker + ".meta"))
        {
            File.Delete(Marker + ".meta");
        }

        // format: title/title|keepTrans   (keepTrans: 0=Trim, 1=None)
        string[] parts = request.Split('|');
        if (parts.Length < 2)
        {
            Debug.LogError("[AtlasPack] invalid request: " + request);
            return;
        }

        string sourceRel = parts[0].Trim();
        bool keepTrans = parts[1].Trim() == "1";
        string sourceAssetDir = ResourceConst.PATH_ATLAS_SOURCE + sourceRel;
        TexturePackerImporter.TexturePackerTool.PackAtlasSourceAssetPath(sourceAssetDir, trimTrans: !keepTrans);
        AssetDatabase.Refresh();
        Debug.Log("[AtlasPack] done: " + sourceAssetDir + (keepTrans ? " (保留透明)" : ""));
    }
}
#endif
